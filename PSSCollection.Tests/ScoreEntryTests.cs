using System;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;
using Xunit;

namespace PSSCollection.Tests
{
    public class ScoreEntryTests
    {
        [Theory]
        [InlineData(null, "AAA")]
        [InlineData("", "AAA")]
        [InlineData("   ", "AAA")]
        [InlineData("pfj", "PFJ")]
        [InlineData("a", "AAA")]
        [InlineData("ab", "ABA")]
        [InlineData("a1b2c3", "A1B")]
        [InlineData("!!", "AAA")]
        [InlineData("  john  ", "JOH")]
        [InlineData("123", "123")]
        public void NamesAreSanitisedToThreeArcadeInitials(string input, string expected)
        {
            Assert.Equal(expected, ScoreEntry.SanitiseName(input));
        }

        [Fact]
        public void NegativeScoresAreClampedToZero()
        {
            var entry = new ScoreEntry("PFJ", -100, SkillLevel.Ace, 4, DateTimeOffset.UtcNow);
            Assert.Equal(0, entry.Score);
        }

        [Fact]
        public void SkillIsClampedAndLevelIsAtLeastOne()
        {
            var entry = new ScoreEntry("PFJ", 10, (SkillLevel)42, 0, DateTimeOffset.UtcNow);

            Assert.Equal(SkillLevel.Ace, entry.Skill);
            Assert.Equal(1, entry.Level);
        }

        [Fact]
        public void DefaultEntryIsUsable()
        {
            var entry = new ScoreEntry();

            Assert.Equal("AAA", entry.Name);
            Assert.Equal(0, entry.Score);
            Assert.Equal(SkillLevel.Novice, entry.Skill);
            Assert.True(entry.AchievedUtc > DateTimeOffset.MinValue);
        }

        [Fact]
        public void CopyPreservesEverything()
        {
            var when = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
            var entry = new ScoreEntry("ABC", 1234, SkillLevel.Veteran, 7, when);

            ScoreEntry copy = entry.Copy();

            Assert.Equal(entry.Name, copy.Name);
            Assert.Equal(entry.Score, copy.Score);
            Assert.Equal(entry.Skill, copy.Skill);
            Assert.Equal(entry.Level, copy.Level);
            Assert.Equal(entry.AchievedUtc, copy.AchievedUtc);
            Assert.NotSame(entry, copy);
        }

        [Fact]
        public void ToStringShowsInitialsScoreAndSkill()
        {
            var entry = new ScoreEntry("PFJ", 900, SkillLevel.Ace, 3, DateTimeOffset.UtcNow);
            Assert.Equal("PFJ 900 (ACE)", entry.ToString());
        }
    }
}
