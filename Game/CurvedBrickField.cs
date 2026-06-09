using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorBreakout.Math;

namespace VectorBreakout.Game;

public sealed class CurvedBrickField
{
    private static readonly Color[] BrickPalette =
    [
        Color.Cyan,
        Color.Yellow,
        Color.Red,
        BrickColorShimmer.Purple,
    ];

    public sealed class Brick
    {
        public readonly List<Vector2> OutlinePoints;
        public readonly Vector2 Center;
        public readonly Color Color;
        public readonly int Ring;
        public readonly int Index;
        public readonly int BricksInRing;
        public readonly bool IsOuterWall;
        public bool IsAlive = true;
        public float HitFlashTimer;
        public bool PendingDestroy;

        public Brick(List<Vector2> outlinePoints, Color color, int ring, int index, int bricksInRing, bool isOuterWall)
        {
            OutlinePoints = outlinePoints;
            Color = color;
            Ring = ring;
            Index = index;
            BricksInRing = bricksInRing;
            IsOuterWall = isOuterWall;
            Vector2 center = Vector2.Zero;
            foreach (Vector2 point in outlinePoints)
            {
                center += point;
            }

            Center = center / outlinePoints.Count;
        }
    }

    private const float RingRotationSpeed = 0.22f;
    private const int CenterRingCount = 4;

    private readonly List<Brick> _bricks = new();
    private readonly float[] _ringRotations = new float[CenterRingCount];
    private readonly List<Vector2> _scratchOutline = new();

    public IReadOnlyList<Brick> Bricks => _bricks;

    public void Update(float dt)
    {
        for (int ring = 0; ring < _ringRotations.Length; ring++)
        {
            float direction = (ring % 2 == 0) ? 1f : -1f;
            _ringRotations[ring] += RingRotationSpeed * direction * dt;
        }

        UpdatePendingDestroys(dt);
    }

    private void UpdatePendingDestroys(float dt)
    {
        foreach (Brick brick in _bricks)
        {
            if (!brick.IsAlive || !brick.PendingDestroy)
            {
                continue;
            }

            brick.HitFlashTimer = MathHelper.Max(0f, brick.HitFlashTimer - dt);
            if (brick.HitFlashTimer <= 0f)
            {
                brick.IsAlive = false;
                brick.PendingDestroy = false;
            }
        }
    }

    public void ScheduleDestroy(Brick brick, float flashDuration = 0.055f)
    {
        if (!brick.IsAlive || brick.PendingDestroy)
        {
            return;
        }

        brick.PendingDestroy = true;
        brick.HitFlashTimer = flashDuration;
    }

    public bool IsHittable(Brick brick) => brick.IsAlive && !brick.PendingDestroy;

    public void GetWorldOutline(Brick brick, Vector2 fieldCenter, List<Vector2> destination)
    {
        destination.Clear();
        if (brick.Ring < 0)
        {
            destination.AddRange(brick.OutlinePoints);
            return;
        }

        float angle = _ringRotations[brick.Ring];
        foreach (Vector2 point in brick.OutlinePoints)
        {
            destination.Add(RotateAround(point, fieldCenter, angle));
        }
    }

    public Vector2 GetWorldCenter(Brick brick, Vector2 fieldCenter)
    {
        if (brick.Ring < 0)
        {
            return brick.Center;
        }

        return RotateAround(brick.Center, fieldCenter, _ringRotations[brick.Ring]);
    }

    public void BuildCenter(Vector2 center, float startRadius, int rings, int bricksPerRing)
    {
        float radialThickness = 16f;
        float gap = 0.055f;
        for (int ring = 0; ring < rings; ring++)
        {
            float radius = startRadius + ring * 34f;
            float ringOffset = ring * 0.14f;
            for (int i = 0; i < bricksPerRing; i++)
            {
                float a0 = ringOffset + (MathHelper.TwoPi / bricksPerRing) * i;
                float a1 = ringOffset + (MathHelper.TwoPi / bricksPerRing) * (i + 1) - gap;

                float outerRadius = radius + radialThickness * 0.5f;
                float innerRadius = radius - radialThickness * 0.5f;

                List<Vector2> outerArc = CurveSampler.SampleArcAsQuadratic(center, outerRadius, a0, a1, 7);
                List<Vector2> innerArc = CurveSampler.SampleArcAsQuadratic(center, innerRadius, a0, a1, 7);

                var outline = new List<Vector2>(outerArc.Count + innerArc.Count + 1);
                outline.AddRange(outerArc);
                for (int j = innerArc.Count - 1; j >= 0; j--)
                {
                    outline.Add(innerArc[j]);
                }

                if (outline.Count > 0 && outline[0] != outline[^1])
                {
                    outline.Add(outline[0]);
                }

                Color color = BrickPalette[(ring + i) % BrickPalette.Length];
                _bricks.Add(new Brick(outline, color, ring, i, bricksPerRing, isOuterWall: false));
            }
        }
    }

