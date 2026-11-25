using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using PSSCollection.Core;
using PSSCollection.Core.Platform;

namespace PSSCollection.Android
{
    /// <summary>Android entry point: landscape, full screen, played by touch.</summary>
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden |
                               ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode
    )]
    public class MainActivity : AndroidGameActivity
    {
        private PssGame _game;
        private View _view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            _game = new PssGame(new AndroidPlatformServices(this, FilesDir?.AbsolutePath));
            _view = _game.Services.GetService(typeof(View)) as View;

            SetContentView(_view);
            HideSystemUi();
            _game.Run();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus)
                HideSystemUi();
        }

        private void HideSystemUi()
        {
            if (Window == null)
                return;

            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var controller = Window.InsetsController;
                if (controller != null)
                {
                    controller.Hide(WindowInsets.Type.SystemBars());
                    controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
                }
            }
            else
            {
#pragma warning disable CA1422
                Window.DecorView.SystemUiFlags =
                    SystemUiFlags.ImmersiveSticky | SystemUiFlags.Fullscreen | SystemUiFlags.HideNavigation |
                    SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutStable;
#pragma warning restore CA1422
            }

            if (OperatingSystem.IsAndroidVersionAtLeast(28) && Window.Attributes != null)
            {
                var attributes = Window.Attributes;
                attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                Window.Attributes = attributes;
            }
        }
    }

    internal sealed class AndroidPlatformServices : DesktopPlatformServices
    {
        private readonly string _dataDirectory;
        private readonly Activity _activity;

        public AndroidPlatformServices(Activity activity, string dataDirectory)
        {
            _activity = activity;
            _dataDirectory = dataDirectory;
        }

        public override bool IsMobile => true;

        public override string PlatformName => "ANDROID";

        public override string DataDirectory => _dataDirectory ?? base.DataDirectory;

        /// <summary>The collection is landscape on phones and tablets.</summary>
        public override void SetOrientation(Game game, bool portrait) =>
            _activity.RunOnUiThread(() =>
                _activity.RequestedOrientation = portrait ? ScreenOrientation.SensorPortrait : ScreenOrientation.SensorLandscape);

        // The Back button finishes the activity; the game does not quit itself.
        public override void Quit(Game game) => game.Exit();
    }
}
