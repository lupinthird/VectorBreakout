using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorBreakout.Effects;

public sealed class SparkParticleSystem
{
    private struct Spark
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public Color Tint;
    }

    private readonly List<Spark> _sparks = new();
    private readonly Random _rng = new();

    public void Spawn(Vector2 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);
            float speed = 90f + (float)_rng.NextDouble() * 420f;
            float life = 0.28f + (float)_rng.NextDouble() * 0.52f;
            _sparks.Add(new Spark
            {
                Position = at,
                Velocity = new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle)) * speed,
                Life = life,
                MaxLife = life,
                Tint = Color.White,
            });
        }
    }

    public void SpawnImpact(Vector2 at, Vector2 normal, Color tint, int count, float speedScale = 1f)
    {
        if (normal.LengthSquared() < 0.0001f)
        {
            normal = Vector2.UnitY;
        }
        else
        {
            normal = Vector2.Normalize(normal);
        }

        Vector2 tangent = new Vector2(-normal.Y, normal.X);
        for (int i = 0; i < count; i++)
        {
            float spread = ((float)_rng.NextDouble() - 0.5f) * 1.4f;
            Vector2 direction = Vector2.Normalize(normal * (0.65f + (float)_rng.NextDouble() * 0.55f) + tangent * spread);
            float speed = (70f + (float)_rng.NextDouble() * 260f) * speedScale;
            float life = 0.12f + (float)_rng.NextDouble() * 0.28f;
            _sparks.Add(new Spark
            {
                Position = at,
                Velocity = direction * speed,
                Life = life,
                MaxLife = life,
                Tint = tint,
            });
        }
    }

    public void Update(float dt)
    {
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            Spark spark = _sparks[i];
            spark.Life -= dt;
            if (spark.Life <= 0f)
            {
                _sparks.RemoveAt(i);
                continue;
            }

            float t = 1f - (spark.Life / spark.MaxLife);
            spark.Velocity *= 0.99f - (0.15f * t);
            spark.Position += spark.Velocity * dt;
            _sparks[i] = spark;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 screenOffset = default)
    {
        foreach (Spark spark in _sparks)
        {
            float progress = 1f - (spark.Life / spark.MaxLife);
            float alpha = MathHelper.Clamp(1f - progress * 0.92f, 0f, 1f);
            Color color = Color.Lerp(HeatColor(progress), spark.Tint, 0.45f) * alpha;
            const float size = 1f;
            spriteBatch.Draw(
                pixel,
                spark.Position + screenOffset,
                null,
                color,
                0f,
                new Vector2(0.5f, 0.5f),
                size,
                SpriteEffects.None,
                0f);
        }
    }

    private static Color HeatColor(float t)
    {
        if (t < 0.2f) return Color.Lerp(Color.White, Color.Yellow, t / 0.2f);
        if (t < 0.4f) return Color.Lerp(Color.Yellow, Color.Orange, (t - 0.2f) / 0.2f);
        if (t < 0.6f) return Color.Lerp(Color.Orange, Color.Red, (t - 0.4f) / 0.2f);
        if (t < 0.8f) return Color.Lerp(Color.Red, new Color(90, 0, 0), (t - 0.6f) / 0.2f);
        return Color.Lerp(new Color(90, 0, 0), new Color(10, 10, 10), (t - 0.8f) / 0.2f);
    }
}
