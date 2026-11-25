using System;
using System.Collections.Generic;
using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.Games;
using Xunit;

namespace PSSCollection.Tests
{
    public class CatalogueTests
    {
        [Fact]
        public void CollectionHasFiveGames()
        {
            Assert.Equal(5, Catalogue.Count);
            Assert.Equal(5, Catalogue.All.Count);
        }

        [Fact]
        public void EveryGameIdHasAnEntry()
        {
            foreach (GameId id in Enum.GetValues<GameId>())
            {
                GameInfo info = Catalogue.Get(id);
                Assert.Equal(id, info.Id);
                Assert.False(string.IsNullOrWhiteSpace(info.Title));
                Assert.False(string.IsNullOrWhiteSpace(info.Blurb));
                Assert.False(string.IsNullOrWhiteSpace(info.HowToPlay));
                Assert.False(string.IsNullOrWhiteSpace(info.Controls));
                Assert.Equal("PERSONAL SOFTWARE SERVICES", info.Publisher);
            }
        }

        [Fact]
        public void TitlesAreUnique()
        {
            var titles = Catalogue.All.Select(g => g.Title).ToList();
            Assert.Equal(titles.Count, titles.Distinct().Count());
        }

        [Fact]
        public void GamesArePssOricTitles()
        {
            foreach (GameInfo game in Catalogue.All)
            {
                Assert.Equal(1983, game.Year);
                Assert.StartsWith("PSS ", game.Caption.Substring(game.Caption.Length - 8).TrimStart());
                Assert.Contains("PSS", game.Caption);
            }
        }

        [Fact]
        public void ExcludedTitlesAreNotInTheCollection()
        {
            foreach (string excluded in Catalogue.ExcludedTitles)
            {
                Assert.DoesNotContain(Catalogue.All, g => g.Title.Equals(excluded, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(Catalogue.All, g => g.OriginalTitle.Equals(excluded, StringComparison.OrdinalIgnoreCase));
            }

            Assert.Contains("THE ULTRA", Catalogue.ExcludedTitles);
            Assert.Contains("ORICMON", Catalogue.ExcludedTitles);
        }

        [Fact]
        public void TheCollectionIsTheFivePssOricArcadeGames()
        {
            var ids = Catalogue.All.Select(g => g.Id).ToList();

            Assert.Contains(GameId.Invaders, ids);
            Assert.Contains(GameId.Centipede, ids);
            Assert.Contains(GameId.Hopper, ids);
            Assert.Contains(GameId.ElektroStorm, ids);
            Assert.Contains(GameId.LightCycle, ids);
        }

        [Fact]
        public void IndexOfAndAtWalkTheCatalogue()
        {
            for (int i = 0; i < Catalogue.Count; i++)
            {
                GameId id = Catalogue.At(i);
                Assert.Equal(i, Catalogue.IndexOf(id));
            }
        }

        [Fact]
        public void AtClampsOutOfRangeIndexes()
        {
            Assert.Equal(Catalogue.At(0), Catalogue.At(-5));
            Assert.Equal(Catalogue.At(Catalogue.Count - 1), Catalogue.At(99));
        }

        [Fact]
        public void UnknownGameThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Catalogue.Get((GameId)42));
            Assert.Equal(-1, Catalogue.IndexOf((GameId)42));
        }

        [Fact]
        public void EveryCatalogueGameCanBeInstantiated()
        {
            var games = new List<IGame>
            {
                new InvadersGame(), new CentipedeGame(), new HopperGame(), new ElektroStormGame(),
                new LightCycleGame()
            };

            Assert.Equal(Catalogue.Count, games.Count);
            Assert.Equal(Catalogue.Count, Enum.GetValues<GameId>().Length);
            foreach (IGame game in games)
            {
                game.Start(SkillLevel.Skilled, new XorShiftRandom(7));
                Assert.Equal(Catalogue.Get(game.Id).Id, game.Id);
                Assert.True(game.PlayfieldSize.X > 0);
                Assert.True(game.PlayfieldSize.Y > 0);
                Assert.NotEmpty(game.Entities);
            }
        }
    }

