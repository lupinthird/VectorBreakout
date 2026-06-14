using Microsoft.Xna.Framework;

namespace VectorBreakout.Input;

/// <summary>
/// Tracks observed potentiometer extremes so partial HID ranges still span the full orbit.
/// </summary>
public sealed class PaddleAxisCalibration
{
    public const float RequiredSpan = 0.28f;
    public const float EndThreshold = 0.22f;

    private bool _hasSample;
    private bool _seenLow;
    private bool _seenHigh;

    public float Min { get; private set; }
    public float Max { get; private set; }

    public bool IsComplete => _seenLow && _seenHigh && Max - Min >= RequiredSpan;

    public void Reset()
    {
        _hasSample = false;
        _seenLow = false;
        _seenHigh = false;
        Min = 0f;
        Max = 0f;
    }

    public void Sample(float axis)
    {
        if (!_hasSample)
        {
            Min = Max = axis;
            _hasSample = true;
        }
        else
        {
            Min = MathF.Min(Min, axis);
            Max = MathF.Max(Max, axis);
        }

        if (axis <= -EndThreshold)
        {
            _seenLow = true;
        }

        if (axis >= EndThreshold)
        {
            _seenHigh = true;
        }
    }

    public float MapTo01(float axis)
    {
        if (!IsComplete)
        {
            return MathHelper.Clamp((axis + 1f) * 0.5f, 0f, 1f);
        }

        float span = Max - Min;
        if (span < 0.0001f)
        {
            return 0.5f;
        }

        return MathHelper.Clamp((axis - Min) / span, 0f, 1f);
    }
}
