using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorBreakout.Audio;

namespace VectorBreakout.Game;

public sealed class LevelClearSequence
{
    private const int BonusPointsPerOuterBrick = 500;
    private const float SecondsPerBrick = 0.075f;
    private const float GrandTotalPauseSeconds = 0.65f;

    private readonly List<CurvedBrickField.Brick> _outerBricks = new();
    private float _brickTimer;
    private float _grandTotalTimer;
    private int _nextBrickIndex;

    public bool IsActive { get; private set; }
    public bool IsTallying { get; private set; }
    public bool ShowGrandTotal { get; private set; }
    public bool AwaitingAdvance { get; private set; }
    public bool CornerScoreUpdated { get; private set; }

    public int BaseScoreAtClear { get; private set; }
    public int BonusAccumulated { get; private set; }
    public int GrandTotal => BaseScoreAtClear + BonusAccumulated;

    public void Begin(GameState state)
    {
        Reset();
        IsActive = true;
        IsTallying = true;
        BaseScoreAtClear = state.Score;
        state.Bricks.CollectAliveOuterWallBricks(_outerBricks);
        _nextBrickIndex = 0;
        _brickTimer = 0f;
    }

    public void Update(float dt, GameState state, ProceduralSfxPlayer? sfx)
    {
        if (!IsActive)
        {
            return;
        }

        if (IsTallying)
        {
            UpdateTally(dt, state, sfx);
            return;
        }

        if (ShowGrandTotal && !CornerScoreUpdated)
        {
            _grandTotalTimer += dt;
            if (_grandTotalTimer >= GrandTotalPauseSeconds)
            {
                state.ApplyLevelClearScore(GrandTotal);
                CornerScoreUpdated = true;
                AwaitingAdvance = true;
                ShowGrandTotal = false;
            }
        }
    }

    private void UpdateTally(float dt, GameState state, ProceduralSfxPlayer? sfx)
    {
        if (_nextBrickIndex >= _outerBricks.Count)
        {
            FinishTally();
            return;
        }

        _brickTimer += dt;
        while (_brickTimer >= SecondsPerBrick && _nextBrickIndex < _outerBricks.Count)
        {
            _brickTimer -= SecondsPerBrick;
            CurvedBrickField.Brick brick = _outerBricks[_nextBrickIndex++];
            state.Bricks.DestroyOuterWallImmediate(brick);
            BonusAccumulated += BonusPointsPerOuterBrick;
            sfx?.PlayTypewriterClick(0.55f);
        }

        if (_nextBrickIndex >= _outerBricks.Count)
        {
            FinishTally();
        }
    }

    private void FinishTally()
    {
        IsTallying = false;
        ShowGrandTotal = true;
        _grandTotalTimer = 0f;
    }

    public void Reset()
    {
        IsActive = false;
        IsTallying = false;
        ShowGrandTotal = false;
        AwaitingAdvance = false;
        CornerScoreUpdated = false;
        BaseScoreAtClear = 0;
        BonusAccumulated = 0;
        _outerBricks.Clear();
        _nextBrickIndex = 0;
        _brickTimer = 0f;
        _grandTotalTimer = 0f;
    }
}
