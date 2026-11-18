using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Aruun.Game
{
    /// <summary>Gameplay keys (not dev keys), read through whichever input system the project has on.</summary>
    public enum PlayKey { Bag, Interact, Guard, Dodge, Heavy, Speak, Confirm, Back, Journal, Skills, Shape }

    /// <summary>
    /// The player's gameplay input: bag, interact, guard, dodge, heavy attack, speaking words (X), holding the mouse
    /// buttons and the scroll wheel. Off whenever DevInput is off (screenshot runs, unfocused window).
    /// </summary>
    public static class PlayInput
    {
        static bool On { get { return DevInput.Enabled && Application.isFocused; } }

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] Keys = { Key.Tab, Key.E, Key.Q, Key.LeftAlt, Key.R, Key.X, Key.Enter, Key.Backspace, Key.L, Key.P, Key.C };
        public static bool Down(PlayKey k) { return On && Keyboard.current != null && Keyboard.current[Keys[(int)k]].wasPressedThisFrame; }
        public static bool Holding(PlayKey k) { return On && Keyboard.current != null && Keyboard.current[Keys[(int)k]].isPressed; }
        public static bool LeftHeld { get { return On && Mouse.current != null && Mouse.current.leftButton.isPressed; } }
        public static bool LeftDown { get { return On && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; } }
        public static bool RightDown { get { return On && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame; } }
        public static bool MiddleDown { get { return On && Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame; } }
        public static float Scroll { get { return On && Mouse.current != null ? Mathf.Sign(Mouse.current.scroll.ReadValue().y) * (Mathf.Abs(Mouse.current.scroll.ReadValue().y) > 0.01f ? 1 : 0) : 0f; } }
#else
        static readonly KeyCode[] Keys = { KeyCode.Tab, KeyCode.E, KeyCode.Q, KeyCode.LeftAlt, KeyCode.R, KeyCode.X, KeyCode.Return, KeyCode.Backspace, KeyCode.L, KeyCode.P, KeyCode.C };
        public static bool Down(PlayKey k) { return On && Input.GetKeyDown(Keys[(int)k]); }
        public static bool Holding(PlayKey k) { return On && Input.GetKey(Keys[(int)k]); }
        public static bool LeftHeld { get { return On && Input.GetMouseButton(0); } }
        public static bool LeftDown { get { return On && Input.GetMouseButtonDown(0); } }
        public static bool RightDown { get { return On && Input.GetMouseButtonDown(1); } }
        public static bool MiddleDown { get { return On && Input.GetMouseButtonDown(2); } }
        public static float Scroll { get { float s = On ? Input.mouseScrollDelta.y : 0f; return Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f; } }
#endif
    }
}
