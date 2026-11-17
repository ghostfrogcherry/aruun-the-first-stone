using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Aruun.Game
{
    /// <summary>
    /// Keyboard and mouse for the development build, read through whichever input system the
    /// project has switched on. Real, rebindable player input (and controllers) replaces this.
    /// </summary>
    /// <summary>One-press dev keys. They always work, with no menu open.</summary>
    public enum DevKey { Fly, Map, Place, Home, BigDig, Faster, Slower, Screenshot, Help, ThirdPerson, Jump, Shift }

    public static class DevInput
    {
        /// <summary>False in screenshot runs, so stray input can never move the camera.</summary>
        public static bool Enabled = true;
        /// <summary>Input only counts while the game window has focus.</summary>
        static bool On { get { return Enabled && Application.isFocused; } }

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] DevKeys = { Key.F, Key.M, Key.T, Key.H, Key.B, Key.Equals, Key.Minus, Key.F2, Key.F1, Key.V, Key.Space, Key.LeftShift };
        public static bool Down(DevKey k) { return On && Pressed(DevKeys[(int)k]); }
        /// <summary>Any key by its legacy KeyCode name (letters, F1-F12, Equals, Minus...).</summary>
        public static bool KeyDown(KeyCode code)
        {
            Key k;
            string name = code.ToString();
            if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
            return On && System.Enum.TryParse(name, out k) && Pressed(k);
        }
        public static bool ShiftHeld { get { return Held(Key.LeftShift) || Held(Key.RightShift); } }
        public static bool Holding(DevKey k) { return On && Held(DevKeys[(int)k]); }
        public static bool ClickDown { get { return On && (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame); } }
        static bool Held(Key k) { return Keyboard.current != null && Keyboard.current[k].isPressed; }
        static bool Pressed(Key k) { return Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame; }

        public static Vector2 Move { get { return On ? new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0)) : Vector2.zero; } }
        public static float Rise { get { return On ? (Held(Key.Space) ? 1 : 0) - (Held(Key.LeftCtrl) ? 1 : 0) : 0f; } }
        public static bool Fast { get { return On && (Held(Key.LeftShift)); } }
        public static Vector2 Look { get { return On ? Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero : Vector2.zero; } }
        public static bool DigDown { get { return On && (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame); } }
        public static bool PlaceDown { get { return On && (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame); } }
        public static bool EscapeDown { get { return On && (Pressed(Key.Escape)); } }
        public static bool OverlayDown { get { return On && (Pressed(Key.F3)); } }
        public static bool SaveDown { get { return On && (Pressed(Key.F5)); } }
        public static int NumberDown
        {
            get
            {
                if (!On) return -1;
                for (int i = 0; i < 9; i++) if (Pressed(UnityEngine.InputSystem.Key.Digit1 + i)) return i;
                return -1;
            }
        }
#else
        static readonly KeyCode[] DevKeys = { KeyCode.F, KeyCode.M, KeyCode.T, KeyCode.H, KeyCode.B, KeyCode.Equals, KeyCode.Minus, KeyCode.F2, KeyCode.F1, KeyCode.V, KeyCode.Space, KeyCode.LeftShift };
        public static bool Down(DevKey k) { return On && Input.GetKeyDown(DevKeys[(int)k]); }
        public static bool KeyDown(KeyCode code) { return On && Input.GetKeyDown(code); }
        public static bool ShiftHeld { get { return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); } }
        public static bool Holding(DevKey k) { return On && Input.GetKey(DevKeys[(int)k]); }
        public static Vector2 Move { get { return On ? new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)) : Vector2.zero; } }
        public static float Rise { get { return On ? (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) ? 1 : 0) : 0f; } }
        public static bool Fast { get { return On && (Input.GetKey(KeyCode.LeftShift)); } }
        public static Vector2 Look { get { return On ? new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) : Vector2.zero; } }
        public static bool DigDown { get { return On && (Input.GetMouseButtonDown(0)); } }
        public static bool PlaceDown { get { return On && (Input.GetMouseButtonDown(1)); } }
        public static bool EscapeDown { get { return On && (Input.GetKeyDown(KeyCode.Escape)); } }
        public static bool OverlayDown { get { return On && (Input.GetKeyDown(KeyCode.F3)); } }
        public static bool SaveDown { get { return On && (Input.GetKeyDown(KeyCode.F5)); } }
        public static int NumberDown
        {
            get
            {
                if (!On) return -1;
                for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
                return -1;
            }
        }
#endif
    }
}
