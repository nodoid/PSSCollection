using System;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;

namespace PSSCollection.Core.Screens
{
    /// <summary>The front page menu: pick one of the PSS games.</summary>
    public sealed class MenuScreen : Screen
    {
        private const int GridColumns = 3;
        private const int CardWidth = 384;
        private const int CardHeight = 202;
        private const int GapX = 22;
        private const int GapY = 24;
        private const int GridTop = 118;

        private static readonly Rectangle QuitButton = new Rectangle(1146, 618, 96, 34);

        private int _index;
        private double _time;

        public MenuScreen(PssGame game) : base(game)
        {
        }

        private static int Count => Catalogue.Count;

        private static Rectangle CardRect(int index)
        {
            int totalWidth = GridColumns * CardWidth + (GridColumns - 1) * GapX;
            int left = (ScreenCanvas.VirtualWidth - totalWidth) / 2;

            int column = index % GridColumns;
            int row = index / GridColumns;
            return new Rectangle(left + column * (CardWidth + GapX), GridTop + row * (CardHeight + GapY),
                CardWidth, CardHeight);
        }

        public override void Enter() => Game.Audio.PlayMusic(Catalogue.At(_index));

        public override void Update(double deltaSeconds, GameInput input)
        {
            _time += deltaSeconds;

            if (Game.IsMobile && input.PointerClicked)
            {
                // A tap that misses every card still picks the closest one, so a small mapping
                // error cannot send the player into the wrong game.
                int picked = NearestCard(input.Pointer);
                _index = picked;
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new SkillScreen(Game, Catalogue.At(picked)));
                return;
            }

            // A click on any card picks that game straight away, not just the highlighted one.
            for (int i = 0; i < Count; i++)
            {
                if (!Hover(input, CardRect(i)))
                    continue;

                if (input.PointerClicked)
                {
                    _index = i;
                    Game.Audio.Play(SoundId.MenuSelect);
                    Go(new SkillScreen(Game, Catalogue.At(i)));
                    return;
                }

                if (input.PointerMoved)
                    SetIndex(i, false);
            }

            if (!Game.IsMobile && input.PointerClicked &&
                QuitButton.Contains((int)input.Pointer.X, (int)input.Pointer.Y))
            {
                Game.QuitToDesktop();
                return;
            }

            if (input.Horizontal != 0)
                SetIndex(Wrap(_index + input.Horizontal), true);

            if (input.Vertical != 0)
                SetIndex(Wrap(_index + input.Vertical * GridColumns), true);