    public class SkillLevelTests
    {
        [Fact]
        public void AllFourLevelsAreListed()
        {
            Assert.Equal(4, SkillLevels.All.Length);
            Assert.Equal(new[] { SkillLevel.Novice, SkillLevel.Skilled, SkillLevel.Veteran, SkillLevel.Ace }, SkillLevels.All);
        }

        [Fact]
        public void EnemySpeedRisesWithSkill()
        {
            for (int i = 1; i < SkillLevels.All.Length; i++)
                Assert.True(SkillLevels.EnemySpeed(SkillLevels.All[i]) > SkillLevels.EnemySpeed(SkillLevels.All[i - 1]));
        }

        [Fact]
        public void EnemyFireRateRisesWithSkill()
        {
            for (int i = 1; i < SkillLevels.All.Length; i++)
                Assert.True(SkillLevels.EnemyFireRate(SkillLevels.All[i]) > SkillLevels.EnemyFireRate(SkillLevels.All[i - 1]));
        }

        [Fact]
        public void ScoreMultiplierRisesWithSkill()
        {
            for (int i = 1; i < SkillLevels.All.Length; i++)
                Assert.True(SkillLevels.ScoreMultiplier(SkillLevels.All[i]) > SkillLevels.ScoreMultiplier(SkillLevels.All[i - 1]));
        }

        [Fact]
        public void NoviceIsTheGentlestAndAceTheHarshest()
        {
            Assert.True(SkillLevels.EnemySpeed(SkillLevel.Ace) > 2 * SkillLevels.EnemySpeed(SkillLevel.Novice));
            Assert.Equal(1.0, SkillLevels.ScoreMultiplier(SkillLevel.Novice));
            Assert.Equal(2.0, SkillLevels.ScoreMultiplier(SkillLevel.Ace));
        }

        [Fact]
        public void NoviceGetsAnExtraLife()
        {
            Assert.Equal(4, SkillLevels.StartingLives(SkillLevel.Novice));
            Assert.Equal(3, SkillLevels.StartingLives(SkillLevel.Skilled));
            Assert.Equal(3, SkillLevels.StartingLives(SkillLevel.Veteran));
            Assert.Equal(3, SkillLevels.StartingLives(SkillLevel.Ace));
        }

        [Fact]
        public void ExtraEnemiesRiseWithSkill()
        {
            Assert.Equal(0, SkillLevels.ExtraStartingEnemies(SkillLevel.Novice));
            Assert.True(SkillLevels.ExtraStartingEnemies(SkillLevel.Ace) > SkillLevels.ExtraStartingEnemies(SkillLevel.Veteran));
        }

        [Theory]
        [InlineData(SkillLevel.Novice, "NOVICE", "NOV")]
        [InlineData(SkillLevel.Skilled, "SKILLED", "SKL")]
        [InlineData(SkillLevel.Veteran, "VETERAN", "VET")]
        [InlineData(SkillLevel.Ace, "ACE", "ACE")]
        public void NamesAndAbbreviationsAreStable(SkillLevel level, string name, string abbreviation)
        {
            Assert.Equal(name, SkillLevels.Name(level));
            Assert.Equal(abbreviation, SkillLevels.Abbreviation(level));
            Assert.False(string.IsNullOrWhiteSpace(SkillLevels.Description(level)));
        }

        [Theory]
        [InlineData("NOVICE", SkillLevel.Novice)]
        [InlineData("nov", SkillLevel.Novice)]
        [InlineData("Skilled", SkillLevel.Skilled)]
        [InlineData("VET", SkillLevel.Veteran)]
        [InlineData("ace", SkillLevel.Ace)]
        [InlineData(" rubbish ", SkillLevel.Skilled)]
        [InlineData(null, SkillLevel.Skilled)]
        public void ParseUnderstandsNamesAndAbbreviations(string text, SkillLevel expected)
        {
            Assert.Equal(expected, SkillLevels.Parse(text));
        }

        [Fact]
        public void ClampKeepsValuesInRange()
        {
            Assert.Equal(SkillLevel.Novice, SkillLevels.Clamp((SkillLevel)(-4)));
            Assert.Equal(SkillLevel.Ace, SkillLevels.Clamp((SkillLevel)19));
            Assert.Equal(SkillLevel.Veteran, SkillLevels.Clamp(SkillLevel.Veteran));
        }
    }
}
