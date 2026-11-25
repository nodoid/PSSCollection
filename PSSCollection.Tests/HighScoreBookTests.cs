using System;
using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;
using Xunit;

namespace PSSCollection.Tests
{
    public class HighScoreBookTests
    {
        private static readonly DateTimeOffset When = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

        [Fact]
        public void EveryGameStartsWithAnEmptyTable()
        {
            var book = new HighScoreBook();

            foreach (GameId id in Enum.GetValues<GameId>())
            {
                Assert.True(book.For(id).IsEmpty);
                Assert.Equal(0, book.Best(id));
            }
        }

        [Fact]
        public void SubmitAddsTheEntryAndReturnsItsRank()
        {
            var book = new HighScoreBook();

            int rank = book.Submit(GameId.Invaders, "PFJ", 4200, SkillLevel.Veteran, 3, When);

            Assert.Equal(0, rank);
            ScoreEntry entry = book.For(GameId.Invaders).Entries[0];
            Assert.Equal("PFJ", entry.Name);
            Assert.Equal(4200, entry.Score);
            Assert.Equal(SkillLevel.Veteran, entry.Skill);
            Assert.Equal(3, entry.Level);
            Assert.Equal(When, entry.AchievedUtc);
        }

        [Fact]
        public void TablesAreIsolatedPerGame()
        {
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "AAA", 100, SkillLevel.Skilled, 1, When);
            book.Submit(GameId.Hopper, "BBB", 900, SkillLevel.Ace, 2, When);

            Assert.Single(book.For(GameId.Invaders).Entries);
            Assert.Single(book.For(GameId.Hopper).Entries);
            Assert.Empty(book.For(GameId.Centipede).Entries);
            Assert.Equal(100, book.Best(GameId.Invaders));
            Assert.Equal(900, book.Best(GameId.Hopper));
            Assert.Equal(0, book.Best(GameId.ElektroStorm));
        }

        [Fact]
        public void ZeroScoreIsNotSubmitted()
        {
            var book = new HighScoreBook();

            Assert.Equal(-1, book.Submit(GameId.Invaders, "PFJ", 0, SkillLevel.Ace, 1, When));
            Assert.True(book.For(GameId.Invaders).IsEmpty);
        }

        [Fact]
        public void NonQualifyingScoreReturnsMinusOne()
        {
            var book = new HighScoreBook();
            for (int i = 0; i < ScoreTable.Capacity; i++)
                book.Submit(GameId.Centipede, "AAA", 1000 + i, SkillLevel.Skilled, 1, When);

            Assert.Equal(-1, book.Submit(GameId.Centipede, "BBB", 5, SkillLevel.Novice, 1, When));
            Assert.Equal(ScoreTable.Capacity, book.For(GameId.Centipede).Count);
        }

        [Fact]
        public void BestCanBeFilteredBySkill()
        {
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "NOV", 500, SkillLevel.Novice, 1, When);
            book.Submit(GameId.Invaders, "ACE", 9000, SkillLevel.Ace, 4, When);

            Assert.Equal(9000, book.Best(GameId.Invaders));
            Assert.Equal(500, book.Best(GameId.Invaders, SkillLevel.Novice));
            Assert.Equal(0, book.Best(GameId.Invaders, SkillLevel.Veteran));
        }

        [Fact]
        public void TopRespectsCountAndSkillFilter()
        {
            var book = new HighScoreBook();
            for (int i = 1; i <= 8; i++)
                book.Submit(GameId.ElektroStorm, "AAA", i * 100, i % 2 == 0 ? SkillLevel.Ace : SkillLevel.Skilled, 1, When);

            Assert.Equal(5, book.Top(GameId.ElektroStorm, 5).Count);
            Assert.Equal(4, book.Top(GameId.ElektroStorm, 20, SkillLevel.Ace).Count);
            Assert.Equal(800, book.Top(GameId.ElektroStorm, 1)[0].Score);
        }

        [Fact]
        public void QualifiesMatchesTheTable()
        {
            var book = new HighScoreBook();
            Assert.True(book.Qualifies(GameId.Invaders, 1));
            Assert.False(book.Qualifies(GameId.Invaders, 0));
        }

        [Fact]
        public void ClearOnlyEmptiesOneGame()
        {
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "AAA", 100, SkillLevel.Skilled, 1, When);
            book.Submit(GameId.Hopper, "BBB", 200, SkillLevel.Skilled, 1, When);

            book.Clear(GameId.Invaders);

            Assert.True(book.For(GameId.Invaders).IsEmpty);
            Assert.Single(book.For(GameId.Hopper).Entries);
        }

        [Fact]
        public void ClearAllEmptiesEverything()
        {
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "AAA", 100, SkillLevel.Skilled, 1, When);
            book.Submit(GameId.Hopper, "BBB", 200, SkillLevel.Skilled, 1, When);

            book.ClearAll();

            Assert.All(Enum.GetValues<GameId>(), id => Assert.True(book.For(id).IsEmpty));
        }

        [Fact]
        public void TryGetTableReportsMissingGames()
        {
            var book = new HighScoreBook();
            book.For(GameId.Invaders);

            Assert.True(book.TryGetTable(GameId.Invaders, out ScoreTable table));
            Assert.NotNull(table);
            Assert.False(book.TryGetTable((GameId)77, out _));
        }

        [Fact]
        public void ScoresSurviveSaveAndLoad()
        {
            // This is the "scores are still there after a restart" guarantee.
            var store = new InMemoryScoreStore();
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "PFJ", 12345, SkillLevel.Ace, 6, When);
            book.Submit(GameId.Hopper, "ABC", 6789, SkillLevel.Novice, 2, When);

            Assert.True(book.TrySave(store, out string error), error);

            HighScoreBook reloaded = HighScoreBook.Load(store, out string warning);

            Assert.Null(warning);
            Assert.Equal(12345, reloaded.Best(GameId.Invaders));
            Assert.Equal("PFJ", reloaded.For(GameId.Invaders).Entries[0].Name);
            Assert.Equal(SkillLevel.Ace, reloaded.For(GameId.Invaders).Entries[0].Skill);
            Assert.Equal(6, reloaded.For(GameId.Invaders).Entries[0].Level);
            Assert.Equal(6789, reloaded.Best(GameId.Hopper));
        }

        [Fact]
        public void LoadingACorruptFileStartsANewBookWithAWarning()
        {
            var store = new InMemoryScoreStore("{ this is not json ");

            HighScoreBook book = HighScoreBook.Load(store, out string warning);

            Assert.NotNull(warning);
            Assert.True(book.For(GameId.Invaders).IsEmpty);
        }

        [Fact]
        public void LoadingAnEmptyStoreIsSilent()
        {
            var store = new InMemoryScoreStore();
            HighScoreBook book = HighScoreBook.Load(store, out string warning);

            Assert.Null(warning);
            Assert.True(book.For(GameId.Centipede).IsEmpty);
        }

        [Fact]
        public void TrySaveReportsAStoreFailure()
        {
            var book = new HighScoreBook();
            var store = new ThrowingStore();

            Assert.False(book.TrySave(store, out string error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.Throws<InvalidOperationException>(() => book.Save(store));
        }

        [Fact]
        public void TrySaveWithNoStoreFails()
        {
            var book = new HighScoreBook();
            Assert.False(book.TrySave(null, out _));
        }

        private sealed class ThrowingStore : IScoreStore
        {
            public bool Exists => true;

            public string Location => "NOWHERE";

            public string Read() => "{}";

            public void Write(string json) => throw new System.IO.IOException("disk full");

            public void Delete()
            {
            }
        }
    }
}
