using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorBreakout.Audio;
using VectorBreakout.Effects;
using VectorBreakout.Math;
using VectorBreakout.Rendering;

namespace VectorBreakout.Game;

public sealed class GameState
{
    private const float ArenaPadding = 26f;
    private const int PointsPerBrick = 1000;
    private const float OuterWallThickness = 20f;
    private const float CenterBrickStartRadius = 90f;
    private const int CenterBrickRingCount = 4;
    private const float CenterBrickRingSpacing = 34f;
    private const float CenterBrickHalfThickness = 8f;
    private const float CenterGravityStrength = 110f;
    private static readonly float MaxPaddleDeflectionRadians = MathHelper.ToRadians(40f);
    private const float BrickHitSpeedMultiplier = 1.08f;
    private const float MaxBallSpeedMultiplier = 2.65f;
    private const float HitStopDurationSeconds = 0.042f;
    private const float BrickFlashDurationSeconds = 0.055f;
    private const float LaunchSpeed = 255f;
    private const float PaddleSizeScale = 0.75f;

    private Vector2 _center;
    private Vector2 _ballLaunchPosition;
    private Vector2 _ballLaunchVelocity;
    private float _arenaRadius;
    private int _viewportWidth;
    private float _baseBallSpeed;
    private Vector2 _previousBallPosition;
    private float _hitStopRemaining;
    private float _paddleBounceCooldown;
    private readonly List<Vector2> _brickDrawOutline = new();
    private readonly List<Vector2> _paddleOutlineScratch = new();
    private readonly BallVisuals _ballVisuals = new();
    private readonly ScreenShake _screenShake = new();
    private readonly LevelClearSequence _levelClear = new();
    private ProceduralSfxPlayer? _sfx;

    public float CenterBrickOuterRadius =>
        CenterBrickStartRadius + ((CenterBrickRingCount - 1) * CenterBrickRingSpacing) + CenterBrickHalfThickness;

    public PaddleOrbitController Paddle { get; }
    public Ball Ball { get; }
    public CurvedBrickField Bricks { get; } = new();
    public ExplosionManager Explosions { get; } = new();
    public ScreenShake ScreenShake => _screenShake;

    public void SetSfx(ProceduralSfxPlayer sfx) => _sfx = sfx;
    public int Lives { get; private set; } = 3;
    public int Score { get; private set; }
    public bool IsGameOver => Lives <= 0;

    public bool IsWin => Bricks.AliveCenterBrickCount() == 0;
    public Vector2 PlayfieldCenter => _center;
    public float ArenaRadius => _arenaRadius;
    public bool CenterGravityEnabled { get; private set; }
    public bool IsLaunched { get; private set; }
    public bool IsLevelClearActive => _levelClear.IsActive;
    public bool IsAwaitingLevelAdvance => _levelClear.AwaitingAdvance;
    public int HudDisplayScore =>
        _levelClear.IsActive && !_levelClear.CornerScoreUpdated ? _levelClear.BaseScoreAtClear : Score;

    public GameState(Viewport viewport)
    {
        Paddle = new PaddleOrbitController(100f);
        Ball = new Ball(Vector2.Zero, Vector2.Zero, 6f);
        RefreshBallRestOnPaddle();
        Explosions.Policy = ExplosionPolicy.SpecialBricksOnly;
        ResizeToViewport(viewport);
    }

    public void ResizeToViewport(Viewport viewport)
    {
        _viewportWidth = viewport.Width;
        _center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
        _arenaRadius = (System.MathF.Min(viewport.Width, viewport.Height) * 0.5f) - ArenaPadding;

        float paddleOrbitRadius = _arenaRadius - (OuterWallThickness * 0.5f) - 14f;
        Paddle.OrbitRadius = paddleOrbitRadius;
        RefreshBallRestOnPaddle();

        if (!IsLaunched)
        {
            Ball.Position = _ballLaunchPosition;
            Ball.Velocity = Vector2.Zero;
        }

        _previousBallPosition = _ballLaunchPosition;
        if (!IsLevelClearActive)
        {
            BuildBricks();
        }
    }

