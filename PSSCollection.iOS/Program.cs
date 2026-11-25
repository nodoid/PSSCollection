using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoreGraphics;
using CoreMotion;
using Foundation;
using UIKit;
using Microsoft.Xna.Framework;
using PSSCollection.Core;
using PSSCollection.Core.Input;
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

        internal static IosPlatformServices TiltSource { get; private set; }

        internal static void RunGame(UIWindowScene scene)
        {
            if (_game != null)
                return;

            TiltSource = new IosPlatformServices();
            _game = new PssGame(TiltSource);
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

            // MonoGame's touch panel reports a position that does not follow the finger on this
            // build, so a transparent view on top reads the real touches from UIKit instead.
            UIView root = _window.RootViewController?.View;
            if (root != null)
                root.AddSubview(new TouchOverlayView(root.Bounds));

            TiltSource.Start(_window);
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

    /// <summary>Collects touches from UIKit, in view points.</summary>
    internal static class IosTouchFeed
    {
        private static readonly List<TouchPoint> Points = new List<TouchPoint>();
        private static readonly object Gate = new object();

        public static bool HasDelivered { get; private set; }

        public static void Publish(int id, float x, float y, bool began, bool ended)
        {
            lock (Gate)
            {
                Points.Add(new TouchPoint(id, new Vector2(x, y), began, ended));
                HasDelivered = true;
                if (Points.Count > 128)
                    Points.RemoveRange(0, Points.Count - 128);
            }
        }

        public static IReadOnlyList<TouchPoint> Take()
        {
            lock (Gate)
            {
                if (Points.Count == 0)
                    return Array.Empty<TouchPoint>();

                var copy = Points.ToArray();
                Points.Clear();
                return copy;
            }
        }
    }

    internal sealed class IosTouchSource : ITouchSource
    {
        public IReadOnlyList<TouchPoint> Read() => IosTouchFeed.Take();

        public bool HasDelivered => IosTouchFeed.HasDelivered;
    }

    /// <summary>A clear view over the game that turns UIKit touches into a feed.</summary>
    internal sealed class TouchOverlayView : UIView
    {
        private readonly Dictionary<IntPtr, int> _ids = new Dictionary<IntPtr, int>();
        private int _nextId = 1;

        public TouchOverlayView(CGRect frame) : base(frame)
        {
            BackgroundColor = UIColor.Clear;
            MultipleTouchEnabled = true;
            UserInteractionEnabled = true;
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt) => Record(touches, true, false);

        public override void TouchesMoved(NSSet touches, UIEvent evt) => Record(touches, false, false);

        public override void TouchesEnded(NSSet touches, UIEvent evt) => Record(touches, false, true);

        public override void TouchesCancelled(NSSet touches, UIEvent evt) => Record(touches, false, true);

        private void Record(NSSet touches, bool began, bool ended)
        {
            foreach (UITouch touch in touches)
            {
                IntPtr handle = touch.Handle.Handle;

                if (began && !_ids.ContainsKey(handle))
                    _ids[handle] = _nextId++;

                if (!_ids.TryGetValue(handle, out int id))
                    continue;

                CGPoint point = touch.LocationInView(this);
                IosTouchFeed.Publish(id, (float)point.X, (float)point.Y, began, ended);

                if (ended)
                    _ids.Remove(handle);
            }
        }
    }

    internal sealed class IosPlatformServices : DesktopPlatformServices
    {
        private readonly IosTouchSource _touches = new IosTouchSource();
        private readonly CMMotionManager _motion = new CMMotionManager();
        private float _tilt;
        private float _tiltY;
        private double _pitchBase;
        private double _pitchSum;
        private int _pitchSamples;
        private UIWindow _window;

        public override ITouchSource TouchSource => _touches;

        public override float Tilt => _tilt;

        public override float TiltY => _tiltY;

        /// <summary>Starts accelerometer updates and turns device roll into a -1..1 steering value.</summary>
        public void Start(UIWindow window)
        {
            _window = window;
            if (!_motion.AccelerometerAvailable)
                return;

            _motion.AccelerometerUpdateInterval = 1.0 / 45.0;
            _motion.StartAccelerometerUpdates(NSOperationQueue.MainQueue, (data, error) =>
            {
                if (data == null)
                    return;

                CMAcceleration a = data.Acceleration;
                UIInterfaceOrientation orientation = _window?.WindowScene?.InterfaceOrientation
                                                      ?? UIInterfaceOrientation.LandscapeLeft;

                // Roll is rotation about the axis pointing at the viewer: gravity moves along the
                // screen's horizontal axis. Which device axis that is depends on the orientation.
                double screenRight;
                double screenUp;

                if (orientation == UIInterfaceOrientation.LandscapeRight)
                {
                    screenRight = a.Y;
                    screenUp = -a.X;
                }
                else
                {
                    screenRight = -a.Y;
                    screenUp = a.X;
                }

                double roll = Math.Atan2(screenRight, screenUp);

                // Negated: tilting the phone right must send the ship right.
                _tilt = -(float)Math.Clamp(roll / 0.30, -1.0, 1.0);

                // Pitch: the part of gravity pointing out of the screen grows as the top edge is
                // tipped away, which gives a second steering axis for games that need one. How the
                // phone is held at rest varies a lot, so the first second is averaged and used as
                // the neutral pose.
                double inPlane = Math.Max(0.25, Math.Sqrt(screenRight * screenRight + screenUp * screenUp));
                double pitch = Math.Atan2(a.Z, inPlane);

                if (_pitchSamples < 45)
                {
                    _pitchSum += pitch;
                    _pitchSamples++;
                    _pitchBase = _pitchSum / _pitchSamples;
                }

                _tiltY = (float)Math.Clamp((pitch - _pitchBase) / 0.30, -1.0, 1.0);
            });
        }

        public override bool IsMobile => true;

        public override string PlatformName => "IPHONE / IPAD";

        /// <summary>
        /// The game view's size in points - the space iOS reports touches in. Reading it from the
        /// window avoids guessing whether the panel counts points, pixels or back-buffer units.
        /// </summary>
        public override Vector2 TouchSpace
        {
            get
            {
#pragma warning disable CA1422
                UIWindow window = UIApplication.SharedApplication.Windows.FirstOrDefault(w => w.RootViewController != null);
#pragma warning restore CA1422
                return window == null
                    ? Vector2.Zero
                    : new Vector2((float)window.Bounds.Width, (float)window.Bounds.Height);
            }
        }

        /// <summary>Library/Application Support - private to the app and backed up.</summary>
        public override string DataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSSCollection");

        // iOS apps never quit themselves.
        public override void Quit(Game game)
        {
        }
    }
}
