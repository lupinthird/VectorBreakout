using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using VectorBreakout.Input;

namespace VectorBreakout.Game;

public sealed class PaddleOrbitController
{
    public float Angle;
    public float PreviousAngle;
    public float OrbitRadius;
    public float AngularSpeed = 2.6f;
    public float HalfArcWidth = 0.18f;

    /// <summary>
    /// How quickly the paddle catches up to the spinner target each second.
    /// Higher = snappier, lower = smoother.
    /// </summary>
    public float SpinnerAngleSmoothingRate = 130f;

    private float _targetAngle;

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
    }

    public void Update(
        float dt,
        MouseState mouse,
        Vector2 playfieldCenter,
        PaddleControlInput? controllerInput = null)
    {
        PreviousAngle = Angle;

        if (controllerInput.HasValue)
        {
            ApplyControllerInput(controllerInput.Value, dt);
            return;
        }

        Vector2 mouseDelta = new Vector2(mouse.X, mouse.Y) - playfieldCenter;
        if (mouseDelta.LengthSquared() > 16f)
        {
            Angle = MathF.Atan2(mouseDelta.Y, mouseDelta.X);
            _targetAngle = Angle;
            return;
        }

        UpdateFromKeyboardAndGamePad(dt);
        _targetAngle = Angle;
    }

    private void ApplyControllerInput(PaddleControlInput input, float dt)
    {
        switch (input.Mode)
        {
            case BreakoutControllerMode.AbsolutePaddle:
                Angle = input.AbsoluteOrbit01 * MathHelper.TwoPi;
                _targetAngle = Angle;
                break;

            case BreakoutControllerMode.SpinnerDelta:
                _targetAngle += input.SpinnerDeltaRadians;
                SmoothAngleTowardTarget(dt);
                break;

            case BreakoutControllerMode.VelocityStick:
                Angle += input.StickVelocity * AngularSpeed * dt;
                _targetAngle = Angle;
                break;
        }
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
