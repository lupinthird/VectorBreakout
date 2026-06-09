using Microsoft.Xna.Framework;

namespace VectorBreakout.Effects;

public sealed class ScreenShake
{
    private Vector2 _offset;
    private float _intensity;

    public Vector2 Offset => _offset;

    public void Clear()
    {
        _intensity = 0f;
        _offset = Vector2.Zero;
    }

    public void AddImpulse(float intensity)
    {
        _intensity = MathHelper.Clamp(_intensity + intensity, 0f, 9f);
    }

    public void Update(float dt)
    {
        if (_intensity < 0.05f)
        {
            _intensity = 0f;
            _offset = Vector2.Zero;
            return;
        }

        _intensity *= MathHelper.Clamp(1f - dt * 11f, 0f, 1f);
        float angle = (float)(System.Random.Shared.NextDouble() * MathHelper.TwoPi);
        _offset = new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle)) * _intensity;
    }
}