    public void BuildOuterWall(Vector2 center, float wallRadius, int wallBricks)
    {
        const float radialThickness = 20f;
        const float gap = 0.004f;
        for (int i = 0; i < wallBricks; i++)
        {
            float a0 = (MathHelper.TwoPi / wallBricks) * i;
            float a1 = (MathHelper.TwoPi / wallBricks) * (i + 1) - gap;

            float outerRadius = wallRadius + radialThickness * 0.5f;
            float innerRadius = wallRadius - radialThickness * 0.5f;
            List<Vector2> outerArc = CurveSampler.SampleArcAsQuadratic(center, outerRadius, a0, a1, 6);
            List<Vector2> innerArc = CurveSampler.SampleArcAsQuadratic(center, innerRadius, a0, a1, 6);

            var outline = new List<Vector2>(outerArc.Count + innerArc.Count + 1);
            outline.AddRange(outerArc);
            for (int j = innerArc.Count - 1; j >= 0; j--)
            {
                outline.Add(innerArc[j]);
            }

            if (outline.Count > 0 && outline[0] != outline[^1])
            {
                outline.Add(outline[0]);
            }

            Color color = BrickPalette[i % BrickPalette.Length];
            _bricks.Add(new Brick(outline, color, ring: -1, index: i, bricksInRing: wallBricks, isOuterWall: true));
        }
    }

    public void Clear()
    {
        _bricks.Clear();
        Array.Clear(_ringRotations, 0, _ringRotations.Length);
    }

    public bool TryHit(Ball ball, Vector2 fieldCenter, Vector2 previousPosition, out Brick destroyedBrick, out Vector2 hitNormal)
    {
        destroyedBrick = null!;
        hitNormal = Vector2.Zero;

        float bestT = 1.1f;
        Brick bestBrick = null!;
        Vector2 bestNormal = Vector2.Zero;
        bool hasHit = false;

        foreach (Brick brick in _bricks)
        {
            if (!IsHittable(brick))
            {
                continue;
            }

            GetWorldOutline(brick, fieldCenter, _scratchOutline);
            for (int i = 0; i < _scratchOutline.Count - 1; i++)
            {
                if (!Collision.TrySweptBallSegmentCollision(
                        previousPosition,
                        ball.Position,
                        ball.Radius,
                        _scratchOutline[i],
                        _scratchOutline[i + 1],
                        out Vector2 normal))
                {
                    continue;
                }

                float hitT = EstimateHitTime(previousPosition, ball.Position, ball.Radius, _scratchOutline[i], _scratchOutline[i + 1]);
                if (hitT < bestT)
                {
                    bestT = hitT;
                    bestBrick = brick;
                    bestNormal = normal;
                    hasHit = true;
                }
            }
        }

        if (!hasHit)
        {
            return false;
        }

        destroyedBrick = bestBrick;
        hitNormal = bestNormal;
        return true;
    }

    private static float EstimateHitTime(
        Vector2 previousPosition,
        Vector2 currentPosition,
        float ballRadius,
        Vector2 a,
        Vector2 b)
    {
        int sampleCount = 8;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector2 samplePosition = Vector2.Lerp(previousPosition, currentPosition, t);
            if (Collision.TryBallSegmentCollision(samplePosition, ballRadius, a, b, out _, out _))
            {
                return t;
            }
        }

        return 1f;
    }

    public int AliveCount()
    {
        int count = 0;
        foreach (Brick brick in _bricks)
        {
            if (brick.IsAlive)
            {
                count++;
            }
        }

        return count;
    }

    public int AliveCenterBrickCount()
    {
        int count = 0;
        foreach (Brick brick in _bricks)
        {
            if (brick.IsAlive && !brick.IsOuterWall)
            {
                count++;
            }
        }

        return count;
    }

    public void CollectAliveOuterWallBricks(List<Brick> destination)
    {
        destination.Clear();
        foreach (Brick brick in _bricks)
        {
            if (brick.IsAlive && brick.IsOuterWall)
            {
                destination.Add(brick);
            }
        }

        destination.Sort(static (a, b) => a.Index.CompareTo(b.Index));
    }

    public void DestroyOuterWallImmediate(Brick brick)
    {
        if (!brick.IsOuterWall)
        {
            return;
        }

        brick.IsAlive = false;
        brick.PendingDestroy = false;
        brick.HitFlashTimer = 0f;
    }

    public bool Destroy(Brick brick, bool immediate = false)
    {
        if (!brick.IsAlive)
        {
            return false;
        }

        if (immediate)
        {
            brick.IsAlive = false;
            brick.PendingDestroy = false;
            brick.HitFlashTimer = 0f;
            return true;
        }

        ScheduleDestroy(brick);
        return true;
    }

    public List<Brick> GetContiguousNeighbors(Brick brick)
    {
        var neighbors = new List<Brick>(2);
        int left = (brick.Index - 1 + brick.BricksInRing) % brick.BricksInRing;
        int right = (brick.Index + 1) % brick.BricksInRing;

        foreach (Brick candidate in _bricks)
        {
            if (!candidate.IsAlive)
            {
                continue;
            }

            if (candidate.IsOuterWall != brick.IsOuterWall || candidate.Ring != brick.Ring)
            {
                continue;
            }

            if (candidate.Index == left || candidate.Index == right)
            {
                neighbors.Add(candidate);
            }
        }

        return neighbors;
    }

    private static Vector2 RotateAround(Vector2 point, Vector2 origin, float angle)
    {
        Vector2 offset = point - origin;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return origin + new Vector2(
            offset.X * cos - offset.Y * sin,
            offset.X * sin + offset.Y * cos);
    }
}