    public void Launch()
    {
        if (IsLaunched || IsLevelClearActive)
        {
            return;
        }

        RefreshBallRestOnPaddle();
        IsLaunched = true;
        Ball.Position = _ballLaunchPosition;
        Ball.Velocity = _ballLaunchVelocity;
        _previousBallPosition = _ballLaunchPosition;
        _baseBallSpeed = _ballLaunchVelocity.Length();
        _ballVisuals.Reset();
        _hitStopRemaining = 0f;
    }

    public void AdvanceToNextLevel()
    {
        if (!_levelClear.AwaitingAdvance)
        {
            return;
        }

        _levelClear.Reset();
        CenterGravityEnabled = false;
        BuildBricks();
        IsLaunched = false;
        RefreshBallRestOnPaddle();
        Ball.Position = _ballLaunchPosition;
        Ball.Velocity = Vector2.Zero;
        _previousBallPosition = _ballLaunchPosition;
        _ballVisuals.Reset();
        _hitStopRemaining = 0f;
        _paddleBounceCooldown = 0f;
    }

    public void ApplyLevelClearScore(int totalScore) => Score = totalScore;

    public void Update(float dt, MouseState mouse, bool gameplayActive, float? spinnerAxis = null)
    {
        if (IsGameOver)
        {
            return;
        }

        Paddle.Update(dt, mouse, _viewportWidth, spinnerAxis);

        if (_levelClear.IsActive)
        {
            Bricks.Update(dt);
            RefreshBallRestOnPaddle();
            Ball.Position = _ballLaunchPosition;
            Ball.Velocity = Vector2.Zero;
            _previousBallPosition = _ballLaunchPosition;
            _levelClear.Update(dt, this, _sfx);
            Explosions.Sparks.Update(dt);
            return;
        }

        Bricks.Update(dt);
        TryBeginLevelClear();

        if (!gameplayActive || !IsLaunched)
        {
            RefreshBallRestOnPaddle();
            Ball.Position = _ballLaunchPosition;
            Ball.Velocity = Vector2.Zero;
            _previousBallPosition = _ballLaunchPosition;
            return;
        }

        _previousBallPosition = Ball.Position;

        _screenShake.Update(dt);
        _paddleBounceCooldown = MathHelper.Max(0f, _paddleBounceCooldown - dt);
        float physicsDt = dt;
        if (_hitStopRemaining > 0f)
        {
            _hitStopRemaining = MathHelper.Max(0f, _hitStopRemaining - dt);
            physicsDt *= 0.12f;
        }

        _ballVisuals.Update(dt, Ball.Position, Ball.Velocity);

        ApplyCenterGravity(physicsDt);
        Ball.Update(physicsDt);

        BounceFromPaddle();
        BounceFromBricks();
        HandleArenaExit();

        Explosions.Sparks.Update(dt);
    }

    public void ToggleCenterGravity()
    {
        CenterGravityEnabled = !CenterGravityEnabled;
    }

    private void ApplyCenterGravity(float dt)
    {
        if (!CenterGravityEnabled)
        {
            return;
        }

        Vector2 toCenter = _center - Ball.Position;
        float distance = toCenter.Length();
        if (distance < 1f)
        {
            return;
        }

        Vector2 direction = toCenter / distance;
        Ball.Velocity += direction * CenterGravityStrength * dt;
    }

    private void HandleArenaExit()
    {
        Vector2 toBall = Ball.Position - _center;
        float distance = toBall.Length();
        if (distance - Ball.Radius < _arenaRadius + OuterWallThickness)
        {
            return;
        }

        Lives--;
        ResetBall(launchImmediately: false);
    }

