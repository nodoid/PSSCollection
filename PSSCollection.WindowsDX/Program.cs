using System;
using PSSCollection.Core;
using PSSCollection.Core.Platform;

namespace PSSCollection.WindowsDX
{
    /// <summary>Windows and Xbox entry point (DirectX 11).</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using var game = new PssGame(new WindowsPlatformServices());
            game.Run();
        }
    }

    internal sealed class WindowsPlatformServices : DesktopPlatformServices
    {
        public override string PlatformName => "WINDOWS / XBOX";
    }
}
