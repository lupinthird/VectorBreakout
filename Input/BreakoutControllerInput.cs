using L3Controller.Input;
using Microsoft.Xna.Framework;

namespace VectorBreakout.Input;

public enum BreakoutControllerMode
{
    None,
    AbsolutePaddle,
    SpinnerDelta,
    VelocityStick,
}

public readonly struct PaddleControlInput
{
    public BreakoutControllerMode Mode { get; init; }
    public float AbsoluteOrbit01 { get; init; }
    public float SpinnerDeltaRadians { get; init; }
    public float StickVelocity { get; init; }
}

/// <summary>
/// Single-player L3 controller input via <see cref="ControllerManager"/> slot claiming.
/// Paddle devices use absolute orbit positioning; spinner devices use axis delta and speed.
/// </summary>
public sealed class BreakoutControllerInput
{
    public const float SpinnerSensitivity = 1f;

    /// <summary>HID button 1 — start game / launch ball.</summary>
    public const int ButtonLaunch = 0;

    /// <summary>HID button 2 — gravity toggle / continue after level clear.</summary>
    public const int ButtonGravity = 1;

    /// <summary>HID button 3 — restart run.</summary>
    public const int ButtonRestart = 2;

    private readonly ControllerManager _manager = new(maxSlots: 1);
    private readonly PaddleAxisCalibration _paddleCalibration = new();
    private readonly MenuExitHold _menuExitHold = new();
    private BreakoutControllerMode? _lockedMode;
    private string? _boundDeviceId;

    public bool IsConnected => _manager.GetSlot(0) != null;

    public string? DeviceName
    {
        get
        {
            TrackedController? tracked = _manager.GetSlot(0);
            if (tracked == null)
            {
                return null;
            }

            if (tracked.TryGetDeviceInfo(out L3DeviceInfo info))
            {
                return info.DisplayName;
            }

            return string.IsNullOrWhiteSpace(tracked.ClaimLabel) ? tracked.DisplayName : tracked.ClaimLabel;
        }
    }

    public BreakoutControllerMode ActiveMode => _lockedMode ?? BreakoutControllerMode.None;

    public bool LaunchPressed { get; private set; }
    public bool GravityPressed { get; private set; }
    public bool RestartPressed { get; private set; }

    public float MenuExitHoldProgress => _menuExitHold.HoldProgress;

    public bool TryConsumeMenuExitHold(float deltaSeconds, bool enabled)
    {
        _menuExitHold.Update(_manager.GetSlot(0), deltaSeconds, enabled);
        return _menuExitHold.Triggered;
    }

    public void Initialize() => _manager.Initialize();

    public void Update(float deltaSeconds)
    {
        _manager.Update(deltaSeconds);

        LaunchPressed = WasButtonPressed(ButtonLaunch);
        GravityPressed = WasButtonPressed(ButtonGravity);
        RestartPressed = WasButtonPressed(ButtonRestart);
    }

    public PaddleControlInput? GetPaddleControl(float deltaSeconds)
    {
        TrackedController? tracked = _manager.GetSlot(0);
        if (tracked == null)
        {
            ClearBinding();
            return null;
        }

        EnsureBinding(tracked);
        BreakoutControllerMode mode = ResolveMode(tracked);

        return mode switch
        {
            BreakoutControllerMode.AbsolutePaddle => new PaddleControlInput
            {
                Mode = mode,
                AbsoluteOrbit01 = MapCalibratedPaddleToOrbit01(tracked),
            },
            BreakoutControllerMode.SpinnerDelta => new PaddleControlInput
            {
                Mode = mode,
                SpinnerDeltaRadians = ComputeSpinnerDeltaRadians(tracked, deltaSeconds),
            },
            BreakoutControllerMode.VelocityStick => new PaddleControlInput
            {
                Mode = mode,
                StickVelocity = ReadStickVelocity(tracked.Current.LeftStick),
            },
            _ => null,
        };
    }

    public void ResetBinding()
    {
        ClearBinding();
        _menuExitHold.Reset();
        _manager.ResetClaims();
    }

    private void EnsureBinding(TrackedController tracked)
    {
        if (tracked.Id == _boundDeviceId)
        {
            return;
        }

        _boundDeviceId = tracked.Id;
        _lockedMode = null;
        _paddleCalibration.Reset();
    }

    private void ClearBinding()
    {
        _boundDeviceId = null;
        _lockedMode = null;
        _paddleCalibration.Reset();
    }

    private BreakoutControllerMode ResolveMode(TrackedController tracked)
    {
        if (_lockedMode is BreakoutControllerMode locked)
        {
            return locked;
        }

        DeviceProfile profile = ResolveProfile(tracked);
        BreakoutControllerMode inferred = InferMode(tracked, profile);

        if (profile is DeviceProfile.Spinner or DeviceProfile.Paddle or DeviceProfile.GenericGamepad)
        {
            _lockedMode = inferred;
            return inferred;
        }

        if (TryInferModeFromActivity(tracked, out BreakoutControllerMode activityMode))
        {
            _lockedMode = activityMode;
            return activityMode;
        }

        _lockedMode = inferred;
        return inferred;
    }

    private static DeviceProfile ResolveProfile(TrackedController tracked)
    {
        if (tracked.TryGetDeviceInfo(out L3DeviceInfo info) && info.IsValid)
        {
            return info.Type;
        }

        DeviceProfile fromName = L3ControllerIdentity.GetDeviceProfile(tracked.DisplayName);
        return fromName != DeviceProfile.Unknown ? fromName : tracked.Profile;
    }

