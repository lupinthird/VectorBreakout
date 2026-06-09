using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VectorBreakout.Input;

/// <summary>
/// Input reader for L3 Labs BLE HID controllers (L3 Spinner/Paddle/Combo).
/// Spinner/combo map encoder to HID Desktop Z; paddle/combo pot maps to Rz.
/// </summary>
public sealed class SpinnerControllerInput
{
    public const float DefaultSpinDeadZone = 0.04f;
    private const int MinimumScanSlots = 16;

    /// <summary>HID button 1 — start game / launch ball.</summary>
    public const int ButtonLaunch = 0;

    /// <summary>HID button 2 — gravity toggle / continue after level clear.</summary>
    public const int ButtonGravity = 1;

    /// <summary>HID button 3 — restart run.</summary>
    public const int ButtonRestart = 2;

    private static readonly Buttons[] GamePadActionButtons =
    [
        Buttons.A,
        Buttons.B,
        Buttons.X,
    ];

    private static bool _loggedDeviceScan;

    private int _deviceIndex = -1;
    private bool _useGamePadChannel;
    private bool _useWindowsFallback;
    private JoystickState _previousJoystick;
    private GamePadState _previousGamePad;
    private bool _previousWinLaunchHeld;
    private bool _previousWinGravityHeld;
    private bool _previousWinRestartHeld;
    private string? _windowsDeviceName;

    public bool IsConnected => _deviceIndex >= 0;

    public string? DeviceName =>
        _deviceIndex < 0
            ? null
            : _useWindowsFallback
                ? _windowsDeviceName
                : DescribeDevice(_deviceIndex, _useGamePadChannel);

    public float SpinAxis { get; private set; }

    /// <summary>Normalized axis without dead-zone zeroing; use for absolute spinner position.</summary>
    public float SpinAxisRaw { get; private set; }

    public bool LaunchPressed { get; private set; }

    public bool GravityPressed { get; private set; }

    public bool RestartPressed { get; private set; }

    public void Update(float spinDeadZone = DefaultSpinDeadZone)
    {
        if (!_loggedDeviceScan)
        {
            LogConnectedDevices();
            _loggedDeviceScan = true;
        }

        if (TryResolveDevice(out int deviceIndex, out bool useGamePadChannel))
        {
            _deviceIndex = deviceIndex;
            _useGamePadChannel = useGamePadChannel;
            _useWindowsFallback = false;

            JoystickState joystick = ReadJoystick(deviceIndex);
            GamePadState gamePad = ReadGamePad(deviceIndex);

            float rawAxis = ReadSpinAxisRaw(joystick, gamePad, useGamePadChannel);
            SpinAxisRaw = rawAxis;
            SpinAxis = MathF.Abs(rawAxis) < spinDeadZone
                ? 0f
                : MathHelper.Clamp(rawAxis, -1f, 1f);
            LaunchPressed = WasButtonPressed(ButtonLaunch, joystick, gamePad, useGamePadChannel);
            GravityPressed = WasButtonPressed(ButtonGravity, joystick, gamePad, useGamePadChannel);
            RestartPressed = WasButtonPressed(ButtonRestart, joystick, gamePad, useGamePadChannel);

            _previousJoystick = joystick;
            _previousGamePad = gamePad;
            return;
        }

        if (OperatingSystem.IsWindows()
            && SpinnerWinmmJoystickFallback.TryRead(out SpinnerWinmmJoystickFallback.Reading windowsReading))
        {
            _deviceIndex = 0;
            _useGamePadChannel = false;
            _useWindowsFallback = true;

            SpinAxis = MathF.Abs(windowsReading.SpinAxis) < spinDeadZone
                ? 0f
                : MathHelper.Clamp(windowsReading.SpinAxis, -1f, 1f);
            SpinAxisRaw = MathHelper.Clamp(windowsReading.SpinAxis, -1f, 1f);
            LaunchPressed = windowsReading.LaunchHeld && !_previousWinLaunchHeld;
            GravityPressed = windowsReading.GravityHeld && !_previousWinGravityHeld;
            RestartPressed = windowsReading.RestartHeld && !_previousWinRestartHeld;

            _previousWinLaunchHeld = windowsReading.LaunchHeld;
            _previousWinGravityHeld = windowsReading.GravityHeld;
            _previousWinRestartHeld = windowsReading.RestartHeld;
            _windowsDeviceName = windowsReading.DeviceName + " (winmm)";
            return;
        }

        _deviceIndex = -1;
        _useGamePadChannel = false;
        _useWindowsFallback = false;
        _windowsDeviceName = null;
        SpinAxis = 0f;
        SpinAxisRaw = 0f;
        LaunchPressed = false;
        GravityPressed = false;
        RestartPressed = false;
    }

