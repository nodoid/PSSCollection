using System;
using System.Globalization;

namespace PSSCollection.Engine.HighScores
{
    /// <summary>One row of a high-score table.</summary>
    public sealed class ScoreEntry
    {
        public const int MaxNameLength = 3;

        public ScoreEntry()
        {
            Name = "AAA";
            AchievedUtc = DateTimeOffset.UtcNow;
        }

        public ScoreEntry(string name, int score, SkillLevel skill, int level, DateTimeOffset achievedUtc)
        {
            Name = SanitiseName(name);
            Score = Math.Max(0, score);
            Skill = SkillLevels.Clamp(skill);
            Level = Math.Max(1, level);
            AchievedUtc = achievedUtc;
        }

        public string Name { get; set; }

        public int Score { get; set; }

        public SkillLevel Skill { get; set; }

        /// <summary>The wave / level the run ended on.</summary>
        public int Level { get; set; }

        public DateTimeOffset AchievedUtc { get; set; }

        /// <summary>Arcade initials: letters and digits only, three characters, upper case.</summary>
        public static string SanitiseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "AAA";

            var builder = new System.Text.StringBuilder(MaxNameLength);
            foreach (char c in name.Trim().ToUpperInvariant())
            {
                if (char.IsLetterOrDigit(c))
                    builder.Append(c);
                if (builder.Length == MaxNameLength)
                    break;
            }

            if (builder.Length == 0)
                return "AAA";

            while (builder.Length < MaxNameLength)
                builder.Append('A');

            return builder.ToString();
        }

        public string ScoreText => Score.ToString(CultureInfo.InvariantCulture);

        public ScoreEntry Copy() => new ScoreEntry(Name, Score, Skill, Level, AchievedUtc);

        public override string ToString() => $"{Name} {Score} ({SkillLevels.Abbreviation(Skill)})";
    }
}
