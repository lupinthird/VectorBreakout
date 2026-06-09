using System;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Game;

public static class BrickColorShimmer
{
    public static readonly Color Purple = new(190, 60, 255);

    public static readonly Color[] Colors =
    [
        Color.Cyan,
        Color.Yellow,
        Color.Red,
        Purple,
    ];

    public const float ShimmerInterval = 10f;

    /// <summary>Staggered start offset per color (2.5s after the previous).</summary>
    public static readonly float[] PhaseOffsets = [0f, 2.5f, 5f, 7.5f];

    public static int GetColorIndex(Color color)
    {
        if (color == Color.Cyan)
        {
            return 0;
        }

        if (color == Color.Yellow)
        {
            return 1;
        }

        if (color == Color.Red)
        {
            return 2;
        }

        if (color == Purple)
        {
            return 3;
        }

        return 0;
    }

    public static float ComputeShine(Vector2 worldPosition, Vector2 playfieldCenter, float totalTime, int colorIndex)
    {
        colorIndex = System.Math.Clamp(colorIndex, 0, PhaseOffsets.Length - 1);
        float angle = MathF.Atan2(worldPosition.Y - playfieldCenter.Y, worldPosition.X - playfieldCenter.X);
        float normalized = angle / MathHelper.TwoPi + 0.5f;
        float phase = (totalTime - PhaseOffsets[colorIndex]) / ShimmerInterval;
        float wave = normalized - phase;
        wave -= MathF.Floor(wave);
        return SmoothBand(wave, 0f, 0.05f, 0.14f);
    }

    public static Color ApplyFillShine(Color fillColor, Color highlightColor, float shine)
    {
        Color result = Color.Lerp(fillColor, highlightColor, shine * 0.55f);
        int alphaBoost = (int)(shine * 170f);
        result.A = (byte)System.Math.Clamp(fillColor.A + alphaBoost, 0, 255);
        return result;
    }

    public static Color ApplyOutlineShine(Color outlineColor, float shine)
    {
        return Color.Lerp(outlineColor * 0.55f, outlineColor, 0.55f + shine * 0.45f);
    }

    private static float SmoothBand(float x, float start, float peak, float end)
    {
        float rise = MathHelper.Clamp((x - start) / (peak - start), 0f, 1f);
        float fall = 1f - MathHelper.Clamp((x - peak) / (end - peak), 0f, 1f);
        return MathHelper.Clamp(rise * fall, 0f, 1f);
    }
}
