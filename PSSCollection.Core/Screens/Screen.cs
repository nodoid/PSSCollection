using Microsoft.Xna.Framework;
using PSSCollection.Core.Graphics;
using PSSCollection.Core.Input;

namespace PSSCollection.Core.Screens
{
    /// <summary>A full-screen page of the front end or a running game.</summary>
    public abstract class Screen
    {
        protected Screen(PssGame game)
        {
            Game = game;
        }

        protected PssGame Game { get; }

        protected ScreenCanvas Canvas => Game.Canvas;

        public virtual void Enter()
        {
        }

        public virtual void Leave()
        {
        }

        public abstract void Update(double deltaSeconds, GameInput input);

        /// <summary>Draws in virtual coordinates; the canvas batch is already open.</summary>
        public abstract void Draw(double time);

        protected void Go(Screen next) => Game.Navigate(next);

        /// <summary>Position of the pointer in virtual coordinates, for hover states.</summary>
        protected static Vector2 Pointer(GameInput input) => input.Pointer;

        protected static bool Hover(GameInput input, Rectangle area) => input.PointerIn(area);
    }
}