    private void BounceFromPaddle()
    {
        if (_paddleBounceCooldown > 0f)
        {
            return;
        }

        if (!TryFindPaddleHit(Paddle.PreviousAngle, out float hitT, out Vector2 hitNormal)
            && !TryFindPaddleHit(Paddle.Angle, out hitT, out hitNormal))
        {
            return;
        }

        if (hitNormal.LengthSquared() < 0.0001f)
        {
            return;
        }

        hitNormal = Vector2.Normalize(hitNormal);
        float maxSpeed = GetMaxBallSpeed();
        float incomingSpeed = Ball.Velocity.Length();
        float speed = MathHelper.Clamp(MathHelper.Max(incomingSpeed, 155f), 0f, maxSpeed);

        Vector2 hitPosition = Vector2.Lerp(_previousBallPosition, Ball.Position, MathHelper.Clamp(hitT, 0f, 1f));
        Ball.Position = hitPosition + hitNormal * (Ball.Radius + 5f);

        float normalSpeed = Vector2.Dot(Ball.Velocity, hitNormal);
        if (normalSpeed < 0f)
        {
            Vector2 reflected = Collision.Reflect(Ball.Velocity, hitNormal);
            Vector2 bounceReference = -Paddle.GetForward();
            Ball.Velocity = Collision.ClampReflectionAngle(reflected, bounceReference, MaxPaddleDeflectionRadians);
            if (Ball.Velocity.LengthSquared() > 0.001f)
            {
                speed = MathHelper.Min(speed * 1.02f, maxSpeed);
                Ball.Velocity = Vector2.Normalize(Ball.Velocity) * speed;
            }
        }
        else
        {
            Ball.Velocity -= hitNormal * normalSpeed;
            if (Ball.Velocity.LengthSquared() < 0.001f)
            {
                Ball.Velocity = hitNormal * 155f;
            }
            else
            {
                Ball.Velocity = Vector2.Normalize(Ball.Velocity) * MathHelper.Min(speed, maxSpeed);
            }
        }

        _paddleBounceCooldown = 0.07f;
        RegisterPaddleImpact(hitNormal);
    }

    private bool TryFindPaddleHit(float paddleAngle, out float hitT, out Vector2 hitNormal)
    {
        hitT = 1.1f;
        hitNormal = Vector2.Zero;
        bool found = false;

        BuildPaddleOutline(paddleAngle, _paddleOutlineScratch);
        for (int i = 0; i < _paddleOutlineScratch.Count - 1; i++)
        {
            Vector2 a = _paddleOutlineScratch[i];
            Vector2 b = _paddleOutlineScratch[i + 1];
            if (!Collision.TrySweptBallSegmentCollision(
                    _previousBallPosition,
                    Ball.Position,
                    Ball.Radius + 1f,
                    a,
                    b,
                    out Vector2 normal))
            {
                continue;
            }

            float segmentHitT = Collision.EstimateSweptHitTime(_previousBallPosition, Ball.Position, Ball.Radius + 1f, a, b);
            if (segmentHitT < hitT)
            {
                hitT = segmentHitT;
                hitNormal = normal;
                found = true;
            }
        }

        return found;
    }

    private void BounceFromBricks()
    {
        if (!Bricks.TryHit(Ball, _center, _previousBallPosition, out CurvedBrickField.Brick hitBrick, out Vector2 normal))
        {
            return;
        }

        Vector2 contactPoint = Ball.Position - normal * Ball.Radius;
        Ball.Position += normal * (Ball.Radius + 2f);
        Ball.Velocity = Collision.Reflect(Ball.Velocity, normal);
        ApplyBrickCollisionSpeedBoost();

        float maxSpeed = _baseBallSpeed * MaxBallSpeedMultiplier;
        float speedT = maxSpeed > 1f ? MathHelper.Clamp(Ball.Velocity.Length() / maxSpeed, 0f, 1f) : 0.5f;
        RegisterBrickImpact(hitBrick, normal, speedT);
        Explosions.OnBrickImpact(contactPoint, normal, hitBrick.Color, Ball.Velocity.Length(), maxSpeed);
        ResolveBrickDestroyed(hitBrick);
    }

    private void RegisterPaddleImpact(Vector2 normal)
    {
        _ballVisuals.OnImpact(normal, 0.35f);
        _hitStopRemaining = MathHelper.Max(_hitStopRemaining, 0.014f);
        _screenShake.AddImpulse(0.25f);
        _sfx?.PlayPaddleThump(0.5f);
    }

