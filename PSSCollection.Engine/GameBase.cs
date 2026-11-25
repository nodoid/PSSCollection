using System;
using System.Collections.Generic;
using System.Numerics;

namespace PSSCollection.Engine
{
    /// <summary>
    /// Shared plumbing for the four games: scoring with the skill multiplier, extra lives,
    /// the sound queue, on-screen messages and pausing.
    /// </summary>
    public abstract class GameBase : IGame
    {
        /// <summary>Every 10,000 points earns an extra life, as the arcade machines did.</summary>
        public const int ExtraLifeInterval = 10000;

        private readonly List<SoundId> _sounds = new List<SoundId>();
        private int _nextExtraLife = ExtraLifeInterval;

        protected IRandomSource Rng = new XorShiftRandom(1);

        public abstract GameId Id { get; }

        public abstract Vector2 PlayfieldSize { get; }

        public SkillLevel Skill { get; private set; } = SkillLevel.Skilled;

        public int Score { get; private set; }

        public int Lives { get; protected set; }

        public int Level { get; protected set; }

        public bool IsGameOver { get; protected set; }

        public bool IsPaused { get; set; }

        public int ExtraLivesAwarded { get; private set; }

        /// <summary>Seconds the current run has lasted, used by the HUD and by tests.</summary>
        public double Elapsed { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public double MessageTimer { get; private set; }

        public double BackgroundPhase { get; protected set; }

        public abstract IReadOnlyList<Entity> Entities { get; }

        public float ScoreMultiplier => (float)SkillLevels.ScoreMultiplier(Skill);

        /// <summary>True while a "READY" or similar short message is showing.</summary>
        public bool IsShowingMessage => MessageTimer > 0 && !string.IsNullOrEmpty(Message);

        public void Start(SkillLevel skill, IRandomSource rng)
        {
            Skill = SkillLevels.Clamp(skill);
            Rng = rng ?? new XorShiftRandom(1);
            Score = 0;
            Level = 1;
            Lives = SkillLevels.StartingLives(Skill);
            IsGameOver = false;
            IsPaused = false;
            ExtraLivesAwarded = 0;
            Elapsed = 0;
            _nextExtraLife = ExtraLifeInterval;
            _sounds.Clear();
            Message = string.Empty;
            MessageTimer = 0;
            BackgroundPhase = 0;
            OnStart();
        }

        public void Update(double deltaSeconds, InputState input)
        {
            if (IsGameOver)
                return;

            if (input.Pause)
            {
                IsPaused = !IsPaused;
                Queue(input.Pause ? SoundId.MenuSelect : SoundId.MenuMove);
            }

            if (IsPaused)
                return;

            if (deltaSeconds < 0)
                deltaSeconds = 0;

            Elapsed += deltaSeconds;

            if (MessageTimer > 0)
            {
                MessageTimer -= deltaSeconds;
                if (MessageTimer <= 0)
                {
                    MessageTimer = 0;
                    Message = string.Empty;
                }
            }

            BackgroundPhase += deltaSeconds;
            UpdateCore(deltaSeconds, input);
        }

        protected abstract void UpdateCore(double deltaSeconds, InputState input);

        protected abstract void OnStart();

        /// <summary>Adds points, applying the skill multiplier and the extra-life rule.</summary>
        protected void Award(int rawPoints)
        {
            if (rawPoints <= 0)
                return;

            double scaled = rawPoints * SkillLevels.ScoreMultiplier(Skill);
            int points = (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
            if (points <= 0)
                points = 1;

            Score += points;

            while (Score >= _nextExtraLife)
            {
                _nextExtraLife += ExtraLifeInterval;
                ExtraLivesAwarded++;
                Lives++;
                Queue(SoundId.ExtraLife);
                ShowMessage("EXTRA LIFE", 1.6);
            }
        }

        protected void LoseLife()
        {
            Lives--;
            Queue(SoundId.PlayerHit);
        }

        /// <summary>Wins a wave and moves to the next, giving the usual clear bonus.</summary>
        protected void CompleteWave(int bonus)
        {
            Award(bonus);
            Level++;
            Queue(SoundId.WaveClear);
            ShowMessage("WAVE " + Level, 1.6);
        }

        protected void EndGame()
        {
            if (IsGameOver)
                return;

            IsGameOver = true;
            Queue(SoundId.GameOver);
            ShowMessage("GAME OVER", 3.0);
        }

        protected void Queue(SoundId sound)
        {
            if (_sounds.Count < 16)
                _sounds.Add(sound);
        }

        protected void ShowMessage(string message, double seconds)
        {
            Message = message ?? string.Empty;
            MessageTimer = Math.Max(0.1, seconds);
        }

        public IReadOnlyList<SoundId> DrainSounds()
        {
            if (_sounds.Count == 0)
                return Array.Empty<SoundId>();

            var copy = _sounds.ToArray();
            _sounds.Clear();
            return copy;
        }
    }
}
