using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;

namespace PSSCollection.Core.Screens
{
    /// <summary>The high-score tables: one per game, with a per-skill best strip.</summary>
    public sealed class HighScoresScreen : Screen
    {
        private static Rectangle Tab(int index)
        {
            const int left = 48;
            const int total = 1184;
            const int gap = 12;
            int count = Catalogue.Count;
            int width = (total - gap * (count - 1)) / count;
            return new Rectangle(left + index * (width + gap), 116, width, 46);
        }

        private int _gameIndex;
        private bool _clearArmed;
        private double _armedTimer;

        public HighScoresScreen(PssGame game, GameId? focus, SkillLevel? skill) : base(game)
        {
            _gameIndex = focus.HasValue ? Math.Max(0, Catalogue.IndexOf(focus.Value)) : 0;
        }

        public override void Enter() => Game.Audio.PlayMusic(null);

        public override void Update(double deltaSeconds, GameInput input)
        {
            if (_armedTimer > 0)
            {
                _armedTimer -= deltaSeconds;
                if (_armedTimer <= 0)
                    _clearArmed = false;
            }

            for (int i = 0; i < Catalogue.Count; i++)
            {
                if (Hover(input, Tab(i)) && input.PointerMoved && i != _gameIndex)
                {
                    _gameIndex = i;
                    _clearArmed = false;
                    Game.Audio.Play(SoundId.MenuMove);
                }
            }

            if (input.Horizontal != 0)
            {
                _gameIndex = (_gameIndex + input.Horizontal + Catalogue.Count) % Catalogue.Count;
                _clearArmed = false;
                Game.Audio.Play(SoundId.MenuMove);
            }

            GameId id = Catalogue.At(_gameIndex);

            if (input.Clear)
            {
                if (_clearArmed)
                {
                    Game.Scores.Clear(id);
                    _clearArmed = false;
                    Game.Audio.Play(SoundId.MenuSelect);
                }
                else
                {
                    _clearArmed = true;
                    _armedTimer = 4.0;
                    Game.Audio.Play(SoundId.MenuMove);
                }
            }

            if (input.Back)
            {
                Go(new MenuScreen(Game));
                return;
            }

            if (input.Confirm)
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new SkillScreen(Game, id));
            }
        }

        public override void Draw(double time)
        {
            GameId id = Catalogue.At(_gameIndex);
            Color accent = Palette.ForGame(id);
            GameInfo info = Catalogue.Get(id);

            Ui.Backdrop(Canvas, time, accent);
            Ui.Header(Canvas, "HIGH SCORES", info.Caption, accent, time);

            for (int i = 0; i < Catalogue.Count; i++)
            {
                GameInfo tab = Catalogue.All[i];
                Color tabColour = Palette.ForGame(tab.Id);
                bool selected = i == _gameIndex;
                Rectangle area = Tab(i);

                Ui.Panel(Canvas, area, tabColour, selected);
                Canvas.TextCentred(tab.Title, area, 1.7f, selected ? Palette.Text : Palette.TextDim);
            }

            DrawTable(id, accent, time);
            DrawSkillBests(id, accent);

            if (_clearArmed)
            {
                Canvas.TextCentred("PRESS C AGAIN TO CLEAR THIS TABLE", new Rectangle(0, 620, 1280, 24), 2f,
                    Palette.Red);
            }
            else if (!string.IsNullOrEmpty(Game.Scores.Warning))
            {
                Canvas.TextCentred(Game.Scores.Warning, new Rectangle(0, 620, 1280, 24), 1.7f, Palette.Amber);
            }

            Ui.Footer(Canvas,
                Game.IsMobile ? "TAP A TAB    FIRE PLAYS" : "ARROWS CHANGE GAME    C CLEARS    ESC BACK",
                Game.Scores.ShortLocation, accent);
        }

        private void DrawTable(GameId id, Color accent, double time)
        {
            var header = new Rectangle(140, 186, 1000, 24);
            Canvas.Text("RANK", new Vector2(header.X, header.Y), 1.6f, Palette.TextDim);
            Canvas.Text("NAME", new Vector2(header.X + 90, header.Y), 1.6f, Palette.TextDim);
            Canvas.TextRight("SCORE", new Rectangle(header.X + 200, header.Y, 200, 24), 1.6f, Palette.TextDim);
            Canvas.Text("SKILL", new Vector2(header.X + 460, header.Y), 1.6f, Palette.TextDim);
            Canvas.Text("WAVE", new Vector2(header.X + 620, header.Y), 1.6f, Palette.TextDim);
            Canvas.Text("WHEN", new Vector2(header.X + 740, header.Y), 1.6f, Palette.TextDim);

            Canvas.Rect(140, 214, 1000, 1, Palette.WithAlpha(accent, 0.3f));

            IReadOnlyList<ScoreEntry> entries = Game.Scores.Top(id, ScoreTable.Capacity);
            int rowHeight = 34;

            if (entries.Count == 0)
            {
                Canvas.TextCentred("NO SCORES YET - BE THE FIRST", new Rectangle(140, 320, 1000, 40), 2.6f,
                    Palette.WithAlpha(Palette.TextDim, 0.8f));
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                ScoreEntry entry = entries[i];
                int y = 226 + i * rowHeight;

                if (i == 0)
                {
                    float pulse = 0.15f + 0.1f * Ui.Pulse(time, 2.0);
                    Canvas.Rect(136, y - 4, 1008, rowHeight, Palette.WithAlpha(Palette.Amber, pulse));
                }

                Color rowColour = i == 0 ? Palette.Amber : Palette.Text;
                Canvas.Text((i + 1) + ".", 150, y, 2.2f, Palette.TextDim);
                Canvas.Text(entry.Name, 230, y, 2.6f, rowColour);
                Canvas.TextRight(Ui.FormatScore(entry.Score), new Rectangle(340, y, 200, 24), 2.4f, rowColour);
                Canvas.Text(SkillLevels.Name(entry.Skill), 600, y + 4, 1.7f, Palette.ForSkill(entry.Skill));
                Canvas.Text(entry.Level.ToString(), 760, y + 4, 1.9f, Palette.TextDim);
                Canvas.Text(entry.AchievedUtc.ToLocalTime().ToString("dd MMM yy"), 880, y + 4, 1.7f, Palette.TextDim);
            }
        }

        private void DrawSkillBests(GameId id, Color accent)
        {
            Canvas.Rect(140, 572, 1000, 1, Palette.WithAlpha(accent, 0.25f));

            for (int i = 0; i < SkillLevels.All.Length; i++)
            {
                SkillLevel level = SkillLevels.All[i];
                int best = Game.Scores.Best(id, level);
                float x = 148 + i * 250;

                Canvas.Text(SkillLevels.Abbreviation(level), x, 582, 1.6f, Palette.ForSkill(level));
                Canvas.Text(best > 0 ? Ui.FormatScore(best) : "-----", x + 58, 580, 2.2f,
                    best > 0 ? Palette.Text : Palette.WithAlpha(Palette.TextDim, 0.6f));
            }
        }
    }
}
