using HarmonyLib;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Static flags controlling whether game input processing should be suppressed.
    /// Set by accessibility states during HandleInput, checked by Harmony PREFIX patches.
    /// Reset each frame by InputRouter.ProcessInput().
    /// </summary>
    public static class InputSuppressor
    {
        /// <summary>
        /// When true, InputManager.Update() is skipped entirely.
        /// Set this when a menu state (conversation, inventory, dialog) is handling arrow keys.
        /// </summary>
        public static bool ShouldSuppressGameInput { get; set; }

        /// <summary>
        /// When true, UICamera.ProcessOthers() is skipped.
        /// Prevents NGUI from processing arrow keys / D-Pad for UI navigation
        /// when accessibility states are using those keys.
        /// </summary>
        public static bool ShouldSuppressUINavigation { get; set; }

        /// <summary>
        /// When true, EventManager.Update() is skipped entirely.
        /// Prevents button events (Enter="Attack Current Target", Escape="Back")
        /// from being dispatched to GUIScreens via their registered buttonDownEventHandlers.
        /// </summary>
        public static bool ShouldSuppressButtonEvents { get; set; }

        /// <summary>
        /// Reset all suppression flags. Called at the start of each frame
        /// by InputRouter.ProcessInput().
        /// </summary>
        public static void Reset()
        {
            ShouldSuppressGameInput = false;
            ShouldSuppressUINavigation = false;
            ShouldSuppressButtonEvents = false;
        }
    }

    /// <summary>
    /// PREFIX patch on InputManager.Update() - skips the game's entire input
    /// processing when an accessibility menu state is handling input.
    /// </summary>
    [HarmonyPatch(typeof(InputManager), "Update")]
    public class InputManager_Update_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (InputSuppressor.ShouldSuppressGameInput)
            {
                return false; // Skip original InputManager.Update()
            }
            return true; // Let original run
        }
    }

    /// <summary>
    /// PREFIX patch on UICamera.ProcessOthers() - prevents NGUI from processing
    /// arrow keys and D-Pad input when accessibility states are using those keys.
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "ProcessOthers")]
    public class UICamera_ProcessOthers_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (InputSuppressor.ShouldSuppressUINavigation)
            {
                return false; // Skip NGUI navigation processing
            }
            return true; // Let original run
        }
    }

    /// <summary>
    /// PREFIX patch on EventManager.Update() - prevents button events (Enter, Escape, etc.)
    /// from being dispatched to GUIScreens when accessibility states are handling input.
    /// EventManager.Update() uses cInput.GetButtonDown() to detect key presses and dispatches
    /// them to registered buttonDownEventHandlers (GUIScreen.OnButtonDown), causing double
    /// processing when our states already handle these keys via Input.GetKeyDown().
    /// </summary>
    [HarmonyPatch(typeof(EventManager), "Update")]
    public class EventManager_Update_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (InputSuppressor.ShouldSuppressButtonEvents)
            {
                return false; // Skip EventManager button dispatch
            }
            return true; // Let original run
        }
    }
}
