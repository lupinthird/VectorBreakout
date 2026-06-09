using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
namespace VectorBreakout.Input;

/// <summary>
/// Reads the spinner through winmm.dll — the same multimedia joystick API joy.cpl uses.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SpinnerWinmmJoystickFallback
{
    private const int JoyReturnX = 0x00000001;
    private const int JoyReturnY = 0x00000002;
    private const int JoyReturnZ = 0x00000004;
    private const int JoyReturnR = 0x00000008;
    private const int JoyReturnButtons = 0x00000040;
    private const int JoyReturnAll = JoyReturnX | JoyReturnY | JoyReturnZ | JoyReturnR | JoyReturnButtons;

    private const int JoyButton1 = 0x0001;
    private const int JoyButton2 = 0x0002;
    private const int JoyButton3 = 0x0004;

    public readonly struct Reading
    {
        public float SpinAxis { get; init; }
        public bool LaunchHeld { get; init; }
        public bool GravityHeld { get; init; }
        public bool RestartHeld { get; init; }
        public string DeviceName { get; init; }
    }

    public static bool TryRead(out Reading reading)
    {
        reading = default;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        int deviceCount = joyGetNumDevs();
        if (deviceCount <= 0)
        {
            return false;
        }

        int selectedId = -1;
        string selectedName = string.Empty;
        int bestScore = int.MinValue;
        int readableCount = 0;

        for (int id = 0; id < deviceCount; id++)
        {
            if (!CanReadPosition(id))
            {
                continue;
            }

            readableCount++;
            TryGetDeviceName(id, out string name);
            int score = ScoreDeviceName(name);
            if (readableCount == 1 && bestScore < 0)
            {
                score = System.Math.Max(score, 25);
            }

            if (score > bestScore)
            {
                bestScore = score;
                selectedId = id;
                selectedName = string.IsNullOrWhiteSpace(name) ? $"Joystick {id}" : name;
            }
        }

        if (selectedId < 0)
        {
            return false;
        }

        var info = new JoyInfoEx
        {
            dwSize = Marshal.SizeOf<JoyInfoEx>(),
            dwFlags = JoyReturnAll,
        };

        if (joyGetPosEx(selectedId, ref info) != 0)
        {
            return false;
        }

        reading = new Reading
        {
            SpinAxis = ReadDominantAxis(info),
            LaunchHeld = (info.dwButtons & JoyButton1) != 0,
            GravityHeld = (info.dwButtons & JoyButton2) != 0,
            RestartHeld = (info.dwButtons & JoyButton3) != 0,
            DeviceName = selectedName,
        };

        return true;
    }

    private static float ReadDominantAxis(JoyInfoEx info)
    {
        float x = NormalizeWinmmAxis(info.dwXpos);
        float y = NormalizeWinmmAxis(info.dwYpos);
        float z = NormalizeWinmmAxis(info.dwZpos);
        float r = NormalizeWinmmAxis(info.dwRpos);

        float best = 0f;
        foreach (float candidate in new[] { z, x, y, r })
        {
            if (MathF.Abs(candidate) > MathF.Abs(best))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static float NormalizeWinmmAxis(int raw)
    {
        return (raw - 32767f) / 32767f;
    }

    private static int ScoreDeviceName(string? name)
    {
        int score = SpinnerControllerInput.ScoreL3ControllerName(name);
        if (score > 0)
        {
            return score;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (name.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
            || name.Contains("XInput", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        score = 10;
        if (name.Contains("axis", StringComparison.OrdinalIgnoreCase)
            && name.Contains("button", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        return score;
    }

    private static bool CanReadPosition(int deviceId)
    {
        var probe = new JoyInfoEx
        {
            dwSize = Marshal.SizeOf<JoyInfoEx>(),
            dwFlags = JoyReturnAll,
        };

        return joyGetPosEx(deviceId, ref probe) == 0;
    }

    private static bool TryGetDeviceName(int deviceId, out string name)
    {
        var caps = new JoyCaps();
        caps.szPname = new string('\0', 32);
        caps.szOEMVxD = new string('\0', 260);

        if (joyGetDevCaps(deviceId, ref caps, Marshal.SizeOf<JoyCaps>()) != 0)
        {
            name = string.Empty;
            return false;
        }

        name = caps.szPname.TrimEnd('\0').Trim();
        return !string.IsNullOrWhiteSpace(name);
    }

    [DllImport("winmm.dll")]
    private static extern int joyGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(int uJoyID, ref JoyInfoEx pji);

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int joyGetDevCaps(int uJoyID, ref JoyCaps pjc, int cbjc);

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public int dwSize;
        public int dwFlags;
        public int dwXpos;
        public int dwYpos;
        public int dwZpos;
        public int dwRpos;
        public int dwUpos;
        public int dwVpos;
        public int dwButtons;
        public int dwButtonNumber;
        public int dwPOV;
        public int dwReserved1;
        public int dwReserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct JoyCaps
    {
        public ushort wMid;
        public ushort wPid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public int wXmin;
        public int wXmax;
        public int wYmin;
        public int wYmax;
        public int wZmin;
        public int wZmax;
        public int wNumButtons;
        public int wPeriodMin;
        public int wPeriodMax;
        public int wRmin;
        public int wRmax;
        public int wUmin;
        public int wUmax;
        public int wVmin;
        public int wVmax;
        public int wCaps;
        public int wMaxAxes;
        public int wNumAxes;
        public int wMaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szRegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szOEMVxD;
    }
}
