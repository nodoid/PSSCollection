using System;
using System.Collections.Generic;

namespace PSSCollection.Engine
{
    /// <summary>The games in the collection. Every one is a Personal Software Services Oric release.</summary>
    public enum GameId
    {
        Invaders = 0,
        Centipede = 1,
        Hopper = 2,
        ElektroStorm = 3,
        LightCycle = 4
    }

    /// <summary>Everything the front-end needs to describe a game.</summary>
    public sealed class GameInfo
    {
        public GameInfo(GameId id, string title, string originalTitle, int year, string tagline,
            string blurb, string howToPlay, string controls)
        {
            Id = id;
            Title = title;
            OriginalTitle = originalTitle;
            Year = year;
            Tagline = tagline;
            Blurb = blurb;
            HowToPlay = howToPlay;
            Controls = controls;
        }

        public GameId Id { get; }

        /// <summary>Title shown in the menu.</summary>
        public string Title { get; }

        /// <summary>Title as it appeared on the PSS cassette inlay.</summary>
        public string OriginalTitle { get; }

        public int Year { get; }

        public string Publisher => "PERSONAL SOFTWARE SERVICES";

        public string Tagline { get; }

        public string Blurb { get; }

        public string HowToPlay { get; }

        public string Controls { get; }

        /// <summary>e.g. "INVADERS - PSS 1983".</summary>
        public string Caption => Title.ToUpperInvariant() + " - PSS " + Year;
    }

    /// <summary>
    /// The collection's line-up. PSS released a large arcade catalogue for the Oric-1 and
    /// Atmos in 1983-84; these four are the ones recreated here. The Ultra and OricMon are
    /// deliberately not part of the collection.
    /// </summary>
    public static class Catalogue
    {
        /// <summary>Titles that must never appear in the collection.</summary>
        public static readonly string[] ExcludedTitles = { "THE ULTRA", "ORICMON" };

        private static readonly GameInfo[] Games =
        {
            new GameInfo(
                GameId.Invaders,
                "INVADERS",
                "Invaders",
                1983,
                "THE CLASSIC DESCENT",
                "PSS'S ORIC TAKE ON THE ARCADE ORIGINAL: ROWS OF ALIENS STEP TOWARDS THE\n" +
                "DEFENCE LINE WHILE YOU SHELTER BEHIND FOUR ERODING BUNKERS.",
                "CLEAR EVERY ALIEN BEFORE THE FORMATION REACHES THE GROUND. THE LOWER THE\n" +
                "ROW THE FEWER POINTS IT IS WORTH. A MYSTERY SHIP CROSSES THE TOP PERIODICALLY.",
                "ARROWS / STICK TO MOVE, FIRE TO SHOOT, P TO PAUSE"),

            new GameInfo(
                GameId.Centipede,
                "CENTIPEDE",
                "Centipede",
                1983,
                "MUSHROOM FIELD",
                "A SEGMENTED CENTIPEDE SNAKES DOWN THROUGH A FIELD OF MUSHROOMS. BLAST IT\n" +
                "AND IT SPLITS INTO TWO, EACH HALF HUNTING YOU INDEPENDENTLY.",
                "STAY IN THE BOTTOM BAND. SHOOT THE CENTIPEDE, THE BOUNCING SPIDER AND THE\n" +
                "FLEA. MUSHROOMS TAKE FOUR HITS AND BLOCK ITS PATH.",
                "ARROWS / STICK TO MOVE IN THE LOWER BAND, FIRE TO SHOOT, P TO PAUSE"),

            new GameInfo(
                GameId.Hopper,
                "HOPPER",
                "Hopper",
                1983,
                "CROSS THE ROADS",
                "PSS'S FROGGER-STYLE CROSSING: DODGE FOUR LANES OF TRAFFIC, RIDE THE LOGS\n" +
                "AND TURTLES ACROSS THE RIVER, AND FILL ALL FIVE HOME BAYS BEFORE TIME RUNS OUT.",
                "ONE HOP AT A TIME - ARROWS MOVE A CELL. A CAR OR A SPLASH COSTS A LIFE,\n" +
                "AND SO DOES RIDING A LOG OFF THE SCREEN. FILL EVERY BAY TO CLEAR THE LEVEL.",
                "ARROWS / STICK TO HOP, P TO PAUSE"),

            new GameInfo(
                GameId.ElektroStorm,
                "ELEKTRO STORM",
                "Elektro Storm",
                1983,
                "DEFEND THE GRID",
                "PSS'S FAST ORIC SHOOT-EM-UP: WAVE AFTER WAVE OF ATTACK CRAFT DIVE ON THE\n" +
                "GRID WHILE YOU SLIP BETWEEN THEIR FIRE AND COLLECT POWER CELLS.",
                "DESTROY EACH WAVE. ENEMY CRAFT DIVE AND FIRE; COLLECT DROPPED POWER CELLS\n" +
                "FOR RAPID FIRE, TWIN SHOT AND SHIELDS. A GUARDIAN CRAFT COMMANDS EVERY\n" +
                "FOURTH WAVE.",
                "ARROWS / STICK TO FLY, FIRE TO SHOOT, P TO PAUSE"),

            new GameInfo(
                GameId.LightCycle,
                "LIGHT CYCLE",
                "Light Cycle",
                1983,
                "TRAILS OF LIGHT",
                "PSS'S GRID ARENA: YOUR CYCLE LEAVES A WALL OF LIGHT BEHIND IT. TURN,\n" +
                "BOX IN THE RIVAL CYCLES AND BE THE LAST ONE RUNNING.",
                "YOU CAN ONLY TURN, NEVER REVERSE. TOUCHING A TRAIL - EVEN YOUR OWN - ENDS\n" +
                "YOUR RUN. SURVIVE A ROUND TO SCORE, THEN FACE MORE RIVALS AT HIGHER SPEED.",
                "ARROWS / STICK TO TURN, P TO PAUSE")
        };

        public static IReadOnlyList<GameInfo> All => Games;

        public static int Count => Games.Length;

        public static GameInfo Get(GameId id)
        {
            foreach (GameInfo game in Games)
            {
                if (game.Id == id)
                    return game;
            }

            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown game.");
        }

        public static int IndexOf(GameId id)
        {
            for (int i = 0; i < Games.Length; i++)
            {
                if (Games[i].Id == id)
                    return i;
            }

            return -1;
        }

        public static GameId At(int index)
        {
            if (index < 0)
                index = 0;
            if (index >= Games.Length)
                index = Games.Length - 1;
            return Games[index].Id;
        }
    }
}