    private void RegisterBrickImpact(CurvedBrickField.Brick brick, Vector2 normal, float speedT)
    {
        bool outerWall = brick.IsOuterWall;
        float squashIntensity = outerWall ? 0.7f + speedT * 0.25f : 0.5f + speedT * 0.3f;
        float hitStop = outerWall ? HitStopDurationSeconds * 1.05f : HitStopDurationSeconds * 0.8f;
        float shake = outerWall
            ? 4.5f + speedT * 3.5f
            : 0.35f + speedT * 0.45f;

        _ballVisuals.OnImpact(normal, squashIntensity);
        _hitStopRemaining = MathHelper.Max(_hitStopRemaining, hitStop);
        _screenShake.AddImpulse(shake);

        if (outerWall)
        {
            _sfx?.PlayWallThump(0.65f + speedT * 0.35f);
        }
        else
        {
            _sfx?.PlayBrickThump(0.4f + speedT * 0.35f);
        }
    }

    private float GetMaxBallSpeed() => _baseBallSpeed * MaxBallSpeedMultiplier;

    private void ApplyBrickCollisionSpeedBoost()
    {
        float speed = Ball.Velocity.Length();
        if (speed < 0.001f)
        {
            return;
        }

        float maxSpeed = _baseBallSpeed * MaxBallSpeedMultiplier;
        speed = MathHelper.Min(speed * BrickHitSpeedMultiplier, maxSpeed);
        Ball.Velocity = Vector2.Normalize(Ball.Velocity) * speed;
    }

    private void ResolveBrickDestroyed(CurvedBrickField.Brick brick)
    {
        int destroyedCount = 0;
        if (Bricks.Destroy(brick, immediate: false))
        {
            destroyedCount++;
            ApplyBrickBreakEffects(brick);
        }

        if (brick.Color == Color.Red)
        {
            foreach (CurvedBrickField.Brick neighbor in Bricks.GetContiguousNeighbors(brick))
            {
                if (Bricks.Destroy(neighbor, immediate: true))
                {
                    destroyedCount++;
                    Vector2 neighborCenter = Bricks.GetWorldCenter(neighbor, _center);
                    Explosions.OnBrickImpact(neighborCenter, neighborCenter - Bricks.GetWorldCenter(brick, _center), neighbor.Color, Ball.Velocity.Length(), GetMaxBallSpeed());
                }
            }
        }

        Score += destroyedCount * PointsPerBrick;
        TryBeginLevelClear();
    }

    private void TryBeginLevelClear()
    {
        if (!IsLaunched || _levelClear.IsActive || Bricks.AliveCenterBrickCount() > 0)
        {
            return;
        }

        IsLaunched = false;
        RefreshBallRestOnPaddle();
        Ball.Position = _ballLaunchPosition;
        Ball.Velocity = Vector2.Zero;
        _previousBallPosition = _ballLaunchPosition;
        _levelClear.Begin(this);
    }

    private void RefreshBallRestOnPaddle()
    {
        Vector2 paddlePos = Paddle.GetPosition(_center);
        Vector2 launchDir = -Paddle.GetForward();
        if (launchDir.LengthSquared() < 0.0001f)
        {
            launchDir = new Vector2(0f, -1f);
        }
        else
        {
            launchDir = Vector2.Normalize(launchDir);
        }

        _ballLaunchPosition = paddlePos + launchDir * (Ball.Radius + 6f);
        _ballLaunchVelocity = launchDir * LaunchSpeed;
    }

    private void ApplyBrickBreakEffects(CurvedBrickField.Brick brick)
    {
        if (brick.Color == Color.Red)
        {
            Explosions.OnBrickDestroyed(Bricks.GetWorldCenter(brick, _center), isSpecial: true);
            _sfx?.PlayExplosion(0.85f);
        }
    }

    private void BuildBricks()
    {
        Bricks.Clear();
        Bricks.BuildCenter(_center, CenterBrickStartRadius, CenterBrickRingCount, 22);
        Bricks.BuildOuterWall(_center, _arenaRadius, 56);
    }

    private void ResetBall(bool launchImmediately)
    {
        RefreshBallRestOnPaddle();
        Ball.Reset(_ballLaunchPosition, launchImmediately ? _ballLaunchVelocity : Vector2.Zero);
        IsLaunched = launchImmediately;
        _baseBallSpeed = LaunchSpeed;
        _ballVisuals.Reset();
        _hitStopRemaining = 0f;
        _paddleBounceCooldown = 0f;
    }

