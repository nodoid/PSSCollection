using System;
using System.Collections.Generic;

namespace PSSCollection.Engine.HighScores
{
    /// <summary>The best ten runs for one game, highest first.</summary>
    public sealed class ScoreTable
    {
        public const int Capacity = 10;

        private readonly List<ScoreEntry> _entries = new List<ScoreEntry>(Capacity);

        public ScoreTable()
        {
        }

        public ScoreTable(IEnumerable<ScoreEntry> entries)
        {
            if (entries != null)
            {
                foreach (ScoreEntry entry in entries)
                    Add(entry);
            }
        }

        public IReadOnlyList<ScoreEntry> Entries => _entries;

        public int Count => _entries.Count;

        public bool IsEmpty => _entries.Count == 0;

        public ScoreEntry this[int index] => _entries[index];

        /// <summary>The lowest score that would still make the table.</summary>
        public int LowestScore => _entries.Count == 0 ? 0 : _entries[_entries.Count - 1].Score;

        public int HighestScore => _entries.Count == 0 ? 0 : _entries[0].Score;

        /// <summary>True when this score earns a place. Zero never does.</summary>
        public bool Qualifies(int score)
        {
            if (score <= 0)
                return false;

            if (_entries.Count < Capacity)
                return true;

            return score > _entries[Capacity - 1].Score;
        }

        /// <summary>
        /// Inserts an entry and returns its zero-based rank, or -1 when it did not make the table.
        /// Equal scores are placed after the ones already there, so older runs keep their rank.
        /// </summary>
        public int Add(ScoreEntry entry)
        {
            if (entry == null || entry.Score <= 0)
                return -1;

            int index = 0;
            while (index < _entries.Count && _entries[index].Score >= entry.Score)
                index++;

            if (index >= Capacity)
                return -1;

            _entries.Insert(index, entry);

            while (_entries.Count > Capacity)
                _entries.RemoveAt(_entries.Count - 1);

            return index;
        }

        /// <summary>The rank a score would take, or -1 when it would not qualify.</summary>
        public int RankFor(int score)
        {
            if (!Qualifies(score))
                return -1;

            int index = 0;
            while (index < _entries.Count && _entries[index].Score > score)
                index++;

            return index;
        }

        public int Best(SkillLevel? skill = null)
        {
            foreach (ScoreEntry entry in _entries)
            {
                if (skill == null || entry.Skill == skill.Value)
                    return entry.Score;
            }

            return 0;
        }

        public IReadOnlyList<ScoreEntry> Top(int max, SkillLevel? skill = null)
        {
            var result = new List<ScoreEntry>();
            if (max <= 0)
                return result;

            foreach (ScoreEntry entry in _entries)
            {
                if (skill != null && entry.Skill != skill.Value)
                    continue;

                result.Add(entry);
                if (result.Count == max)
                    break;
            }

            return result;
        }

        public void Clear() => _entries.Clear();

        public ScoreTable Copy() => new ScoreTable(_entries);

        /// <summary>Re-sorts and trims, used after loading a file that may have been edited.</summary>
        internal void Normalise()
        {
            var sorted = new List<ScoreEntry>(_entries);
            sorted.Sort(Compare);

            _entries.Clear();
            foreach (ScoreEntry entry in sorted)
            {
                if (_entries.Count == Capacity)
                    break;
                if (entry == null || entry.Score <= 0)
                    continue;

                entry.Name = ScoreEntry.SanitiseName(entry.Name);
                entry.Skill = SkillLevels.Clamp(entry.Skill);
                if (entry.Level < 1)
                    entry.Level = 1;
                _entries.Add(entry);
            }
        }

        private static int Compare(ScoreEntry a, ScoreEntry b)
        {
            if (a == null)
                return b == null ? 0 : 1;
            if (b == null)
                return -1;

            int byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0)
                return byScore;

            return a.AchievedUtc.CompareTo(b.AchievedUtc);
        }
    }
}
