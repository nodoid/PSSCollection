using System.Collections.Generic;
using System.Numerics;
using PSSCollection.Engine;
using Xunit;

namespace PSSCollection.Tests
{
    public class GameBaseTests
    {
        /// <summary>A minimal game exposing the protected hooks so the shared rules can be tested.</summary>
        private sealed class ProbeGame : GameBase
        {
            public override GameId Id => GameId.Invaders;

            public override Vector2 PlayfieldSize => new Vector2(100, 100);

            public override IReadOnlyList<Entity> Entities { get; } = new List<Entity>();

            public double Updates;

            protected override void OnStart()
            {
                Updates = 0;
            }

            protected override void UpdateCore(double deltaSeconds, InputState input)
            {
                Updates++;
            }

            public void Score1(int points) => Award(points);

            public void Wave(int bonus) => CompleteWave(bonus);

            public void Kill() => LoseLife();

            public void Finish() => EndGame();

            public void Say(string text, double seconds) => ShowMessage(text, seconds);

            public void Ping(SoundId sound) => Queue(sound);
        }

        private static ProbeGame Start(SkillLevel skill = SkillLevel.Skilled) =>
            Start(skill, new XorShiftRandom(1));

        private static ProbeGame Start(SkillLevel skill, IRandomSource rng)
        {
            var game = new ProbeGame();
            game.Start(skill, rng);
            return game;
        }

        [Fact]
        public void StartResetsEverything()
        {
            ProbeGame game = Start(SkillLevel.Ace);

            Assert.Equal(0, game.Score);
            Assert.Equal(1, game.Level);
            Assert.Equal(3, game.Lives);
            Assert.False(game.IsGameOver);
            Assert.False(game.IsPaused);
            Assert.Equal(0, game.ExtraLivesAwarded);
            Assert.Equal(0, game.Elapsed);
            Assert.Empty(game.DrainSounds());
        }

        [Fact]
        public void StartUsesTheSkillForLivesAndMultiplier()
        {
            ProbeGame novice = Start(SkillLevel.Novice);
            ProbeGame ace = Start(SkillLevel.Ace);

            Assert.Equal(4, novice.Lives);
            Assert.Equal(1.0f, novice.ScoreMultiplier);
            Assert.Equal(2.0f, ace.ScoreMultiplier);
        }

        [Fact]
        public void SkillIsClampedOnStart()
        {
            ProbeGame game = Start((SkillLevel)99);
            Assert.Equal(SkillLevel.Ace, game.Skill);
        }

        [Fact]
        public void AwardAppliesTheSkillMultiplierAndRoundsAwayFromZero()
        {
            ProbeGame game = Start(SkillLevel.Skilled);

            game.Score1(10);
            Assert.Equal(13, game.Score);   // 12.5 rounds up

            game.Score1(100);
            Assert.Equal(138, game.Score);  // +125
        }

        [Fact]
        public void AwardIgnoresZeroAndNegativePoints()
        {
            ProbeGame game = Start();
            game.Score1(0);
            game.Score1(-50);

            Assert.Equal(0, game.Score);
        }

        [Fact]
        public void EveryTenThousandPointsAwardsAnExtraLife()
        {
            ProbeGame game = Start(SkillLevel.Novice);

            game.Score1(10000);

            Assert.Equal(10000, game.Score);
            Assert.Equal(1, game.ExtraLivesAwarded);
            Assert.Equal(5, game.Lives);   // Novice starts with four
            Assert.Contains(SoundId.ExtraLife, game.DrainSounds());
        }

        [Fact]
        public void TwoExtraLivesForTwentyThousandPoints()
        {
            ProbeGame game = Start(SkillLevel.Novice);
            game.Score1(20000);

            Assert.Equal(2, game.ExtraLivesAwarded);
            Assert.Equal(6, game.Lives);
        }

        [Fact]
        public void ExtraLifeIntervalAccountsForTheMultiplier()
        {
            ProbeGame game = Start(SkillLevel.Ace);
            game.Score1(5000);   // 10,000 with the x2 multiplier

            Assert.Equal(10000, game.Score);
            Assert.Equal(1, game.ExtraLivesAwarded);
        }

        [Fact]
        public void CompleteWaveAddsTheBonusAndAdvancesTheLevel()
        {
            ProbeGame game = Start(SkillLevel.Novice);
            game.Wave(500);

            Assert.Equal(500, game.Score);
            Assert.Equal(2, game.Level);
            Assert.Equal("WAVE 2", game.Message);
            Assert.Contains(SoundId.WaveClear, game.DrainSounds());
        }

