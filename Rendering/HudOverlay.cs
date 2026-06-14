using Microsoft.Xna.Framework;
using VectorBreakout.Game;

namespace VectorBreakout.Rendering;

public static class HudOverlay
{
    private const float HudMargin = 24f;
    private const float ScoreScale = 3.75f;
    private const float LifeIconRadius = 8f;
    private const float LifeIconSpacing = 28f;
    private const float PromptScale = 2.35f;
    private const float CountdownScale = 6.5f;
    private const float GameOverScale = 4.2f;

    private static float UiScale => PlayfieldMetrics.Scale;

    public static void DrawScoreAndLives(VectorLineRenderer renderer, int score, int lives)
    {
        Color hudColor = Color.White;
        float scale = UiScale;
        float margin = HudMargin * scale;
        Vector2 scorePosition = new Vector2(margin, margin);
        VectorLordsFont.DrawInteger(renderer, score, scorePosition, ScoreScale * scale, hudColor);

        float scoreHeight = VectorLordsFont.GlyphHeight * ScoreScale * scale;
        Vector2 livesOrigin = new Vector2(margin, margin + scoreHeight + 14f * scale);
        for (int i = 0; i < lives; i++)
        {
            Vector2 center = livesOrigin + new Vector2(i * LifeIconSpacing * scale, 0f);
            BallOutline.DrawOctagon(renderer, center, LifeIconRadius * scale, Color.White);
        }
    }

    public static void DrawPressButton1ToStart(VectorLineRenderer renderer)
    {
        float scale = UiScale;
        VectorLordsFont.DrawString(
            renderer,
            "PRESS BUTTON 1 TO START",
            new Vector2(HudMargin * scale, HudMargin * scale),
            PromptScale * scale,
            Color.White);
    }

    public static void DrawGameOver(VectorLineRenderer renderer, Vector2 playfieldCenter)
    {
        float scale = UiScale;
        Color accent = new Color(255, 120, 120);
        VectorLordsFont.DrawStringCentered(
            renderer,
            "GAME OVER",
            playfieldCenter - new Vector2(0f, 40f * scale),
            GameOverScale * scale,
            accent);
    }

    public static void DrawLaunchCountdown(VectorLineRenderer renderer, Vector2 playfieldCenter, int secondsRemaining)
    {
        float scale = UiScale;
        VectorLordsFont.DrawIntegerCentered(
            renderer,
            secondsRemaining,
            playfieldCenter + new Vector2(0f, 20f * scale),
            CountdownScale * scale,
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
        float scale = UiScale;
        float lineGap = 52f * scale;

        VectorLordsFont.DrawStringCentered(
            renderer,
            "LEVEL CLEAR",
            playfieldCenter - new Vector2(0f, 150f * scale),
            titleScale * scale,
            accent);
        VectorLordsFont.DrawIntegerCentered(
            renderer,
            baseScore,
            playfieldCenter - new Vector2(0f, 35f * scale),
            mainScale * scale,
            hudColor);

        Vector2 bonusLabelCenter = playfieldCenter + new Vector2(0f, 35f * scale + lineGap);
        if (bonusScore > 0 || showGrandTotal)
        {
            VectorLordsFont.DrawStringCentered(renderer, "BONUS", bonusLabelCenter, labelScale * scale, accent);
            VectorLordsFont.DrawIntegerCentered(
                renderer,
                bonusScore,
                bonusLabelCenter + new Vector2(0f, 38f * scale),
                valueScale * scale,
                accent);
        }

        if (showGrandTotal)
        {
            Vector2 totalLabelCenter = playfieldCenter + new Vector2(0f, 35f * scale + lineGap * 2.4f);
            VectorLordsFont.DrawStringCentered(renderer, "TOTAL", totalLabelCenter, labelScale * scale, hudColor);
            VectorLordsFont.DrawIntegerCentered(
                renderer,
                baseScore + bonusScore,
                totalLabelCenter + new Vector2(0f, 42f * scale),
                mainScale * scale,
                hudColor);
        }

        if (showAdvancePrompt)
        {
            VectorLordsFont.DrawStringCentered(
                renderer,
                "PRESS BUTTON 2 TO CONTINUE",
                playfieldCenter + new Vector2(0f, 230f * scale),
                promptScale * scale,
                Color.White);
        }
    }
}
