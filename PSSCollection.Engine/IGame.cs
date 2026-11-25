using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine
{
    /// <summary>What the screens and the renderer need from a running game.</summary>
    public interface IGame
    {
        GameId Id { get; }

        SkillLevel Skill { get; }

        int Score { get; }

        int Lives { get; }

        int Level { get; }

        bool IsGameOver { get; }

        bool IsPaused { get; set; }

        int ExtraLivesAwarded { get; }

        double Elapsed { get; }

        /// <summary>Short on-screen message ("WAVE 2", "EXTRA LIFE").</summary>
        string Message { get; }

        double MessageTimer { get; }

        bool IsShowingMessage { get; }

        /// <summary>Logical size of the playfield the entities live in.</summary>
        Vector2 PlayfieldSize { get; }

        /// <summary>Free-running timer games use for background animation.</summary>
        double BackgroundPhase { get; }

        IReadOnlyList<Entity> Entities { get; }

        /// <summary>Takes the sounds queued since the last call.</summary>
        IReadOnlyList<SoundId> DrainSounds();

        void Start(SkillLevel skill, IRandomSource rng);

        void Update(double deltaSeconds, InputState input);
    }
}
