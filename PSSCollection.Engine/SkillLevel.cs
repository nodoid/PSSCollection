using System;

namespace PSSCollection.Engine
{
    /// <summary>The four skill levels every game offers.</summary>
    public enum SkillLevel
    {
        Novice = 0,
        Skilled = 1,
        Veteran = 2,
        Ace = 3
    }

    /// <summary>
    /// Per-skill tuning. Every game reads its difficulty from here so the levels stay
    /// consistent across the collection, and the values grow monotonically with skill.
    /// </summary>
    public static class SkillLevels
    {
        public static readonly SkillLevel[] All =
        {
            SkillLevel.Novice, SkillLevel.Skilled, SkillLevel.Veteran, SkillLevel.Ace
        };

        public static SkillLevel Clamp(SkillLevel level)
        {
            if (level < SkillLevel.Novice)
                return SkillLevel.Novice;
            if (level > SkillLevel.Ace)
                return SkillLevel.Ace;
            return level;
        }

        public static string Name(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return "NOVICE";
                case SkillLevel.Skilled: return "SKILLED";
                case SkillLevel.Veteran: return "VETERAN";
                default: return "ACE";
            }
        }

        public static string Description(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return "FOR THE NEW PILOT. SLOW ENEMIES, GENEROUS LIVES.";
                case SkillLevel.Skilled: return "THE ORIGINAL CHALLENGE, AS IT WAS IN 1983.";
                case SkillLevel.Veteran: return "FASTER ENEMIES, HEAVIER FIRE, SCORES 1.5x.";
                default: return "NO MERCY. EVERYTHING IS FASTER AND SCORES DOUBLE.";
            }
        }

        /// <summary>Multiplier applied to enemy movement speeds.</summary>
        public static double EnemySpeed(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return 0.75;
                case SkillLevel.Skilled: return 1.0;
                case SkillLevel.Veteran: return 1.3;
                default: return 1.6;
            }
        }

        /// <summary>Multiplier applied to how often enemies shoot (higher = more shots).</summary>
        public static double EnemyFireRate(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return 0.55;
                case SkillLevel.Skilled: return 1.0;
                case SkillLevel.Veteran: return 1.45;
                default: return 2.0;
            }
        }

        /// <summary>Multiplier applied to every point the player earns.</summary>
        public static double ScoreMultiplier(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return 1.0;
                case SkillLevel.Skilled: return 1.25;
                case SkillLevel.Veteran: return 1.5;
                default: return 2.0;
            }
        }

        public static int StartingLives(SkillLevel level) => level == SkillLevel.Novice ? 4 : 3;

        /// <summary>Extra enemies a game adds at the start for tougher skills.</summary>
        public static int ExtraStartingEnemies(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return 0;
                case SkillLevel.Skilled: return 1;
                case SkillLevel.Veteran: return 2;
                default: return 3;
            }
        }

        /// <summary>Short tag shown on the high-score table.</summary>
        public static string Abbreviation(SkillLevel level)
        {
            switch (Clamp(level))
            {
                case SkillLevel.Novice: return "NOV";
                case SkillLevel.Skilled: return "SKL";
                case SkillLevel.Veteran: return "VET";
                default: return "ACE";
            }
        }

        public static SkillLevel Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return SkillLevel.Skilled;

            foreach (SkillLevel level in All)
            {
                if (string.Equals(Name(level), text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Abbreviation(level), text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(level.ToString(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                    return level;
            }

            return SkillLevel.Skilled;
        }
    }
}
