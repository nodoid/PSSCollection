using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using PSSCollection.Engine;

namespace PSSCollection.Core.Audio
{
    /// <summary>Owns every sound effect and the looping music bed.</summary>
    public sealed class SoundBank : IDisposable
    {
        private readonly Dictionary<SoundId, SoundEffect> _effects = new Dictionary<SoundId, SoundEffect>();
        private readonly Dictionary<GameId, SoundEffect> _themes = new Dictionary<GameId, SoundEffect>();
        private SoundEffectInstance _musicInstance;
        private GameId? _currentTheme;

        public SoundBank()
        {
            try
            {
                foreach (SoundId id in Enum.GetValues<SoundId>())
                {
                    short[] samples = Synth.Effect(id);
                    _effects[id] = new SoundEffect(Synth.ToPcm16(samples), Synth.SampleRate, AudioChannels.Mono);
                }

                Available = true;
            }
            catch (Exception)
            {
                // No audio device (or the platform refused it): the game still runs silently.
                Available = false;
            }
        }

        public bool Available { get; }

        public bool SoundEnabled { get; set; } = true;

        public bool MusicEnabled { get; set; } = true;

        public float EffectVolume { get; set; } = 0.55f;

        public float MusicVolume { get; set; } = 0.32f;

        public void Play(SoundId id)
        {
            if (!Available || !SoundEnabled)
                return;

            if (!_effects.TryGetValue(id, out SoundEffect effect))
                return;

            try
            {
                effect.Play(EffectVolume, 0f, 0f);
            }
            catch (Exception)
            {
                // Losing a single effect must never break the game loop.
            }
        }

        /// <summary>Switches the looping music to a game's theme. Passing null stops it.</summary>
        public void PlayMusic(GameId? game)
        {
            if (!Available)
                return;

            if (_currentTheme == game)
            {
                if (_musicInstance != null && MusicEnabled && _musicInstance.State == SoundState.Paused)
                    _musicInstance.Resume();
                return;
            }

            StopMusic();
            _currentTheme = game;

            if (game == null || !MusicEnabled)
                return;

            try
            {
                if (!_themes.TryGetValue(game.Value, out SoundEffect theme))
                {
                    theme = new SoundEffect(Synth.ToPcm16(Synth.Theme(game.Value)), Synth.SampleRate, AudioChannels.Mono);
                    _themes[game.Value] = theme;
                }

                _musicInstance = theme.CreateInstance();
                _musicInstance.IsLooped = true;
                _musicInstance.Volume = MusicVolume;
                _musicInstance.Play();
            }
            catch (Exception)
            {
                _musicInstance = null;
            }
        }

        public void PauseMusic()
        {
            try
            {
                if (_musicInstance != null && _musicInstance.State == SoundState.Playing)
                    _musicInstance.Pause();
            }
            catch (Exception)
            {
            }
        }

        public void ResumeMusic()
        {
            try
            {
                if (_musicInstance != null && MusicEnabled && _musicInstance.State == SoundState.Paused)
                    _musicInstance.Resume();
            }
            catch (Exception)
            {
            }
        }

        public void StopMusic()
        {
            try
            {
                _musicInstance?.Stop();
                _musicInstance?.Dispose();
            }
            catch (Exception)
            {
            }

            _musicInstance = null;
            _currentTheme = null;
        }

        public void Dispose()
        {
            StopMusic();

            foreach (SoundEffect effect in _effects.Values)
                effect.Dispose();
            foreach (SoundEffect theme in _themes.Values)
                theme.Dispose();

            _effects.Clear();
            _themes.Clear();
        }
    }
}
