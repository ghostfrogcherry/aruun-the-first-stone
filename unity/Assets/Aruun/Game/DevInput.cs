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
    public static class DevInput
    {
#if ENABLE_INPUT_SYSTEM
        static bool Held(Key k) { return Keyboard.current != null && Keyboard.current[k].isPressed; }
        static bool Pressed(Key k) { return Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame; }

        public static Vector2 Move { get { return new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0)); } }
        public static float Rise { get { return (Held(Key.Space) ? 1 : 0) - (Held(Key.LeftCtrl) ? 1 : 0); } }
        public static bool Fast { get { return Held(Key.LeftShift); } }
        public static Vector2 Look { get { return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero; } }
        public static bool DigDown { get { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; } }
        public static bool PlaceDown { get { return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame; } }
        public static bool EscapeDown { get { return Pressed(Key.Escape); } }
        public static bool OverlayDown { get { return Pressed(Key.F3); } }
        public static bool SaveDown { get { return Pressed(Key.F5); } }
        public static int NumberDown
        {
            get
            {
                for (int i = 0; i < 9; i++) if (Pressed(UnityEngine.InputSystem.Key.Digit1 + i)) return i;
                return -1;
            }
        }
#else
        public static Vector2 Move { get { return new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0)); } }
        public static float Rise { get { return (Input.GetKey(KeyCode.Space) ? 1 : 0) - (Input.GetKey(KeyCode.LeftControl) ? 1 : 0); } }
        public static bool Fast { get { return Input.GetKey(KeyCode.LeftShift); } }
        public static Vector2 Look { get { return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")); } }
        public static bool DigDown { get { return Input.GetMouseButtonDown(0); } }
        public static bool PlaceDown { get { return Input.GetMouseButtonDown(1); } }
        public static bool EscapeDown { get { return Input.GetKeyDown(KeyCode.Escape); } }
        public static bool OverlayDown { get { return Input.GetKeyDown(KeyCode.F3); } }
        public static bool SaveDown { get { return Input.GetKeyDown(KeyCode.F5); } }
        public static int NumberDown
        {
            get
            {
                for (int i = 0; i < 9; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
                return -1;
            }
        }
#endif
    }
}