            if (input.HighScores)
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new HighScoresScreen(Game, Catalogue.At(_index), null));
                return;
            }

            if (input.Back)
            {
                Go(new TitleScreen(Game));
                return;
            }

            if (input.Confirm)
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new SkillScreen(Game, Catalogue.At(_index)));
            }
        }

        /// <summary>The card whose centre is closest to a point - used when a tap lands between cards.</summary>
        private static int NearestCard(Vector2 point)
        {
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Count; i++)
            {
                Rectangle card = CardRect(i);
                float dx = point.X - (card.X + card.Width * 0.5f);
                float dy = point.Y - (card.Y + card.Height * 0.5f);
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        private static int Wrap(int index)
        {
            int count = Count;
            if (index < 0)
                index += count * ((-index + count - 1) / count);
            return ((index % count) + count) % count;
        }

        private void SetIndex(int index, bool sound)
        {
            if (index == _index)
                return;

            _index = index;
            if (sound)
                Game.Audio.Play(SoundId.MenuMove);

            Game.Audio.PlayMusic(Catalogue.At(_index));
        }

        public override void Draw(double time)
        {
            GameId selected = Catalogue.At(_index);
            Color accent = Palette.ForGame(selected);
            Ui.Backdrop(Canvas, time, accent);
            Ui.Header(Canvas, "THE PSS ORIC COLLECTION", "CHOOSE A GAME", accent, time);

            for (int i = 0; i < Count; i++)
                DrawCard(i, time, i == _index);

            DrawCreditsPanel();

            string left = Game.IsMobile
                ? "TAP A GAME"
                : "CLICK A GAME OR USE THE ARROWS    H HIGH SCORES    ESC QUITS";
            Ui.Footer(Canvas, left, "BEST SCORES ARE KEPT", accent);

            if (!Game.IsMobile)
                Ui.Button(Canvas, QuitButton, "QUIT", false, Palette.TextDim, 1.5f);


            Canvas.Text("EVERY GAME HAS FOUR SKILL LEVELS AND ITS OWN HIGH SCORE TABLE",
                80, 596, 1.7f, Palette.WithAlpha(Palette.TextDim, 0.9f));
        }

        private void DrawCard(int index, double time, bool selected)
        {
            GameInfo info = Catalogue.All[index];
            Rectangle card = CardRect(index);
            Color accent = Palette.ForGame(info.Id);

            Ui.Panel(Canvas, card, accent, selected);

            if (selected)
            {
                float pulse = 0.3f + 0.3f * Ui.Pulse(time, 3.0);
                Canvas.Rect(card.X + 16, card.Bottom - 5, card.Width - 32, 3, Palette.WithAlpha(accent, pulse + 0.4f));
            }

            var iconArea = new Rectangle(card.Right - 82, card.Y + 14, 68, 68);
            Canvas.RoundedRect(iconArea, Palette.WithAlpha(accent, 0.10f), 8);
            Canvas.RoundedOutline(iconArea, Palette.WithAlpha(accent, 0.45f), 8, 1);
            EntityKind kind = info.Id switch
            {
                GameId.Invaders => EntityKind.Alien,
                GameId.Centipede => EntityKind.Mushroom,
                GameId.Hopper => EntityKind.Player,
                GameId.LightCycle => EntityKind.Player,
                _ => EntityKind.Enemy
            };
            var view = new PlayfieldView(new Rectangle(iconArea.X + 12, iconArea.Y + 12, 44, 44),
                new System.Numerics.Vector2(44, 44));
            EntityRenderer.Draw(Canvas, view,
                new Entity(kind, System.Numerics.Vector2.Zero, new System.Numerics.Vector2(44, 44), 0), info.Id, time);

            Canvas.TextShadowed(info.Title, new Vector2(card.X + 18, card.Y + 16), 3f, Palette.Text, Palette.Background);
            Canvas.Text(info.Tagline, card.X + 18, card.Y + 56, 1.7f, Palette.TextDim);

            int best = Game.Scores.Best(info.Id);
            Canvas.Text("BEST", new Vector2(card.X + 18, card.Y + 114), 1.4f, Palette.TextDim);
            Canvas.Text(best > 0 ? Ui.FormatScore(best) : "-----", new Vector2(card.X + 18, card.Y + 130), 2.6f,
                best > 0 ? Palette.Amber : Palette.WithAlpha(Palette.TextDim, 0.6f));

            if (selected)
            {
                string hint = Game.IsMobile ? "TAP TO CHOOSE" : "FIRE TO PLAY";
                Canvas.Text(hint, card.X + 158, card.Y + 136, 1.6f, Palette.WithAlpha(accent, 0.9f));
            }

            // How the game is played, in the words that fit the machine it is running on.
            Canvas.Text(ControlsHint(info.Id, Game.IsMobile), card.X + 18, card.Y + 166, 1.35f,
                Palette.WithAlpha(selected ? accent : Palette.TextDim, 0.85f));
        }

        /// <summary>One short line of controls for a card, matching the device in hand.</summary>
        private static string ControlsHint(GameId id, bool mobile)
        {
            if (mobile)
            {
                return id switch
                {
                    GameId.Invaders => "TILT TO STEER   TAP TO FIRE",
                    GameId.Centipede => "TILT TO STEER   TAP TO FIRE",
                    GameId.Hopper => "TILT TO HOP",
                    GameId.ElektroStorm => "DRAG TO AIM   TAP TO FIRE",
                    _ => "TILT TO TURN"
                };
            }

            return id switch
            {
                GameId.Invaders => "ARROWS TO MOVE   SPACE TO FIRE",
                GameId.Centipede => "ARROWS TO MOVE   SPACE TO FIRE",
                GameId.Hopper => "ARROWS TO HOP   P TO PAUSE",
                GameId.ElektroStorm => "MOUSE TO AIM   CLICK TO FIRE",
                _ => "ARROWS TO TURN   P TO PAUSE"
            };
        }

        private void DrawCreditsPanel()
        {
            int slots = GridColumns * 2;
            if (Count >= slots)
                return;

            Rectangle card = CardRect(slots - 1);
            Canvas.RoundedRect(card, Palette.WithAlpha(Palette.Panel, 0.6f), 12);
            Canvas.RoundedOutline(card, Palette.WithAlpha(Palette.PanelEdge, 0.7f), 12, 1);

            Canvas.TextCentred("PERSONAL SOFTWARE SERVICES", new Rectangle(card.X, card.Y + 38, card.Width, 24), 1.7f,
                Palette.WithAlpha(Palette.TextDim, 0.9f));
            Canvas.TextCentred("ORIC-1 / ATMOS - 1983", new Rectangle(card.X, card.Y + 66, card.Width, 24), 1.6f,
                Palette.WithAlpha(Palette.TextDim, 0.7f));
            Canvas.TextCentred("RECREATED IN MONOGAME", new Rectangle(card.X, card.Y + 96, card.Width, 24), 1.6f,
                Palette.WithAlpha(Palette.Cyan, 0.8f));
            Canvas.TextCentred("HIGH SCORES PERSIST BETWEEN RUNS", new Rectangle(card.X, card.Y + 132, card.Width, 24),
                1.3f, Palette.WithAlpha(Palette.TextDim, 0.6f));
        }
    }
}
