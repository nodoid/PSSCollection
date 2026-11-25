using System;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;
using PSSCollection.Engine;

namespace PSSCollection.Core.Screens
{
    /// <summary>The front page: collection title, then the menu.</summary>
    public sealed class TitleScreen : Screen
    {
        public TitleScreen(PssGame game) : base(game)
        {
        }

        public override void Enter() => Game.Audio.PlayMusic(null);

        public override void Update(double deltaSeconds, GameInput input)
        {
            if (input.Confirm || input.Player.FirePressed)
            {
                Game.Audio.Play(SoundId.MenuSelect);
                Go(new MenuScreen(Game));
                return;
            }

            if (input.HighScores)
            {
                Go(new HighScoresScreen(Game, null, null));
                return;
            }

            if (input.Back)
                Game.QuitToDesktop();
        }

        public override void Draw(double time)
        {
            Ui.Backdrop(Canvas, time, Palette.Cyan);

            // PSS roundel.
            var badge = new Rectangle(560, 96, 160, 160);
            Canvas.Circle(new Vector2(badge.Center.X, badge.Center.Y), 80, Palette.WithAlpha(Palette.Cyan, 0.08f));
            Canvas.CircleOutline(new Vector2(badge.Center.X, badge.Center.Y), 76, Palette.WithAlpha(Palette.Cyan, 0.55f), 2f);
            Canvas.CircleOutline(new Vector2(badge.Center.X, badge.Center.Y), 66, Palette.WithAlpha(Palette.Cyan, 0.25f), 1f);
            Canvas.TextCentred("PSS", badge, 6f, Palette.Cyan);

            Canvas.TextCentredShadowed("THE PSS", new Rectangle(0, 258, ScreenCanvas.VirtualWidth, 60), 5.5f,
                Palette.Text, Palette.Background);
            Canvas.TextCentredShadowed("ORIC COLLECTION", new Rectangle(0, 306, ScreenCanvas.VirtualWidth, 86), 7f,
                Palette.Text, Palette.Background);

            Canvas.Rect(340, 400, 600, 2, Palette.WithAlpha(Palette.Cyan, 0.3f));
            Canvas.TextCentred("5 CLASSIC ORIC GAMES BASED ON THE PSS ORIGINALS", new Rectangle(0, 410, 1280, 24),
                1.9f, Palette.TextDim);

            var titles = new System.Text.StringBuilder();
            foreach (GameInfo info in Catalogue.All)
            {
                if (titles.Length > 0)
                    titles.Append("   ");
                titles.Append(info.Title);
            }

            Canvas.TextCentred(titles.ToString(), new Rectangle(0, 434, 1280, 24), 1.9f, Palette.Amber);

            float pulse = 0.45f + 0.55f * Ui.Pulse(time, 2.6);
            string prompt = Game.IsMobile ? "TAP TO START" : "PRESS FIRE TO START";
            Canvas.TextCentred(prompt, new Rectangle(0, 496, 1280, 34), 3f, Palette.WithAlpha(Palette.Green, pulse));

            Canvas.Text(Game.IsMobile ? "HIGH SCORES FROM THE MENU" : "HIGH SCORES IN H, OR FROM THE MENU", 48, 636, 1.6f,
                Palette.TextDim);
            Canvas.TextRight(Game.BuildLabel + "  " + Game.Platform.PlatformName, new Rectangle(48, 632, 1184, 24),
                1.6f, Palette.TextDim);
            Canvas.TextCentred("SCORES ARE KEPT BETWEEN SESSIONS", new Rectangle(0, 664, 1280, 20), 1.5f,
                Palette.WithAlpha(Palette.TextDim, 0.8f));
        }
    }
}
