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

        public virtual void Quit(Game game) => game.Exit();

        public virtual void SetOrientation(Game game, bool portrait)
        {
        }
    }
}
