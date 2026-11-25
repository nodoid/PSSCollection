using System;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;

namespace PSSCollection.Core.Screens
{
    /// <summary>Choose one of the four skill levels for the selected game.</summary>
    public sealed class SkillScreen : Screen
    {
        private static readonly Rectangle[] Rows =
        {
            new Rectangle(120, 150, 1040, 92),
            new Rectangle(120, 256, 1040, 92),
            new Rectangle(120, 362, 1040, 92),
            new Rectangle(120, 468, 1040, 92)
        };

        private static readonly Rectangle BackButton = new Rectangle(1064, 22, 176, 48);

        private readonly GameId _gameId;
        private int _index = 1;

        public SkillScreen(PssGame game, GameId gameId) : base(game)
        {
            _gameId = gameId;
        }

        public override void Enter() => Game.Audio.PlayMusic(_gameId);

        public override void Update(double deltaSeconds, GameInput input)
        {
            if (Game.IsMobile && input.TouchPressedIn(BackButton))
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new MenuScreen(Game));
                return;
            }

            for (int i = 0; i < Rows.Length; i++)
            {
                if (Hover(input, Rows[i]) && input.PointerMoved)
                    _index = i;
            }

            if (input.Vertical != 0)
            {
                _index = Math.Clamp(_index + input.Vertical, 0, 3);
                Game.Audio.Play(SoundId.MenuMove);
            }

            if (input.Horizontal != 0)
            {
                _index = Math.Clamp(_index + input.Horizontal, 0, 3);
                Game.Audio.Play(SoundId.MenuMove);
            }

            if (input.Back)
            {
                Go(new MenuScreen(Game));
                return;
            }

            if (input.Confirm || (input.PointerClicked && Hover(input, Rows[_index])))
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new PlayScreen(Game, _gameId, SkillLevels.All[_index]));
            }
        }

        public override void Draw(double time)
        {
            GameInfo info = Catalogue.Get(_gameId);
            Color accent = Palette.ForGame(_gameId);

            Ui.Backdrop(Canvas, time, accent);
            Ui.Header(Canvas, info.Title, "CHOOSE SKILL", accent, time);

            if (Game.IsMobile)
                Ui.Button(Canvas, BackButton, "MENU", false, accent, 1.8f);

            for (int i = 0; i < Rows.Length; i++)
            {
                SkillLevel level = SkillLevels.All[i];
                Rectangle row = Rows[i];
                bool selected = i == _index;
                Color levelColour = Palette.ForSkill(level);

                Ui.Panel(Canvas, row, levelColour, selected);

                Canvas.Rect(row.X + 4, row.Y + 10, 6, row.Height - 20,
                    selected ? levelColour : Palette.WithAlpha(levelColour, 0.4f));

                Canvas.TextShadowed(SkillLevels.Name(level), new Vector2(row.X + 30, row.Y + 16), 3.6f,
                    selected ? Palette.Text : Palette.TextDim, Palette.Background);
                Canvas.Text(SkillLevels.Description(level), row.X + 30, row.Y + 58, 1.6f, Palette.TextDim);

                int best = Game.Scores.Best(_gameId, level);
                Canvas.Text("BEST", new Vector2(row.Right - 230, row.Y + 20), 1.5f, Palette.TextDim);
                Canvas.Text(best > 0 ? Ui.FormatScore(best) : "-----", new Vector2(row.Right - 230, row.Y + 40), 2.6f,
                    best > 0 ? Palette.Amber : Palette.WithAlpha(Palette.TextDim, 0.6f));

                if (selected)
                    Canvas.Text(Game.IsMobile ? "TAP TO START" : "FIRE TO START", row.Right - 230, row.Bottom - 28,
                        1.5f, levelColour);
            }

            Canvas.Text(info.HowToPlay, 120, 578, 1.5f, Palette.WithAlpha(Palette.TextDim, 0.9f));
            Ui.Footer(Canvas, info.Controls, "ESC BACK", accent);
        }
    }
}
