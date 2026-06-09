using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorBreakout.Rendering;

namespace VectorBreakout.Game;

public sealed class BallVisuals
{
    private const int TrailCapacity = 10;
    private const float TrailSampleDistanceSq = 36f;
    private const float ImpactSquashDecay = 14f;

    private readonly Vector2[] _trail = new Vector2[TrailCapacity];
    private int _trailCount;
    private int _trailHead;

    public Vector2 ImpactNormal { get; private set; }
    public float ImpactSquash { get; private set; }

    public void Reset()
    {
        _trailCount = 0;
        _trailHead = 0;
        ImpactSquash = 0f;
        ImpactNormal = Vector2.UnitY;
    }

    public void Update(float dt, Vector2 position, Vector2 velocity)
    {
        ImpactSquash = MathHelper.Max(0f, ImpactSquash - dt * ImpactSquashDecay);
        SampleTrail(position, velocity);
    }

    public void OnImpact(Vector2 normal, float intensity)
    {
        if (normal.LengthSquared() > 0.0001f)
        {
            ImpactNormal = Vector2.Normalize(normal);
        }

        ImpactSquash = MathHelper.Clamp(ImpactSquash + intensity, 0f, 1f);
    }

    public void Draw(VectorLineRenderer renderer, Ball ball, float maxSpeed)
    {
        DrawTrail(renderer, ball.Radius);
        BallOutline.DrawDeformed(
            renderer,
            ball.Position,
            ball.Radius + 1.5f,
            ball.Velocity,
            maxSpeed,
            ImpactNormal,
            ImpactSquash,
            Color.White);
    }

    private void DrawTrail(VectorLineRenderer renderer, float ballRadius)
    {
        if (_trailCount < 2)
        {
            return;
        }

        for (int i = 0; i < _trailCount - 1; i++)
        {
            int index = (_trailHead + i) % TrailCapacity;
            int nextIndex = (_trailHead + i + 1) % TrailCapacity;
            float t = i / (float)(_trailCount - 1);
            float alpha = t * 0.42f;
            float width = ballRadius * (0.35f + t * 0.25f);
            Color color = Color.White * alpha;
            renderer.AddLine(_trail[index], _trail[nextIndex], color);
            BallOutline.DrawOctagon(renderer, _trail[index], width, color * 0.55f);
        }
    }

    private void SampleTrail(Vector2 position, Vector2 velocity)
    {
        if (_trailCount == 0)
        {
            PushTrail(position);
            return;
        }

        int lastIndex = (_trailHead + _trailCount - 1) % TrailCapacity;
        Vector2 delta = position - _trail[lastIndex];
        if (delta.LengthSquared() < TrailSampleDistanceSq)
        {
            return;
        }

        PushTrail(position);

        float speed = velocity.Length();
        if (speed > 220f && _trailCount >= 4)
        {
            int trim = _trailCount > 7 ? 2 : 1;
            _trailHead = (_trailHead + trim) % TrailCapacity;
            _trailCount -= trim;
        }
    }

    private void PushTrail(Vector2 position)
    {
        if (_trailCount < TrailCapacity)
        {
            int index = (_trailHead + _trailCount) % TrailCapacity;
            _trail[index] = position;
            _trailCount++;
            return;
        }

        _trailHead = (_trailHead + 1) % TrailCapacity;
        _trail[(_trailHead + _trailCount - 1) % TrailCapacity] = position;
    }
}
