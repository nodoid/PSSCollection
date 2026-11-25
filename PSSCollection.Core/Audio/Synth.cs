using System;
using PSSCollection.Engine;

namespace PSSCollection.Core.Audio
{
    public enum Waveform
    {
        Square,
        Triangle,
        Saw,
        Sine
    }

    /// <summary>
    /// Builds PCM audio in memory: every effect and every piece of music in the collection is
    /// synthesised at start-up, so the game ships no audio files.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 22050;

        public static short[] CreateBuffer(double seconds) =>
            new short[(int)Math.Max(1, seconds * SampleRate)];

        public static byte[] ToPcm16(short[] samples)
        {
            var bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                bytes[i * 2] = (byte)(samples[i] & 0xFF);
                bytes[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
            }

            return bytes;
        }

        /// <summary>Adds a tone with an optional pitch glide.</summary>
        public static void Tone(short[] buffer, double start, double length, double frequency, double volume,
            Waveform waveform = Waveform.Square, double glideTo = 0, double attack = 0.004, double release = 0.03)
        {
            if (buffer == null || length <= 0 || frequency <= 0)
                return;

            int first = (int)(start * SampleRate);
            int count = (int)(length * SampleRate);
            if (first >= buffer.Length)
                return;

            count = Math.Min(count, buffer.Length - first);
            double phase = 0;
            double endFrequency = glideTo > 0 ? glideTo : frequency;

            for (int i = 0; i < count; i++)
            {
                double t = i / (double)SampleRate;
                double progress = count <= 1 ? 0 : i / (double)(count - 1);
                double frequencyNow = frequency + (endFrequency - frequency) * progress;

                phase += frequencyNow / SampleRate;
                if (phase >= 1.0)
                    phase -= 1.0;

                double sample;
                switch (waveform)
                {
                    case Waveform.Triangle:
                        sample = 4 * Math.Abs(phase - 0.5) - 1;
                        break;
                    case Waveform.Saw:
                        sample = 2 * phase - 1;
                        break;
                    case Waveform.Sine:
                        sample = Math.Sin(phase * Math.PI * 2);
                        break;
                    default:
                        sample = phase < 0.5 ? 1 : -1;
                        break;
                }

                double envelope = 1.0;
                if (attack > 0 && t < attack)
                    envelope = t / attack;
                double remaining = length - t;
                if (release > 0 && remaining < release)
                    envelope = Math.Min(envelope, remaining / release);

                Add(buffer, first + i, sample * volume * envelope);
            }
        }

        /// <summary>Adds a noise burst, roughly low-passed, for explosions and impacts.</summary>
        public static void Noise(short[] buffer, double start, double length, double volume,
            double smoothing = 0.7, int seed = 12345)
        {
            if (buffer == null || length <= 0)
                return;

            int first = (int)(start * SampleRate);
            int count = (int)(length * SampleRate);
            if (first >= buffer.Length)
                return;

            count = Math.Min(count, buffer.Length - first);
            uint state = (uint)seed | 1;
            double filtered = 0;

            for (int i = 0; i < count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                double white = ((state & 0xFFFF) / 32768.0) - 1.0;

                filtered = filtered * smoothing + white * (1 - smoothing);

                double t = i / (double)SampleRate;
                double envelope = Math.Exp(-t * (3.5 / length)) * (1 - i / (double)count);

                Add(buffer, first + i, filtered * volume * envelope);
            }
        }

        public static void Add(short[] buffer, int index, double value)
        {
            if (index < 0 || index >= buffer.Length)
                return;

            long mixed = buffer[index] + (long)(value * 32767);
            if (mixed > short.MaxValue)
                mixed = short.MaxValue;
            if (mixed < short.MinValue)
                mixed = short.MinValue;
            buffer[index] = (short)mixed;
        }

        public static void Mix(short[] target, short[] source, double gain = 1.0)
        {
            if (target == null || source == null)
                return;

            int count = Math.Min(target.Length, source.Length);
            for (int i = 0; i < count; i++)
            {
                long mixed = target[i] + (long)(source[i] * gain);
                target[i] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
            }
        }

