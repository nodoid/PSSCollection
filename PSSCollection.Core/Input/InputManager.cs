using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using PSSCollection.Engine;

namespace PSSCollection.Core.Input
{
    /// <summary>A finger on the screen, in virtual pixels.</summary>
    public readonly struct TouchContact
    {
        public TouchContact(int id, Vector2 position, bool pressed)
        {
            Id = id;
            Position = position;
            Pressed = pressed;
        }

        public int Id { get; }

        public Vector2 Position { get; }

        /// <summary>True on the frame the finger went down.</summary>
        public bool Pressed { get; }
    }

    /// <summary>One frame of input, translated for the screens.</summary>
    public sealed class GameInput
    {
        private readonly List<TouchContact> _touches;

        public GameInput(int horizontal, int vertical, bool confirm, bool back, bool pause, bool highScores, bool clear,
            Vector2 pointer, bool pointerMoved, bool pointerClicked, InputState player, List<TouchContact> touches)
        {
            Horizontal = horizontal;
            Vertical = vertical;
            Confirm = confirm;
            Back = back;
            Pause = pause;
            HighScores = highScores;
            Clear = clear;
            Pointer = pointer;
            PointerMoved = pointerMoved;
            PointerClicked = pointerClicked;
            Player = player;
            _touches = touches;
        }

        /// <summary>-1 (left), 0 or 1 (right), edge triggered with key repeat.</summary>
        public int Horizontal { get; }

        /// <summary>-1 (up), 0 or 1 (down), edge triggered with key repeat.</summary>
        public int Vertical { get; }

        public bool Confirm { get; }

        public bool Back { get; }

        public bool Pause { get; }

        /// <summary>The "H" key: jump to the high-score tables.</summary>
        public bool HighScores { get; }

        /// <summary>The "C" key: arm or confirm clearing a table.</summary>
        public bool Clear { get; }

        public Vector2 Pointer { get; }

        public bool PointerMoved { get; }

        public bool PointerClicked { get; }

        /// <summary>Held directions and fire for gameplay (keyboard, pad or touch pad).</summary>
        public InputState Player { get; }

        public IReadOnlyList<TouchContact> Touches => _touches;

        public bool TouchPressedIn(Rectangle area)
        {
            foreach (TouchContact touch in _touches)
            {
                if (touch.Pressed && area.Contains((int)touch.Position.X, (int)touch.Position.Y))
                    return true;
            }

            return false;
        }

        public bool TouchHeldIn(Rectangle area)
        {
            foreach (TouchContact touch in _touches)
            {
                if (area.Contains((int)touch.Position.X, (int)touch.Position.Y))
                    return true;
            }

            return false;
        }

        public bool PointerIn(Rectangle area) =>
            area.Contains((int)Pointer.X, (int)Pointer.Y);
    }

    /// <summary>
    /// Reads the keyboard, gamepad, mouse and touch screen once a frame. Screen points are
    /// converted into the game's virtual 1280x720 space so every screen can hit-test simply.
    /// </summary>
    public sealed class InputManager
    {
        private KeyboardState _keys;
        private KeyboardState _previousKeys;
        private GamePadState _pad;
        private GamePadState _previousPad;
        private MouseState _mouse;
        private MouseState _previousMouse;
        private readonly HashSet<int> _previousTouches = new HashSet<int>();
        private readonly List<TouchContact> _touches = new List<TouchContact>();
        private readonly List<TouchContact> _published = new List<TouchContact>();

        private float _horizontalTimer;
        private float _verticalTimer;
        private int _horizontalDirection;
        private int _verticalDirection;
        private bool _hasDirectionTimer;

        public InputManager(bool isMobile)
        {
            IsMobile = isMobile;
            if (isMobile)
                TouchPanel.EnabledGestures = GestureType.None;
        }

        public bool IsMobile { get; }

        public GameInput Current { get; private set; }

        /// <summary>True when the mouse is over the game area (desktop).</summary>
        public bool PointerActive { get; private set; }

