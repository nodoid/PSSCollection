using System;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace PSSCollection.Core.Platform
{
    /// <summary>What a platform head tells the shared game about itself.</summary>
    public interface IPlatformServices
    {
        bool IsMobile { get; }

        /// <summary>Writable folder for the high-score file.</summary>
        string DataDirectory { get; }

        /// <summary>Shown on the title screen.</summary>
        string PlatformName { get; }

        /// <summary>
        /// Size of the space the platform reports touch positions in, when it knows it. Zero means
        /// "work it out from the touch panel", which is what the desktop heads do.
        /// </summary>
        Vector2 TouchSpace { get; }

        /// <summary>Touch feed from the platform itself, when it can provide one.</summary>
        Input.ITouchSource TouchSource { get; }

        /// <summary>
        /// Device roll for tilt steering: -1 fully left, 0 level, +1 fully right. Always 0 on a
        /// platform without a motion sensor.
        /// </summary>
        float Tilt { get; }

        /// <summary>
        /// Device pitch for vertical tilt steering: -1 tilted away, 0 level, +1 tilted towards the
        /// player. Always 0 on a platform without a motion sensor.
        /// </summary>
        float TiltY { get; }

        void Quit(Game game);

        void SetOrientation(Game game, bool portrait);
    }

    /// <summary>Defaults for the desktop heads.</summary>
    public class DesktopPlatformServices : IPlatformServices
    {
        public virtual bool IsMobile => false;

        public virtual string DataDirectory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root))
                    root = AppContext.BaseDirectory;
                return System.IO.Path.Combine(root, "PSSCollection");
            }
        }

        public virtual string PlatformName => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
            ? "WINDOWS"
            : "DESKTOP";

        public virtual Vector2 TouchSpace => Vector2.Zero;

        public virtual Input.ITouchSource TouchSource => null;

        public virtual float Tilt => 0f;

        public virtual float TiltY => 0f;

        public virtual void Quit(Game game) => game.Exit();

        public virtual void SetOrientation(Game game, bool portrait)
        {
        }
    }
}