    public static bool IsL3ControllerName(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.StartsWith("L3 ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseL3ControllerName(string? name, out string kind, out string color)
    {
        kind = string.Empty;
        color = string.Empty;

        if (!IsL3ControllerName(name))
        {
            return false;
        }

        string[] parts = name!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        kind = parts[1];
        color = parts[2];
        return true;
    }

    public static bool IsSpinnerDeviceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (TryParseL3ControllerName(name, out string kind, out _))
        {
            return kind.Equals("Spinner", StringComparison.OrdinalIgnoreCase)
                || kind.Equals("Combo", StringComparison.OrdinalIgnoreCase);
        }

        return name.Contains("Spinner", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Spinner Controller", StringComparison.OrdinalIgnoreCase);
    }

    public static int ScoreL3ControllerName(string? name)
    {
        if (!TryParseL3ControllerName(name, out string kind, out _))
        {
            return IsSpinnerDeviceName(name) ? 100 : 0;
        }

        if (kind.Equals("Spinner", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("Combo", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        if (kind.Equals("Paddle", StringComparison.OrdinalIgnoreCase))
        {
            return 80;
        }

        return 60;
    }

    private static bool TryResolveDevice(out int deviceIndex, out bool useGamePadChannel)
    {
        deviceIndex = -1;
        useGamePadChannel = false;

        if (!Joystick.IsSupported)
        {
            return TryResolveGamePadOnly(out deviceIndex, out useGamePadChannel);
        }

        int scanLimit = ComputeScanLimit();
        var joystickCandidates = new List<int>();
        int bestScore = int.MinValue;
        int bestIndex = -1;

        for (int index = 0; index <= scanLimit; index++)
        {
            if (!IsJoystickConnected(index))
            {
                continue;
            }

            joystickCandidates.Add(index);
            JoystickCapabilities caps = Joystick.GetCapabilities(index);
            int score = ScoreJoystickCandidate(caps);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }
        }

        if (bestScore > 0)
        {
            deviceIndex = bestIndex;
            return true;
        }

        if (joystickCandidates.Count == 1)
        {
            deviceIndex = joystickCandidates[0];
            return true;
        }

        return TryResolveGamePadOnly(out deviceIndex, out useGamePadChannel);
    }

    private static bool TryResolveGamePadOnly(out int deviceIndex, out bool useGamePadChannel)
    {
        deviceIndex = -1;
        useGamePadChannel = false;

        for (int index = 0; index < 4; index++)
        {
            if (!GamePad.GetCapabilities(index).IsConnected)
            {
                continue;
            }

            string name = GamePad.GetCapabilities(index).DisplayName;
            if (IsSpinnerDeviceName(name))
            {
                deviceIndex = index;
                useGamePadChannel = true;
                return true;
            }
        }

        return false;
    }

    private static int ScoreJoystickCandidate(JoystickCapabilities caps)
    {
        int score = ScoreL3ControllerName(caps.DisplayName);
        if (score > 0)
        {
            return score;
        }

        if (IsLikelyStandardGamepadName(caps.DisplayName))
        {
            return 0;
        }

        score = 10;
        if (caps.DisplayName.Contains("axis", StringComparison.OrdinalIgnoreCase)
            && caps.DisplayName.Contains("button", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (caps.ButtonCount >= 3)
        {
            score += 10;
        }

        if (caps.AxisCount is >= 1 and <= 8)
        {
            score += 5;
        }

        return score;
    }

    private static bool IsLikelyStandardGamepadName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("XInput", StringComparison.OrdinalIgnoreCase)
            || name.Contains("X-Input", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DualShock", StringComparison.OrdinalIgnoreCase)
            || name.Contains("DualSense", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Switch", StringComparison.OrdinalIgnoreCase);
    }

    private static int ComputeScanLimit()
    {
        int lastIndex = Joystick.IsSupported ? Joystick.LastConnectedIndex : -1;
        return System.Math.Max(MinimumScanSlots, lastIndex);
    }

    private static bool IsJoystickConnected(int deviceIndex)
    {
        return Joystick.IsSupported && Joystick.GetCapabilities(deviceIndex).IsConnected;
    }

    private static JoystickState ReadJoystick(int deviceIndex)
    {
        return IsJoystickConnected(deviceIndex)
            ? Joystick.GetState(deviceIndex)
            : default;
    }

    private static GamePadState ReadGamePad(int deviceIndex)
    {
        return GamePad.GetCapabilities(deviceIndex).IsConnected
            ? GamePad.GetState(deviceIndex)
            : default;
    }

    private static string DescribeDevice(int deviceIndex, bool useGamePadChannel)
    {
        if (useGamePadChannel && GamePad.GetCapabilities(deviceIndex).IsConnected)
        {
            return GamePad.GetCapabilities(deviceIndex).DisplayName;
        }

        if (IsJoystickConnected(deviceIndex))
        {
            return Joystick.GetCapabilities(deviceIndex).DisplayName;
        }

        return "controller";
    }

    private static float ReadSpinAxisRaw(
        JoystickState joystick,
        GamePadState gamePad,
        bool preferGamePadChannel)
    {
        float axis = 0f;

        if (preferGamePadChannel && gamePad.IsConnected)
        {
            axis = gamePad.ThumbSticks.Right.X;
        }
        else if (joystick.Axes.Length > 0)
        {
            axis = ReadDominantJoystickAxis(joystick);
        }
        else if (gamePad.IsConnected)
        {
            axis = gamePad.ThumbSticks.Right.X;
        }

        return MathHelper.Clamp(axis, -1f, 1f);
    }

    private static float ReadDominantJoystickAxis(JoystickState joystick)
    {
        float best = 0f;
        int limit = System.Math.Min(joystick.Axes.Length, 8);
        for (int i = 0; i < limit; i++)
        {
            float value = NormalizeAxis(joystick.Axes[i]);
            if (MathF.Abs(value) > MathF.Abs(best))
            {
                best = value;
            }
        }

        return best;
    }

    private static float NormalizeAxis(int raw)
    {
        if (System.Math.Abs(raw) <= 127)
        {
            return raw / 127f;
        }

        return MathHelper.Clamp(raw / 32767f, -1f, 1f);
    }

    private bool WasButtonPressed(
        int buttonIndex,
        JoystickState joystick,
        GamePadState gamePad,
        bool preferGamePadChannel)
    {
        if (!preferGamePadChannel && WasJoystickButtonPressed(buttonIndex, joystick))
        {
            return true;
        }

        if (buttonIndex < GamePadActionButtons.Length
            && gamePad.IsConnected
            && gamePad.IsButtonDown(GamePadActionButtons[buttonIndex])
            && _previousGamePad.IsButtonUp(GamePadActionButtons[buttonIndex]))
        {
            return true;
        }

        if (preferGamePadChannel)
        {
            return false;
        }

        return WasJoystickButtonPressed(buttonIndex, joystick);
    }

    private bool WasJoystickButtonPressed(int buttonIndex, JoystickState joystick)
    {
        if (buttonIndex >= joystick.Buttons.Length)
        {
            return false;
        }

        if (joystick.Buttons[buttonIndex] != ButtonState.Pressed)
        {
            return false;
        }

        return buttonIndex >= _previousJoystick.Buttons.Length
            || _previousJoystick.Buttons[buttonIndex] != ButtonState.Pressed;
    }

    private static void LogConnectedDevices()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Joystick.IsSupported: " + Joystick.IsSupported);

        int scanLimit = Joystick.IsSupported ? ComputeScanLimit() : 0;
        for (int index = 0; index <= scanLimit; index++)
        {
            if (IsJoystickConnected(index))
            {
                JoystickCapabilities caps = Joystick.GetCapabilities(index);
                builder.AppendLine(
                    $"  Joystick {index}: \"{caps.DisplayName}\" axes={caps.AxisCount} buttons={caps.ButtonCount} gamepad={caps.IsGamepad}");
            }
        }

        for (int index = 0; index < 4; index++)
        {
            if (GamePad.GetCapabilities(index).IsConnected)
            {
                GamePadCapabilities caps = GamePad.GetCapabilities(index);
                builder.AppendLine(
                    $"  GamePad {index}: \"{caps.DisplayName}\" type={caps.GamePadType}");
            }
        }

        if (OperatingSystem.IsWindows())
        {
            builder.AppendLine("winmm devices:");
            int winmmCount = SpinnerWinmmJoystickFallback.TryRead(out SpinnerWinmmJoystickFallback.Reading sample)
                ? 1
                : 0;
            builder.AppendLine("  readable: " + (winmmCount > 0 ? sample.DeviceName : "none"));
        }

        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "input_devices.log");
            File.WriteAllText(path, builder.ToString());
        }
        catch
        {
            // Diagnostics only.
        }
    }
}
