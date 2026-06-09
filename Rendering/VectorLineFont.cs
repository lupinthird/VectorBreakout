using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Rendering;

/// <summary>
/// Vector HUD font: 7-segment digits and stroke letters on a 6×9 unit cell.
/// </summary>
public static class VectorLineFont
{
    private const float GlyphCellWidth = 6f;
    private const float GlyphCellHeight = 9f;
    private const float DigitAdvanceScale = 2.45f;
    private const float LetterAdvanceScale = 1.95f;

    /// <summary>Seven-segment 8 (0b1111111) without bottom segment (d).</summary>
    private const byte SevenSegmentA = 0b1110111;

    private static readonly byte[] DigitSegmentMasks =
    [
        0b0111111, // 0
        0b0000110, // 1
        0b1011011, // 2
        0b1001111, // 3
        0b1100110, // 4
        0b1101101, // 5
        0b1111101, // 6
        0b0000111, // 7
        0b1111111, // 8
        0b1101111, // 9
    ];

    private static readonly Dictionary<char, LineSegment[]> StrokeGlyphs = BuildStrokeGlyphs();

    public static void DrawInteger(VectorLineRenderer renderer, int value, Vector2 position, float scale, Color color)
    {
        if (value == 0)
        {
            DrawDigit(renderer, 0, position, scale, color);
            return;
        }

        Span<int> digits = stackalloc int[12];
        int count = 0;
        int remaining = System.Math.Abs(value);
        while (remaining > 0)
        {
            digits[count++] = remaining % 10;
            remaining /= 10;
        }

        float digitAdvance = GetDigitAdvance(scale);
        Vector2 cursor = position + new Vector2(digitAdvance * (count - 1), 0f);
        for (int i = 0; i < count; i++)
        {
            DrawDigit(renderer, digits[i], cursor, scale, color);
            cursor.X -= digitAdvance;
        }
    }

    public static float MeasureStringWidth(string text, float scale)
    {
        float letterAdvance = GetLetterAdvance(scale);
        float spaceAdvance = letterAdvance * 0.6f;
        float width = 0f;
        foreach (char raw in text)
        {
            width += char.ToUpperInvariant(raw) == ' ' ? spaceAdvance : letterAdvance;
        }

        return width;
    }

    public static float MeasureIntegerWidth(int value, float scale)
    {
        if (value == 0)
        {
            return GetDigitAdvance(scale);
        }

        int count = 0;
        int remaining = System.Math.Abs(value);
        while (remaining > 0)
        {
            count++;
            remaining /= 10;
        }

        return GetDigitAdvance(scale) * count;
    }

    public static void DrawIntegerCentered(
        VectorLineRenderer renderer,
        int value,
        Vector2 center,
        float scale,
        Color color)
    {
        float width = MeasureIntegerWidth(value, scale);
        float height = GlyphCellHeight * scale;
        DrawInteger(renderer, value, center - new Vector2(width * 0.5f, height * 0.5f), scale, color);
    }

    public static void DrawStringCentered(
        VectorLineRenderer renderer,
        string text,
        Vector2 center,
        float scale,
        Color color)
    {
        float width = MeasureStringWidth(text, scale);
        float height = GlyphCellHeight * scale;
        DrawString(renderer, text, center - new Vector2(width * 0.5f, height * 0.5f), scale, color);
    }

    public static void DrawString(
        VectorLineRenderer renderer,
        string text,
        Vector2 position,
        float scale,
        Color color)
    {
        float letterAdvance = GetLetterAdvance(scale);
        float spaceAdvance = letterAdvance * 0.6f;
        Vector2 cursor = position;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == ' ')
            {
                cursor.X += spaceAdvance;
                continue;
            }