        [Fact]
        public void LoseLifeDecrementsAndQueuesTheHitSound()
        {
            ProbeGame game = Start();
            game.Kill();

            Assert.Equal(2, game.Lives);
            Assert.Contains(SoundId.PlayerHit, game.DrainSounds());
        }

        [Fact]
        public void EndGameStopsFurtherUpdates()
        {
            ProbeGame game = Start();
            game.Update(0.1, InputState.None);
            double before = game.Updates;

            game.Finish();
            Assert.True(game.IsGameOver);

            game.Update(0.1, InputState.None);
            Assert.Equal(before, game.Updates);
            Assert.Contains(SoundId.GameOver, game.DrainSounds());
        }

        [Fact]
        public void EndGameIsIdempotent()
        {
            ProbeGame game = Start();
            game.Finish();
            game.Finish();

            Assert.Single(game.DrainSounds());
        }

        [Fact]
        public void PauseStopsTheGameClockUntilUnpaused()
        {
            ProbeGame game = Start();
            game.Update(0.5, InputState.None);
            double elapsed = game.Elapsed;
            double updates = game.Updates;

            game.Update(0.5, new InputState(false, false, false, false, false, true, false));

            Assert.True(game.IsPaused);
            Assert.Equal(elapsed, game.Elapsed);
            Assert.Equal(updates, game.Updates);

            game.Update(0.5, new InputState(false, false, false, false, false, true, false));

            Assert.False(game.IsPaused);
            Assert.Equal(elapsed + 0.5, game.Elapsed, 3);
        }

        [Fact]
        public void BackgroundPhaseAdvancesWithTime()
        {
            ProbeGame game = Start();
            game.Update(0.25, InputState.None);
            game.Update(0.25, InputState.None);

            Assert.Equal(0.5, game.BackgroundPhase, 3);
        }

        [Fact]
        public void MessagesExpire()
        {
            ProbeGame game = Start();
            game.Say("HELLO", 1.0);

            Assert.True(game.IsShowingMessage);
            Assert.Equal("HELLO", game.Message);

            game.Update(1.1, InputState.None);

            Assert.False(game.IsShowingMessage);
            Assert.Equal(string.Empty, game.Message);
        }

        [Fact]
        public void SoundsAreDrainedOnce()
        {
            ProbeGame game = Start();
            game.Ping(SoundId.Shoot);
            game.Ping(SoundId.Explosion);

            IReadOnlyList<SoundId> sounds = game.DrainSounds();
            Assert.Equal(new[] { SoundId.Shoot, SoundId.Explosion }, sounds);
            Assert.Empty(game.DrainSounds());
        }

        [Fact]
        public void SoundQueueIsBounded()
        {
            ProbeGame game = Start();
            for (int i = 0; i < 50; i++)
                game.Ping(SoundId.Shoot);

            Assert.Equal(16, game.DrainSounds().Count);
        }

        [Fact]
        public void NegativeDeltaIsTreatedAsZero()
        {
            ProbeGame game = Start();
            game.Update(-5, InputState.None);

            Assert.Equal(0, game.Elapsed);
        }

        [Fact]
        public void SameSeedGivesTheSameRandomSequence()
        {
            var a = new XorShiftRandom(123);
            var b = new XorShiftRandom(123);

            for (int i = 0; i < 100; i++)
                Assert.Equal(a.Next(1000), b.Next(1000));
        }

        [Fact]
        public void DifferentSeedsDiverge()
        {
            var a = new XorShiftRandom(1);
            var b = new XorShiftRandom(2);

            bool different = false;
            for (int i = 0; i < 20; i++)
            {
                if (a.Next(1000000) != b.Next(1000000))
                {
                    different = true;
                    break;
                }
            }

            Assert.True(different);
        }

        [Fact]
        public void RandomStaysInRange()
        {
            var random = new XorShiftRandom(9);
            for (int i = 0; i < 500; i++)
            {
                int value = random.Next(7);
                Assert.InRange(value, 0, 6);

                double unit = random.NextDouble();
                Assert.InRange(unit, 0.0, 0.9999999999);
            }
        }

        [Fact]
        public void RandomRangeIncludesBothEnds()
        {
            var random = new XorShiftRandom(3);
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
                seen.Add(random.Range(2, 5));

            Assert.Contains(2, seen);
            Assert.Contains(5, seen);
            Assert.DoesNotContain(6, seen);
        }

        [Fact]
        public void RandomChanceAlwaysTrueAtOne()
        {
            var random = new XorShiftRandom(1);
            Assert.True(random.Chance(1.0));
            Assert.False(random.Chance(0.0));
        }
    }
}
