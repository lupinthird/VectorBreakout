using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Math;

public static class CurveSampler
{
    public static List<Vector2> SampleQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, int segments)
    {
        segments = System.Math.Max(2, segments);
        var points = new List<Vector2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            points.Add(Bezier.Quadratic(p0, p1, p2, t));
        }

        return points;
    }

    public static List<Vector2> SampleCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int segments)
    {
        segments = System.Math.Max(2, segments);
        var points = new List<Vector2>(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            points.Add(Bezier.Cubic(p0, p1, p2, p3, t));
        }

        return points;
    }

    public static List<Vector2> SampleArcAsQuadratic(Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        float mid = (startAngle + endAngle) * 0.5f;
        Vector2 p0 = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;
        Vector2 p2 = center + new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle)) * radius;
        Vector2 p1 = center + new Vector2(MathF.Cos(mid), MathF.Sin(mid)) * radius;
        return SampleQuadratic(p0, p1, p2, segments);
    }
}
