using System;
using System.Collections.Generic;

namespace PSSCollection.Engine.HighScores
{
    /// <summary>Where the scores live between runs.</summary>
    public interface IScoreStore
    {
        /// <summary>True when something has been saved before.</summary>
        bool Exists { get; }

        /// <summary>Human readable location, shown on the high-score screen.</summary>
        string Location { get; }

        /// <summary>The saved JSON, or null when nothing has been saved yet.</summary>
        string Read();

        /// <summary>Replaces the saved JSON.</summary>
        void Write(string json);

        /// <summary>Discards the saved scores.</summary>
        void Delete();
    }

    /// <summary>
    /// Every game's table plus the submission rules. Saving and loading goes through
    /// an <see cref="IScoreStore"/> so the same code runs on every platform.
    /// </summary>
    public sealed class HighScoreBook
    {
        private readonly Dictionary<GameId, ScoreTable> _tables = new Dictionary<GameId, ScoreTable>();

        public HighScoreBook()
        {
            foreach (GameId id in Enum.GetValues<GameId>())
                _tables[id] = new ScoreTable();
        }

        public ScoreTable For(GameId id)
        {
            if (!_tables.TryGetValue(id, out ScoreTable table))
            {
                table = new ScoreTable();
                _tables[id] = table;
            }

            return table;
        }

        public bool TryGetTable(GameId id, out ScoreTable table) => _tables.TryGetValue(id, out table);

        public IReadOnlyList<ScoreEntry> Top(GameId id, int max, SkillLevel? skill = null) =>
            For(id).Top(max, skill);

        public int Best(GameId id, SkillLevel? skill = null) => For(id).Best(skill);

        public bool Qualifies(GameId id, int score) => For(id).Qualifies(score);

        /// <summary>
        /// Submits a finished run. Returns its rank, or -1 when it did not make the table.
        /// </summary>
        public int Submit(GameId id, string name, int score, SkillLevel skill, int level, DateTimeOffset achievedUtc)
        {
            if (score <= 0)
                return -1;

            var entry = new ScoreEntry(name, score, skill, level, achievedUtc);
            return For(id).Add(entry);
        }

        public int Submit(GameId id, string name, int score, SkillLevel skill, int level) =>
            Submit(id, name, score, skill, level, DateTimeOffset.UtcNow);

        public void Clear(GameId id) => For(id).Clear();

        public void ClearAll()
        {
            foreach (ScoreTable table in _tables.Values)
                table.Clear();
        }

        internal void Normalise()
        {
            foreach (ScoreTable table in _tables.Values)
                table.Normalise();
        }

        /// <summary>Reads the store, returning an empty book when there is nothing readable.</summary>
        public static HighScoreBook Load(IScoreStore store, out string warning)
        {
            warning = null;
            var book = new HighScoreBook();

            if (store == null)
                return book;

            string json;
            try
            {
                json = store.Read();
            }
            catch (Exception e)
            {
                warning = "SCORES COULD NOT BE READ: " + e.Message;
                return book;
            }

            if (string.IsNullOrWhiteSpace(json))
                return book;

            if (!ScoreJson.TryDeserialize(json, book, out string error))
            {
                warning = "HIGH SCORES COULD NOT BE READ (" + error + "). A NEW TABLE WAS STARTED.";
                return new HighScoreBook();
            }

            return book;
        }

        public static HighScoreBook Load(IScoreStore store) => Load(store, out _);

        /// <summary>Saves the book. Returns false and an error message when the write failed.</summary>
        public bool TrySave(IScoreStore store, out string error)
        {
            error = null;
            if (store == null)
                return false;

            try
            {
                store.Write(ScoreJson.Serialize(this));
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public bool TrySave(IScoreStore store) => TrySave(store, out _);

        public void Save(IScoreStore store)
        {
            if (!TrySave(store, out string error))
                throw new InvalidOperationException("Could not save the high scores: " + error);
        }
    }
}
