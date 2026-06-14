using Microsoft.Xna.Framework;

namespace VectorBreakout.Game;

/// <summary>
/// Uniform playfield layout scaled from a 1080p reference. Center rings keep comfortable ball
/// travel lanes; paddle orbit targets a corridor of ~20% of screen width to the outer center ring.
/// </summary>
public static class PlayfieldMetrics
{
    private const float ReferenceMinDimension = 1080f;
    private const float PaddleToCenterCorridorScreenWidthFraction = 0.20f;
    private const float PaddleSizeScale = 0.75f;
    private const float MinRingTravelGapReference = 16f;
    private const float PaddleWallClearanceReference = 6f;

    public const float RefArenaPadding = 28f;
    public const float RefCenterBrickStartRadius = 60f;
    public const int RefCenterBrickRingCount = 4;
    public const float RefCenterBrickRingSpacing = 30f;
    public const int RefCenterBricksPerRing = 18;
    public const float RefCenterBrickRadialThickness = 12f;
    public const float RefOuterWallThickness = 16f;
    public const float RefBallRadius = 6f;
    public const float RefLaunchSpeed = 255f;
    public const float RefPaddleThickness = 18f;
    public const float RefCenterGravityStrength = 110f;

    public static float Scale { get; private set; } = 1f;
    public static Vector2 Center { get; private set; }
    public static float ArenaRadius { get; private set; }
    public static int ViewportWidth { get; private set; }
    public static int ViewportHeight { get; private set; }

    public static float CenterBrickStartRadius { get; private set; }
    public static int CenterBrickRingCount { get; private set; }
    public static float CenterBrickRingSpacing { get; private set; }
    public static float PaddleOrbitRadius { get; private set; }
    public static float PaddleToCenterCorridor { get; private set; }

    public static float S(float referencePixels) => referencePixels * Scale;

    public static int CenterBricksPerRing => RefCenterBricksPerRing;
    public static float CenterBrickRadialThickness => S(RefCenterBrickRadialThickness);
    public static float CenterBrickHalfThickness => S(RefCenterBrickRadialThickness) * 0.5f;
    public static float OuterWallThickness => S(RefOuterWallThickness);
    public static float BallRadius => S(RefBallRadius);
    public static float LaunchSpeed => S(RefLaunchSpeed);
    public static float PaddleThickness => S(RefPaddleThickness);
    public static float CenterGravityStrength => S(RefCenterGravityStrength);
    public static float WallInnerRadius => ArenaRadius - (OuterWallThickness * 0.5f);

    public static float CenterBrickOuterRadius =>
        CenterBrickStartRadius + ((CenterBrickRingCount - 1) * CenterBrickRingSpacing) + CenterBrickHalfThickness;

    public static void Update(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        float minDim = MathF.Min(width, height);
        Scale = minDim / ReferenceMinDimension;
        Center = new Vector2(width * 0.5f, height * 0.5f);
        ArenaRadius = (minDim * 0.5f) - S(RefArenaPadding);

        SolveCenterClusterAndPaddle(width);
    }

    private static void SolveCenterClusterAndPaddle(int viewportWidth)
    {
        float desiredCorridor = viewportWidth * PaddleToCenterCorridorScreenWidthFraction;
        float paddleHalfThickness = PaddleThickness * PaddleSizeScale * 0.5f;
        float wallInner = WallInnerRadius;
        float maxPaddleOrbit = wallInner - paddleHalfThickness - S(PaddleWallClearanceReference);
        float minRingSpacing = CenterBrickRadialThickness + S(MinRingTravelGapReference);
        float preferredSpacing = S(RefCenterBrickRingSpacing);
        float startRadius = S(RefCenterBrickStartRadius);

        int rings = RefCenterBrickRingCount;
        float spacing = preferredSpacing;

        while (true)
        {
            float outerRadius = startRadius + ((rings - 1) * spacing) + CenterBrickHalfThickness;
            float idealOrbit = outerRadius + desiredCorridor + paddleHalfThickness;
            float orbitRadius = MathF.Min(idealOrbit, maxPaddleOrbit);
            float corridor = orbitRadius - paddleHalfThickness - outerRadius;

            if (orbitRadius > startRadius + CenterBrickHalfThickness + minRingSpacing && corridor > 0f)
            {
                CenterBrickRingCount = rings;
                CenterBrickRingSpacing = spacing;
                CenterBrickStartRadius = startRadius;
                PaddleOrbitRadius = orbitRadius;
                PaddleToCenterCorridor = corridor;
                return;
            }

            if (spacing > minRingSpacing)
            {
                spacing = MathF.Max(minRingSpacing, spacing - S(2f));
                continue;
            }

            if (rings > 2)
            {
                rings--;
                spacing = preferredSpacing;
                continue;
            }

            CenterBrickRingCount = 2;
            CenterBrickRingSpacing = minRingSpacing;
            CenterBrickStartRadius = startRadius;
            outerRadius = startRadius + minRingSpacing + CenterBrickHalfThickness;
            orbitRadius = MathF.Min(outerRadius + desiredCorridor + paddleHalfThickness, maxPaddleOrbit);
            PaddleOrbitRadius = orbitRadius;
            PaddleToCenterCorridor = MathF.Max(0f, orbitRadius - paddleHalfThickness - outerRadius);
            return;
        }
    }
}
