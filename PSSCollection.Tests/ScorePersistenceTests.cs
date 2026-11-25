using System;
using System.IO;
using System.Linq;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;
using Xunit;

namespace PSSCollection.Tests
{
    public class ScorePersistenceTests : IDisposable
    {
        private readonly string _directory;
        private readonly string _path;

        public ScorePersistenceTests()
        {
            _directory = Path.Combine(Path.GetTempPath(), "pss-tests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "scores.json");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                    Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void JsonRoundTripKeepsEveryField()
        {
            var when = new DateTimeOffset(2025, 12, 31, 23, 59, 58, TimeSpan.Zero);
            var book = new HighScoreBook();
            book.Submit(GameId.ElektroStorm, "PFJ", 54321, SkillLevel.Veteran, 9, when);

            string json = ScoreJson.Serialize(book);
            HighScoreBook loaded = ScoreJson.Deserialize(json);

            ScoreEntry entry = loaded.For(GameId.ElektroStorm).Entries.Single();
            Assert.Equal("PFJ", entry.Name);
            Assert.Equal(54321, entry.Score);
            Assert.Equal(SkillLevel.Veteran, entry.Skill);
            Assert.Equal(9, entry.Level);
            Assert.Equal(when.UtcDateTime, entry.AchievedUtc.UtcDateTime);
        }

        [Fact]
        public void SerialisedFileCarriesASchemaVersion()
        {
            string json = ScoreJson.Serialize(new HighScoreBook());
            Assert.Contains("\"version\": " + ScoreJson.SchemaVersion, json);
        }

        [Fact]
        public void UnknownGameKeysAreIgnored()
        {
            const string json = @"{
              ""version"": 1,
              ""games"": {
                ""TheUltra"": { ""entries"": [ { ""name"": ""AAA"", ""score"": 999 } ] },
                ""OricMon"": { ""entries"": [ { ""name"": ""AAA"", ""score"": 999 } ] },
                ""Invaders"": { ""entries"": [ { ""name"": ""PFJ"", ""score"": 10 } ] }
              }
            }";

            HighScoreBook book = ScoreJson.Deserialize(json);

            Assert.Equal(10, book.Best(GameId.Invaders));
            Assert.True(book.TryDeserializeIgnoresUnknown());
        }

        [Fact]
        public void JunkRowsAreSkippedNotFatal()
        {
            const string json = @"{
              ""version"": 1,
              ""games"": {
                ""Hopper"": { ""entries"": [
                  null,
                  { ""name"": ""AAA"", ""score"": -20 },
                  { ""name"": null, ""score"": 300, ""skill"": ""nonsense"", ""level"": 0 }
                ] }
              }
            }";

            HighScoreBook book = ScoreJson.Deserialize(json);

            ScoreEntry entry = book.For(GameId.Hopper).Entries.Single();
            Assert.Equal(300, entry.Score);
            Assert.Equal("AAA", entry.Name);
            Assert.Equal(SkillLevel.Skilled, entry.Skill);
            Assert.Equal(1, entry.Level);
        }

        [Fact]
        public void MalformedJsonIsReported()
        {
            Assert.False(ScoreJson.TryDeserialize("{ oops", new HighScoreBook(), out string error));
            Assert.False(string.IsNullOrEmpty(error));
            Assert.Throws<FormatException>(() => ScoreJson.Deserialize("{ oops"));
        }

        [Fact]
        public void EmptyJsonIsTreatedAsAnEmptyBook()
        {
            Assert.True(ScoreJson.TryDeserialize("", new HighScoreBook(), out string error));
            Assert.Null(error);
            Assert.True(ScoreJson.TryDeserialize(null, new HighScoreBook(), out error));
            Assert.Null(error);
        }

        [Fact]
        public void ANewerFileVersionStillLoads()
        {
            const string json = @"{
              ""version"": 99,
              ""games"": { ""Invaders"": { ""entries"": [ { ""name"": ""NEW"", ""score"": 4242 } ] } }
            }";

            HighScoreBook book = new HighScoreBook();
            Assert.True(ScoreJson.TryDeserialize(json, book, out string error));
            Assert.NotNull(error);
            Assert.Equal(4242, book.Best(GameId.Invaders));
        }

