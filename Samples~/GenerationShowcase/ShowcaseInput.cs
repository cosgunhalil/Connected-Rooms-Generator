using UnityEngine;
#if CRG_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace CRG.Samples.GenerationShowcase
{
    // Keyboard and mouse through whichever input handling the project uses: the Input System package when the legacy
    // Input Manager is turned off, the Input Manager otherwise.
    internal static class ShowcaseInput
    {
        // key is a lowercase letter or ' ' for space.
        public static bool Pressed(char key)
        {
#if CRG_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current == null)
            {
                return false;
            }
            return Keyboard.current[key == ' ' ? Key.Space : Key.A + (key - 'a')].wasPressedThisFrame;
#else
            return Input.GetKeyDown(key == ' ' ? KeyCode.Space : KeyCode.A + (key - 'a'));
#endif
        }

        // Whether the left or right mouse button is held.
        public static bool Dragging()
        {
#if CRG_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed);
#else
            return Input.GetMouseButton(0) || Input.GetMouseButton(1);
#endif
        }

        // Mouse movement this frame, in pixels.
        public static Vector2 MouseDelta()
        {
#if CRG_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 20f;
#endif
        }

        // Scroll wheel movement this frame, in notches (positive away from the user).
        public static float Scroll()
        {
#if CRG_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }
    }
}
