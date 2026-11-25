using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PSSCollection.Engine.HighScores
{
    /// <summary>Versioned JSON for the high-score file.</summary>
    public static class ScoreJson
    {
        public const int SchemaVersion = 1;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static string Serialize(HighScoreBook book)
        {
            var document = new ScoreDocument { Version = SchemaVersion };

            foreach (GameId id in Enum.GetValues<GameId>())
            {
                var tableDto = new TableDto();
                foreach (ScoreEntry entry in book.For(id).Entries)
                {
                    tableDto.Entries.Add(new EntryDto
                    {
                        Name = entry.Name,
                        Score = entry.Score,
                        Skill = SkillLevels.Abbreviation(entry.Skill),
                        Level = entry.Level,
                        AchievedUtc = entry.AchievedUtc.UtcDateTime.ToString("o")
                    });
                }

                document.Games[id.ToString()] = tableDto;
            }

            return JsonSerializer.Serialize(document, Options);
        }

        /// <summary>
        /// Parses a score file. Unknown games and malformed rows are skipped rather than
        /// thrown away wholesale, so a newer build's file still loads.
        /// </summary>
        public static bool TryDeserialize(string json, HighScoreBook book, out string error)
        {
            error = null;
            book ??= new HighScoreBook();

            if (string.IsNullOrWhiteSpace(json))
                return true;

            ScoreDocument document;
            try
            {
                document = JsonSerializer.Deserialize<ScoreDocument>(json, Options);
            }
            catch (JsonException e)
            {
                error = e.Message;
                return false;
            }

            if (document == null)
            {
                error = "EMPTY FILE";
                return false;
            }

            if (document.Version > SchemaVersion)
            {
                // Written by a newer build: read what we understand rather than losing it.
                error = "NEWER FILE VERSION " + document.Version;
            }

            if (document.Games == null)
                return true;

            foreach (KeyValuePair<string, TableDto> pair in document.Games)
            {
                if (!Enum.TryParse(pair.Key, true, out GameId id) || !Enum.IsDefined(id))
                    continue;
                if (pair.Value?.Entries == null)
                    continue;

                ScoreTable table = book.For(id);
                table.Clear();

                foreach (EntryDto entry in pair.Value.Entries)
                {
                    if (entry == null || entry.Score <= 0)
                        continue;

                    DateTimeOffset when = DateTimeOffset.UtcNow;
                    if (!string.IsNullOrWhiteSpace(entry.AchievedUtc))
                        DateTimeOffset.TryParse(entry.AchievedUtc, out when);

                    table.Add(new ScoreEntry(
                        entry.Name,
                        entry.Score,
                        SkillLevels.Parse(entry.Skill),
                        entry.Level,
                        when));
                }

                table.Normalise();
            }

            return true;
        }

        public static HighScoreBook Deserialize(string json)
        {
            var book = new HighScoreBook();
            if (!TryDeserialize(json, book, out string error))
                throw new FormatException("The high-score file could not be read: " + error);
            return book;
        }

        private sealed class ScoreDocument
        {
            [JsonPropertyName("version")]
            public int Version { get; set; } = SchemaVersion;

            [JsonPropertyName("games")]
            public Dictionary<string, TableDto> Games { get; set; } = new Dictionary<string, TableDto>();
        }

        private sealed class TableDto
        {
            [JsonPropertyName("entries")]
            public List<EntryDto> Entries { get; set; } = new List<EntryDto>();
        }

        private sealed class EntryDto
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("score")]
            public int Score { get; set; }

            [JsonPropertyName("skill")]
            public string Skill { get; set; }

            [JsonPropertyName("level")]
            public int Level { get; set; }

            [JsonPropertyName("achievedUtc")]
            public string AchievedUtc { get; set; }
        }
    }
}
