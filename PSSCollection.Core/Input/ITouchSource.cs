using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace PSSCollection.Core.Input
{
    /// <summary>One finger, as a platform saw it this frame.</summary>
    public readonly struct TouchPoint
    {
        public TouchPoint(int id, Vector2 position, bool began, bool ended)
        {
            Id = id;
            Position = position;
            Began = began;
            Ended = ended;
        }

        public int Id { get; }

        /// <summary>Position in the platform's own view space (points on iOS).</summary>
        public Vector2 Position { get; }

        public bool Began { get; }

        public bool Ended { get; }
    }

    /// <summary>
    /// A platform's own touch feed. iOS uses this because its touch panel can report a position
    /// that never moves; reading UIKit directly gives the real finger position in view points.
    /// </summary>
    public interface ITouchSource
    {
        IReadOnlyList<TouchPoint> Read();

        /// <summary>True once the source has delivered at least one touch.</summary>
        bool HasDelivered { get; }
    }
}
