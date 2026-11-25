using System;
using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Engine;

namespace PSSCollection.Core.Screens
{
    /// <summary>Shared chrome: panels, buttons, headers and the animated starfield.</summary>
    public static class Ui
    {
        public static readonly Rectangle HeaderArea = new Rectangle(48, 28, 1184, 74);
        public static readonly Rectangle FooterArea = new Rectangle(48, 660, 1184, 34);

        public static void Backdrop(ScreenCanvas canvas, double time, Color accent)
        {
            canvas.Gradient(new Color(8, 10, 22), new Color(3, 4, 10));

            // Horizon grid.
            for (int i = 0; i < 14; i++)
            {
                float y = 430 + i * i * 1.6f;
                if (y > ScreenCanvas.VirtualHeight)
                    break;
                float alpha = 0.05f + i * 0.012f;
                canvas.Rect(0, y, ScreenCanvas.VirtualWidth, 1, Palette.WithAlpha(accent, alpha));
            }

            for (int i = -12; i <= 12; i++)
            {
                float x = ScreenCanvas.VirtualWidth * 0.5f + i * 74;
                canvas.Line(new Vector2(640, 420), new Vector2(x * 2.4f - 620, ScreenCanvas.VirtualHeight),
                    Palette.WithAlpha(accent, 0.045f), 1f);
            }

            Starfield(canvas, time, accent, 90);
        }

        public static void Starfield(ScreenCanvas canvas, double time, Color accent, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = (i * 137.5f) % ScreenCanvas.VirtualWidth;
                float speed = 12 + (i % 5) * 9;
                float y = (float)((i * 53.3 + time * speed) % ScreenCanvas.VirtualHeight);
                float size = i % 11 == 0 ? 3f : i % 5 == 0 ? 2f : 1.4f;
                float brightness = 0.18f + (i % 7) * 0.06f;
                Color colour = i % 9 == 0 ? accent : Palette.Text;
                canvas.Rect(x, y, size, size, Palette.WithAlpha(colour, brightness));
            }
        }

        public static void Panel(ScreenCanvas canvas, Rectangle area, Color accent, bool highlighted = false)
        {
            canvas.RoundedRect(area, highlighted ? new Color(24, 34, 60) : Palette.Panel, 12);
            canvas.RoundedOutline(area, highlighted ? accent : Palette.PanelEdge, 12, 2);

            if (highlighted)
            {
                canvas.RoundedOutline(new Rectangle(area.X - 3, area.Y - 3, area.Width + 6, area.Height + 6),
                    Palette.WithAlpha(accent, 0.35f), 14, 1);
            }
        }

        public static void Button(ScreenCanvas canvas, Rectangle area, string label, bool selected, Color accent, float scale = 3f)
        {
            Panel(canvas, area, accent, selected);
            canvas.TextCentred(label, area, scale, selected ? Palette.Text : Palette.TextDim);

            if (selected)
                canvas.Rect(area.X + 12, area.Bottom - 4, area.Width - 24, 2, accent);
        }

        public static void Header(ScreenCanvas canvas, string title, string subtitle, Color accent, double time)
        {
            canvas.TextShadowed(title, new Vector2(HeaderArea.X, HeaderArea.Y), 4f, Palette.Text, Palette.Background);

            if (!string.IsNullOrEmpty(subtitle))
                canvas.Text(subtitle, HeaderArea.X, HeaderArea.Y + 38, 2f, accent);

            float shimmer = (float)((time * 140) % HeaderArea.Width);
            canvas.Rect(HeaderArea.X + shimmer, HeaderArea.Bottom + 6, 60, 2, Palette.WithAlpha(accent, 0.5f));
            canvas.Rect(HeaderArea.X, HeaderArea.Bottom + 6, HeaderArea.Width, 1, Palette.WithAlpha(accent, 0.18f));
        }

        public static void Footer(ScreenCanvas canvas, string left, string right, Color accent)
        {
            canvas.Rect(FooterArea.X, FooterArea.Y, FooterArea.Width, 1, Palette.WithAlpha(accent, 0.2f));
            canvas.Text(left, FooterArea.X, FooterArea.Y + 10, 2f, Palette.TextDim);
            canvas.TextRight(right, FooterArea, 2f, Palette.TextDim);
        }

        public static Rectangle MiniIcon(EntityKind kind, Vector2 position, float size) =>
            new Rectangle((int)position.X, (int)position.Y, (int)size, (int)size);

        public static string FormatScore(int score) => score.ToString("N0");

        public static float Pulse(double time, double speed = 2.4)
        {
            return (float)((Math.Sin(time * speed) + 1) * 0.5);
        }
    }
}
