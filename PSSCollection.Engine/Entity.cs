using System.Numerics;

namespace PSSCollection.Engine
{
    /// <summary>
    /// The kinds of thing the renderer knows how to draw. The engine only positions them;
    /// the MonoGame layer turns each kind into a drawn shape, so the games stay testable
    /// without a graphics device.
    /// </summary>
    public enum EntityKind
    {
        Player,
        Enemy,
        Alien,
        Bullet,
        Bomb,
        Shield,
        Ufo,
        CentipedeHead,
        CentipedeBody,
        Mushroom,
        Spider,
        Flea,
        IceBlock,
        DiamondBlock,
        Egg,
        Snobee,
        Star,
        Explosion,
        PowerUp,
        Guardian,
        BossBomb,
        Trail,
        Log,
        Turtle,
        Home,
        Snake,
        Fly,
        City,
        Silo,
        Crosshair
    }

    /// <summary>A positioned sprite. Positions are playfield units.</summary>
    public readonly struct Entity
    {
        public Entity(EntityKind kind, Vector2 position, Vector2 size, int variant = 0, float rotation = 0f, bool visible = true)
        {
            Kind = kind;
            Position = position;
            Size = size;
            Variant = variant;
            Rotation = rotation;
            Visible = visible;
        }

        public EntityKind Kind { get; }

        /// <summary>Top-left corner in playfield units.</summary>
        public Vector2 Position { get; }

        public Vector2 Size { get; }

        /// <summary>Colour row / animation frame / power-up type, depending on the kind.</summary>
        public int Variant { get; }

        public float Rotation { get; }

        public bool Visible { get; }

        public float Left => Position.X;
        public float Top => Position.Y;
        public float Right => Position.X + Size.X;
        public float Bottom => Position.Y + Size.Y;

        public Vector2 Centre => new Vector2(Position.X + Size.X * 0.5f, Position.Y + Size.Y * 0.5f);

        public bool Intersects(in Entity other) =>
            Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

        public static bool Overlap(Vector2 positionA, Vector2 sizeA, Vector2 positionB, Vector2 sizeB) =>
            positionA.X < positionB.X + sizeB.X && positionA.X + sizeA.X > positionB.X &&
            positionA.Y < positionB.Y + sizeB.Y && positionA.Y + sizeA.Y > positionB.Y;
    }

    /// <summary>Sounds a game can ask for; the audio layer maps each to a synthesised effect.</summary>
    public enum SoundId
    {
        Shoot,
        EnemyShoot,
        Explosion,
        EnemyDestroyed,
        PlayerHit,
        WaveClear,
        MenuMove,
        MenuSelect,
        GameOver,
        PowerUp,
        Push,
        BlockBreak,
        ExtraLife,
        GuardianAlert,
        HighScore
    }
}
