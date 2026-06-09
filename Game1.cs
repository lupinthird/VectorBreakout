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
    private readonly SpinnerControllerInput _spinnerInput = new();
    private bool _previousSpinnerConnected;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

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
        _sfx = new ProceduralSfxPlayer();
        _state.SetSfx(_sfx);
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
        _spinnerInput.Update();

        if (_spinnerInput.IsConnected != _previousSpinnerConnected)
        {
            _state.Paddle.ResetSpinnerCalibration();
            _previousSpinnerConnected = _spinnerInput.IsConnected;
        }

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
        }

        if (keyboard.IsKeyDown(Keys.F11) && !_previousKeyboard.IsKeyDown(Keys.F11))
        {
            ToggleFullscreen();
        }

        bool leftClickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        bool spacePressed = keyboard.IsKeyDown(Keys.Space) && !_previousKeyboard.IsKeyDown(Keys.Space);
        bool launchPressed = spacePressed || leftClickPressed || _spinnerInput.LaunchPressed;

        bool gravityPressed = leftClickPressed
            || (keyboard.IsKeyDown(Keys.G) && !_previousKeyboard.IsKeyDown(Keys.G))
            || _spinnerInput.GravityPressed;

        bool restartPressed = (keyboard.IsKeyDown(Keys.R) && !_previousKeyboard.IsKeyDown(Keys.R))
            || _spinnerInput.RestartPressed;

        if (!_gameStarted)
        {
            if (launchPressed)
            {
                _gameStarted = true;
            }
        }
        else
        {
            if (restartPressed)
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
            else if (!_state.IsLevelClearActive)
            {
                if (!_state.IsLaunched && (launchPressed || gravityPressed))
                {
                    _state.Launch();
                }
                else if (gravityPressed)
                {
                    _state.ToggleCenterGravity();
                    _starfield.ApplyGravityVisuals(_state.CenterGravityEnabled);
                }
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _starfield.Update(dt);
        float? spinnerAxis = _spinnerInput.IsConnected ? _spinnerInput.SpinAxisRaw : null;
        _state.Update(dt, mouse, _gameStarted, spinnerAxis);

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
            if (!_state.IsLaunched && !_state.IsLevelClearActive)
            {
                HudOverlay.DrawLaunchPrompt(_lineRenderer, _state.PlayfieldCenter);
            }
        }
        else
        {
            HudOverlay.DrawStartPrompt(_lineRenderer);
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

        if (_spinnerInput.IsConnected)
        {
            return $"VectorBreakout — {_state.Score} [{_spinnerInput.DeviceName}]";
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
