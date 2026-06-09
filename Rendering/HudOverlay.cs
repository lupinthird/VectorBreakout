using Microsoft.Xna.Framework;

namespace VectorBreakout.Rendering;

public static class HudOverlay
{
    private const float HudMargin = 24f;
    private const float ScoreScale = 3.75f;
    private const float LifeIconRadius = 8f;
    private const float LifeIconSpacing = 28f;
    private const float StartPromptScale = 2.35f;
    private const float LaunchPromptScale = 2.2f;

    public static void DrawScoreAndLives(VectorLineRenderer renderer, int score, int lives)
    {
        Color hudColor = Color.White;
        Vector2 scorePosition = new Vector2(HudMargin, HudMargin);
        VectorLineFont.DrawInteger(renderer, score, scorePosition, ScoreScale, hudColor);

        float scoreHeight = 9f * ScoreScale;
        Vector2 livesOrigin = new Vector2(HudMargin, HudMargin + scoreHeight + 14f);
        for (int i = 0; i < lives; i++)
        {
            Vector2 center = livesOrigin + new Vector2(i * LifeIconSpacing, 0f);
            BallOutline.DrawOctagon(renderer, center, LifeIconRadius, Color.White);
        }
    }

    public static void DrawStartPrompt(VectorLineRenderer renderer)
    {
        VectorLineFont.DrawString(
            renderer,
            "PRESS SPACE OR CLICK",
            new Vector2(HudMargin, HudMargin),
            StartPromptScale,
            Color.White);
    }

    public static void DrawLaunchPrompt(VectorLineRenderer renderer, Vector2 playfieldCenter)
    {
        VectorLineFont.DrawStringCentered(
            renderer,
            "PRESS SPACE OR CLICK TO LAUNCH",
            playfieldCenter + new Vector2(0f, 130f),
            LaunchPromptScale,
            Color.White);
    }

    public static void DrawLevelClear(
        VectorLineRenderer renderer,
        Vector2 playfieldCenter,
        int baseScore,
        int bonusScore,
        bool showGrandTotal,
        bool showAdvancePrompt)
    {
        const float titleScale = 3.1f;
        const float mainScale = 5.2f;
        const float labelScale = 2.6f;
        const float valueScale = 3.8f;
        const float promptScale = 2.35f;
        Color hudColor = Color.White;
        Color accent = new Color(180, 220, 255);

        float lineGap = 52f;

        VectorLineFont.DrawStringCentered(renderer, "LEVEL CLEAR", playfieldCenter - new Vector2(0f, 150f), titleScale, accent);
        VectorLineFont.DrawIntegerCentered(renderer, baseScore, playfieldCenter - new Vector2(0f, 35f), mainScale, hudColor);

        Vector2 bonusLabelCenter = playfieldCenter + new Vector2(0f, 35f + lineGap);
        if (bonusScore > 0 || showGrandTotal)
        {
            VectorLineFont.DrawStringCentered(renderer, "BONUS", bonusLabelCenter, labelScale, accent);
            VectorLineFont.DrawIntegerCentered(renderer, bonusScore, bonusLabelCenter + new Vector2(0f, 38f), valueScale, accent);
        }

        if (showGrandTotal)
        {
            Vector2 totalLabelCenter = playfieldCenter + new Vector2(0f, 35f + lineGap * 2.4f);
            VectorLineFont.DrawStringCentered(renderer, "TOTAL", totalLabelCenter, labelScale, hudColor);
            VectorLineFont.DrawIntegerCentered(renderer, baseScore + bonusScore, totalLabelCenter + new Vector2(0f, 42f), mainScale, hudColor);
        }

        if (showAdvancePrompt)
        {
            VectorLineFont.DrawStringCentered(
                renderer,
                "PRESS GRAVITY TO CONTINUE",
                playfieldCenter + new Vector2(0f, 230f),
                promptScale,
                Color.White);
        }
    }
}