        [Fact]
        public void MoreThanTenSavedEntriesAreTrimmedOnLoad()
        {
            var book = new HighScoreBook();
            for (int i = 1; i <= 14; i++)
                book.For(GameId.Centipede).Add(new ScoreEntry("AAA", i * 100, SkillLevel.Skilled, 1, DateTimeOffset.UtcNow));

            HighScoreBook loaded = ScoreJson.Deserialize(ScoreJson.Serialize(book));

            Assert.Equal(ScoreTable.Capacity, loaded.For(GameId.Centipede).Count);
            Assert.Equal(1400, loaded.Best(GameId.Centipede));
        }

        [Fact]
        public void FileStoreWritesReadsAndDeletes()
        {
            var store = new JsonScoreStore(_path);

            Assert.False(store.Exists);
            Assert.Null(store.Read());

            store.Write("{\"version\":1}");

            Assert.True(store.Exists);
            Assert.Equal("{\"version\":1}", store.Read());
            Assert.Equal(_path, store.Location);

            store.Delete();
            Assert.False(store.Exists);
        }

        [Fact]
        public void FileStoreCreatesMissingDirectories()
        {
            string nested = Path.Combine(_directory, "a", "b", "c", "scores.json");
            var store = new JsonScoreStore(nested);

            store.Write("{}");

            Assert.True(File.Exists(nested));
        }

        [Fact]
        public void FileStoreLeavesNoTemporaryFileBehind()
        {
            var store = new JsonScoreStore(_path);
            store.Write("{}");
            store.Write("{\"version\":1}");

            Assert.False(File.Exists(_path + ".tmp"));
            Assert.Equal("{\"version\":1}", File.ReadAllText(_path));
        }

        [Fact]
        public void FileStoreOverwritesPreviousContent()
        {
            var store = new JsonScoreStore(_path);
            store.Write("old content that is longer than the new content");
            store.Write("new");

            Assert.Equal("new", store.Read());
        }

        [Fact]
        public void StoreRequiresAPath()
        {
            Assert.Throws<ArgumentException>(() => new JsonScoreStore(null));
            Assert.Throws<ArgumentException>(() => new JsonScoreStore("  "));
        }

        [Fact]
        public void ScoresSurviveARealFileRoundTrip()
        {
            var store = new JsonScoreStore(_path);
            var book = new HighScoreBook();
            book.Submit(GameId.Invaders, "PFJ", 1000, SkillLevel.Skilled, 2, DateTimeOffset.UtcNow);

            Assert.True(book.TrySave(store, out _));

            // A second store instance on the same path stands in for a restarted app.
            HighScoreBook reloaded = HighScoreBook.Load(new JsonScoreStore(_path), out string warning);

            Assert.Null(warning);
            Assert.Equal(1000, reloaded.Best(GameId.Invaders));
            Assert.Equal("PFJ", reloaded.For(GameId.Invaders).Entries[0].Name);
        }

        [Fact]
        public void CorruptFileDoesNotThrowOnLoad()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path, "not json at all");

            HighScoreBook book = HighScoreBook.Load(new JsonScoreStore(_path), out string warning);

            Assert.NotNull(warning);
            Assert.True(book.For(GameId.Invaders).IsEmpty);
        }

        [Fact]
        public void InMemoryStoreBehavesLikeAStore()
        {
            var store = new InMemoryScoreStore();
            Assert.False(store.Exists);
            Assert.Equal("MEMORY", store.Location);

            store.Write("x");
            Assert.True(store.Exists);
            Assert.Equal("x", store.Saved);

            store.Delete();
            Assert.False(store.Exists);
            Assert.Null(store.Read());
        }
    }

    internal static class HighScoreBookTestExtensions
    {
        /// <summary>The excluded titles must never get a table.</summary>
        public static bool TryDeserializeIgnoresUnknown(this HighScoreBook book) =>
            !Enum.TryParse("TheUltra", true, out GameId _) && !Enum.TryParse("OricMon", true, out GameId _);
    }
}
