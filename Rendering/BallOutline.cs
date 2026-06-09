using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Rendering;

public static class BallOutline
{
    private const int DefaultSides = 8;
    private const float AngleOffset = MathHelper.PiOver4 * 0.5f;

    public static void DrawOctagon(
        VectorLineRenderer renderer,
        Vector2 center,
        float radius,
        Color color,
        int sides = DefaultSides)
    {
        var points = BuildUnitOctagon(sides);
        for (int i = 0; i < points.Count; i++)
        {
            points[i] = center + points[i] * radius;
        }

        points.Add(points[0]);
        renderer.AddPolyline(points, color);
    }

    public static void DrawOctagonFilled(
        VectorLineRenderer renderer,
        Vector2 center,
        float radius,
        Color fillColor,
        Color outlineColor,
        int sides = DefaultSides)
    {
        var points = BuildUnitOctagon(sides);
        for (int i = 0; i < points.Count; i++)
        {
            points[i] = center + points[i] * radius;
        }

        points.Add(points[0]);
        renderer.AddSolidPolygonFill(points, center, fillColor);
        renderer.AddPolyline(points, outlineColor);
    }

    public static void DrawDeformed(
        VectorLineRenderer renderer,
        Vector2 center,
        float baseRadius,
        Vector2 velocity,
        float maxSpeed,
        Vector2 impactNormal,
        float impactSquash,
        Color color,
        int sides = DefaultSides)
    {
        List<Vector2> points = BuildDeformedOutline(
            center,
            baseRadius,
            velocity,
            maxSpeed,
            impactNormal,
            impactSquash,
            sides);
        points.Add(points[0]);
        renderer.AddPolyline(points, color);
    }

    public static void DrawDeformedFilled(
        VectorLineRenderer renderer,
        Vector2 center,
        float baseRadius,
        Vector2 velocity,
        float maxSpeed,
        Vector2 impactNormal,
        float impactSquash,
        Color fillColor,
        Color outlineColor,
        int sides = DefaultSides)
    {
        List<Vector2> points = BuildDeformedOutline(
            center,
            baseRadius,
            velocity,
            maxSpeed,
            impactNormal,
            impactSquash,
            sides);
        points.Add(points[0]);
        renderer.AddSolidPolygonFill(points, center, fillColor);
        renderer.AddPolyline(points, outlineColor);
    }

    public static List<Vector2> BuildDeformedOutline(
        Vector2 center,
        float baseRadius,
        Vector2 velocity,
        float maxSpeed,
        Vector2 impactNormal,
        float impactSquash,
        int sides = DefaultSides)
    {
        float speed = velocity.Length();
        Vector2 velDir = speed > 8f ? velocity / speed : Vector2.UnitY;
        float speedT = maxSpeed > 1f ? MathHelper.Clamp(speed / maxSpeed, 0f, 1f) : 0f;
        float speedStretchT = speedT > 0.9f ? MathHelper.Clamp((speedT - 0.9f) / 0.1f, 0f, 1f) : 0f;
        speedStretchT *= speedStretchT;
        float impactDampen = 1f - MathHelper.Clamp(impactSquash * 1.2f, 0f, 1f);
        speedStretchT *= impactDampen;
        float stretchAlongVel = 1f + speedStretchT * 0.09f;
        float squeezePerpVel = 1f - speedStretchT * 0.04f;

        float squashAlongImpact = 1f - impactSquash * 0.4f;
        float bulgePerpImpact = 1f + impactSquash * 0.24f;
        bool useImpact = impactSquash > 0.02f && impactNormal.LengthSquared() > 0.0001f;
        Vector2 impactDir = useImpact ? Vector2.Normalize(impactNormal) : Vector2.Zero;

        var points = new List<Vector2>(sides);
        for (int i = 0; i < sides; i++)
        {
            float angle = MathHelper.TwoPi * (i / (float)sides) + AngleOffset;
            Vector2 unit = new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle));

            float parallelVel = Vector2.Dot(unit, velDir);
            Vector2 perpVel = unit - (velDir * parallelVel);
            Vector2 radial = (velDir * (parallelVel * baseRadius * stretchAlongVel))
                + (perpVel * baseRadius * squeezePerpVel);

            if (useImpact)
            {
                float parallelImpact = Vector2.Dot(radial, impactDir);
                Vector2 impactParallel = impactDir * parallelImpact;
                Vector2 impactTangent = radial - impactParallel;
                radial = (impactParallel * squashAlongImpact) + (impactTangent * bulgePerpImpact);
            }

            points.Add(center + radial);
        }

        return points;
    }

    private static List<Vector2> BuildUnitOctagon(int sides)
    {
        var points = new List<Vector2>(sides);
        for (int i = 0; i < sides; i++)
        {
            float angle = MathHelper.TwoPi * (i / (float)sides) + AngleOffset;
            points.Add(new Vector2(System.MathF.Cos(angle), System.MathF.Sin(angle)));
        }

        return points;
    }
}