            if (TryDrawLetter(renderer, c, cursor, scale, color))
            {
                cursor.X += letterAdvance;
            }
        }
    }

    private static float GetDigitAdvance(float scale) => GlyphCellWidth * scale * DigitAdvanceScale;

    private static float GetLetterAdvance(float scale) => GlyphCellWidth * scale * LetterAdvanceScale;

    private static bool TryDrawLetter(
        VectorLineRenderer renderer,
        char c,
        Vector2 position,
        float scale,
        Color color)
    {
        if (c is >= '0' and <= '9')
        {
            DrawDigit(renderer, c - '0', position, scale, color);
            return true;
        }

        if (c == 'A')
        {
            DrawSevenSegmentMask(renderer, SevenSegmentA, position, scale, color);
            return true;
        }

        if (StrokeGlyphs.TryGetValue(c, out LineSegment[]? segments) && segments != null)
        {
            DrawGlyph(renderer, segments, position, scale, color);
            return true;
        }

        return false;
    }

    private static void DrawDigit(VectorLineRenderer renderer, int digit, Vector2 position, float scale, Color color)
    {
        if (digit is < 0 or > 9)
        {
            return;
        }

        DrawSevenSegmentMask(renderer, DigitSegmentMasks[digit], position, scale, color);
    }

    private static void DrawSevenSegmentMask(
        VectorLineRenderer renderer,
        byte mask,
        Vector2 position,
        float scale,
        Color color)
    {
        // Same 6×9 unit box as stroke glyphs (AddUnitSegment multiplies by scale once).
        const float left = 0.65f;
        const float right = 5.35f;
        const float top = 0.7f;
        const float middle = 4.5f;
        const float bottom = 8.3f;
        const float upperMid = middle - 0.28f;
        const float lowerMid = middle + 0.28f;

        if ((mask & 0b0000001) != 0)
        {
            AddUnitSegment(renderer, position, left, top, right, top, scale, color);
        }

        if ((mask & 0b0000010) != 0)
        {
            AddUnitSegment(renderer, position, right, top, right, upperMid, scale, color);
        }

        if ((mask & 0b0000100) != 0)
        {
            AddUnitSegment(renderer, position, right, lowerMid, right, bottom, scale, color);
        }

        if ((mask & 0b0001000) != 0)
        {
            AddUnitSegment(renderer, position, left, bottom, right, bottom, scale, color);
        }

        if ((mask & 0b0010000) != 0)
        {
            AddUnitSegment(renderer, position, left, lowerMid, left, bottom, scale, color);
        }

        if ((mask & 0b0100000) != 0)
        {
            AddUnitSegment(renderer, position, left, top, left, upperMid, scale, color);
        }

        if ((mask & 0b1000000) != 0)
        {
            AddUnitSegment(renderer, position, left, middle, right, middle, scale, color);
        }
    }

    private static void DrawGlyph(
        VectorLineRenderer renderer,
        LineSegment[] segments,
        Vector2 position,
        float scale,
        Color color)
    {
        foreach (LineSegment segment in segments)
        {
            AddUnitSegment(renderer, position, segment.X0, segment.Y0, segment.X1, segment.Y1, scale, color);
        }
    }

    private static void AddUnitSegment(
        VectorLineRenderer renderer,
        Vector2 origin,
        float x0,
        float y0,
        float x1,
        float y1,
        float scale,
        Color color)
    {
        Vector2 start = origin + new Vector2(x0 * scale, y0 * scale);
        Vector2 end = origin + new Vector2(x1 * scale, y1 * scale);
        renderer.AddLine(start, end, color);
    }

    private static Dictionary<char, LineSegment[]> BuildStrokeGlyphs()
    {
        var glyphs = new Dictionary<char, LineSegment[]>();

        void Add(char c, params float[] coords)
        {
            var segments = new LineSegment[coords.Length / 4];
            for (int i = 0; i < segments.Length; i++)
            {
                int o = i * 4;
                segments[i] = new LineSegment(coords[o], coords[o + 1], coords[o + 2], coords[o + 3]);
            }

            glyphs[c] = segments;
        }

        // Coordinates in 6×9 cell (x: 0–6, y: 0 top – 9 bottom).

        Add('B',
            0, 0, 0, 9,
            0, 0, 4.8f, 0, 5.8f, 1.2f, 5.8f, 3.8f, 4.8f, 4.5f, 0, 4.5f,
            0, 4.5f, 5.8f, 5.2f, 5.8f, 7.8f, 4.8f, 9, 0, 9);

        Add('C',
            5.4f, 1f, 4f, 0.7f, 2f, 0.7f, 0.8f, 1.6f,
            0.8f, 2f, 0.8f, 7f, 0.8f, 8.4f,
            2f, 8.8f, 4f, 8.8f, 5.4f, 8f);

        Add('D',
            0, 0, 0, 9,
            0, 0, 4.5f, 0, 5.8f, 1.5f, 5.8f, 7.5f, 4.5f, 9, 0, 9);

        Add('E',
            0, 0, 0, 9,
            0, 0, 5.8f, 0,
            0, 4.5f, 4.8f, 4.5f,
            0, 9, 5.8f, 9);

        Add('G',
            5.5f, 2, 2.2f, 2, 1, 3.2f, 1, 5.8f, 2.2f, 6.8f, 5.5f, 6.8f,
            5.5f, 5.2f, 3.2f, 5.2f, 3.2f, 4.2f, 5.5f, 4.2f);

        Add('H', 0, 0, 0, 9, 6, 0, 6, 9, 0, 4.5f, 6, 4.5f);

        Add('I', 1.2f, 0, 4.8f, 0, 3, 0, 3, 9, 1.2f, 9, 4.8f, 9);

        Add('J',
            4.5f, 0, 6, 0,
            5.2f, 0, 5.2f, 7,
            4.2f, 8.5f, 2.2f, 8.5f, 1.2f, 7.2f);

        Add('K', 0, 0, 0, 9, 0, 4.8f, 6, 0, 0, 4.8f, 6, 9);

        Add('L', 0, 0, 0, 9, 0, 9, 6, 9);

        Add('M', 0, 9, 0, 0, 3, 4.5f, 6, 0, 6, 9);

        Add('N',
            0, 9, 0, 0,
            0, 0, 6, 9,
            6, 9, 6, 0);

        Add('P',
            0, 9, 0, 0,
            0, 0, 5.2f, 0, 5.8f, 1.2f, 5.8f, 3.5f, 5.2f, 4.5f, 0, 4.5f);

        Add('Q',
            1.5f, 0, 4.5f, 0, 5.8f, 1.2f, 5.8f, 7.8f, 4.5f, 9, 1.5f, 9, 0.5f, 7.8f, 0.5f, 1.2f, 1.5f, 0,
            3.8f, 7.2f, 6, 9);

        Add('R',
            0, 9, 0, 0,
            0, 0, 5.2f, 0, 5.8f, 1.2f, 5.8f, 3.5f, 5.2f, 4.5f, 0, 4.5f,
            3.2f, 4.5f, 6, 9);

        Add('T', 0, 0, 6, 0, 3, 0, 3, 9);

        Add('U', 0, 0, 0, 7.2f, 1.2f, 9, 4.8f, 9, 6, 7.2f, 6, 0);

        Add('V', 0, 0, 3, 9, 6, 0);

        Add('W', 0, 0, 1.5f, 9, 3, 4, 4.5f, 9, 6, 0);

        Add('Y', 0, 0, 3, 4.5f, 6, 0, 3, 4.5f, 3, 9);

        Add('Z',
            0, 0, 6, 0,
            6, 0.8f, 0.8f, 8.2f,
            0, 9, 6, 9);

        Add('O',
            1.5f, 0.7f, 4.5f, 0.7f, 5.6f, 1.5f, 5.6f, 7.5f,
            4.5f, 8.3f, 1.5f, 8.3f, 0.6f, 7.5f, 0.6f, 1.5f, 1.5f, 0.7f);

        Add('S',
            5.3f, 1.2f, 1.6f, 1.2f, 0.7f, 2.4f, 0.7f, 3.6f,
            5.3f, 4.2f, 1.6f, 4.5f, 0.7f, 5.6f, 0.7f, 6.8f,
            1.6f, 8.2f, 5.3f, 8.2f);

        return glyphs;
    }

    private readonly struct LineSegment
    {
        public readonly float X0;
        public readonly float Y0;
        public readonly float X1;
        public readonly float Y1;

        public LineSegment(float x0, float y0, float x1, float y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }
    }
}
