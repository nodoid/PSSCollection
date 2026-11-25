using System;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;

namespace PSSCollection.Core.Screens
{
    /// <summary>Arcade initials entry for a run that made the table.</summary>
    public sealed class NameEntryScreen : Screen
    {
        private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private readonly GameId _gameId;
        private readonly SkillLevel _skill;
        private readonly int _score;
        private readonly int _level;
        private readonly char[] _letters = { 'A', 'A', 'A' };

        private int _index;
        private bool _submitted;

        public NameEntryScreen(PssGame game, GameId gameId, SkillLevel skill, int score, int level) : base(game)
        {
            _gameId = gameId;
            _skill = skill;
            _score = score;
            _level = level;
        }

        public override void Enter() => Game.Audio.Play(SoundId.HighScore);

        private static Rectangle Slot(int index) => new Rectangle(468 + index * 122, 284, 104, 128);

        private static Rectangle UpArrow(int index) => new Rectangle(Slot(index).X + 30, Slot(index).Y - 52, 44, 40);

        private static Rectangle DownArrow(int index) => new Rectangle(Slot(index).X + 30, Slot(index).Bottom + 12, 44, 40);

        private static Rectangle OkButton => new Rectangle(520, 504, 240, 66);

        public override void Update(double deltaSeconds, GameInput input)
        {
            if (_submitted)
                return;

            if (input.Back)
            {
                Go(new HighScoresScreen(Game, _gameId, _skill));
                return;
            }

            if (input.Horizontal < 0 && _index > 0)
            {
                _index--;
                Game.Audio.Play(SoundId.MenuMove);
            }

            if (input.Horizontal > 0 && _index < 2)
            {
                _index++;
                Game.Audio.Play(SoundId.MenuMove);
            }

            if (input.Vertical != 0)
                Change(-input.Vertical);

            for (int i = 0; i < 3; i++)
            {
                if (input.TouchPressedIn(UpArrow(i)) || (input.PointerClicked && Hover(input, UpArrow(i))))
                {
                    _index = i;
                    Change(1);
                }

                if (input.TouchPressedIn(DownArrow(i)) || (input.PointerClicked && Hover(input, DownArrow(i))))
                {
                    _index = i;
                    Change(-1);
                }

                if (input.TouchPressedIn(Slot(i)) || (input.PointerClicked && Hover(input, Slot(i))))
                {
                    _index = i;
                    Game.Audio.Play(SoundId.MenuMove);
                }
            }

            if (input.Confirm || input.TouchPressedIn(OkButton) || (input.PointerClicked && Hover(input, OkButton)))
            {
                if (_index < 2)
                {
                    _index++;
                    Game.Audio.Play(SoundId.MenuMove);
                }
                else
                {
                    Submit();
                }
            }
        }

        private void Change(int direction)
        {
            if (direction == 0)
                return;

            int position = Characters.IndexOf(_letters[_index]);
            if (position < 0)
                position = 0;

            position = (position + direction + Characters.Length) % Characters.Length;
            _letters[_index] = Characters[position];
            Game.Audio.Play(SoundId.MenuMove);
        }

        private void Submit()
        {
            _submitted = true;
            Game.Scores.Submit(_gameId, new string(_letters), _score, _skill, _level);
            Go(new HighScoresScreen(Game, _gameId, _skill));
        }

        public override void Draw(double time)
        {
            GameInfo info = Catalogue.Get(_gameId);
            Color accent = Palette.ForGame(_gameId);

            Ui.Backdrop(Canvas, time, accent);
            Ui.Header(Canvas, info.Title, "NEW HIGH SCORE", accent, time);

            Canvas.TextCentredShadowed("YOU MADE THE TABLE", new Rectangle(0, 150, 1280, 46), 3.6f, Palette.Amber,
                Palette.Background);

            int rank = Game.Scores.Book.For(_gameId).RankFor(_score);
            string placing = rank >= 0 ? "RANK " + (rank + 1) : "RANK -";
            Canvas.TextCentred(placing + "    SCORE " + Ui.FormatScore(_score) + "    WAVE " + _level,
                new Rectangle(0, 206, 1280, 26), 2f, Palette.Text);

            Canvas.TextCentred("ENTER YOUR INITIALS", new Rectangle(0, 242, 1280, 26), 2f, Palette.TextDim);

            for (int i = 0; i < 3; i++)
            {
                Rectangle slot = Slot(i);
                bool selected = i == _index;
                Ui.Panel(Canvas, slot, accent, selected);

                if (selected)
                {
                    float pulse = 0.4f + 0.5f * Ui.Pulse(time, 4.0);
                    Canvas.Rect(slot.X + 10, slot.Bottom - 8, slot.Width - 20, 3, Palette.WithAlpha(accent, pulse));
                }

                Canvas.TextCentred(_letters[i].ToString(), slot, 7f, selected ? Palette.Text : Palette.TextDim);

                Rectangle up = UpArrow(i);
                Rectangle down = DownArrow(i);
                Canvas.RoundedRect(up, Palette.WithAlpha(accent, 0.18f), 8);
                Canvas.RoundedRect(down, Palette.WithAlpha(accent, 0.18f), 8);
                Canvas.Line(new Vector2(up.X + 12, up.Y + 26), new Vector2(up.Center.X, up.Y + 12), Palette.Text, 3f);
                Canvas.Line(new Vector2(up.Right - 12, up.Y + 26), new Vector2(up.Center.X, up.Y + 12), Palette.Text, 3f);
                Canvas.Line(new Vector2(down.X + 12, down.Bottom - 26), new Vector2(down.Center.X, down.Bottom - 12), Palette.Text, 3f);
                Canvas.Line(new Vector2(down.Right - 12, down.Bottom - 26), new Vector2(down.Center.X, down.Bottom - 12), Palette.Text, 3f);
            }

            Ui.Button(Canvas, OkButton, "CONFIRM", true, accent, 2.4f);

            Ui.Footer(Canvas, Game.IsMobile ? "TAP THE ARROWS TO CHANGE A LETTER" : "ARROWS CHANGE    FIRE SELECTS",
                "SCORES ARE SAVED AUTOMATICALLY", accent);
        }
    }
}
