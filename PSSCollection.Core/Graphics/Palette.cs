using Microsoft.Xna.Framework;

namespace PSSCollection.Core.Graphics
{
    /// <summary>The collection palette: a dark grid with bright arcade accents.</summary>
    public static class Palette
    {
        public static readonly Color Background = new Color(6, 8, 16);
        public static readonly Color BackgroundLight = new Color(12, 16, 30);
        public static readonly Color Panel = new Color(16, 22, 40);
        public static readonly Color PanelEdge = new Color(38, 54, 92);
        public static readonly Color Text = new Color(228, 240, 255);
        public static readonly Color TextDim = new Color(126, 148, 186);
        public static readonly Color Cyan = new Color(56, 225, 255);
        public static readonly Color Magenta = new Color(255, 61, 127);
        public static readonly Color Amber = new Color(255, 194, 75);
        public static readonly Color Green = new Color(91, 255, 158);
        public static readonly Color Red = new Color(255, 82, 82);
        public static readonly Color Violet = new Color(168, 120, 255);
        public static readonly Color Ice = new Color(120, 196, 255);
        public static readonly Color Black = Color.Black;
        public static readonly Color White = Color.White;

        /// <summary>Accent colour for a game, used by the menu, HUD and particle tints.</summary>
        public static Color ForGame(Engine.GameId id)
        {
            switch (id)
            {
                case Engine.GameId.Invaders: return Green;
                case Engine.GameId.Centipede: return Magenta;
                case Engine.GameId.Hopper: return Ice;
                case Engine.GameId.LightCycle: return Amber;
                default: return Cyan;
            }
        }

        public static Color ForSkill(Engine.SkillLevel level)
        {
            switch (level)
            {
                case Engine.SkillLevel.Novice: return Green;
                case Engine.SkillLevel.Skilled: return Cyan;
                case Engine.SkillLevel.Veteran: return Amber;
                default: return Magenta;
            }
        }

        public static Color WithAlpha(Color color, float alpha) =>
            new Color(color.R, color.G, color.B) * alpha;
    }
}
