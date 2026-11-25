using System.Linq;
using System.Numerics;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class InvadersGameTests
    {
        private static InvadersGame Start(SkillLevel skill = SkillLevel.Skilled, ulong seed = 1)
        {
            var game = new InvadersGame();
            game.Start(skill, new XorShiftRandom(seed));
            return game;
        }

        private static void Step(IGame game, double seconds, InputState input = default, double step = 0.05)
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
        public void StartBuildsAFiveRowFormationWithFourBunkers()
        {
            InvadersGame game = Start();

            Assert.Equal(5 * InvadersGame.Columns, game.AlienCount);
            Assert.Equal(5, game.CurrentRows);
            Assert.Equal(InvadersGame.BunkerRows * InvadersGame.BunkerColumns * InvadersGame.BunkerCount, game.BunkerBlockCount);
            Assert.False(game.IsGameOver);
            Assert.Equal(3, game.Lives);
        }

        [Fact]
        public void NoviceStartsWithAnExtraLife()
        {
            Assert.Equal(4, Start(SkillLevel.Novice).Lives);
            Assert.Equal(3, Start(SkillLevel.Ace).Lives);
        }

        [Fact]
        public void PlayerStartsCentred()
        {
            InvadersGame game = Start();
            Assert.Equal(474f, game.PlayerPosition.X);
            Assert.Equal(InvadersGame.PlayerY, game.PlayerPosition.Y);
        }

        [Fact]
        public void PlayerCannotLeaveTheScreen()
        {
            InvadersGame game = Start();

            Step(game, 2, InputState.Move(true, false));
            Assert.Equal(0, game.PlayerPosition.X);

            Step(game, 4, InputState.Move(false, true));
            Assert.Equal(InvadersGame.Width - InvadersGame.PlayerWidth, game.PlayerPosition.X);
        }

        [Fact]
        public void FiringProducesOneBulletAtATime()
        {
            InvadersGame game = Start();

            game.Update(0.016, InputState.Move(false, false).WithFire(true));
            Assert.True(game.BulletActive);

            game.Update(0.016, InputState.Move(false, false).WithFire(true));
            Assert.Single(game.Entities, e => e.Kind == EntityKind.Bullet);
        }

        [Fact]
        public void FireCooldownAllowsAnotherBulletOnceTheFirstIsGone()
        {
            InvadersGame game = Start();

            game.Update(0.016, InputState.Move(false, false).WithFire(true));
            game.TestClearBombs();
            game.Update(0.3, InputState.Move(false, false).WithFire(true));

            Assert.True(game.BulletActive);
        }

        [Fact]
        public void ShootingAnAlienScoresByRowAndRemovesIt()
        {
            InvadersGame game = Start(SkillLevel.Novice);
            game.TestSetFormation(0, 0);

            // Row 0 scores 30; row 4 scores 10.
            game.TestSpawnBullet(100, 130);
            game.Update(0.05, InputState.None);
            Assert.Equal(30, game.Score);
            Assert.Equal(5 * InvadersGame.Columns - 1, game.AlienCount);

            game.TestSetInvulnerable(true);
            game.TestSpawnBullet(280, 320);
            game.Update(0.05, InputState.None);
            Assert.Equal(40, game.Score);
        }

        [Fact]
        public void SkillMultiplierIsAppliedToAlienScores()
        {
            InvadersGame game = Start(SkillLevel.Ace);
            game.TestSetFormation(0, 0);
            game.TestSpawnBullet(100, 130);
            game.Update(0.05, InputState.None);

            Assert.Equal(60, game.Score);   // 30 x 2
        }

        [Fact]
        public void BulletsErodeTheBunkers()
        {
            InvadersGame game = Start();
            int before = game.BunkerBlockCount;

            game.TestSpawnBullet(130, 560);
            game.Update(0.016, InputState.None);

            Assert.True(game.BunkerBlockCount < before);
        }

        [Fact]
        public void BombsErodeTheBunkers()
        {
            InvadersGame game = Start();
            int before = game.BunkerBlockCount;

            game.TestSpawnBomb(130, 520);
            Step(game, 0.2, InputState.None, 0.02);

            Assert.True(game.BunkerBlockCount < before);
        }

        [Fact]
        public void UfoScoresItsValueWhenShot()
        {
            InvadersGame game = Start(SkillLevel.Novice);
            game.TestSetInvulnerable(true);
            game.TestSpawnUfo(500, 300);

            game.TestSpawnBullet(510, 90);
            game.Update(0.05, InputState.None);

            Assert.False(game.UfoActive);
            Assert.Equal(300, game.Score);
        }

        [Fact]
        public void UfoValuesComeFromTheFourArcadeAmounts()
        {
            var values = new System.Collections.Generic.HashSet<int>();

            for (ulong seed = 1; seed <= 40; seed++)
            {
                InvadersGame game = Start(SkillLevel.Skilled, seed);
                for (int i = 0; i < 120 && !game.UfoActive; i++)
                    game.Update(0.25, InputState.None);

                if (game.UfoActive)
                    values.Add(game.UfoValue);
            }

            Assert.NotEmpty(values);
            Assert.All(values, v => Assert.Contains(v, new[] { 50, 100, 150, 300 }));
        }

        [Fact]
        public void AUfoAppearsWithinHalfAMinute()
        {
            InvadersGame game = Start(SkillLevel.Skilled, 5);

            bool seen = false;
            for (int i = 0; i < 120 && !seen; i++)
            {
                game.Update(0.25, InputState.None);
                seen = game.Entities.Any(e => e.Kind == EntityKind.Ufo);
            }

            Assert.True(seen);
        }

        [Fact]
        public void BombHitCostsALife()
        {
            InvadersGame game = Start();
            game.TestSetInvulnerable(false);
            Vector2 player = game.PlayerPosition;

            game.TestSpawnBomb(player.X + 10, player.Y + 4);
            game.Update(0.016, InputState.None);

            Assert.Equal(2, game.Lives);
            Assert.True(game.PlayerInvulnerable);
        }

        [Fact]
        public void ThreeBombHitsEndTheGame()
        {
            InvadersGame game = Start();

            for (int hit = 0; hit < 3; hit++)
            {
                game.TestSetInvulnerable(false);
                Vector2 player = game.PlayerPosition;
                game.TestSpawnBomb(player.X + 10, player.Y + 4);
                game.Update(0.016, InputState.None);
            }

            Assert.Equal(0, game.Lives);
            Assert.True(game.IsGameOver);
        }

        [Fact]
        public void ClearingAWaveAdvancesTheLevelAndScoresABonus()
        {
            InvadersGame game = Start(SkillLevel.Novice);
            game.TestClearAliens();

            game.Update(0.016, InputState.None);

            Assert.Equal(2, game.Level);
            Assert.Equal(200, game.Score);
            Assert.Equal(5 * InvadersGame.Columns, game.AlienCount);
        }

        [Fact]
        public void WaveBonusGrowsWithTheLevel()
        {
            InvadersGame game = Start(SkillLevel.Novice);
            game.TestClearAliens();
            game.Update(0.016, InputState.None);

            game.TestClearAliens();
            game.Update(0.016, InputState.None);

            Assert.Equal(3, game.Level);
            Assert.Equal(200 + 250, game.Score);
        }

        [Fact]
        public void AliensReachingTheGroundEndTheGame()
        {
            InvadersGame game = Start();
            game.TestSetFormation(0, 520);

            game.Update(0.016, InputState.None);

            Assert.True(game.IsGameOver);
            Assert.Equal(0, game.Lives);
        }

        [Fact]
        public void FormationReversesAtTheRightEdge()
        {
            InvadersGame game = Start();
            game.TestSetFormation(260, 0);

            Step(game, 6, InputState.None, 0.1);

            Assert.Equal(-1, game.Direction);
            Assert.True(game.FormationY > 0);
        }

        [Fact]
        public void FormationStepsFasterAsAliensDie()
        {
            InvadersGame game = Start();
            double full = game.TestStepInterval;

            for (int column = 0; column < InvadersGame.Columns; column++)
                for (int row = 1; row < 5; row++)
                    game.TestSetAlien(row, column, false);

            double thin = game.TestStepInterval;

            Assert.True(thin < full);
        }

        [Fact]
        public void AceFormationStepsFasterThanNovice()
        {
            InvadersGame novice = Start(SkillLevel.Novice);
            InvadersGame ace = Start(SkillLevel.Ace);

            Assert.True(ace.TestStepInterval < novice.TestStepInterval);
        }

        [Fact]
        public void AliensFireBombsOverTime()
        {
            InvadersGame game = Start(SkillLevel.Ace, 3);

            bool fired = false;
            for (int i = 0; i < 120 && !fired; i++)
            {
                game.Update(0.05, InputState.None);
                fired = game.DrainSounds().Contains(SoundId.EnemyShoot);
            }

            Assert.True(fired);
        }

        [Fact]
        public void PausingStopsTheClock()
        {
            InvadersGame game = Start();
            game.Update(0.016, new InputState(false, false, false, false, false, true, false));

            Assert.True(game.IsPaused);
            double elapsed = game.Elapsed;
            game.Update(1.0, InputState.None);
            Assert.Equal(elapsed, game.Elapsed);
        }

        [Fact]
        public void SameSeedSameGame()
        {
            InvadersGame a = Start(SkillLevel.Veteran, 99);
            InvadersGame b = Start(SkillLevel.Veteran, 99);

            Step(a, 20, InputState.Move(true, false).WithFire(true), 0.05);
            Step(b, 20, InputState.Move(true, false).WithFire(true), 0.05);

            Assert.Equal(a.Score, b.Score);
            Assert.Equal(a.AlienCount, b.AlienCount);
            Assert.Equal(a.FormationX, b.FormationX);
        }

        [Fact]
        public void EntitiesDescribePlayersAliensBunkersAndBullets()
        {
            InvadersGame game = Start();
            game.Update(0.016, InputState.Move(false, false).WithFire(true));

            var kinds = game.Entities.Select(e => e.Kind).Distinct().ToList();
            Assert.Contains(EntityKind.Player, kinds);
            Assert.Contains(EntityKind.Alien, kinds);
            Assert.Contains(EntityKind.Shield, kinds);
            Assert.Contains(EntityKind.Bullet, kinds);
        }
    }
}