    private static BreakoutControllerMode InferMode(TrackedController tracked, DeviceProfile profile) =>
        profile switch
        {
            DeviceProfile.Spinner => BreakoutControllerMode.SpinnerDelta,
            DeviceProfile.Paddle => BreakoutControllerMode.AbsolutePaddle,
            DeviceProfile.GenericGamepad => BreakoutControllerMode.VelocityStick,
            DeviceProfile.Combo => InferComboMode(tracked),
            _ => InferFromSources(tracked, profile),
        };

    private static BreakoutControllerMode InferComboMode(TrackedController tracked)
    {
        if (TryInferModeFromActivity(tracked, out BreakoutControllerMode activityMode))
        {
            return activityMode;
        }

        return tracked.PaddleSource != PaddleSourceKind.None
            ? BreakoutControllerMode.AbsolutePaddle
            : BreakoutControllerMode.SpinnerDelta;
    }

    private static BreakoutControllerMode InferFromSources(TrackedController tracked, DeviceProfile profile)
    {
        if (profile == DeviceProfile.Paddle
            || (tracked.PaddleSource != PaddleSourceKind.None && tracked.SpinnerSource == SpinnerSourceKind.None))
        {
            return BreakoutControllerMode.AbsolutePaddle;
        }

        if (profile == DeviceProfile.Spinner
            || (tracked.SpinnerSource != SpinnerSourceKind.None && tracked.PaddleSource == PaddleSourceKind.None))
        {
            return BreakoutControllerMode.SpinnerDelta;
        }

        if (tracked.Current.HasLeftStick)
        {
            return BreakoutControllerMode.VelocityStick;
        }

        if (tracked.PaddleSource != PaddleSourceKind.None && tracked.SpinnerSource != SpinnerSourceKind.None)
        {
            return InferComboMode(tracked);
        }

        return BreakoutControllerMode.AbsolutePaddle;
    }

    private static bool TryInferModeFromActivity(TrackedController tracked, out BreakoutControllerMode mode)
    {
        bool spinMotion = InputMapping.DetectAxisChangeFromBaseline(
            tracked.Current.RawZ,
            tracked.Baseline.RawZ,
            InputMapping.SpinnerDeltaThreshold);
        bool potMotion = InputMapping.DetectAxisChangeFromBaseline(
            tracked.Current.RawRz,
            tracked.Baseline.RawRz,
            InputMapping.PaddleDeltaThreshold);

        if (potMotion && !spinMotion)
        {
            mode = BreakoutControllerMode.AbsolutePaddle;
            return true;
        }

        if (spinMotion && !potMotion)
        {
            mode = BreakoutControllerMode.SpinnerDelta;
            return true;
        }

        if (tracked.Previous != null)
        {
            float zDelta = MathF.Abs(L3ControllerIdentity.ComputeWrapAwareDelta(
                tracked.Current.RawZ ?? 0f,
                tracked.Previous.RawZ ?? 0f));
            float rzDelta = MathF.Abs(L3ControllerIdentity.ComputeWrapAwareDelta(
                tracked.Current.RawRz ?? 0f,
                tracked.Previous.RawRz ?? 0f));

            if (rzDelta >= InputMapping.PaddleDeltaThreshold && rzDelta > zDelta + 0.002f)
            {
                mode = BreakoutControllerMode.AbsolutePaddle;
                return true;
            }

            if (zDelta >= InputMapping.SpinnerDeltaThreshold && zDelta > rzDelta + 0.002f)
            {
                mode = BreakoutControllerMode.SpinnerDelta;
                return true;
            }
        }

        mode = BreakoutControllerMode.None;
        return false;
    }

    private float MapCalibratedPaddleToOrbit01(TrackedController tracked)
    {
        float paddleAxis = GetNormalizedPaddleAxis(tracked);
        _paddleCalibration.Sample(paddleAxis);
        return _paddleCalibration.MapTo01(paddleAxis);
    }

    private static float GetNormalizedPaddleAxis(TrackedController tracked) =>
        tracked.Current.RawRz is float rawRz
            ? InputMapping.MapPaddleFromAxis(rawRz)
            : tracked.PaddlePosition;

    private static float ComputeSpinnerDeltaRadians(TrackedController tracked, float deltaSeconds)
    {
        if (tracked.SpinnerSource == SpinnerSourceKind.LeftStickX)
        {
            float degrees = InputMapping.IntegrateSpinnerFromStick(tracked.Current.LeftStick.X, deltaSeconds);
            return MathHelper.ToRadians(degrees) * SpinnerSensitivity;
        }

        if (tracked.Current.RawZ is not float currentZ || tracked.Previous?.RawZ is not float previousZ)
        {
            return 0f;
        }

        float delta = L3ControllerIdentity.ComputeWrapAwareDelta(currentZ, previousZ);
        float axisDegrees = InputMapping.IntegrateSpinnerFromAxisDelta(delta);
        return MathHelper.ToRadians(axisDegrees) * SpinnerSensitivity;
    }

    private static float ReadStickVelocity(Vector2 stick)
    {
        if (MathF.Abs(stick.X) < InputMapping.ActivityThreshold)
        {
            return 0f;
        }

        return MathHelper.Clamp(stick.X, -1f, 1f);
    }

    private bool WasButtonPressed(int buttonIndex)
    {
        TrackedController? tracked = _manager.GetSlot(0);
        if (tracked?.Previous == null)
        {
            return false;
        }

        bool[] current = tracked.Current.Buttons;
        bool[] previous = tracked.Previous.Buttons;
        if (buttonIndex >= current.Length || buttonIndex >= previous.Length)
        {
            return false;
        }

        return current[buttonIndex] && !previous[buttonIndex];
    }
}
