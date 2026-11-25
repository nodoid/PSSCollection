using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;

namespace PSSCollection.Core.Screens
{
    /// <summary>Runs one game, draws its HUD and hands the result to the high-score table.</summary>
    public sealed class PlayScreen : Screen
    {
        private static readonly Rectangle DesktopPlayfieldArea = new Rectangle(40, 104, 1200, 500);
        private static readonly Rectangle PauseButton = new Rectangle(1196, 20, 56, 48);
        private static readonly Rectangle ExitButton = new Rectangle(1128, 20, 56, 48);
        private static readonly Rectangle ResumeButton = new Rectangle(320, 412, 300, 62);
        private static readonly Rectangle QuitButton = new Rectangle(660, 412, 300, 62);

        private static readonly Vector2 DpadCentre = new Vector2(168, 654);
        private const float DpadRadius = 78f;
        private static readonly Vector2 FireCentre = new Vector2(1112, 654);
        private const float FireRadius = 58f;

        private readonly GameId _gameId;
        private readonly SkillLevel _skill;
        private readonly IGame _game;
        private readonly List<SoundId> _pendingSounds = new List<SoundId>();

        private const float TouchMoveThreshold = 2.5f;

        /// <summary>Hopper and Light Cycle never shoot, so their touch layout has no fire button.</summary>
        private bool UsesFire => _gameId is GameId.Invaders or GameId.Centipede or GameId.ElektroStorm;

        /// <summary>Invaders steers by tilting the phone; tapping the screen still fires.</summary>
        private static bool UsesTilt(GameId id) => id == GameId.Invaders;

        /// <summary>Touches that start on a HUD button belong to that button, never the game.</summary>
        private static bool InHudButton(Vector2 point)
        {
            int x = (int)point.X;
            int y = (int)point.Y;
            return ExitButton.Contains(x, y) || PauseButton.Contains(x, y);
        }

        private double _finishedTimer;
        private bool _finished;
        private int _bestBefore;
        private Vector2? _lastTouchRaw;
        private double _steerHold;
        private int _steerX;
        private int _steerY;
        private bool _tapCandidate;

        public PlayScreen(PssGame game, GameId gameId, SkillLevel skill) : base(game)
        {
            _gameId = gameId;
            _skill = skill;
            _game = Create(gameId);
        }

        private static IGame Create(GameId id)
        {
            switch (id)
            {
                case GameId.Invaders: return new InvadersGame();
                case GameId.Centipede: return new CentipedeGame();
                case GameId.Hopper: return new HopperGame();
                case GameId.LightCycle: return new LightCycleGame();
                default: return new ElektroStormGame();
            }
        }

        public override void Enter()
        {
            _bestBefore = Game.Scores.Best(_gameId);
            _game.Start(_skill, new XorShiftRandom((ulong)DateTime.UtcNow.Ticks));
            Game.Audio.PlayMusic(_gameId);
        }

        public override void Leave() => Game.Audio.StopMusic();

        public override void Update(double deltaSeconds, GameInput input)
        {
            if (input.Back)
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new MenuScreen(Game));
                return;
            }

            if (_finished)
            {
                _finishedTimer += deltaSeconds;
                if (_finishedTimer >= 1.4)
                    Finish();
                return;
            }

            if (Game.IsMobile)
            {
                // Leaving a game must be possible on a device with no keyboard: the HUD exit
                // button, or QUIT on the pause panel.
                if (input.TouchPressedIn(ExitButton))
                {
                    Game.Audio.Play(SoundId.MenuSelect);
                    Go(new MenuScreen(Game));
                    return;
                }

                if (_game.IsPaused && input.TouchPressedIn(ResumeButton))
                {
                    _game.IsPaused = false;
                    Game.Audio.Play(SoundId.MenuSelect);
                    return;
                }

                if (_game.IsPaused && input.TouchPressedIn(QuitButton))
                {
                    Game.Audio.Play(SoundId.MenuSelect);
                    Go(new MenuScreen(Game));
                    return;
                }
            }

