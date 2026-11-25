using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class ElektroStormGameTests
    {
        private static ElektroStormGame Start(SkillLevel skill = SkillLevel.Skilled, ulong seed = 9)
        {
            var game = new ElektroStormGame();
            game.Start(skill, new XorShiftRandom(seed));
            return game;
        }

        [Fact]
        public void StartsWithSixCitiesAndThreeBatteries()
        {
            ElektroStormGame game = Start();

            Assert.Equal(6, game.CitiesLeft);
            Assert.Equal(6, game.Lives);
            Assert.Equal(3 * ElektroStormGame.AmmoPerSilo, game.AmmoLeft);
            Assert.Equal(1, game.Level);
        }

        [Fact]
        public void TheCrosshairMovesWithTheStick()
        {
            ElektroStormGame game = Start();
            float before = game.Crosshair.X;

            game.Update(0.1, InputState.Move(false, true, false, false));

            Assert.True(game.Crosshair.X > before);
        }

        [Fact]
        public void TheCrosshairStaysInsideTheSky()
        {
            ElektroStormGame game = Start();
            game.AimAt(-500, 5000);

            Assert.True(game.Crosshair.X >= 0);
            Assert.True(game.Crosshair.Y <= ElektroStormGame.GroundY);
        }

        [Fact]
        public void FiringUsesTheNearestBattery()
        {
            ElektroStormGame game = Start();

            game.TestFireAt(200, 200);

            Assert.Equal(3 * ElektroStormGame.AmmoPerSilo - 1, game.AmmoLeft);
        }

        [Fact]
        public void AnEmptyBatteryCannotFire()
        {
            ElektroStormGame game = Start();
            for (int i = 0; i < 3; i++)
                game.TestSetAmmo(i, 0);

            game.TestFireAt(400, 200);

            Assert.Equal(0, game.AmmoLeft);
            Assert.Equal(0, game.Entities.Count(e => e.Kind == EntityKind.Bullet));
        }

        [Fact]
        public void ABlastDestroysAnIncomingMissile()
        {
            ElektroStormGame game = Start();
            game.TestClearIncoming();
            game.TestSpawnMissile(500, 560, true, 0);

            game.TestFireAt(500, 14);

            for (int i = 0; i < 60; i++)
                game.Update(0.02, InputState.None);

            Assert.Equal(0, game.IncomingCount);
            Assert.True(game.Score > 0);
        }

        [Fact]
        public void AMissileThatGetsThroughDestroysACity()
        {
            ElektroStormGame game = Start();
            game.TestClearIncoming();
            game.TestSpawnMissile(300, game.CityCentreX(2), true, 2);

            for (int i = 0; i < 600 && game.CitiesLeft == 6; i++)
                game.Update(0.02, InputState.None);

            Assert.Equal(5, game.CitiesLeft);
            Assert.Equal(5, game.Lives);
        }

        [Fact]
        public void LosingEveryCityEndsTheGame()
        {
            ElektroStormGame game = Start();
            game.TestClearIncoming();

            for (int i = 0; i < 6; i++)
                game.TestDestroyCity(i);

            Assert.Equal(0, game.CitiesLeft);
            Assert.True(game.IsGameOver);
        }

        [Fact]
        public void ClearingAWavePaysForCitiesAndAmmoAndRaisesTheWave()
        {
            ElektroStormGame game = Start();
            game.TestClearIncoming();
            int before = game.Score;

            game.Update(0.02, InputState.None);

            Assert.Equal(2, game.Level);
            Assert.True(game.Score > before);
        }

        [Fact]
        public void HigherSkillsSendMoreMissilesFaster()
        {
            ElektroStormGame novice = Start(SkillLevel.Novice);
            ElektroStormGame ace = Start(SkillLevel.Ace);

            Assert.True(ace.PendingSpawns > novice.PendingSpawns);
            Assert.True(ace.MissileSpeedForLevel > novice.MissileSpeedForLevel);
        }

        [Fact]
        public void IncomingMissilesFallTowardsTheGround()
        {
            ElektroStormGame game = Start();
            game.TestClearIncoming();
            game.TestSpawnMissile(400, 400, true, 0);

            var start = game.Entities.First(e => e.Kind == EntityKind.Enemy).Position;
            game.Update(0.2, InputState.None);
            var later = game.Entities.First(e => e.Kind == EntityKind.Enemy).Position;

            Assert.True(later.Y > start.Y);
        }

        [Fact]
        public void TheArenaDrawsCitiesBatteriesAndTheCrosshair()
        {
            ElektroStormGame game = Start();

            Assert.Equal(6, game.Entities.Count(e => e.Kind == EntityKind.City));
            Assert.Equal(3, game.Entities.Count(e => e.Kind == EntityKind.Silo));
            Assert.Single(game.Entities.Where(e => e.Kind == EntityKind.Crosshair));
        }

        [Fact]
        public void SameSeedGivesTheSameStorm()
        {
            ElektroStormGame a = Start(SkillLevel.Veteran, 42);
            ElektroStormGame b = Start(SkillLevel.Veteran, 42);

            for (int i = 0; i < 80; i++)
            {
                a.Update(0.05, InputState.None);
                b.Update(0.05, InputState.None);
            }

            Assert.Equal(a.IncomingCount, b.IncomingCount);
            Assert.Equal(a.CitiesLeft, b.CitiesLeft);
            Assert.Equal(a.Score, b.Score);
        }
    }
}
