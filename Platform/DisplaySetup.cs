using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorBreakout.Platform;

public static class DisplaySetup
{
    public const int DefaultWindowedWidth = 1280;
    public const int DefaultWindowedHeight = 900;

    private static readonly int[] StandardRefreshRatesHz =
    [
        30, 48, 50, 60, 75, 90, 100, 120, 144, 165, 180, 240, 360,
    ];

    public static DisplayMode GetPrimaryDisplayMode()
    {
        return GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
    }

    public static void ApplyNativeFullscreen(GraphicsDeviceManager graphics)
    {
        DisplayMode mode = GetPrimaryDisplayMode();
        graphics.PreferredBackBufferWidth = mode.Width;
        graphics.PreferredBackBufferHeight = mode.Height;
        graphics.IsFullScreen = true;
    }

    public static void ApplyWindowed(GraphicsDeviceManager graphics, int width = DefaultWindowedWidth, int height = DefaultWindowedHeight)
    {
        graphics.IsFullScreen = false;
        graphics.PreferredBackBufferWidth = width;
        graphics.PreferredBackBufferHeight = height;
    }

    /// <summary>
    /// Best-effort refresh rate for the primary display on any supported OS.
    /// </summary>
    public static int GetPrimaryRefreshRateHint()
    {
        if (TryGetPlatformRefreshRateHz(out int hz))
        {
            return SnapToStandardRefreshRate(hz);
        }

        return 60;
    }

    /// <summary>
    /// Raw platform refresh rate, or 0 if unknown. Safe to call on all OSes.
    /// </summary>
    public static int GetPlatformRefreshRateHz()
    {
        return TryGetPlatformRefreshRateHz(out int hz) ? hz : 0;
    }

    public static int SnapToStandardRefreshRate(double measuredHz)
    {
        int best = 60;
        float bestDifference = float.MaxValue;
        foreach (int rate in StandardRefreshRatesHz)
        {
            float difference = System.MathF.Abs((float)measuredHz - rate);
            if (difference < bestDifference)
            {
                bestDifference = difference;
                best = rate;
            }
        }

        return best;
    }

    public static void BeginRefreshRateCalibration(Microsoft.Xna.Framework.Game game, GraphicsDeviceManager graphics)
    {
        graphics.SynchronizeWithVerticalRetrace = false;
        game.IsFixedTimeStep = false;
    }

    public static void ApplyFrameRateLock(Microsoft.Xna.Framework.Game game, GraphicsDeviceManager graphics, int refreshRateHz)
    {
        refreshRateHz = System.Math.Clamp(refreshRateHz, 30, 360);
        game.IsFixedTimeStep = true;
        game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / refreshRateHz);
        graphics.SynchronizeWithVerticalRetrace = true;
    }

    private static bool TryGetPlatformRefreshRateHz(out int refreshRateHz)
    {
        refreshRateHz = 0;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return WindowsDisplayRefreshRate.TryQueryHz(out refreshRateHz);
    }
}

public sealed class RefreshRateCalibrator
{
    private const double CalibrationDurationSeconds = 0.75;
    private const int MinimumSamples = 45;
    private readonly List<double> _frameSeconds = new(256);
    private double _peakHz;

    public bool IsComplete { get; private set; }
    public int MeasuredHz { get; private set; }

    public void Reset(int fallbackHz)
    {
        _frameSeconds.Clear();
        _peakHz = 0;
        IsComplete = false;
        MeasuredHz = System.Math.Clamp(fallbackHz, 30, 360);
    }

    public void AddSample(GameTime gameTime)
    {
        if (IsComplete)
        {
            return;
        }

        double frameSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        if (frameSeconds > 0.0002 && frameSeconds < 0.2)
        {
            _frameSeconds.Add(frameSeconds);
            _peakHz = System.Math.Max(_peakHz, 1.0 / frameSeconds);
        }

        double totalSeconds = 0;
        foreach (double sample in _frameSeconds)
        {
            totalSeconds += sample;
        }

        if (_frameSeconds.Count < MinimumSamples || totalSeconds < CalibrationDurationSeconds)
        {
            return;
        }

        double averageHz = 1.0 / _frameSeconds.Average();
        int platformHz = DisplaySetup.GetPlatformRefreshRateHz();
        double bestHz = System.Math.Max(_peakHz, averageHz);
        if (platformHz > 0)
        {
            bestHz = System.Math.Max(bestHz, platformHz);
        }

        MeasuredHz = DisplaySetup.SnapToStandardRefreshRate(bestHz);
        IsComplete = true;
    }
}