    public void DrawLevelClearOverlay(VectorLineRenderer renderer)
    {
        if (!_levelClear.IsActive)
        {
            return;
        }

        HudOverlay.DrawLevelClear(
            renderer,
            _center,
            _levelClear.BaseScoreAtClear,
            _levelClear.BonusAccumulated,
            _levelClear.ShowGrandTotal,
            _levelClear.AwaitingAdvance);
    }

    public void Reset()
    {
        Lives = 3;
        Score = 0;
        IsLaunched = false;
        CenterGravityEnabled = false;
        ResetBall(launchImmediately: false);
        BuildBricks();
        Explosions.ResetRun();
        Explosions.Policy = ExplosionPolicy.SpecialBricksOnly;
        _ballVisuals.Reset();
        _screenShake.Clear();
        _hitStopRemaining = 0f;
        _paddleBounceCooldown = 0f;
        _levelClear.Reset();
        Paddle.ResetToDefault();
    }

    public void Draw(VectorLineRenderer renderer, float totalTime)
    {
        DrawPaddle(renderer);
        DrawBricks(renderer, totalTime);
        _ballVisuals.Draw(renderer, Ball, GetMaxBallSpeed());
    }

    private void DrawPaddle(VectorLineRenderer renderer)
    {
        List<Vector2> outline = GetPaddleOutline();
        renderer.AddPolyline(outline, Color.White);
    }

    private List<Vector2> GetPaddleOutline() => GetPaddleOutline(Paddle.Angle);

    private List<Vector2> GetPaddleOutline(float paddleAngle)
    {
        BuildPaddleOutline(paddleAngle, _paddleOutlineScratch);
        return _paddleOutlineScratch;
    }

    private void BuildPaddleOutline(float paddleAngle, List<Vector2> destination)
    {
        const float basePaddleThickness = 18f;
        float paddleThickness = basePaddleThickness * PaddleSizeScale;
        float halfArc = Paddle.HalfArcWidth * PaddleSizeScale;
        float start = paddleAngle - halfArc;
        float end = paddleAngle + halfArc;
        List<Vector2> outerArc = CurveSampler.SampleArcAsQuadratic(_center, Paddle.OrbitRadius + paddleThickness * 0.5f, start, end, 20);
        List<Vector2> innerArc = CurveSampler.SampleArcAsQuadratic(_center, Paddle.OrbitRadius - paddleThickness * 0.5f, start, end, 20);

        destination.Clear();
        destination.Capacity = System.Math.Max(destination.Capacity, outerArc.Count + innerArc.Count + 1);
        destination.AddRange(outerArc);
        for (int i = innerArc.Count - 1; i >= 0; i--)
        {
            destination.Add(innerArc[i]);
        }

        if (destination[0] != destination[^1])
        {
            destination.Add(destination[0]);
        }
    }

    private void DrawBricks(VectorLineRenderer renderer, float totalTime)
    {
        foreach (CurvedBrickField.Brick brick in Bricks.Bricks)
        {
            if (!brick.IsAlive)
            {
                continue;
            }

            Bricks.GetWorldOutline(brick, _center, _brickDrawOutline);
            Vector2 worldCenter = Bricks.GetWorldCenter(brick, _center);
            Color fillColor = brick.Color * 0.1f;
            int shimmerIndex = BrickColorShimmer.GetColorIndex(brick.Color);
            float shine = BrickColorShimmer.ComputeShine(worldCenter, _center, totalTime, shimmerIndex);
            Color outlineColor = BrickColorShimmer.ApplyOutlineShine(brick.Color, shine);

            if (brick.HitFlashTimer > 0f)
            {
                float flash = MathHelper.Clamp(brick.HitFlashTimer / BrickFlashDurationSeconds, 0f, 1f);
                outlineColor = Color.Lerp(outlineColor, Color.White, flash);
                fillColor = Color.Lerp(fillColor, brick.Color * 0.35f, flash * 0.8f);
            }

            renderer.AddPolygonFill(
                _brickDrawOutline,
                worldCenter,
                fillColor,
                brick.Color,
                shimmerIndex,
                totalTime,
                _center);
            renderer.AddPolyline(_brickDrawOutline, outlineColor);
        }
    }
}
