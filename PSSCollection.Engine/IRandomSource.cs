using System;

namespace PSSCollection.Engine
{
    /// <summary>Random numbers, abstracted so tests can supply a fixed sequence.</summary>
    public interface IRandomSource
    {
        /// <summary>0 .. maxExclusive-1. Returns 0 when maxExclusive is 0 or less.</summary>
        int Next(int maxExclusive);

        /// <summary>0.0 (inclusive) .. 1.0 (exclusive).</summary>
        double NextDouble();
    }

    /// <summary>xorshift64* - small, fast and repeatable for a given seed.</summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private ulong _state;

        public XorShiftRandom(ulong seed)
        {
            // A zero state would be a fixed point, so mix the seed with a constant.
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0)
                return 0;

            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        private ulong NextULong()
        {
            _state ^= _state >> 12;
            _state ^= _state << 25;
            _state ^= _state >> 27;
            return _state * 0x2545F4914F6CDD1DUL;
        }
    }

    public static class RandomSourceExtensions
    {
        /// <summary>min .. maxInclusive.</summary>
        public static int Range(this IRandomSource random, int min, int maxInclusive)
        {
            if (maxInclusive <= min)
                return min;

            return min + random.Next(maxInclusive - min + 1);
        }

        public static double Range(this IRandomSource random, double min, double max) =>
            min + random.NextDouble() * (max - min);

        public static bool Chance(this IRandomSource random, double probability) =>
            random.NextDouble() < probability;
    }
}