            bool pause = input.Pause;
            if (Game.IsMobile && input.TouchPressedIn(PauseButton))
                pause = true;
            if (_game.IsPaused && input.Confirm)
                pause = true;

            AimStormIfPointer(input);

            InputState state = BuildPlayerInput(deltaSeconds, input, pause);
            _game.Update(deltaSeconds, state);

            PlaySounds();

            if (_game.IsGameOver)
            {
                _finished = true;
                _finishedTimer = 0;
                Game.Audio.StopMusic();
            }
        }

        /// <summary>Touch and mouse aim the crosshair directly in the Missile Command game.</summary>
        private void AimStormIfPointer(GameInput input)
        {
            if (_game is not ElektroStormGame storm)
                return;

            if (input.PointerClicked)
            {
                Vector2 point = ToPlayfield(input.Pointer);
                storm.Target(point.X, point.Y);
                return;
            }

            // A touch screen aims with the drag-and-tap controls instead: the crosshair follows
            // the stick and a tap fires, which needs no absolute touch mapping.
            _ = storm;
        }

        /// <summary>On a phone the game fills the screen; desktop keeps the framed board.</summary>
        private Rectangle PlayfieldArea => Game.IsMobile
            ? new Rectangle(0, 0, Graphics.ScreenCanvas.VirtualWidth, Graphics.ScreenCanvas.VirtualHeight)
            : DesktopPlayfieldArea;

        private Vector2 ToPlayfield(Vector2 screen)
        {
            var view = new PlayfieldView(PlayfieldArea,
                new System.Numerics.Vector2(ElektroStormGame.Width, ElektroStormGame.Height));
            return new Vector2((screen.X - view.Offset.X) / view.Scale, (screen.Y - view.Offset.Y) / view.Scale);
        }

        private InputState BuildPlayerInput(double deltaSeconds, GameInput input, bool pause)
        {
            bool left = input.Player.Left;
            bool right = input.Player.Right;
            bool up = input.Player.Up;
            bool down = input.Player.Down;
            bool fire = input.Player.Fire;
            bool firePressed = input.Player.FirePressed;

            if (Game.IsMobile && UsesTilt(_gameId))
            {
                // Only taps on the playfield fire: the HUD buttons must never shoot.
                bool touching = false;
                foreach (TouchContact touch in input.Touches)
                {
                    if (InHudButton(touch.Position))
                        continue;

                    touching = true;
                    break;
                }

                float tilt = Game.Platform.Tilt;
                if (tilt < -0.15f)
                    left = true;
                else if (tilt > 0.15f)
                    right = true;

                if (touching)
                {
                    fire = true;
                    if (_lastTouchRaw == null)
                        firePressed = true;
                }

                _lastTouchRaw = touching ? new Vector2(0, 0) : (Vector2?)null;
                return new InputState(left, right, up, down, fire, pause, firePressed);
            }

            if (Game.IsMobile)
            {
                Vector2? raw = null;
                foreach (TouchContact touch in input.Touches)
                {
                    if (InHudButton(touch.Position))
                        continue;

                    raw = touch.Raw;
                    break;
                }

                // Steering follows frame-to-frame movement of the finger. Comparing successive
                // positions needs no anchoring and no stable touch id, which is what made the
                // earlier drag scheme dead on a real device.
                if (raw.HasValue && _lastTouchRaw.HasValue)
                {
                    Vector2 delta = raw.Value - _lastTouchRaw.Value;
                    if (Math.Abs(delta.X) > TouchMoveThreshold || Math.Abs(delta.Y) > TouchMoveThreshold)
                    {
                        if (Math.Abs(delta.X) >= Math.Abs(delta.Y))
                        {
                            _steerX = Math.Sign(delta.X);
                            _steerY = 0;
                        }
                        else
                        {
                            _steerX = 0;
                            _steerY = Math.Sign(delta.Y);
                        }

                        _steerHold = 0.22;
                        _tapCandidate = false;
                    }
                }

                if (raw.HasValue)
                {
                    if (_steerHold > 0)
                    {
                        _steerHold -= deltaSeconds;
                        left = _steerX < 0;
                        right = _steerX > 0;
                        up = _steerY < 0;
                        down = _steerY > 0;
                    }
                    else
                    {
                        // Standing finger: fall back to where it is on the screen, measured
                        // against the space the device has actually used.
                        float nx = input.TouchSpaceWidth > 0 ? raw.Value.X / input.TouchSpaceWidth : 0.5f;
                        float ny = input.TouchSpaceHeight > 0 ? raw.Value.Y / input.TouchSpaceHeight : 0.5f;

                        if (Math.Abs(nx - 0.5f) >= Math.Abs(ny - 0.5f))
                        {
                            if (nx < 0.45f)
                                left = true;
                            else if (nx > 0.55f)
                                right = true;
                        }
                        else
                        {
                            if (ny < 0.45f)
                                up = true;
                            else if (ny > 0.55f)
                                down = true;
                        }
                    }

                    if (UsesFire && _game is not ElektroStormGame)
                        fire = true;
                }
                else
                {
                    _steerHold = 0;

                    // Missile Command spends a shot on a deliberate tap and nothing else.
                    if (_game is ElektroStormGame && _tapCandidate)
                        firePressed = true;
                }

                _tapCandidate = raw.HasValue && _steerHold <= 0;
                _lastTouchRaw = raw;
            }

            return new InputState(left, right, up, down, fire, pause, firePressed);
        }

        private void PlaySounds()
        {
            IReadOnlyList<SoundId> sounds = _game.DrainSounds();
            if (sounds.Count == 0)
                return;

            _pendingSounds.Clear();
            foreach (SoundId sound in sounds)
            {
                if (_pendingSounds.Count >= 4)
                    break;
                _pendingSounds.Add(sound);
            }

            foreach (SoundId sound in _pendingSounds)
                Game.Audio.Play(sound);
        }

        private void Finish()
        {
            int score = _game.Score;
            int award = score;

            if (Game.Scores.Book.Qualifies(_gameId, award))
            {
                Go(new NameEntryScreen(Game, _gameId, _skill, award, _game.Level));
                return;
            }

            Go(new HighScoresScreen(Game, _gameId, _skill));
        }

        public override void Draw(double time)
        {
            GameInfo info = Catalogue.Get(_gameId);
            Color accent = Palette.ForGame(_gameId);

            Canvas.Fill(Palette.Background);

            var view = new PlayfieldView(PlayfieldArea, _game.PlayfieldSize);
            EntityRenderer.Background(Canvas, _gameId, view, _game.BackgroundPhase);

            foreach (Entity entity in _game.Entities)
                EntityRenderer.Draw(Canvas, view, entity, _gameId, time);

            DrawHud(info, accent, time);

            if (_game.IsShowingMessage)
            {
                float alpha = MathHelper.Clamp((float)_game.MessageTimer, 0f, 1f);
                // Full-screen play puts the alien rows right under the HUD, so mobile shows the
                // message in the empty middle of the field instead.
                Canvas.TextCentredShadowed(_game.Message,
                    new Rectangle(0, Game.IsMobile ? 400 : PlayfieldArea.Y + 28, 1280, 40), 3f,
                    Palette.WithAlpha(Palette.Amber, alpha), Palette.WithAlpha(Palette.Black, alpha));
            }

            if (_game.IsPaused)
                DrawPauseOverlay(accent);

            if (_finished)
                DrawGameOverOverlay(time);

            if (Game.IsMobile)
                DrawTouchControls(time);
            else
                Canvas.TextCentred(info.Controls, new Rectangle(0, 624, 1280, 26), 1.7f, Palette.WithAlpha(Palette.TextDim, 0.8f));
        }

        private void DrawHud(GameInfo info, Color accent, double time)
        {
            // On mobile the game runs underneath the HUD, so the bar is translucent there.
            Canvas.Rect(0, 0, 1280, 96, Game.IsMobile ? new Color(8, 11, 22, 198) : new Color(8, 11, 22));
            Canvas.Rect(0, 94, 1280, 2, Palette.WithAlpha(accent, 0.55f));

            Canvas.TextShadowed(info.Title, new Vector2(40, 18), 3f, Palette.Text, Palette.Background);

            var skillBadge = new Rectangle(40, 56, 132, 26);
            Canvas.RoundedRect(skillBadge, Palette.WithAlpha(Palette.ForSkill(_skill), 0.18f), 6);
            Canvas.TextCentred(SkillLevels.Name(_skill), skillBadge, 1.8f, Palette.ForSkill(_skill));

            Canvas.TextCentred("SCORE", new Rectangle(400, 12, 480, 18), 1.6f, Palette.TextDim);
            Canvas.TextCentredShadowed(Ui.FormatScore(_game.Score), new Rectangle(400, 30, 480, 44), 4.2f,
                Palette.Text, Palette.Background);

            int best = Math.Max(_bestBefore, _game.Score);
            Canvas.TextCentred("BEST " + Ui.FormatScore(best), new Rectangle(400, 74, 480, 18), 1.5f, Palette.TextDim);

            // On mobile the exit and pause buttons own the top right corner, so wave and
            // lives stop short of them instead of running underneath.
            int hudRight = Game.IsMobile ? 1076 : 1120;
            Canvas.TextRight("WAVE " + _game.Level, new Rectangle(hudRight - 300, 20, 300, 26), 2.6f, accent);
            Canvas.TextRight("LIVES", new Rectangle(hudRight - 300, 56, 300, 18), 1.5f, Palette.TextDim);

            for (int i = 0; i < Math.Min(_game.Lives, 6); i++)
            {
                float x = hudRight - i * 34;
                Canvas.Line(new Vector2(x, 86), new Vector2(x - 10, 66), Palette.Green, 3f);
                Canvas.Line(new Vector2(x, 86), new Vector2(x + 10, 66), Palette.Green, 3f);
                Canvas.Line(new Vector2(x - 10, 66), new Vector2(x + 10, 66), Palette.Cyan, 2.5f);
            }

            if (Game.IsMobile)
            {
                Canvas.RoundedRect(ExitButton, Palette.WithAlpha(Palette.Red, 0.18f), 8);
                Canvas.RoundedOutline(ExitButton, Palette.WithAlpha(Palette.Red, 0.55f), 8, 1);
                Canvas.Line(new Vector2(ExitButton.X + 19, ExitButton.Y + 15),
                    new Vector2(ExitButton.X + 37, ExitButton.Y + 33), Palette.Red, 3f);
                Canvas.Line(new Vector2(ExitButton.X + 37, ExitButton.Y + 15),
                    new Vector2(ExitButton.X + 19, ExitButton.Y + 33), Palette.Red, 3f);

                Canvas.RoundedRect(PauseButton, Palette.WithAlpha(accent, 0.18f), 8);
                Canvas.Rect(PauseButton.X + 18, PauseButton.Y + 14, 7, 20, Palette.Text);
                Canvas.Rect(PauseButton.X + 31, PauseButton.Y + 14, 7, 20, Palette.Text);
            }

            float pulse = 0.4f + 0.3f * Ui.Pulse(time, 2.0);
            Canvas.Rect(40, 100, 1200, 2, Palette.WithAlpha(accent, pulse));
        }

        private void DrawPauseOverlay(Color accent)
        {
            Canvas.Rect(ScreenCanvas.FullScreen, new Color(0, 0, 0, 200));
            Canvas.TextCentredShadowed("PAUSED", new Rectangle(0, 280, 1280, 70), 6f, Palette.Text, Palette.Black);
            Canvas.TextCentred(Game.IsMobile ? "TAP TO RESUME OR QUIT" : "PRESS FIRE OR P TO RESUME",
                new Rectangle(0, 366, 1280, 26), 2f, accent);

            if (Game.IsMobile)
            {
                Ui.Button(Canvas, ResumeButton, "RESUME", true, accent, 2.6f);
                Ui.Button(Canvas, QuitButton, "QUIT", true, Palette.Red, 2.6f);
            }
            else
            {
                Ui.Button(Canvas, ResumeButton, "RESUME", true, accent, 2.6f);
                Canvas.TextCentred("ESC LEAVES THE GAME", new Rectangle(0, 500, 1280, 24), 1.8f, Palette.TextDim);
            }
        }

        private void DrawGameOverOverlay(double time)
        {
            Canvas.Rect(ScreenCanvas.FullScreen, new Color(0, 0, 0, 170));
            Canvas.TextCentredShadowed("GAME OVER", new Rectangle(0, 292, 1280, 80), 6.5f, Palette.Magenta, Palette.Black);
            Canvas.TextCentred("SCORE " + Ui.FormatScore(_game.Score), new Rectangle(0, 380, 1280, 30), 2.6f, Palette.Text);
            _ = time;
        }

        private void DrawTouchControls(double time)
        {
            float pulse = 0.5f + 0.2f * Ui.Pulse(time, 2.4);

            if (UsesTilt(_gameId))
            {
                // Tilt steering: no direction pad at all, just the fire instruction.
                // Tilt steers and a tap anywhere fires, so nothing is drawn over the field.
                return;
            }

            Canvas.Circle(DpadCentre, DpadRadius, new Color(255, 255, 255, 18));
            Canvas.CircleOutline(DpadCentre, DpadRadius, Palette.WithAlpha(Palette.Cyan, 0.35f), 2f);
            Canvas.Circle(DpadCentre, 26, new Color(255, 255, 255, 14));

            Canvas.Line(DpadCentre + new Vector2(-58, 0), DpadCentre + new Vector2(-28, -16), Palette.WithAlpha(Palette.Cyan, pulse), 4f);
            Canvas.Line(DpadCentre + new Vector2(-58, 0), DpadCentre + new Vector2(-28, 16), Palette.WithAlpha(Palette.Cyan, pulse), 4f);
            Canvas.Line(DpadCentre + new Vector2(58, 0), DpadCentre + new Vector2(28, -16), Palette.WithAlpha(Palette.Cyan, pulse), 4f);
            Canvas.Line(DpadCentre + new Vector2(58, 0), DpadCentre + new Vector2(28, 16), Palette.WithAlpha(Palette.Cyan, pulse), 4f);

            if (UsesFire)
            {
                Canvas.Circle(FireCentre, FireRadius, new Color(255, 255, 255, 18));
                Canvas.CircleOutline(FireCentre, FireRadius, Palette.WithAlpha(Palette.Magenta, 0.5f), 3f);
                Canvas.Circle(FireCentre, FireRadius * 0.62f, Palette.WithAlpha(Palette.Magenta, 0.16f));
                Canvas.TextCentred("FIRE", new Rectangle((int)(FireCentre.X - 50), (int)(FireCentre.Y - 12), 100, 24),
                    2f, Palette.WithAlpha(Palette.Magenta, 0.9f));
            }

            Canvas.TextCentred(UsesFire ? "DRAG TO MOVE - YOUR GUNS FIRE AS YOU MOVE" : "DRAG TO MOVE",
                new Rectangle(0, 600, 1280, 22), 1.6f, Palette.WithAlpha(Palette.TextDim, 0.85f));
        }
    }
}
