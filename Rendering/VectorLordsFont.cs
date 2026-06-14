using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Rendering;

/// <summary>
/// Stroke-based vector font ported from VectorLords. Uniform cap height; printable ASCII 0x20–0x7E.
/// </summary>
public static class VectorLordsFont
{
    public const float GlyphHeight = 10f;
    private const float DefaultCellWidth = 7f;
    private const float LetterAdvanceScale = 1.85f;
    private const float SpaceAdvanceScale = 0.55f;

    private static readonly Dictionary<char, GlyphDef> Glyphs = BuildGlyphs();

    public static float MeasureStringWidth(string text, float scale)
    {
        float advance = GetLetterAdvance(scale);
        float spaceAdvance = advance * SpaceAdvanceScale;
        float width = 0f;
        foreach (char raw in text)
        {
            char c = Normalize(raw);
            if (c == ' ')
            {
                width += spaceAdvance;
            }
            else if (Glyphs.TryGetValue(c, out GlyphDef glyph))
            {
                width += glyph.CellWidth * scale * LetterAdvanceScale;
            }
        }

        return width;
    }

    public static float MeasureIntegerWidth(int value, float scale) =>
        MeasureStringWidth(value.ToString(CultureInfo.InvariantCulture), scale);

    public static void DrawString(VectorLineRenderer renderer, string text, Vector2 position, float scale, Color color)
    {
        float advance = GetLetterAdvance(scale);
        float spaceAdvance = advance * SpaceAdvanceScale;
        Vector2 cursor = position;

        foreach (char raw in text)
        {
            char c = Normalize(raw);
            if (c == ' ')
            {
                cursor.X += spaceAdvance;
                continue;
            }

            if (!Glyphs.TryGetValue(c, out GlyphDef glyph))
            {
                continue;
            }

            foreach (Segment seg in glyph.Segments)
            {
                Vector2 a = cursor + seg.A * scale;
                Vector2 b = cursor + seg.B * scale;
                renderer.AddLine(a, b, color);
            }

            cursor.X += glyph.CellWidth * scale * LetterAdvanceScale;
        }
    }

    public static void DrawStringCentered(
        VectorLineRenderer renderer,
        string text,
        Vector2 center,
        float scale,
        Color color)
    {
        float width = MeasureStringWidth(text, scale);
        Vector2 pos = center - new Vector2(width * 0.5f, GlyphHeight * scale * 0.5f);
        DrawString(renderer, text, pos, scale, color);
    }

    public static void DrawInteger(VectorLineRenderer renderer, int value, Vector2 position, float scale, Color color)
    {
        DrawString(renderer, value.ToString(CultureInfo.InvariantCulture), position, scale, color);
    }

    public static void DrawIntegerCentered(
        VectorLineRenderer renderer,
        int value,
        Vector2 center,
        float scale,
        Color color)
    {
        float width = MeasureIntegerWidth(value, scale);
        float height = GlyphHeight * scale;
        DrawInteger(renderer, value, center - new Vector2(width * 0.5f, height * 0.5f), scale, color);
    }

    private static float GetLetterAdvance(float scale) => DefaultCellWidth * scale * LetterAdvanceScale;

    private static char Normalize(char c)
    {
        if (c is >= 'a' and <= 'z')
        {
            return (char)(c - 32);
        }

        return c;
    }

    private readonly struct GlyphDef(float cellWidth, Segment[] segments)
    {
        public float CellWidth { get; } = cellWidth;
        public Segment[] Segments { get; } = segments;
    }

    private readonly struct Segment(Vector2 a, Vector2 b)
    {
        public Vector2 A { get; } = a;
        public Vector2 B { get; } = b;
    }

    private static Dictionary<char, GlyphDef> BuildGlyphs()
    {
        var glyphs = new Dictionary<char, GlyphDef>();
        foreach ((char ch, string strokes) in RawGlyphData())
        {
            glyphs[ch] = ParseGlyph(strokes);
        }

        return glyphs;
    }

