using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorBreakout.Audio;
using VectorBreakout.Effects;
using VectorBreakout.Game;
using VectorBreakout.Input;
using VectorBreakout.Platform;
using VectorBreakout.Rendering;

namespace VectorBreakout;

public class Game1 : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D _pixel = null!;
    private VectorLineRenderer _lineRenderer = null!;
    private Effect _brickShimmerEffect = null!;
    private bool _hasBrickShimmerEffect;
    private Starfield _starfield = null!;
    private GameState _state = null!;
    private readonly RefreshRateCalibrator _refreshRateCalibrator = new();
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private bool _gameStarted;
    private bool _isCalibratingRefreshRate = true;
    private int _lockedRefreshRateHz;
    private ProceduralSfxPlayer? _sfx;
    private readonly BreakoutControllerInput _controllerInput = new();
    private bool _previousControllerConnected;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = !OperatingSystem.IsLinux();

        DisplaySetup.ApplyNativeFullscreen(_graphics);
        _graphics.ApplyChanges();

        _lockedRefreshRateHz = DisplaySetup.GetPrimaryRefreshRateHint();
        _refreshRateCalibrator.Reset(_lockedRefreshRateHz);
        DisplaySetup.BeginRefreshRateCalibration(this, _graphics);
    }

    protected override void Initialize()
    {
        _lineRenderer = new VectorLineRenderer(GraphicsDevice);
        _state = new GameState(GraphicsDevice.Viewport);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);

        try
        {
            _brickShimmerEffect = Content.Load<Effect>("Effects/BrickShimmer");
            _hasBrickShimmerEffect = true;
        }
        catch
        {
            _hasBrickShimmerEffect = false;
        }

        _starfield = new Starfield();
        try
        {
            _sfx = new ProceduralSfxPlayer();
            _state.SetSfx(_sfx);
        }
        catch (Microsoft.Xna.Framework.Audio.NoAudioHardwareException ex)
        {
            Console.Error.WriteLine($"[VectorBreakout] Audio unavailable; continuing without sound. {ex.Message}");
            _sfx = null;
        }

        _controllerInput.Initialize();
        ApplyViewportLayout();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_isCalibratingRefreshRate)
        {
            _refreshRateCalibrator.AddSample(gameTime);
            if (_refreshRateCalibrator.IsComplete)
            {
                _lockedRefreshRateHz = _refreshRateCalibrator.MeasuredHz;
                DisplaySetup.ApplyFrameRateLock(this, _graphics, _lockedRefreshRateHz);
                _isCalibratingRefreshRate = false;
            }

            _previousKeyboard = Keyboard.GetState();
            _previousMouse = Mouse.GetState();
            base.Update(gameTime);
            return;
        }

        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _controllerInput.Update(dt);

        if (_controllerInput.IsConnected != _previousControllerConnected)
        {
            _state.Paddle.ResetToDefault();
            _previousControllerConnected = _controllerInput.IsConnected;
        }

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (!_gameStarted && _controllerInput.TryConsumeMenuExitHold(dt, enabled: true))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.F11) && !_previousKeyboard.IsKeyDown(Keys.F11))
        {
            ToggleFullscreen();
        }

        bool leftClickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        bool spacePressed = keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space);
        bool button1Pressed = _controllerInput.LaunchPressed || spacePressed || leftClickPressed;

        bool gravityPressed = (keyboard.IsKeyDown(Keys.G) && !_previousKeyboard.IsKeyDown(Keys.G))
            || _controllerInput.GravityPressed;

        bool restartPressed = (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
            || _controllerInput.RestartPressed;

        if (!_gameStarted)
        {
            if (button1Pressed)
            {
                if (_state.IsGameOver)
                {
                    _state.Reset();
                }

                _gameStarted = true;
                _starfield.ApplyGravityVisuals(false);
                _state.BeginLaunchCountdown();
            }
        }
        else
        {
            if (_state.IsGameOver)
            {
                _gameStarted = false;
            }
            else if (restartPressed)
            {
                _gameStarted = false;
                _state.Reset();
                _starfield.ApplyGravityVisuals(false);
            }
            else if (_state.IsAwaitingLevelAdvance)
            {
                if (gravityPressed)
                {
                    _state.AdvanceToNextLevel();
                    _starfield.ApplyGravityVisuals(false);
                }
            }
            else if (_state.IsLaunchCountdownActive && button1Pressed)
            {
                _state.Launch();
            }
            else if (!_state.IsLevelClearActive && gravityPressed)
            {
                _state.ToggleCenterGravity();
                _starfield.ApplyGravityVisuals(_state.CenterGravityEnabled);
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;

        _starfield.Update(dt);
        PaddleControlInput? controllerInput = _controllerInput.GetPaddleControl(dt);
        _state.Update(dt, mouse, _gameStarted, controllerInput);

        Window.Title = BuildWindowTitle();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        if (_isCalibratingRefreshRate)
        {
            base.Draw(gameTime);
            return;
        }

        _spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        _starfield.Draw(_spriteBatch, _pixel);
        _spriteBatch.End();

        _lineRenderer.BeginFrame();
        _lineRenderer.SetScreenOffset(_state.ScreenShake.Offset);
        _state.Draw(_lineRenderer, (float)gameTime.TotalGameTime.TotalSeconds);
        _lineRenderer.SetScreenOffset(Vector2.Zero);
        if (_gameStarted)
        {
            HudOverlay.DrawScoreAndLives(_lineRenderer, _state.HudDisplayScore, _state.Lives);
            if (_state.IsLaunchCountdownActive)
            {
                HudOverlay.DrawLaunchCountdown(
                    _lineRenderer,
                    _state.PlayfieldCenter,
                    _state.LaunchCountdownDisplay);
            }
        }
        else
        {
            if (_state.IsGameOver)
            {
                HudOverlay.DrawGameOver(_lineRenderer, _state.PlayfieldCenter);
            }

            HudOverlay.DrawPressButton1ToStart(_lineRenderer);
        }

        _state.DrawLevelClearOverlay(_lineRenderer);

        _lineRenderer.Draw(
            brickShimmerEffect: _hasBrickShimmerEffect ? _brickShimmerEffect : null,
            totalTime: (float)gameTime.TotalGameTime.TotalSeconds,
            playfieldCenter: _state.PlayfieldCenter);

        _spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            effect: null);
        _state.Explosions.Sparks.Draw(_spriteBatch, _pixel, _state.ScreenShake.Offset);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private string BuildWindowTitle()
    {
        if (_state.IsGameOver)
        {
            return $"VectorBreakout — {_state.Score}";
        }

        if (_controllerInput.IsConnected)
        {
            string mode = _controllerInput.ActiveMode switch
            {
                BreakoutControllerMode.AbsolutePaddle => "paddle",
                BreakoutControllerMode.SpinnerDelta => "spinner",
                BreakoutControllerMode.VelocityStick => "stick",
                _ => "controller",
            };
            return $"VectorBreakout — {_state.Score} [{_controllerInput.DeviceName} · {mode}]";
        }

        if (!Joystick.IsSupported)
        {
            return $"VectorBreakout — {_state.Score} [no joystick API]";
        }

        return $"VectorBreakout — {_state.Score}";
    }

    private void ToggleFullscreen()
    {
        if (_graphics.IsFullScreen)
        {
            DisplaySetup.ApplyWindowed(_graphics);
        }
        else
        {
            DisplaySetup.ApplyNativeFullscreen(_graphics);
        }

        _graphics.ApplyChanges();
        ApplyViewportLayout();
    }

    private void ApplyViewportLayout()
    {
        Viewport viewport = GraphicsDevice.Viewport;
        _lineRenderer.ResizeViewport(viewport.Width, viewport.Height);
        _state.ResizeToViewport(viewport);
        _starfield.Configure(viewport.Width, viewport.Height, _state.ArenaRadius, _state.CenterBrickOuterRadius);
        _starfield.ApplyGravityVisuals(_state.CenterGravityEnabled);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lineRenderer.Dispose();
            _pixel.Dispose();
            _spriteBatch.Dispose();
            _sfx?.Dispose();
        }

        base.Dispose(disposing);
    }
}
