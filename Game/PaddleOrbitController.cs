using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace VectorBreakout.Game;

public sealed class PaddleOrbitController
{
    /// <summary>
    /// HID axis delta of 1.0 equals 180° of spinner shaft rotation (firmware position mode).
    /// </summary>
    private const float SpinnerAxisToOrbitRadians = MathHelper.Pi;

    /// <summary>
    /// Normalized axis wraps at ±1.0; unwrap jumps larger than this as one revolution.
    /// </summary>
    private const float SpinnerAxisWrapThreshold = 1.5f;

    public float Angle;
    public float PreviousAngle;
    public float OrbitRadius;
    public float AngularSpeed = 2.6f;
    public float HalfArcWidth = 0.18f;

    /// <summary>
    /// How quickly the paddle catches up to the spinner target each second.
    /// Higher = snappier, lower = smoother. ~100–160 feels close to raw input.
    /// </summary>
    public float SpinnerAngleSmoothingRate = 90f;

    private float _targetAngle;
    private float _previousSpinnerAxis;
    private bool _spinnerNeedsCalibration = true;

    public PaddleOrbitController(float orbitRadius)
    {
        OrbitRadius = orbitRadius;
        _targetAngle = Angle;
        PreviousAngle = Angle;
    }

    public void ResetToDefault()
    {
        Angle = 0f;
        _targetAngle = 0f;
        PreviousAngle = 0f;
        ResetSpinnerCalibration();
    }

    public void ResetSpinnerCalibration()
    {
        _spinnerNeedsCalibration = true;
    }

    public void Update(float dt, MouseState mouse, int viewportWidth, float? spinnerAxis = null)
    {
        PreviousAngle = Angle;

        if (spinnerAxis.HasValue)
        {
            ApplySpinnerDelta(spinnerAxis.Value);
            SmoothAngleTowardTarget(dt);
            return;
        }

        _spinnerNeedsCalibration = true;

        if (viewportWidth > 1)
        {
            float normalizedX = MathHelper.Clamp(mouse.X / (float)viewportWidth, 0f, 1f);
            Angle = MathHelper.TwoPi * normalizedX;
            _targetAngle = Angle;
            return;
        }

        UpdateFromKeyboardAndGamePad(dt);
        _targetAngle = Angle;
    }

    private void ApplySpinnerDelta(float axis)
    {
        axis = MathHelper.Clamp(axis, -1f, 1f);

        if (_spinnerNeedsCalibration)
        {
            _previousSpinnerAxis = axis;
            _spinnerNeedsCalibration = false;
            _targetAngle = Angle;
            return;
        }

        float deltaAxis = axis - _previousSpinnerAxis;
        if (deltaAxis > SpinnerAxisWrapThreshold)
        {
            deltaAxis -= 2f;
        }
        else if (deltaAxis < -SpinnerAxisWrapThreshold)
        {
            deltaAxis += 2f;
        }

        _targetAngle += deltaAxis * SpinnerAxisToOrbitRadians;
        _previousSpinnerAxis = axis;
    }

    private void SmoothAngleTowardTarget(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        float delta = _targetAngle - Angle;
        if (MathF.Abs(delta) < 0.00002f)
        {
            Angle = _targetAngle;
            return;
        }

        float blend = 1f - MathF.Exp(-SpinnerAngleSmoothingRate * dt);
        Angle += delta * blend;
    }

    private void UpdateFromKeyboardAndGamePad(float dt)
    {
        KeyboardState keyboard = Keyboard.GetState();
        GamePadState gamePad = GamePad.GetState(PlayerIndex.One);

        float input = 0f;
        if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A))
        {
            input -= 1f;
        }

        if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D))
        {
            input += 1f;
        }

        input += gamePad.ThumbSticks.Left.X;
        input += gamePad.Triggers.Right - gamePad.Triggers.Left;
        input = MathHelper.Clamp(input, -1f, 1f);

        Angle += input * AngularSpeed * dt;
    }

    public Vector2 GetPosition(Vector2 center)
    {
        return center + new Vector2(MathF.Cos(Angle), MathF.Sin(Angle)) * OrbitRadius;
    }

    public Vector2 GetForward()
    {
        return new Vector2(MathF.Cos(Angle), MathF.Sin(Angle));
    }
}