        public void Update(Rectangle destination, float elapsedSeconds)
        {
            _previousKeys = _keys;
            _keys = Keyboard.GetState();
            _previousPad = _pad;
            _pad = GamePad.GetState(PlayerIndex.One);
            _previousMouse = _mouse;
            _mouse = Mouse.GetState();

            UpdateTouches(destination);

            Vector2 pointer;
            bool pointerMoved;
            bool pointerClicked;

            if (_touches.Count > 0)
            {
                pointer = _touches[0].Position;
                pointerMoved = _touches[0].Pressed;
                pointerClicked = _touches[0].Pressed;
                PointerActive = true;
            }
            else
            {
                pointer = ScreenToVirtual(new Vector2(_mouse.X, _mouse.Y), destination);
                pointerMoved = Math.Abs(_mouse.X - _previousMouse.X) > 0 || Math.Abs(_mouse.Y - _previousMouse.Y) > 0;
                pointerClicked = _mouse.LeftButton == ButtonState.Pressed &&
                                 _previousMouse.LeftButton == ButtonState.Released;
                PointerActive = destination.Contains(_mouse.X, _mouse.Y);
            }

            bool left = Pressed(Keys.Left) || Pressed(Keys.A) || PadPressed(Buttons.DPadLeft) || PadLeftStick(-1);
            bool right = Pressed(Keys.Right) || Pressed(Keys.D) || PadPressed(Buttons.DPadRight) || PadLeftStick(1);
            bool up = Pressed(Keys.Up) || Pressed(Keys.W) || PadPressed(Buttons.DPadUp) || PadUpStick(-1);
            bool down = Pressed(Keys.Down) || Pressed(Keys.S) || PadPressed(Buttons.DPadDown) || PadUpStick(1);

            bool heldLeft = Held(Keys.Left) || Held(Keys.A) || _pad.DPad.Left == ButtonState.Pressed || _pad.ThumbSticks.Left.X < -0.4f;
            bool heldRight = Held(Keys.Right) || Held(Keys.D) || _pad.DPad.Right == ButtonState.Pressed || _pad.ThumbSticks.Left.X > 0.4f;
            bool heldUp = Held(Keys.Up) || Held(Keys.W) || _pad.DPad.Up == ButtonState.Pressed || _pad.ThumbSticks.Left.Y > 0.4f;
            bool heldDown = Held(Keys.Down) || Held(Keys.S) || _pad.DPad.Down == ButtonState.Pressed || _pad.ThumbSticks.Left.Y < -0.4f;

            int horizontal = 0;
            if (left && !right)
                horizontal = -1;
            else if (right && !left)
                horizontal = 1;

            int vertical = 0;
            if (up && !down)
                vertical = -1;
            else if (down && !up)
                vertical = 1;

            // Key repeat for menu navigation.
            if (horizontal != 0)
            {
                if (horizontal != _horizontalDirection)
                {
                    _horizontalDirection = horizontal;
                    _horizontalTimer = 0.34f;
                }
                else
                {
                    _horizontalTimer -= elapsedSeconds;
                    if (_horizontalTimer > 0)
                        horizontal = 0;
                    else
                        _horizontalTimer = 0.09f;
                }
            }
            else
            {
                _horizontalDirection = 0;
            }

            if (vertical != 0)
            {
                if (vertical != _verticalDirection)
                {
                    _verticalDirection = vertical;
                    _verticalTimer = 0.34f;
                }
                else
                {
                    _verticalTimer -= elapsedSeconds;
                    if (_verticalTimer > 0)
                        vertical = 0;
                    else
                        _verticalTimer = 0.09f;
                }
            }
            else
            {
                _verticalDirection = 0;
            }

            _hasDirectionTimer = true;

            bool confirm = Pressed(Keys.Enter) || Pressed(Keys.Space) || Pressed(Keys.Z) || Pressed(Keys.X) ||
                           PadPressed(Buttons.A) || PadPressed(Buttons.Start) || pointerClicked;
            bool back = Pressed(Keys.Escape) || Pressed(Keys.Back) || PadPressed(Buttons.B) || PadPressed(Buttons.Back);
            bool pause = Pressed(Keys.P) || PadPressed(Buttons.Y);
            bool highScores = Pressed(Keys.H);
            bool clear = Pressed(Keys.C);

            bool fire = Held(Keys.Space) || Held(Keys.Z) || Held(Keys.X) || Held(Keys.LeftControl) ||
                        _pad.Buttons.A == ButtonState.Pressed || _pad.Buttons.X == ButtonState.Pressed ||
                        _pad.Triggers.Right > 0.2f;
            bool firePressed = Pressed(Keys.Space) || Pressed(Keys.Z) || Pressed(Keys.X) || Pressed(Keys.LeftControl) ||
                               PadPressed(Buttons.A) || PadPressed(Buttons.X);

            var player = new InputState(heldLeft, heldRight, heldUp, heldDown, fire, pause, firePressed);

            _published.Clear();
            _published.AddRange(_touches);

            Current = new GameInput(horizontal, vertical, confirm, back, pause, highScores, clear, pointer,
                pointerMoved, pointerClicked, player, _published);
        }

        private void UpdateTouches(Rectangle destination)
        {
            _touches.Clear();
            var current = new HashSet<int>();
            TouchCollection state = TouchPanel.GetState();

            foreach (TouchLocation location in state)
            {
                if (location.State == TouchLocationState.Invalid)
                    continue;

                current.Add(location.Id);
                _touches.Add(new TouchContact(location.Id,
                    ScreenToVirtual(location.Position, destination),
                    !_previousTouches.Contains(location.Id)));
            }

            _previousTouches.Clear();
            foreach (int id in current)
                _previousTouches.Add(id);
        }

        /// <summary>Maps a back-buffer point onto the 1280x720 virtual canvas.</summary>
        public static Vector2 ScreenToVirtual(Vector2 point, Rectangle destination)
        {
            if (destination.Width <= 0 || destination.Height <= 0)
                return point;

            float scale = destination.Width / (float)Graphics.ScreenCanvas.VirtualWidth;
            return new Vector2((point.X - destination.X) / scale, (point.Y - destination.Y) / scale);
        }

        private bool Held(Keys key) => _keys.IsKeyDown(key);

        private bool Pressed(Keys key) => _keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

        private bool PadPressed(Buttons button) =>
            _pad.IsButtonDown(button) && _previousPad.IsButtonUp(button);

        private bool PadLeftStick(int direction) =>
            direction < 0
                ? _pad.ThumbSticks.Left.X < -0.6f && _previousPad.ThumbSticks.Left.X >= -0.6f
                : _pad.ThumbSticks.Left.X > 0.6f && _previousPad.ThumbSticks.Left.X <= 0.6f;

        private bool PadUpStick(int direction) =>
            direction < 0
                ? _pad.ThumbSticks.Left.Y > 0.6f && _previousPad.ThumbSticks.Left.Y <= 0.6f
                : _pad.ThumbSticks.Left.Y < -0.6f && _previousPad.ThumbSticks.Left.Y >= -0.6f;
    }
}