    private static GlyphDef ParseGlyph(string strokes)
    {
        string[] parts = strokes.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
        var segments = new Segment[parts.Length];
        float maxX = 0f;

        for (int i = 0; i < parts.Length; i++)
        {
            string[] nums = parts[i].Split(',');
            float x1 = float.Parse(nums[0], CultureInfo.InvariantCulture);
            float y1 = float.Parse(nums[1], CultureInfo.InvariantCulture);
            float x2 = float.Parse(nums[2], CultureInfo.InvariantCulture);
            float y2 = float.Parse(nums[3], CultureInfo.InvariantCulture);
            maxX = MathF.Max(maxX, MathF.Max(x1, x2));
            segments[i] = new Segment(new Vector2(x1, y1), new Vector2(x2, y2));
        }

        return new GlyphDef(MathF.Max(maxX + 1f, DefaultCellWidth), segments);
    }

    private static IEnumerable<(char Ch, string Strokes)> RawGlyphData()
    {
        yield return (' ', "");
        yield return ('!', "3,0,3,7;3,9,3,9");
        yield return ('"', "1,0,1,3;5,0,5,3");
        yield return ('#', "1,5,6,5;1,7,6,7;2,0,2,10;5,0,5,10");
        yield return ('$', "1,2,6,2;1,8,6,8;4,0,2,2;2,2,4,5;4,5,2,8;2,8,4,10");
        yield return ('%', "0,0,6,10;0,8,2,10;4,0,6,2");
        yield return ('&', "5,10,0,5;0,5,3,5;3,5,5,3;5,3,3,0;3,0,0,3;0,3,3,7;3,7,6,10");
        yield return ('\'', "3,0,3,3");
        yield return ('(', "4,0,2,0;2,0,1,5;1,5,2,10;2,10,4,10");
        yield return (')', "2,0,4,0;4,0,5,5;5,5,4,10;4,10,2,10");
        yield return ('*', "3,2,3,8;1,4,5,6;5,4,1,6");
        yield return ('+', "1,5,6,5;3,3,3,7");
        yield return (',', "3,8,2,10");
        yield return ('-', "1,5,6,5");
        yield return ('.', "3,9,3,9");
        yield return ('/', "6,0,1,10");
        yield return ('0', "1,0,5,0;5,0,6,2;6,2,6,8;6,8,5,10;5,10,1,10;1,10,0,8;0,8,0,2;0,2,1,0");
        yield return ('1', "2,2,3,0;3,0,3,10;1,10,5,10");
        yield return ('2', "1,2,2,0;2,0,5,0;5,0,6,2;6,2,6,4;6,4,0,10;0,10,6,10");
        yield return ('3', "1,0,6,0;6,0,6,10;6,10,1,10;1,5,5,5");
        yield return ('4', "4,0,4,10;0,0,0,5;0,5,6,5");
        yield return ('5', "6,0,1,0;1,0,1,5;1,5,5,5;5,5,6,7;6,7,6,9;6,9,5,10;5,10,1,10;1,10,0,8");
        yield return ('6', "5,0,2,0;2,0,0,2;0,2,0,8;0,8,2,10;2,10,5,10;5,10,6,8;6,8,6,6;6,6,5,5;5,5,1,5;1,5,0,4");
        yield return ('7', "0,0,6,0;6,0,2,10");
        yield return ('8', "1,0,5,0;5,0,6,2;6,2,6,4;6,4,5,5;5,5,1,5;1,5,0,4;0,4,0,2;0,2,1,0;1,5,5,5;5,5,6,6;6,6,6,8;6,8,5,10;5,10,1,10;1,10,0,8;0,8,0,6;0,6,1,5");
        yield return ('9', "5,10,2,10;2,10,0,6;0,6,0,4;0,4,1,3;1,3,5,3;5,3,6,4;6,4,6,8;6,8,5,10");
        yield return (':', "3,2,3,2;3,8,3,8");
        yield return (';', "3,2,3,2;3,8,2,10");
        yield return ('<', "5,5,1,0;1,0,5,5;5,5,1,10");
        yield return ('=', "1,3,6,3;1,7,6,7");
        yield return ('>', "1,5,5,0;5,0,1,5;1,5,5,10");
        yield return ('?', "1,2,2,0;2,0,5,0;5,0,6,2;6,2,6,4;6,4,3,6;3,6,3,8;3,9,3,9");
        yield return ('@', "4,10,1,10;1,10,0,8;0,8,0,2;0,2,2,0;2,0,5,0;5,0,6,2;6,2,6,4;6,4,4,6;4,6,4,4;4,4,6,4");
        yield return ('A', "0,10,3,0;3,0,6,10;1,6,5,6");
        yield return ('B', "0,0,0,10;0,0,5,0;5,0,6,2;6,2,6,4;6,4,5,5;5,5,0,5;0,5,5,5;5,5,6,7;6,7,6,9;6,9,5,10;5,10,0,10");
        yield return ('C', "6,2,5,0;5,0,1,0;1,0,0,2;0,2,0,8;0,8,1,10;1,10,5,10;5,10,6,8");
        yield return ('D', "0,0,0,10;0,0,4,0;4,0,6,2;6,2,6,8;6,8,4,10;4,10,0,10");
        yield return ('E', "6,0,0,0;0,0,0,10;0,10,6,10;0,5,5,5");
        yield return ('F', "0,0,0,10;0,0,6,0;0,5,5,5");
        yield return ('G', "6,2,5,0;5,0,1,0;1,0,0,2;0,2,0,8;0,8,1,10;1,10,5,10;5,10,6,8;6,8,6,6;6,6,4,6;4,6,4,5");
        yield return ('H', "0,0,0,10;6,0,6,10;0,5,6,5");
        yield return ('I', "0,0,6,0;3,0,3,10;0,10,6,10");
        yield return ('J', "4,0,6,0;6,0,6,8;6,8,5,10;5,10,1,10;1,10,0,8");
        yield return ('K', "0,0,0,10;0,5,6,0;0,5,6,10");
        yield return ('L', "0,0,0,10;0,10,6,10");
        yield return ('M', "0,10,0,0;0,0,3,5;3,5,6,0;6,0,6,10");
        yield return ('N', "0,10,0,0;0,0,6,10;6,10,6,0");
        yield return ('O', "1,0,5,0;5,0,6,2;6,2,6,8;6,8,5,10;5,10,1,10;1,10,0,8;0,8,0,2;0,2,1,0");
        yield return ('P', "0,10,0,0;0,0,5,0;5,0,6,2;6,2,6,4;6,4,5,5;5,5,0,5");
        yield return ('Q', "1,0,5,0;5,0,6,2;6,2,6,8;6,8,5,10;5,10,1,10;1,10,0,8;0,8,0,2;0,2,1,0;4,7,6,10");
        yield return ('R', "0,10,0,0;0,0,5,0;5,0,6,2;6,2,6,4;6,4,5,5;5,5,0,5;3,5,6,10");
        yield return ('S', "6,2,5,0;5,0,1,0;1,0,0,2;0,2,0,4;0,4,5,5;5,5,6,6;6,6,6,8;6,8,5,10;5,10,1,10;1,10,0,8");
        yield return ('T', "0,0,6,0;3,0,3,10");
        yield return ('U', "0,0,0,8;0,8,1,10;1,10,5,10;5,10,6,8;6,8,6,0");
        yield return ('V', "0,0,3,10;3,10,6,0");
        yield return ('W', "0,0,1,10;1,10,3,5;3,5,5,10;5,10,6,0");
        yield return ('X', "0,0,6,10;6,0,0,10");
        yield return ('Y', "0,0,3,5;6,0,3,5;3,5,3,10");
        yield return ('Z', "0,0,6,0;6,0,0,10;0,10,6,10");
        yield return ('[', "4,0,2,0;2,0,2,10;2,10,4,10");
        yield return ('\\', "0,0,6,10");
        yield return (']', "2,0,4,0;4,0,4,10;4,10,2,10");
        yield return ('^', "3,0,0,5;3,0,6,5");
        yield return ('_', "0,10,6,10");
        yield return ('`', "2,0,4,2");
        yield return ('{', "4,0,2,1;2,1,2,4;2,4,1,5;1,5,2,6;2,6,2,9;2,9,4,10");
        yield return ('|', "3,0,3,10");
        yield return ('}', "2,0,4,1;4,1,4,4;4,4,5,5;5,5,4,6;4,6,4,9;4,9,2,10");
        yield return ('~', "0,4,2,2;2,2,4,4;4,4,6,2");
    }
}
