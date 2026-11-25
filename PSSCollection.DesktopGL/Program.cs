using System;
using System.IO;
using PSSCollection.Core;
using PSSCollection.Core.Platform;

namespace PSSCollection.DesktopGL
{
    /// <summary>Entry point for the macOS app (the same build runs on Windows and Linux).</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var game = new PssGame(new MacPlatformServices());
            game.Run();
        }
    }

    internal sealed class MacPlatformServices : DesktopPlatformServices
    {
        public override string PlatformName => OperatingSystem.IsMacOS() ? "MACOS" : "DESKTOP";

        public override string DataDirectory
        {
            get
            {
                if (!OperatingSystem.IsMacOS())
                    return base.DataDirectory;

                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "PSSCollection");
            }
        }
    }
}
