using System;
using System.IO;
using System.Linq;
using Foundation;
using UIKit;
using Microsoft.Xna.Framework;
using PSSCollection.Core;
using PSSCollection.Core.Platform;

namespace PSSCollection.iOS
{
    /// <summary>
    /// iOS entry point. iOS 27 requires the UIScene life cycle, so the game is started by
    /// <see cref="SceneDelegate"/> once the window scene connects.
    /// </summary>
    [Register("AppDelegate")]
    internal class AppDelegate : UIApplicationDelegate
    {
        private static PssGame _game;
        private static UIWindow _window;

        internal static void RunGame(UIWindowScene scene)
        {
            if (_game != null)
                return;

            _game = new PssGame(new IosPlatformServices());
            _game.Run();
            AttachToScene(scene);
        }

        private static void AttachToScene(UIWindowScene scene)
        {
            // MonoGame creates its own UIWindow without a scene; give its view controller a scene window.
            var controller = _game.Services.GetService(typeof(UIViewController)) as UIViewController
                             ?? FindGameWindow()?.RootViewController;
            if (controller == null)
                return;

            _window = new UIWindow(scene) { RootViewController = controller };
            _window.MakeKeyAndVisible();
        }

        private static UIWindow FindGameWindow()
        {
#pragma warning disable CA1422
            return UIApplication.SharedApplication.Windows.FirstOrDefault(w => w.RootViewController != null);
#pragma warning restore CA1422
        }

        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions) => true;

        public override UISceneConfiguration GetConfiguration(UIApplication application, UISceneSession connectingSceneSession,
            UISceneConnectionOptions options) =>
            new UISceneConfiguration("Default Configuration", connectingSceneSession.Role) { DelegateType = typeof(SceneDelegate) };

        private static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(AppDelegate));
        }
    }

    /// <summary>Starts the game when iOS connects the app's window scene.</summary>
    [Register("SceneDelegate")]
    internal class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is UIWindowScene windowScene)
                AppDelegate.RunGame(windowScene);
        }
    }

    internal sealed class IosPlatformServices : DesktopPlatformServices
    {
        public override bool IsMobile => true;

        public override string PlatformName => "IPHONE / IPAD";

        /// <summary>Library/Application Support - private to the app and backed up.</summary>
        public override string DataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSSCollection");

        // iOS apps never quit themselves.
        public override void Quit(Game game)
        {
        }
    }
}
