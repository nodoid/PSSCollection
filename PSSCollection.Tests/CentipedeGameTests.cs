using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class CentipedeGameTests
    {
        private static CentipedeGame Start(SkillLevel skill = SkillLevel.Skilled, ulong seed = 1)
        {
            var game = new CentipedeGame();
            game.Start(skill, new XorShiftRandom(seed));
            return game;
        }

        private static void Step(IGame game, double seconds, InputState input = default, double step = 0.02)
        {
            double remaining = seconds;
            while (remaining > 0)
            {
                double delta = System.Math.Min(step, remaining);
                game.Update(delta, input);
                remaining -= delta;
            }
        }

        [Fact]
        public void StartSpawnsATwelveSegmentCentipedeAndAMushroomField()
        {
            CentipedeGame game = Start();

            Assert.Equal(CentipedeGame.StartingSegments, game.SegmentCount);
            Assert.Equal(1, game.ChainCount);
            Assert.True(game.MushroomCount > 10);
            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void NoviceStartsWithAnExtraLife()
        {
            Assert.Equal(4, Start(SkillLevel.Novice).Lives);
        }

        [Fact]
        public void PlayerIsConfinedToTheLowerBand()
        {
            CentipedeGame game = Start();

            Step(game, 3, InputState.Move(false, false, true, false));
            Assert.Equal(CentipedeGame.PlayerZoneTop, game.PlayerPosition.Y);

            Step(game, 3, InputState.Move(false, false, false, true));
            Assert.Equal(CentipedeGame.Height - CentipedeGame.PlayerSize - 2, game.PlayerPosition.Y);

            Step(game, 5, InputState.Move(true, false));
            Assert.Equal(0, game.PlayerPosition.X);
        }

        [Fact]
        public void FiringSendsOneBulletUpwards()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);

            game.Update(0.016, InputState.Move(false, false).WithFire(true));

            Assert.True(game.BulletActive);
            Assert.Single(game.Entities, e => e.Kind == EntityKind.Bullet);
        }

        [Fact]
        public void MushroomsTakeFourHits()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);
            game.TestSetMushroom(10, 20, 4);

            game.TestSpawnBullet(210, 415);
            game.Update(0.016, InputState.None);

            Assert.Equal(3, game.MushroomAt(10, 20));
            Assert.Equal(1, game.Score);
        }

        [Fact]
        public void DestroyedMushroomIsRemovedFromTheCount()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);
            game.TestSetMushroom(10, 20, 1);
            int before = game.MushroomCount;

            game.TestSpawnBullet(210, 415);
            game.Update(0.016, InputState.None);

            Assert.Equal(0, game.MushroomAt(10, 20));
            Assert.Equal(before - 1, game.MushroomCount);
        }

        [Fact]
        public void ShootingTheHeadScoresAHundredAndLeavesAMushroom()
        {
            CentipedeGame game = Start(SkillLevel.Skilled);
            game.TestSetInvulnerable(true);
            game.TestSetChain(20, 5, 6, 1);

            game.TestSpawnBullet(410, 119);
            game.Update(0.016, InputState.None);

            Assert.Equal(125, game.Score);
            Assert.Equal(4, game.MushroomAt(20, 5));
            Assert.Equal(5, game.SegmentCount);
        }

        [Fact]
        public void ShootingTheBodySplitsTheCentipedeInTwo()
        {
            CentipedeGame game = Start(SkillLevel.Skilled);
            game.TestSetInvulnerable(true);
            game.TestSetChain(20, 5, 6, 1);

            game.TestSpawnBullet(370, 119);
            game.Update(0.016, InputState.None);

            Assert.Equal(13, game.Score);
            Assert.Equal(2, game.ChainCount);
            Assert.Equal(5, game.SegmentCount);
            Assert.Equal(4, game.MushroomAt(18, 5));
        }

        private static void ClearColumn(CentipedeGame game, int column)
        {
            // Mushrooms block bullets, so clear the firing lane before testing a hit.
            for (int row = 0; row < CentipedeGame.Rows; row++)
                game.TestSetMushroom(column, row, 0);
        }

        [Fact]
        public void SpiderScoresMoreTheCloserItIs()
        {
            CentipedeGame game = Start(SkillLevel.Novice);
            game.TestSetInvulnerable(true);
            ClearColumn(game, 20);
            game.TestPlacePlayer(390, 560);
            game.TestSpawnSpider(400, 520, 0, 0);

            game.TestSpawnBullet(405, 545);
            game.Update(0.016, InputState.None);

            Assert.Equal(0, game.SpiderCount);
            Assert.Equal(900, game.Score);
        }

        [Fact]
        public void DistantSpiderScoresThreeHundred()
        {
            CentipedeGame game = Start(SkillLevel.Novice);
            game.TestSetInvulnerable(true);
            ClearColumn(game, 35);
            game.TestPlacePlayer(20, 560);
            game.TestSpawnSpider(700, 520, 0, 0);

            game.TestSpawnBullet(705, 545);
            game.Update(0.016, InputState.None);

            Assert.Equal(300, game.Score);
        }

        [Fact]
        public void ClearingTheCentipedeAdvancesTheLevel()
        {
            CentipedeGame game = Start(SkillLevel.Novice);
            game.TestClearChains();

            game.Update(0.016, InputState.None);

            Assert.Equal(2, game.Level);
            Assert.Equal(650, game.Score);
            Assert.Equal(13, game.SegmentCount);
        }

        [Fact]
        public void TouchingASegmentCostsALife()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(false);
            game.TestPlacePlayer(200, 580);
            game.TestSetChain(10, 29, 4, 1);

            game.Update(0.016, InputState.None);

            Assert.Equal(2, game.Lives);
            Assert.True(game.PlayerInvulnerable);
        }

        [Fact]
        public void TheCentipedeDescendsWhenItMeetsAMushroom()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);
            game.TestSetChain(10, 5, 4, 1);
            game.TestSetMushroom(11, 5, 4);
            game.TestSetMushroom(12, 5, 4);

            Step(game, 0.4, InputState.None, 0.02);

            Assert.True(game.TestHeadRow > 5);
        }

        [Fact]
        public void TheCentipedeBouncesOffTheSideWalls()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);
            game.TestSetChain(1, 10, 3, -1);

            Step(game, 0.6, InputState.None, 0.02);

            Assert.True(game.TestHeadRow >= 10);
            Assert.InRange(game.TestHeadColumn, 0, CentipedeGame.Columns - 1);
        }

        [Fact]
        public void AceCentipedeMovesFasterThanNovice()
        {
            CentipedeGame novice = Start(SkillLevel.Novice);
            CentipedeGame ace = Start(SkillLevel.Ace);
            novice.TestSetInvulnerable(true);
            ace.TestSetInvulnerable(true);

            novice.TestSetChain(20, 10, 4, 1);
            ace.TestSetChain(20, 10, 4, 1);

            Step(novice, 1.0, InputState.None, 0.02);
            Step(ace, 1.0, InputState.None, 0.02);

            Assert.True(ace.TestHeadColumn > novice.TestHeadColumn);
        }

        [Fact]
        public void MushroomsAppearAsEntities()
        {
            CentipedeGame game = Start();
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Mushroom);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.CentipedeHead);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Player);
        }

        [Fact]
        public void SameSeedSameField()
        {
            CentipedeGame a = Start(SkillLevel.Skilled, 42);
            CentipedeGame b = Start(SkillLevel.Skilled, 42);

            Assert.Equal(a.MushroomCount, b.MushroomCount);
            for (int row = 0; row < CentipedeGame.Rows; row++)
                for (int column = 0; column < CentipedeGame.Columns; column++)
                    Assert.Equal(a.MushroomAt(column, row), b.MushroomAt(column, row));
        }

        [Fact]
        public void PausingFreezesTheCentipede()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);
            game.TestSetChain(20, 5, 4, 1);

            game.Update(0.016, new InputState(false, false, false, false, false, true, false));
            int before = game.TestHeadColumn;
            Step(game, 2, InputState.None, 0.02);

            Assert.Equal(before, game.TestHeadColumn);
        }

        [Fact]
        public void PlayerCannotFireWhileAheadBulletExists()
        {
            CentipedeGame game = Start();
            game.TestSetInvulnerable(true);

            game.Update(0.016, InputState.Move(false, false).WithFire(true));
            game.Update(0.016, InputState.Move(false, false).WithFire(true));

            Assert.Single(game.Entities, e => e.Kind == EntityKind.Bullet);
        }
    }
}
