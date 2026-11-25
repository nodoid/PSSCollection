using System.Numerics;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    /// <summary>The mystery ship has to be reachable: it flies in clear air above the formation.</summary>
    public class UfoProbeTests
    {
        private static InvadersGame Fresh()
        {
            var game = new InvadersGame();
            game.Start(SkillLevel.Novice, new XorShiftRandom(1));
            game.TestSetInvulnerable(true);
            return game;
        }

        [Fact]
        public void TheSaucerFliesAboveTheFormationAndBelowTheHud()
        {
            Assert.True(InvadersGame.UfoTop + InvadersGame.UfoHeight <= InvadersGame.FormationTop);
            Assert.True(InvadersGame.FormationTop >= 96f, "the top alien row would sit under the mobile HUD");
        }

        [Fact]
        public void ShotInClearAirAtTheSaucerScoresItsValue()
        {
            InvadersGame game = Fresh();

            int before = game.Score;
            game.TestSpawnUfo(400, 300);
            game.TestSpawnBullet(410, InvadersGame.UfoTop + 40f);
            game.Update(0.05, InputState.None);

            Assert.False(game.UfoActive);
            Assert.Equal(before + 300, game.Score);
        }

        [Fact]
        public void TrackingFireFromTheBottomHitsTheSaucer()
        {
            InvadersGame game = Fresh();
            game.TestSpawnUfo(300, 300);

            int before = game.Score;
            var input = new InputState(false, false, false, false, true, false, false);
            for (int frame = 0; frame < 600 && game.UfoActive; frame++)
            {
                game.TestPlacePlayer(game.UfoX + InvadersGame.UfoWidth * 0.5f - InvadersGame.PlayerWidth * 0.5f);
                game.Update(1.0 / 60.0, input);
            }

            Assert.True(game.Score > before, "the saucer was never hit");
        }
    }
}
