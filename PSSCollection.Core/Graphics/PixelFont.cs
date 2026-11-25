using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PSSCollection.Core.Graphics
{
    /// <summary>
    /// A 5x7 bitmap font built into a texture at start-up. Keeping the font procedural means
    /// the game ships without a content pipeline, which keeps every platform head simple.
    /// </summary>
    public sealed class PixelFont : IDisposable
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;
        public const int GlyphAdvance = 6;
        public const int LineAdvance = 10;

        // Each glyph is seven rows of five pixels, written as 35 characters.
        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            [' '] = "00000000000000000000000000000000000",
            ['A'] = "01110100011000111111100011000110001",
            ['B'] = "11110100011000111110100011000111110",
            ['C'] = "01110100011000010000100001000101110",
            ['D'] = "11110100011000110001100011000111110",
            ['E'] = "11111100001000011110100001000011111",
            ['F'] = "11111100001000011110100001000010000",
            ['G'] = "01110100011000010111100011000101111",
            ['H'] = "10001100011000111111100011000110001",
            ['I'] = "01110001000010000100001000010001110",
            ['J'] = "00111000100001000010000101001001100",
            ['K'] = "10001100101010011000101001001010001",
            ['L'] = "10000100001000010000100001000011111",
            ['M'] = "10001110111010110101100011000110001",
            ['N'] = "10001110011010110011100011000110001",
            ['O'] = "01110100011000110001100011000101110",
            ['P'] = "11110100011000111110100001000010000",
            ['Q'] = "01110100011000110001101011001001101",
            ['R'] = "11110100011000111110101001001010001",
            ['S'] = "01111100001000001110000010000111110",
            ['T'] = "11111001000010000100001000010000100",
            ['U'] = "10001100011000110001100011000101110",
            ['V'] = "10001100011000110001100010101000100",
            ['W'] = "10001100011000110101101011101110001",
            ['X'] = "10001100010101000100010101000110001",
            ['Y'] = "10001100010101000100001000010000100",
            ['Z'] = "11111000010001000100010001000011111",
            ['0'] = "01110100011001110101110011000101110",
            ['1'] = "00100011000010000100001000010001110",
            ['2'] = "01110100010000100010001000100011111",
            ['3'] = "11111000100010000010000110001101110",
            ['4'] = "00010001100101010010111110001000010",
            ['5'] = "11111100001111000001000011000101110",
            ['6'] = "00110010001000011110100011000101110",
            ['7'] = "11111000010001000100010000100001000",
            ['8'] = "01110100011000101110100011000101110",
            ['9'] = "01110100011000101111000010001001100",
            ['.'] = "00000000000000000000000000110001100",
            [','] = "00000000000000000000001100011000110",
            [':'] = "00000011000110000000011000110000000",
            [';'] = "00000011000110000000011000110001100",
            ['!'] = "00100001000010000100001000000000100",
            ['?'] = "01110100010000100010001000000000100",
            ['-'] = "00000000000000011111000000000000000",
            ['+'] = "00000001000010011111001000010000000",
            ['='] = "00000000001111100000111110000000000",
            ['/'] = "00001000100001000100010000100010000",
            ['\\'] = "10000010000010000100001000010000100",
            ['('] = "00010001000100001000010000010000010",
            [')'] = "01000001000001000010000100010001000",
            ['['] = "01110010000100001000010000100001110",
            [']'] = "01110000100001000010000100001001110",
            ['\''] = "00100001000000000000000000000000000",
            ['"'] = "01010010100000000000000000000000000",
            ['*'] = "00000010100010011111001000101000000",
            ['#'] = "01010010101111101010111110101001010",
            ['%'] = "11001110100001000100010000101110011",
            ['<'] = "00010001000100010000010000010000010",
            ['>'] = "01000001000001000001000010001000100",
            ['_'] = "00000000000000000000000000000011111",
            ['&'] = "01100100101010001000101011001001101",
            ['@'] = "01110100011011110101101111000001110",
            ['^'] = "00100010101000100000000000000000000"
        };

        private readonly Texture2D _texture;
        private readonly Dictionary<char, int> _index = new Dictionary<char, int>();
        private readonly char[] _order;

        public PixelFont(GraphicsDevice device)
        {
            _order = new char[Glyphs.Count];
            int i = 0;
            foreach (char key in Glyphs.Keys)
            {
                _order[i] = key;
                _index[key] = i;
                i++;
            }

            Array.Sort(_order);
            for (int n = 0; n < _order.Length; n++)
                _index[_order[n]] = n;

            int width = _order.Length * GlyphAdvance;
            _texture = new Texture2D(device, width, GlyphHeight, false, SurfaceFormat.Color);

            var pixels = new Color[width * GlyphHeight];
            for (int glyph = 0; glyph < _order.Length; glyph++)
            {
                string bits = Glyphs[_order[glyph]];
                int originX = glyph * GlyphAdvance;
                for (int row = 0; row < GlyphHeight; row++)
                {
                    for (int column = 0; column < GlyphWidth; column++)
                    {
                        char bit = bits[row * GlyphWidth + column];
                        pixels[row * width + originX + column] = bit == '1' ? Color.White : Color.Transparent;
                    }
                }
            }

            _texture.SetData(pixels);
        }

        public Texture2D Texture => _texture;

        /// <summary>Source rectangle for a character, or null when it has no glyph.</summary>
        public Rectangle? Source(char character)
        {
            if (!_index.TryGetValue(character, out int index))
            {
                char upper = char.ToUpperInvariant(character);
                if (!_index.TryGetValue(upper, out index))
                    return null;
            }

            return new Rectangle(index * GlyphAdvance, 0, GlyphWidth, GlyphHeight);
        }

        public void Dispose() => _texture?.Dispose();
    }
}
