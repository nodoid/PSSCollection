using System;
using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;
using Xunit;

namespace PSSCollection.Tests
{
    public class ScoreTableTests
    {
        private static ScoreEntry Entry(int score, string name = "PFJ", SkillLevel skill = SkillLevel.Skilled, int seconds = 0) =>
            new ScoreEntry(name, score, skill, 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(seconds));

        [Fact]
        public void NewTableIsEmpty()
        {
            var table = new ScoreTable();

            Assert.True(table.IsEmpty);
            Assert.Equal(0, table.Count);
            Assert.Equal(0, table.HighestScore);
            Assert.Equal(0, table.LowestScore);
            Assert.Empty(table.Entries);
        }

        [Fact]
        public void EntriesAreKeptHighestFirst()
        {
            var table = new ScoreTable();
            table.Add(Entry(100));
            table.Add(Entry(500));
            table.Add(Entry(250));

            Assert.Equal(new[] { 500, 250, 100 }, table.Entries.Select(e => e.Score));
            Assert.Equal(500, table.HighestScore);
            Assert.Equal(100, table.LowestScore);
        }

        [Fact]
        public void TableIsCappedAtTenEntries()
        {
            var table = new ScoreTable();
            for (int i = 1; i <= 15; i++)
                table.Add(Entry(i * 100));

            Assert.Equal(ScoreTable.Capacity, table.Count);
            Assert.Equal(1500, table.HighestScore);
            Assert.Equal(600, table.LowestScore);
            Assert.DoesNotContain(table.Entries, e => e.Score < 600);
        }

        [Fact]
        public void AddReturnsTheRankOfTheNewEntry()
        {
            var table = new ScoreTable();
            table.Add(Entry(1000));
            table.Add(Entry(500));

            Assert.Equal(1, table.Add(Entry(750)));
            Assert.Equal(0, table.Add(Entry(2000)));
            Assert.Equal(new[] { 2000, 1000, 750, 500 }, table.Entries.Select(e => e.Score));
        }

        [Fact]
        public void EqualScoresGoBelowTheExistingEntry()
        {
            var table = new ScoreTable();
            table.Add(Entry(100, "AAA"));
            int rank = table.Add(Entry(100, "BBB"));

            Assert.Equal(1, rank);
            Assert.Equal("AAA", table.Entries[0].Name);
            Assert.Equal("BBB", table.Entries[1].Name);
        }

        [Fact]
        public void ZeroAndNegativeScoresNeverQualify()
        {
            var table = new ScoreTable();

            Assert.False(table.Qualifies(0));
            Assert.False(table.Qualifies(-5));
            Assert.Equal(-1, table.Add(Entry(0)));
            Assert.Equal(-1, table.Add(Entry(-1)));
            Assert.True(table.Qualifies(1));
        }

        [Fact]
        public void AFullTableRejectsScoresEqualToTheLowest()
        {
            var table = new ScoreTable();
            for (int i = 1; i <= ScoreTable.Capacity; i++)
                table.Add(Entry(i * 100));

            Assert.False(table.Qualifies(100));
            Assert.True(table.Qualifies(101));
            Assert.Equal(-1, table.Add(Entry(100)));
            Assert.Equal(-1, table.RankFor(100));
        }

        [Fact]
        public void RankForPredictsWhereAScoreWouldLand()
        {
            var table = new ScoreTable();
            table.Add(Entry(1000));
            table.Add(Entry(500));

            Assert.Equal(0, table.RankFor(2000));
            Assert.Equal(2, table.RankFor(400));
            Assert.Equal(-1, table.RankFor(0));
        }

        [Fact]
        public void BestCanBeFilteredBySkill()
        {
            var table = new ScoreTable();
            table.Add(Entry(5000, "ACE", SkillLevel.Ace));
            table.Add(Entry(3000, "VET", SkillLevel.Veteran));
            table.Add(Entry(1000, "NOV", SkillLevel.Novice));

            Assert.Equal(5000, table.Best());
            Assert.Equal(3000, table.Best(SkillLevel.Veteran));
            Assert.Equal(1000, table.Best(SkillLevel.Novice));
            Assert.Equal(0, table.Best(SkillLevel.Skilled));
        }

        [Fact]
        public void TopReturnsAtMostTheRequestedNumber()
        {
            var table = new ScoreTable();
            for (int i = 1; i <= 6; i++)
                table.Add(Entry(i * 100, skill: i % 2 == 0 ? SkillLevel.Ace : SkillLevel.Novice));

            Assert.Equal(3, table.Top(3).Count);
            Assert.Equal(6, table.Top(99).Count);
            Assert.Empty(table.Top(0));
            Assert.Equal(3, table.Top(99, SkillLevel.Ace).Count);
            Assert.All(table.Top(99, SkillLevel.Ace), e => Assert.Equal(SkillLevel.Ace, e.Skill));
        }

        [Fact]
        public void ClearEmptiesTheTable()
        {
            var table = new ScoreTable();
            table.Add(Entry(100));
            table.Clear();

            Assert.True(table.IsEmpty);
            Assert.Equal(0, table.HighestScore);
        }

        [Fact]
        public void CopyIsIndependent()
        {
            var table = new ScoreTable();
            table.Add(Entry(100));

            ScoreTable copy = table.Copy();
            copy.Add(Entry(500));

            Assert.Equal(1, table.Count);
            Assert.Equal(2, copy.Count);
        }

        [Fact]
        public void NormaliseSortsTrimsAndSanitises()
        {
            // Built out of order on purpose, including junk rows and bad names.
            var table = new ScoreTable();
            table.Add(new ScoreEntry("   ", 50, SkillLevel.Ace, 0, DateTimeOffset.UtcNow));
            table.Add(new ScoreEntry("abcdef", 900, (SkillLevel)99, -3, DateTimeOffset.UtcNow));
            table.Add(new ScoreEntry(null, 0, SkillLevel.Novice, 1, DateTimeOffset.UtcNow));
            table.Add(new ScoreEntry("zz", 400, SkillLevel.Veteran, 2, DateTimeOffset.UtcNow));

            table.Normalise();

            Assert.Equal(3, table.Count);
            Assert.Equal(900, table.Entries[0].Score);
            Assert.Equal("ABC", table.Entries[0].Name);
            Assert.Equal(SkillLevel.Ace, table.Entries[0].Skill);
            Assert.Equal(1, table.Entries[0].Level);
            Assert.Equal(400, table.Entries[1].Score);
            Assert.Equal("ZZA", table.Entries[1].Name);
            Assert.Equal(50, table.Entries[2].Score);
            Assert.Equal("AAA", table.Entries[2].Name);
        }

        [Fact]
        public void NormaliseCapsTheTable()
        {
            var entries = Enumerable.Range(1, 20)
                .Select(i => new ScoreEntry("AAA", i * 10, SkillLevel.Skilled, 1, DateTimeOffset.UtcNow));

            var table = new ScoreTable(entries);
            table.Normalise();

            Assert.Equal(ScoreTable.Capacity, table.Count);
            Assert.Equal(200, table.HighestScore);
        }

        [Fact]
        public void AddedEntryIsTheSameInstance()
        {
            var entry = Entry(42);
            var table = new ScoreTable();
            table.Add(entry);

            Assert.Same(entry, table[0]);
        }

        [Fact]
        public void AddNullIsIgnored()
        {
            var table = new ScoreTable();
            Assert.Equal(-1, table.Add(null));
            Assert.True(table.IsEmpty);
        }
    }
}
