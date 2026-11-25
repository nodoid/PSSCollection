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
#if DEBUG
            ShotDriver.Start(_window);
#endif
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

#if DEBUG
    /// <summary>
    /// Replays a scripted sequence of taps so store screenshots can be taken on a simulator,
    /// which offers no way to send touches. Debug builds only, and only when PSSC_SHOT names
    /// the screen to reach, so the shipping app has no such path.
    /// </summary>
    internal static class ShotDriver
    {
        // A timer that nothing references is collected before it fires.
        private static readonly List<System.Threading.Timer> Timers = new List<System.Threading.Timer>();

        public static void Start(UIWindow window)
        {
            string shot = Environment.GetEnvironmentVariable("PSSC_SHOT");
            Console.WriteLine("SHOT driver start: '" + shot + "' bounds " + window.Bounds);
            if (string.IsNullOrEmpty(shot))
                return;

            int id = 900;
            foreach ((double at, float x, float y) in Plan(shot))
            {
                int tapId = id++;
                Schedule(window, tapId, at, x, y, true);
                Schedule(window, tapId, at + 0.12, x, y, false);
            }
        }

        /// <summary>Virtual 1280x720 positions, timed from launch.</summary>
        private static (double, float, float)[] Plan(string shot)
        {
            if (shot == "title")
                return Array.Empty<(double, float, float)>();

            var plan = new List<(double, float, float)> { (1.6, 640, 390) };
            if (shot == "menu")
                return plan.ToArray();

            // Card centres on the 3x2 selection grid, then the SKILLED row.
            (float X, float Y) card = shot switch
            {
                "invaders" => (234, 219),
                "centipede" => (640, 219),
                "hopper" => (1046, 219),
                "storm" => (234, 445),
                _ => (640, 445)
            };
            plan.Add((2.5, card.X, card.Y));
            plan.Add((3.4, 640, 302));

            if (shot == "storm")
            {
                plan.Add((5.0, 500, 300));
                plan.Add((5.6, 820, 220));
            }
            else if (shot == "hopper")
            {
                plan.Add((5.2, 1100, 640));
            }

            return plan.ToArray();
        }

        private static void Schedule(UIWindow window, int id, double at, float x, float y, bool began)
        {
            var timer = new System.Threading.Timer(_ =>
            {
                var bounds = window.Bounds;
                double scale = Math.Min(bounds.Width / 1280.0, bounds.Height / 720.0);
                double offsetX = (bounds.Width - 1280 * scale) * 0.5;
                double offsetY = (bounds.Height - 720 * scale) * 0.5;
                Console.WriteLine($"SHOT tap {id} {began} -> {offsetX + x * scale:0.0},{offsetY + y * scale:0.0}");
                IosTouchFeed.Publish(id, (float)(offsetX + x * scale), (float)(offsetY + y * scale), began, !began);
            }, null, TimeSpan.FromSeconds(at), System.Threading.Timeout.InfiniteTimeSpan);
            Timers.Add(timer);
        }
    }
#endif

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
        private double _baseRight;
        private double _baseUp;
        private double _sumRight;
        private double _sumUp;
        private int _samples;
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

                // Each steering axis is its own gravity component, measured against the pose the
                // phone is first held in. Leaning sideways moves weight along the screen's
                // horizontal axis and tipping forwards moves it along the vertical one, so the two
                // stay separate however the phone is held - the angles derived from them did not:
                // held flat the in-plane gravity is tiny, and the angle between two tiny vectors is
                // mostly noise, which sent forward tips off as side-to-side hops.
                if (_samples < 45)
                {
                    _sumRight += screenRight;
                    _sumUp += screenUp;
                    _samples++;
                    _baseRight = _sumRight / _samples;
                    _baseUp = _sumUp / _samples;
                }

                // Negated: leaning the phone right must steer right.
                _tilt = -(float)Math.Clamp((screenRight - _baseRight) / 0.30, -1.0, 1.0);
                _tiltY = (float)Math.Clamp((screenUp - _baseUp) / 0.30, -1.0, 1.0);
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
