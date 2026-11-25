using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PSSCollection.Core.Graphics
{
    /// <summary>
    /// A drawing surface in virtual (1280x720) coordinates. Everything the game draws is a
    /// generated shape or the built-in pixel font, so there is no art to load.
    /// </summary>
    public sealed class ScreenCanvas : IDisposable
    {
        public const int VirtualWidth = 1280;
        public const int VirtualHeight = 720;

        private readonly GraphicsDevice _device;
        private readonly SpriteBatch _batch;
        private readonly Texture2D _pixel;
        private readonly Texture2D _circle;
        private readonly Texture2D _glow;
        private readonly PixelFont _font;

        public ScreenCanvas(GraphicsDevice device, SpriteBatch batch)
        {
            _device = device;
            _batch = batch;
            _font = new PixelFont(device);

            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _circle = CreateCircle(device, 128);
            _glow = CreateGlow(device, 128);
        }

        public PixelFont Font => _font;

        public SpriteBatch Batch => _batch;

        public static Rectangle FullScreen => new Rectangle(0, 0, VirtualWidth, VirtualHeight);

        /// <summary>Starts a batch in virtual coordinates.</summary>
        public void Begin()
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
        }

        /// <summary>Starts an additive batch, for glows and laser beams.</summary>
        public void BeginAdditive()
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
        }

        public void End() => _batch.End();

        public void Fill(Color color) => _batch.Draw(_pixel, FullScreen, color);

        /// <summary>A vertical two-colour background.</summary>
        public void Gradient(Color top, Color bottom)
        {
            const int bands = 48;
            int height = VirtualHeight / bands + 1;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                Color colour = Color.Lerp(top, bottom, t);
                _batch.Draw(_pixel, new Rectangle(0, i * height, VirtualWidth, height + 1), colour);
            }
        }

        public void Rect(Rectangle rectangle, Color color) => _batch.Draw(_pixel, rectangle, color);

        public void Rect(float x, float y, float width, float height, Color color) =>
            _batch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), color);

        public void RectOutline(Rectangle rectangle, Color color, int thickness = 2)
        {
            Rect(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
            Rect(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
            Rect(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
            Rect(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
        }

        public void RoundedRect(Rectangle rectangle, Color color, int radius = 10)
        {
            radius = Math.Max(1, Math.Min(radius, Math.Min(rectangle.Width, rectangle.Height) / 2));
            Rect(new Rectangle(rectangle.X + radius, rectangle.Y, rectangle.Width - radius * 2, rectangle.Height), color);
            Rect(new Rectangle(rectangle.X, rectangle.Y + radius, radius, rectangle.Height - radius * 2), color);
            Rect(new Rectangle(rectangle.Right - radius, rectangle.Y + radius, radius, rectangle.Height - radius * 2), color);

            int diameter = radius * 2;
            Circle(new Vector2(rectangle.X + radius, rectangle.Y + radius), radius, color);
            Circle(new Vector2(rectangle.Right - radius, rectangle.Y + radius), radius, color);
            Circle(new Vector2(rectangle.X + radius, rectangle.Bottom - radius), radius, color);
            Circle(new Vector2(rectangle.Right - radius, rectangle.Bottom - radius), radius, color);
            _ = diameter;
        }

        public void RoundedOutline(Rectangle rectangle, Color color, int radius = 10, int thickness = 2)
        {
            RoundedRect(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness + radius), color, radius);
            RoundedRect(new Rectangle(rectangle.X, rectangle.Bottom - thickness - radius, rectangle.Width, thickness + radius), color, radius);
            RoundedRect(new Rectangle(rectangle.X, rectangle.Y + radius, thickness, rectangle.Height - radius * 2), color, radius);
            RoundedRect(new Rectangle(rectangle.Right - thickness, rectangle.Y + radius, thickness, rectangle.Height - radius * 2), color, radius);
        }

        public void Circle(Vector2 centre, float radius, Color color)
        {
            var destination = new Rectangle((int)(centre.X - radius), (int)(centre.Y - radius),
                (int)(radius * 2), (int)(radius * 2));
            _batch.Draw(_circle, destination, color);
        }

        public void Ring(Vector2 centre, float radius, Color color, float thickness = 2f)
        {
            int steps = Math.Max(10, (int)(radius * 0.9f));
            for (int i = 0; i < steps; i++)
            {
                float angle = MathHelper.TwoPi * i / steps;
                var point = centre + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                Circle(point, thickness * 0.5f, color);
            }
        }

        public void CircleOutline(Vector2 centre, float radius, Color color, float thickness = 2f)
        {
            int steps = Math.Clamp((int)(radius * 1.4f), 12, 160);
            for (int i = 0; i < steps; i++)
            {
                float angle = MathHelper.TwoPi * i / steps;
                var point = centre + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                int size = Math.Max(2, (int)thickness);
                _batch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y, size, size), color);
            }
        }

        public void Line(Vector2 from, Vector2 to, Color color, float thickness = 1f)
        {
            Vector2 delta = to - from;
            float length = delta.Length();
            if (length < 0.01f)
                return;

            float angle = (float)Math.Atan2(delta.Y, delta.X);
            _batch.Draw(_pixel, from, null, color, angle, new Vector2(0f, 0.5f),
                new Vector2(length, Math.Max(0.5f, thickness)), SpriteEffects.None, 0f);
        }

        /// <summary>Soft radial light, drawn additively.</summary>
        public void Glow(Vector2 centre, float radius, Color color)
        {
            var destination = new Rectangle((int)(centre.X - radius), (int)(centre.Y - radius),
                (int)(radius * 2), (int)(radius * 2));
            _batch.Draw(_glow, destination, color);
        }

        public void Sprite(Texture2D texture, Rectangle destination, Color color, float rotation = 0f) =>
            _batch.Draw(texture, destination, null, color, rotation, new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                SpriteEffects.None, 0f);

        // ---- Text ---------------------------------------------------------------------

        public Vector2 Measure(string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
                return Vector2.Zero;

            int longest = 0;
            int current = 0;
            int lines = 1;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    lines++;
                    longest = Math.Max(longest, current);
                    current = 0;
                    continue;
                }

                current++;
            }

            longest = Math.Max(longest, current);

            return new Vector2(longest * PixelFont.GlyphAdvance * scale,
                (lines * PixelFont.LineAdvance - (PixelFont.LineAdvance - PixelFont.GlyphHeight)) * scale);
        }

        public void Text(string text, Vector2 position, float scale, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            float x = position.X;
            float y = position.Y;
            float advance = PixelFont.GlyphAdvance * scale;
            float lineHeight = PixelFont.LineAdvance * scale;

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    x = position.X;
                    y += lineHeight;
                    continue;
                }

                Rectangle? source = _font.Source(c);
                if (source == null)
                {
                    x += advance;
                    continue;
                }

                var destination = new Rectangle((int)x, (int)y,
                    (int)(PixelFont.GlyphWidth * scale), (int)(PixelFont.GlyphHeight * scale));
                _batch.Draw(_font.Texture, destination, source, color);
                x += advance;
            }
        }

        public void Text(string text, float x, float y, float scale, Color color) =>
            Text(text, new Vector2(x, y), scale, color);

        public void TextCentred(string text, Rectangle area, float scale, Color color)
        {
            Vector2 size = Measure(text, scale);
            Text(text, new Vector2(area.X + (area.Width - size.X) * 0.5f, area.Y + (area.Height - size.Y) * 0.5f),
                scale, color);
        }

        public void TextRight(string text, Rectangle area, float scale, Color color)
        {
            Vector2 size = Measure(text, scale);
            Text(text, new Vector2(area.Right - size.X, area.Y + (area.Height - size.Y) * 0.5f), scale, color);
        }

        /// <summary>Draws text with a one-pixel drop shadow, for readability over playfields.</summary>
        public void TextShadowed(string text, Vector2 position, float scale, Color color, Color shadow)
        {
            Text(text, position + new Vector2(scale, scale), scale, shadow);
            Text(text, position, scale, color);
        }

        public void TextCentredShadowed(string text, Rectangle area, float scale, Color color, Color shadow)
        {
            Vector2 size = Measure(text, scale);
            var position = new Vector2(area.X + (area.Width - size.X) * 0.5f, area.Y + (area.Height - size.Y) * 0.5f);
            Text(text, position + new Vector2(scale, scale), scale, shadow);
            Text(text, position, scale, color);
        }

        /// <summary>Wraps text to a width and returns the lines.</summary>
        public static IReadOnlyList<string> Wrap(string text, int columns)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
                return lines;

            foreach (string paragraph in text.Split('\n'))
            {
                string line = string.Empty;
                foreach (string word in paragraph.Split(' '))
                {
                    if (word.Length == 0)
                        continue;

                    if (line.Length == 0)
                    {
                        line = word;
                    }
                    else if (line.Length + 1 + word.Length <= columns)
                    {
                        line += " " + word;
                    }
                    else
                    {
                        lines.Add(line);
                        line = word;
                    }
                }

                lines.Add(line);
            }

            return lines;
        }

        private static Texture2D CreateCircle(GraphicsDevice device, int size)
        {
            var texture = new Texture2D(device, size, size);
            var pixels = new Color[size * size];
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    float alpha = MathHelper.Clamp(radius - distance, 0f, 1f);
                    pixels[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(pixels);
            return texture;
        }

        private static Texture2D CreateGlow(GraphicsDevice device, int size)
        {
            var texture = new Texture2D(device, size, size);
            var pixels = new Color[size * size];
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - radius;
                    float dy = y + 0.5f - radius;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy) / radius;
                    float alpha = MathHelper.Clamp(1f - distance, 0f, 1f);
                    alpha *= alpha;
                    pixels[y * size + x] = Color.White * alpha;
                }
            }

            texture.SetData(pixels);
            return texture;
        }

        public void Dispose()
        {
            _pixel.Dispose();
            _circle.Dispose();
            _glow.Dispose();
            _font.Dispose();
        }
    }
}
