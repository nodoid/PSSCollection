namespace PSSCollection.Engine
{
    /// <summary>
    /// One frame of input, already mapped from whatever the platform offers
    /// (keyboard, gamepad, touch pad or tilt).
    /// </summary>
    public readonly struct InputState
    {
        public InputState(bool left, bool right, bool up, bool down, bool fire, bool pause, bool firePressed)
        {
            Left = left;
            Right = right;
            Up = up;
            Down = down;
            Fire = fire;
            Pause = pause;
            FirePressed = firePressed;
        }

        public bool Left { get; }
        public bool Right { get; }
        public bool Up { get; }
        public bool Down { get; }
        public bool Fire { get; }

        /// <summary>True only on the frame the fire button went down.</summary>
        public bool FirePressed { get; }

        /// <summary>True only on the frame that asked to pause.</summary>
        public bool Pause { get; }

        public bool AnyDirection => Left || Right || Up || Down;

        public static InputState None => default;

        public static InputState Move(bool left, bool right, bool up = false, bool down = false) =>
            new InputState(left, right, up, down, false, false, false);

        public InputState WithFire(bool fire) => new InputState(Left, Right, Up, Down, fire, Pause, fire);

        public override string ToString() =>
            $"L={Left} R={Right} U={Up} D={Down} F={Fire} P={Pause}";
    }
}
