using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorBreakout.Effects;

/// <summary>
/// Warp-style starfield. Default flow moves outward (forward). With gravity active, flow reverses:
/// stars spawn beyond the playfield edge and scroll inward.
/// </summary>
public sealed class Starfield
{
    private struct Star
    {
        public float Angle;
        public float Distance;
        public float Speed;
        public byte Layer;
    }

    private readonly Random _rng = new();
    private Star[] _stars = Array.Empty<Star>();
    private Vector2 _center;
    private float _maxDistance;
    private float _spawnMinRadius;
    private float _brightenEndRadius;
    private float _arenaRadius;
    private float _centerBrickOuterRadius;
    private bool _reverseFlow;

    public float SpeedMultiplier { get; private set; } = 0.4f;

    public void Configure(int width, int height, float arenaRadius, float centerBrickOuterRadius, int starCount = 420)
    {
        _center = new Vector2(width * 0.5f, height * 0.5f);
        float minDim = MathF.Min(width, height);
        _arenaRadius = arenaRadius;
        _centerBrickOuterRadius = centerBrickOuterRadius;
        _maxDistance = arenaRadius + 36f;

        _spawnMinRadius = minDim * 0.12f;
        _brightenEndRadius = _spawnMinRadius + minDim * 0.1f;

        _stars = new Star[starCount];
        RespawnAllStars();
    }

    public void ApplyGravityVisuals(bool gravityEnabled)
    {
        _reverseFlow = gravityEnabled;
        SpeedMultiplier = gravityEnabled ? 1.0f : 0.4f;
        RespawnAllStars();
    }

    public void Update(float dt)
    {
        float flowDirection = _reverseFlow ? -1f : 1f;

        for (int i = 0; i < _stars.Length; i++)
        {
            Star star = _stars[i];
            star.Distance += star.Speed * SpeedMultiplier * dt * flowDirection;

            if (!_reverseFlow && star.Distance >= _maxDistance)
            {
                star = CreateStar(respawn: true);
            }
            else if (_reverseFlow && star.Distance <= _spawnMinRadius)
            {
                star = CreateStar(respawn: true);
            }

            _stars[i] = star;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel)
    {
        foreach (Star star in _stars)
        {
            float brightness = GetDistanceBrightness(star.Distance);
            if (brightness <= 0.02f)
            {
                continue;
            }

            float cos = MathF.Cos(star.Angle);
            float sin = MathF.Sin(star.Angle);
            Vector2 head = _center + new Vector2(cos, sin) * star.Distance;

            float streakLength = star.Speed * SpeedMultiplier * (0.022f + star.Layer * 0.014f) * brightness;
            float tailDistance = _reverseFlow ? star.Distance + streakLength : star.Distance - streakLength;
            Vector2 tail = _center + new Vector2(MathF.Cos(star.Angle), MathF.Sin(star.Angle)) * tailDistance;

            Color color = star.Layer switch
            {
                0 => new Color(90, 110, 150),
                1 => new Color(170, 190, 230),
                _ => new Color(240, 245, 255),
            };

            color *= brightness * (star.Layer switch
            {
                0 => 0.45f,
                1 => 0.7f,
                _ => 0.95f,
            });

            float size = (0.8f + star.Layer * 0.35f) * (0.55f + 0.45f * brightness);
            DrawStreak(spriteBatch, pixel, tail, head, color, size);
        }
    }

    private void RespawnAllStars()
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i] = CreateStar(respawn: false);
        }
    }

    private float GetDistanceBrightness(float distance)
    {
        if (!_reverseFlow)
        {
            return SmoothStep(_spawnMinRadius, _brightenEndRadius, distance);
        }

        if (distance >= _centerBrickOuterRadius)
        {
            return 1f;
        }

        if (distance <= _spawnMinRadius)
        {
            return 0f;
        }

        return SmoothStep(_spawnMinRadius, _centerBrickOuterRadius, distance);
    }

    private Star CreateStar(bool respawn)
    {
        byte layer = (byte)_rng.Next(3);
        float speed = layer switch
        {
            0 => 48f,
            1 => 105f,
            _ => 185f,
        };

        speed *= 0.85f + _rng.NextSingle() * 0.35f;

        float distance;
        if (_reverseFlow)
        {
            distance = respawn
                ? _maxDistance - _rng.NextSingle() * 24f
                : _spawnMinRadius + _rng.NextSingle() * (_maxDistance - _spawnMinRadius);
        }
        else
        {
            distance = respawn
                ? _spawnMinRadius + _rng.NextSingle() * 18f
                : _spawnMinRadius + _rng.NextSingle() * (_maxDistance - _spawnMinRadius);
        }

        return new Star
        {
            Angle = (float)(_rng.NextDouble() * MathHelper.TwoPi),
            Distance = distance,
            Speed = speed,
            Layer = layer,
        };
    }

    private static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = MathHelper.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void DrawStreak(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tail, Vector2 head, Color color, float size)
    {
        Vector2 delta = head - tail;
        float length = delta.Length();
        if (length < 0.75f)
        {
            spriteBatch.Draw(pixel, head, null, color, 0f, new Vector2(0.5f), size, SpriteEffects.None, 0f);
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(
            pixel,
            tail,
            null,
            color,
            angle,
            new Vector2(0f, 0.5f),
            new Vector2(length, size),
            SpriteEffects.None,
            0f);
    }
}
