using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class LightCycleGameTests
    {
        private static LightCycleGame Start(SkillLevel skill = SkillLevel.Skilled, ulong seed = 1)
        {
            var game = new LightCycleGame();
            game.Start(skill, new XorShiftRandom(seed));
            return game;
        }

        private static void Crash(LightCycleGame game)
        {
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(LightCycleGame.Columns - 1, 5, 1, 0);
            game.TestStepOnce();
        }

        [Fact]
        public void StartPlacesThePlayerAndRivals()
        {
            LightCycleGame game = Start();

            Assert.Equal((3, LightCycleGame.Rows / 2), game.PlayerCell);
            Assert.Equal(2, game.OpponentCount);      // 1 + 1 extra for Skilled
            Assert.Equal(3, game.Lives);
            Assert.True(game.PlayerAlive);
            Assert.True(game.IsCountingDown);
        }

        [Fact]
        public void HigherSkillsFieldMoreRivals()
        {
            Assert.Equal(1, Start(SkillLevel.Novice).OpponentCount);
            Assert.True(Start(SkillLevel.Ace).OpponentCount > Start(SkillLevel.Veteran).OpponentCount);
        }

        [Fact]
        public void TheCountdownStopsTheCycles()
        {
            LightCycleGame game = Start();
            game.Update(0.2, InputState.None);

            Assert.Equal((3, LightCycleGame.Rows / 2), game.PlayerCell);
            Assert.True(game.IsCountingDown);

            game.Update(LightCycleGame.CountdownSeconds, InputState.None);
            Assert.False(game.IsCountingDown);
        }

        [Fact]
        public void TheCycleMovesAndLeavesATrail()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();

            game.Update(0.2, InputState.None);

            Assert.Equal((4, LightCycleGame.Rows / 2), game.PlayerCell);
            Assert.True(game.PlayerTrailLength >= 2);
        }

        [Fact]
        public void TurningChangesTheDirection()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(10, 10, 1, 0);

            game.TestQueueTurn(0, 1);
            game.TestStepOnce();

            Assert.Equal((10, 11), game.PlayerCell);
            Assert.Equal((0, 1), game.PlayerDirection);
        }

        [Fact]
        public void ALightCycleCannotReverse()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(10, 10, 1, 0);

            game.TestQueueTurn(-1, 0);
            game.TestStepOnce();

            Assert.Equal((11, 10), game.PlayerCell);
            Assert.Equal((1, 0), game.PlayerDirection);
        }

        [Fact]
        public void InputTurnsThePlayer()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();

            game.TestQueueTurn(0, -1);                  // up, away from the rivals
            game.TestStepOnce();

            Assert.Equal((0, -1), game.PlayerDirection);
            Assert.Equal((3, LightCycleGame.Rows / 2 - 1), game.PlayerCell);
        }

        [Fact]
        public void HittingTheWallCostsALife()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(LightCycleGame.Columns - 1, 5, 1, 0);

            game.TestStepOnce();

            Assert.False(game.PlayerAlive);
            Assert.Equal(2, game.Lives);
        }

        [Fact]
        public void HittingATrailCostsALife()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(10, 10, 1, 0);
            game.TestOccupy(11, 10);

            game.TestStepOnce();

            Assert.False(game.PlayerAlive);
            Assert.Equal(2, game.Lives);
        }

        [Fact]
        public void ThreeCrashesEndTheGame()
        {
            LightCycleGame game = Start();

            for (int i = 0; i < 3; i++)
            {
                Crash(game);
                game.Update(LightCycleGame.RestartSeconds + 0.2, InputState.None);
            }

            Assert.Equal(0, game.Lives);
            Assert.True(game.IsGameOver);
        }

        [Fact]
        public void SurvivingTheRoundAdvancesAndScores()
        {
            LightCycleGame game = Start(SkillLevel.Skilled);
            game.TestSkipCountdown();
            game.TestClearOpponents();

            game.TestStepOnce();

            Assert.Equal(2, game.Level);
            Assert.Equal(1375, game.Score);      // (1000 + 100) x 1.25
        }

        [Fact]
        public void CrashingRivalsScoresPoints()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.TestClearOpponents();
            game.TestPlacePlayer(3, LightCycleGame.Rows / 2, 1, 0);
            game.TestAddOpponent(20, 10, 1, 0);
            game.TestOccupy(21, 10);   // straight ahead
            game.TestOccupy(20, 9);    // turn one way
            game.TestOccupy(20, 11);   // turn the other

            game.TestStepOnce();

            Assert.True(game.Score >= 313);      // 250 x 1.25 per rival
            Assert.Equal(2, game.Level);
        }

        [Fact]
        public void TrailsAppearAsEntities()
        {
            LightCycleGame game = Start();
            game.TestSkipCountdown();
            game.Update(0.2, InputState.None);

            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Trail);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Player);
            Assert.Contains(game.Entities, e => e.Kind == EntityKind.Enemy);
        }

        [Fact]
        public void SameSeedGivesTheSameArena()
        {
            LightCycleGame a = Start(SkillLevel.Veteran, 21);
            LightCycleGame b = Start(SkillLevel.Veteran, 21);

            for (int i = 0; i < 40; i++)
            {
                a.Update(0.15, InputState.None);
                b.Update(0.15, InputState.None);
            }

            Assert.Equal(a.PlayerCell, b.PlayerCell);
            Assert.Equal(a.OpponentCount, b.OpponentCount);
            Assert.Equal(a.Score, b.Score);
            Assert.Equal(a.PlayerTrailLength, b.PlayerTrailLength);
        }

        [Fact]
        public void RivalsTurnAndSurviveForAWhile()
        {
            LightCycleGame game = Start(SkillLevel.Novice);

            for (int i = 0; i < 120; i++)
                game.Update(0.1, InputState.None);

            // The arena is big enough that the rivals should still be running after 12 seconds.
            Assert.True(game.OpponentCount > 0 || game.Score > 0);
            Assert.True(game.PlayerTrailLength > 3);
        }

        [Fact]
        public void AceRunsFasterThanNovice()
        {
            Assert.True(Start(SkillLevel.Ace).CellsPerSecond > Start(SkillLevel.Novice).CellsPerSecond);
        }

        [Fact]
        public void RestartingAfterACrashRebuildsTheArena()
        {
            LightCycleGame game = Start();
            Crash(game);

            game.Update(LightCycleGame.RestartSeconds + 0.2, InputState.None);

            Assert.True(game.PlayerAlive);
            Assert.Equal((3, LightCycleGame.Rows / 2), game.PlayerCell);
            Assert.Equal(2, game.Lives);
        }
    }
}
