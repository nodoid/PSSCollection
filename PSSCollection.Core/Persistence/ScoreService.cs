using System;
using System.Collections.Generic;
using System.IO;
using PSSCollection.Core.Platform;
using PSSCollection.Engine;
using PSSCollection.Engine.HighScores;

namespace PSSCollection.Core.Persistence
{
    /// <summary>
    /// Loads the score tables at start-up and writes them back after every change, so the
    /// tables survive closing and reopening the app.
    /// </summary>
    public sealed class ScoreService
    {
        private readonly IScoreStore _store;

        public ScoreService(IPlatformServices platform)
        {
            string path = null;
            try
            {
                path = Path.Combine(platform.DataDirectory, "highscores.json");
                _store = new JsonScoreStore(path);
            }
            catch (Exception)
            {
                _store = new InMemoryScoreStore();
            }

            Book = HighScoreBook.Load(_store, out string warning);
            Warning = warning;

            // Prove the folder is writable straight away; if it is not, keep going in memory.
            try
            {
                Directory.CreateDirectory(platform.DataDirectory);
            }
            catch (Exception)
            {
                _store = new InMemoryScoreStore();
            }
        }

        public HighScoreBook Book { get; }

        public string Location => _store.Location;

        /// <summary>Just the folder and file, for the footer - the full path is far too wide.</summary>
        public string ShortLocation
        {
            get
            {
                string location = Location;
                if (string.IsNullOrEmpty(location))
                    return "SAVED BETWEEN SESSIONS";

                string folder = Path.GetFileName(Path.GetDirectoryName(location));
                string file = Path.GetFileName(location);
                return string.IsNullOrEmpty(folder) ? file : folder + "/" + file;
            }
        }

        /// <summary>Set when a saved file could not be read, for the high-score screen to show.</summary>
        public string Warning { get; private set; }

        /// <summary>Submits a finished run and saves immediately. Returns the rank, or -1.</summary>
        public int Submit(GameId id, string name, int score, SkillLevel skill, int level)
        {
            int rank = Book.Submit(id, name, score, skill, level, DateTimeOffset.Now);
            Save();
            return rank;
        }

        public void Clear(GameId id)
        {
            Book.Clear(id);
            Save();
        }

        public void ClearAll()
        {
            Book.ClearAll();
            Save();
        }

        public int Best(GameId id) => Book.Best(id);

        public int Best(GameId id, SkillLevel skill) => Book.Best(id, skill);

        public IReadOnlyList<ScoreEntry> Top(GameId id, int max, SkillLevel? skill = null) =>
            Book.Top(id, max, skill);

        private void Save()
        {
            if (!Book.TrySave(_store, out string error))
                Warning = "SCORES COULD NOT BE SAVED: " + error;
        }
    }
}
