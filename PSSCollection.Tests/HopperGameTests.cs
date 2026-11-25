using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class HopperGameTests
    {
        private static HopperGame Start(SkillLevel skill = SkillLevel.Skilled, ulong seed = 5)
        {
            var game = new HopperGame();
            game.Start(skill, new XorShiftRandom(seed));
            return game;
        }

        [Fact]
        public void StartsOnTheBankWithAFullClock()
        {
            HopperGame game = Start();

            Assert.Equal(HopperGame.StartRow, game.PlayerRow);
            Assert.Equal(9, game.PlayerColumn);
            Assert.Equal(HopperGame.TimeUnits, game.TimeLeft);
            Assert.Equal(4, game.Lives);
            Assert.Equal(0, game.BaysFilled);
        }

        [Fact]
        public void NoviceGetsMoreLivesThanAce()
        {
            Assert.True(Start(SkillLevel.Novice).Lives > Start(SkillLevel.Ace).Lives);
        }

        [Fact]
        public void HoppingUpMovesOneLaneAndScores()
        {
            HopperGame game = Start();
            game.TestClearTraffic();

            game.TestHop(0, -1);

            Assert.Equal(HopperGame.StartRow - 1, game.PlayerRow);
            Assert.True(game.Score > 0);
        }

        [Fact]
        public void HoppingLeftAndRightChangesColumn()
        {
            HopperGame game = Start();
            game.TestClearTraffic();

            game.TestHop(-1, 0);
            Assert.Equal(8, game.PlayerColumn);

            game.TestHop(1, 0);
            game.TestHop(1, 0);
            Assert.Equal(10, game.PlayerColumn);
        }

        [Fact]
        public void TheFrogCannotHopBelowTheStartBank()
        {
            HopperGame game = Start();
            game.TestClearTraffic();

            game.TestHop(0, 1);

            Assert.Equal(HopperGame.StartRow, game.PlayerRow);
        }

        [Fact]
        public void LandingInTheWaterDrownsTheFrog()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(5, 2);

            game.Update(0.05, InputState.None);

            Assert.Equal(3, game.Lives);
            Assert.Contains("SPLASH", game.Message);
        }

        [Fact]
        public void RidingALogCarriesTheFrog()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(5, 1);
            game.TestAddFloat(1, 5 * HopperGame.Cell, 3, 120f);

            game.Update(0.2, InputState.None);

            Assert.True(game.IsRiding);
            Assert.True(game.PlayerX > 5 * HopperGame.Cell);
        }

        [Fact]
        public void ATurtleThatDivesDropsTheFrog()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(5, 2);
            game.TestAddFloat(2, 5 * HopperGame.Cell, 2, 0f);

            // Sheet two is where the turtles start diving.
            game.TestSetLevel(2);
            Assert.True(game.TurtlesDive);

            game.TestClearTraffic();
            game.TestSetPlayer(5, 2);
            game.TestAddFloat(2, 5 * HopperGame.Cell, 2, 0f);

            int lives = game.Lives;
            for (int i = 0; i < 500 && game.Lives == lives; i++)
                game.Update(0.02, InputState.None);

            Assert.True(game.Lives < lives);
        }

        [Fact]
        public void ACarKillsTheFrog()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(5, HopperGame.RoadTopRow);
            game.TestAddCar(HopperGame.RoadTopRow, 5 * HopperGame.Cell, 2);

            game.Update(0.05, InputState.None);

            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void TheSnakePatrolsTheMiddleBank()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(5, HopperGame.MedianRow);
            game.TestSetSnake(5 * HopperGame.Cell);

            game.Update(0.05, InputState.None);

            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void LandingInAHomeBayScoresAndRespawns()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetPlayer(9, 1);

            game.TestHop(0, -1);

            Assert.Equal(1, game.BaysFilled);
            Assert.Equal(HopperGame.StartRow, game.PlayerRow);
            Assert.True(game.Score >= 125);
        }

        [Fact]
        public void FiveHomesClearTheSheetAndPayTheTimeBonus()
        {
            HopperGame game = Start();
            game.TestClearTraffic();

            for (int bay = 0; bay < 4; bay++)
                game.TestFillBay(bay);

            game.TestSetPlayer(17, 1);
            int before = game.Score;
            game.TestHop(0, -1);

            Assert.Equal(2, game.Level);
            Assert.Equal(0, game.BaysFilled);
            Assert.Equal(HopperGame.TimeUnits, game.TimeLeft);
            Assert.True(game.Score - before >= HopperGame.TimeUnits * 10);
        }

        [Fact]
        public void RunningOutOfTimeCostsALifeAndResetsTheClock()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestSetTimeLeft(1);

            game.Update(1.0, InputState.None);

            Assert.Equal(3, game.Lives);
            Assert.Equal(HopperGame.TimeUnits, game.TimeLeft);
        }

        [Fact]
        public void TheFlyAppearsFromTheThirdSheet()
        {
            HopperGame game = Start();
            Assert.False(game.FliesAppear);

            game.TestSpawnFly(3);

            Assert.True(game.FliesAppear);
            Assert.Equal(3, game.FlyBay);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Fly);
        }

        [Fact]
        public void TheArenaHasThreeRoadLanesAndFourRiverLanes()
        {
            HopperGame game = Start();
            game.TestClearTraffic();
            game.TestAddCar(HopperGame.RoadTopRow, 0, 2);
            game.TestAddFloat(HopperGame.RiverTopRow, 0, 3, 0);

            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Enemy);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Log || e.Kind == EntityKind.Turtle);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Home);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Snake);
        }

        [Fact]
        public void AceTrafficIsDenserThanNovice()
        {
            Assert.True(Start(SkillLevel.Ace).CarCount >= Start(SkillLevel.Novice).CarCount);
        }

        [Fact]
        public void SameSeedGivesTheSameCrossing()
        {
            HopperGame a = Start(SkillLevel.Veteran, 77);
            HopperGame b = Start(SkillLevel.Veteran, 77);

            for (int i = 0; i < 60; i++)
            {
                a.Update(0.1, InputState.None);
                b.Update(0.1, InputState.None);
            }

            Assert.Equal(a.Lives, b.Lives);
            Assert.Equal(a.Score, b.Score);
            Assert.Equal(a.SnakeX, b.SnakeX);
        }
    }
}