        public static short[] Effect(SoundId id)
        {
            switch (id)
            {
                case SoundId.Shoot:
                {
                    short[] buffer = CreateBuffer(0.09);
                    Tone(buffer, 0, 0.09, 1100, 0.35, Waveform.Square, 440, 0.001, 0.04);
                    return buffer;
                }
                case SoundId.EnemyShoot:
                {
                    short[] buffer = CreateBuffer(0.12);
                    Tone(buffer, 0, 0.12, 320, 0.22, Waveform.Saw, 140, 0.001, 0.05);
                    return buffer;
                }
                case SoundId.Explosion:
                {
                    short[] buffer = CreateBuffer(0.42);
                    Noise(buffer, 0, 0.42, 0.55, 0.55, 7);
                    Tone(buffer, 0, 0.2, 160, 0.25, Waveform.Saw, 40, 0.001, 0.1);
                    return buffer;
                }
                case SoundId.EnemyDestroyed:
                {
                    short[] buffer = CreateBuffer(0.16);
                    Noise(buffer, 0, 0.16, 0.3, 0.4, 21);
                    Tone(buffer, 0, 0.14, 520, 0.25, Waveform.Square, 130, 0.001, 0.05);
                    return buffer;
                }
                case SoundId.PlayerHit:
                {
                    short[] buffer = CreateBuffer(0.5);
                    Tone(buffer, 0, 0.5, 260, 0.4, Waveform.Saw, 50, 0.002, 0.2);
                    Noise(buffer, 0, 0.5, 0.35, 0.5, 33);
                    return buffer;
                }
                case SoundId.WaveClear:
                {
                    short[] buffer = CreateBuffer(0.5);
                    double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.1, 0.14, notes[i], 0.3, Waveform.Triangle);
                    return buffer;
                }
                case SoundId.MenuMove:
                {
                    short[] buffer = CreateBuffer(0.05);
                    Tone(buffer, 0, 0.05, 660, 0.2, Waveform.Square);
                    return buffer;
                }
                case SoundId.MenuSelect:
                {
                    short[] buffer = CreateBuffer(0.14);
                    Tone(buffer, 0, 0.06, 880, 0.25, Waveform.Square);
                    Tone(buffer, 0.06, 0.08, 1320, 0.25, Waveform.Square);
                    return buffer;
                }
                case SoundId.GameOver:
                {
                    short[] buffer = CreateBuffer(1.1);
                    double[] notes = { 440, 349.23, 261.63, 196 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.22, 0.24, notes[i], 0.3, Waveform.Triangle, notes[i] * 0.98, 0.005, 0.08);
                    return buffer;
                }
                case SoundId.PowerUp:
                {
                    short[] buffer = CreateBuffer(0.32);
                    double[] notes = { 523.25, 659.25, 880, 1046.5 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.07, 0.1, notes[i], 0.28, Waveform.Square);
                    return buffer;
                }
                case SoundId.Push:
                {
                    short[] buffer = CreateBuffer(0.08);
                    Noise(buffer, 0, 0.08, 0.2, 0.85, 5);
                    Tone(buffer, 0, 0.06, 180, 0.15, Waveform.Triangle, 120);
                    return buffer;
                }
                case SoundId.BlockBreak:
                {
                    short[] buffer = CreateBuffer(0.22);
                    Noise(buffer, 0, 0.22, 0.4, 0.35, 11);
                    Tone(buffer, 0, 0.12, 380, 0.25, Waveform.Square, 90);
                    return buffer;
                }
                case SoundId.ExtraLife:
                {
                    short[] buffer = CreateBuffer(0.42);
                    double[] notes = { 1046.5, 1318.5, 1568, 2093 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.09, 0.12, notes[i], 0.25, Waveform.Triangle);
                    return buffer;
                }
                case SoundId.GuardianAlert:
                {
                    short[] buffer = CreateBuffer(0.7);
                    for (int i = 0; i < 3; i++)
                    {
                        Tone(buffer, i * 0.22, 0.18, 150, 0.3, Waveform.Square, 110, 0.01, 0.05);
                        Tone(buffer, i * 0.22 + 0.08, 0.12, 220, 0.2, Waveform.Saw, 160, 0.01, 0.05);
                    }

                    return buffer;
                }
                default:
                {
                    short[] buffer = CreateBuffer(0.5);
                    double[] notes = { 523.25, 659.25, 783.99, 1046.5, 1318.5 };
                    for (int i = 0; i < notes.Length; i++)
                        Tone(buffer, i * 0.09, 0.12, notes[i], 0.3, Waveform.Square);
                    return buffer;
                }
            }
        }

        // ---- Music --------------------------------------------------------------------

        private static readonly int[] InvadersLead = { 57, 0, 60, 0, 64, 0, 60, 0, 57, 0, 60, 0, 65, 64, 62, 60 };
        private static readonly int[] InvadersBass = { 33, 33, 40, 33, 33, 33, 38, 40 };

        private static readonly int[] CentipedeLead = { 64, 66, 67, 69, 71, 69, 67, 66, 64, 62, 60, 62, 64, 0, 67, 0 };
        private static readonly int[] CentipedeBass = { 40, 40, 43, 40, 45, 43, 40, 38 };

        private static readonly int[] HopperLead = { 72, 76, 79, 76, 72, 74, 77, 74, 72, 76, 79, 84, 83, 79, 76, 72 };
        private static readonly int[] HopperBass = { 48, 48, 55, 48, 53, 53, 55, 55 };

        private static readonly int[] StormLead = { 69, 69, 76, 69, 72, 69, 76, 81, 69, 69, 76, 69, 74, 72, 69, 67 };
        private static readonly int[] StormBass = { 33, 33, 33, 40, 33, 33, 31, 31 };

        public static (int[] Lead, int[] Bass) Pattern(GameId game)
        {
            switch (game)
            {
                case GameId.Invaders: return (InvadersLead, InvadersBass);
                case GameId.Centipede: return (CentipedeLead, CentipedeBass);
                case GameId.Hopper: return (HopperLead, HopperBass);
                default: return (StormLead, StormBass);
            }
        }

        /// <summary>A looping chiptune theme for a game, about seven seconds long.</summary>
        public static short[] Theme(GameId game)
        {
            const double step = 0.21;
            (int[] lead, int[] bass) = Pattern(game);

            short[] buffer = CreateBuffer(step * lead.Length * 2 + 0.2);

            for (int repeat = 0; repeat < 2; repeat++)
            {
                double offset = repeat * step * lead.Length;

                for (int i = 0; i < lead.Length; i++)
                {
                    int note = lead[i];
                    if (note > 0)
                    {
                        double frequency = MidiToFrequency(note + (repeat == 1 ? 12 : 0) - 12);
                        Tone(buffer, offset + i * step, step * 0.92, frequency, 0.10, Waveform.Square, 0, 0.008, 0.06);
                    }

                    // Light off-beat lead echo.
                    if (note > 0 && i % 4 == 2)
                        Tone(buffer, offset + i * step + step * 0.5, step * 0.2,
                            MidiToFrequency(note - 12), 0.05, Waveform.Triangle);
                }

                for (int bar = 0; bar < 2; bar++)
                {
                    for (int i = 0; i < bass.Length; i++)
                    {
                        int note = bass[i];
                        if (note <= 0)
                            continue;

                        double when = offset + bar * bass.Length * step + i * step;
                        Tone(buffer, when, step * 0.85, MidiToFrequency(note), 0.13, Waveform.Triangle, 0, 0.005, 0.05);
                    }
                }

                for (int i = 0; i < lead.Length * 2; i++)
                {
                    Noise(buffer, offset + i * step * 0.5, 0.03, i % 2 == 0 ? 0.045 : 0.025, 0.9, 3 + i);
                }
            }

            return buffer;
        }

        private static double MidiToFrequency(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    }
}
