using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PSSCollection.Core.Audio;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Core.Persistence;
using PSSCollection.Core.Platform;
using PSSCollection.Core.Screens;

namespace PSSCollection.Core
{
    /// <summary>
    /// The shared MonoGame application: a 1280x720 virtual canvas, the screen stack, the
    /// score service and the sound bank. Every platform head constructs one of these.
    /// </summary>
    public sealed class PssGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly IPlatformServices _platform;

        private SpriteBatch _spriteBatch;
        private RenderTarget2D _target;
        private InputManager _input;
        private Screen _screen;
        private Rectangle _destination;
        private double _time;
        private bool _applyingSize;

        public PssGame(IPlatformServices platform)
        {
            _platform = platform ?? new DesktopPlatformServices();

            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = ScreenCanvas.VirtualWidth,
                PreferredBackBufferHeight = ScreenCanvas.VirtualHeight,
                SynchronizeWithVerticalRetrace = true,
                HardwareModeSwitch = false,
                PreferMultiSampling = false
            };

            Content.RootDirectory = "Content";
            IsMouseVisible = !_platform.IsMobile;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
            Window.Title = "The PSS Oric Collection";
            Window.AllowUserResizing = !_platform.IsMobile;

            SizeWindowToDisplay();

            Scores = new ScoreService(_platform);
            _platform.SetOrientation(this, false);
        }

        /// <summary>
        /// Fits the window on the current display without distorting it. A window larger than
        /// the screen would otherwise clip the virtual canvas.
        /// </summary>
        private void SizeWindowToDisplay()
        {
            if (_platform.IsMobile)
                return;

            DisplayMode mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            if (mode.Width <= 0 || mode.Height <= 0)
                return;

            float scale = Math.Min(mode.Width * 0.82f / ScreenCanvas.VirtualWidth,
                mode.Height * 0.82f / ScreenCanvas.VirtualHeight);
            scale = Math.Min(scale, 1f);
            if (scale <= 0.2f)
                scale = 1f;

            _graphics.PreferredBackBufferWidth = (int)(ScreenCanvas.VirtualWidth * scale);
            _graphics.PreferredBackBufferHeight = (int)(ScreenCanvas.VirtualHeight * scale);
        }

        public ScoreService Scores { get; }

        public SoundBank Audio { get; private set; }

        public ScreenCanvas Canvas { get; private set; }

        public IPlatformServices Platform => _platform;

        public bool IsMobile => _platform.IsMobile;

        public string BuildLabel
        {
            get
            {
                Version version = GetType().Assembly.GetName().Version;
                return "v" + (version?.ToString(3) ?? "1.0.0");
            }
        }

        public Screen CurrentScreen => _screen;

        /// <summary>Replaces the current screen.</summary>
        public void Navigate(Screen screen)
        {
            if (screen == null)
                return;

            _screen?.Leave();
            _screen = screen;
            _screen.Enter();
        }

        /// <summary>Quits where the platform allows it.</summary>
        public void QuitToDesktop() => _platform.Quit(this);

        protected override void Initialize()
        {
            Window.ClientSizeChanged += (_, _) => SyncBackBufferToWindow();
            base.Initialize();
            ComputeDestination();
        }

        /// <summary>Keeps the render target blit aligned with the actual window after a resize.</summary>
        private void SyncBackBufferToWindow()
        {
            if (_applyingSize || _platform.IsMobile)
            {
                ComputeDestination();
                return;
            }

            int width = Window.ClientBounds.Width;
            int height = Window.ClientBounds.Height;
            if (width <= 0 || height <= 0)
                return;

            if (width == _graphics.PreferredBackBufferWidth && height == _graphics.PreferredBackBufferHeight)
            {
                ComputeDestination();
                return;
            }

            _applyingSize = true;
            try
            {
                _graphics.PreferredBackBufferWidth = width;
                _graphics.PreferredBackBufferHeight = height;
                _graphics.ApplyChanges();
            }
            finally
            {
                _applyingSize = false;
            }

            ComputeDestination();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            Canvas = new ScreenCanvas(GraphicsDevice, _spriteBatch);
            _target = new RenderTarget2D(GraphicsDevice, ScreenCanvas.VirtualWidth, ScreenCanvas.VirtualHeight,
                false, SurfaceFormat.Color, DepthFormat.None);
            _input = new InputManager(_platform.IsMobile);
            Audio = new SoundBank();

            Navigate(new TitleScreen(this));
            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            Audio?.Dispose();
            Canvas?.Dispose();
            _target?.Dispose();
            _spriteBatch?.Dispose();
            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            double delta = Math.Min(gameTime.ElapsedGameTime.TotalSeconds, 0.1);
            ComputeDestination();
            _input.Update(_destination, (float)delta);
            _screen?.Update(delta, _input.Current);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            _time += gameTime.ElapsedGameTime.TotalSeconds;

            GraphicsDevice.SetRenderTarget(_target);
            GraphicsDevice.Clear(Palette.Background);

            Canvas.Begin();
            _screen?.Draw(_time);
            Canvas.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_target, _destination, Color.White);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private int _debugFrames;

        private void ComputeDestination()
        {
            // The viewport is the surface actually being drawn into; the presentation
            // parameters can lag behind a window that the OS has resized.
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;
            if (width <= 0 || height <= 0)
                return;

            float scale = Math.Min(width / (float)ScreenCanvas.VirtualWidth, height / (float)ScreenCanvas.VirtualHeight);
            int destinationWidth = (int)(ScreenCanvas.VirtualWidth * scale);
            int destinationHeight = (int)(ScreenCanvas.VirtualHeight * scale);

            var destination = new Rectangle((width - destinationWidth) / 2, (height - destinationHeight) / 2,
                destinationWidth, destinationHeight);

            if (destination != _destination)
                _destination = destination;
        }
    }
}
