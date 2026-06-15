using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Unified input routing system. Called every frame from MelonMod.OnUpdate()
    /// (which runs BEFORE Unity's Update methods). Routes keyboard input to the
    /// highest-priority active accessibility state.
    /// </summary>
    public static class InputRouter
    {
        // States sorted by priority (highest first)
        private static readonly List<IAccessibilityState> states = new List<IAccessibilityState>();

        // Tracks whether input was consumed this frame
        public static bool InputConsumedThisFrame { get; private set; }

        // Tracks previous active state of each registered state for activation/deactivation callbacks
        private static readonly Dictionary<IAccessibilityState, bool> previousActiveState =
            new Dictionary<IAccessibilityState, bool>();

        /// <summary>
        /// Register a state with the input router. States are automatically
        /// sorted by priority (highest first).
        /// </summary>
        public static void Register(IAccessibilityState state)
        {
            states.Add(state);
            previousActiveState[state] = false;

            // Sort by priority descending (highest priority first)
            states.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            ModLog.Debug($"[InputRouter] Registered state: {state.Name} (priority {state.Priority})");
        }

        /// <summary>
        /// Called every frame from OnUpdate. Processes input through the priority chain.
        /// Must be called BEFORE game input processing (OnUpdate runs before Unity Update).
        /// </summary>
        public static void ProcessInput()
        {
            InputConsumedThisFrame = false;

            // Reset suppression flags each frame
            InputSuppressor.Reset();

            // During a cutscene or movie, step out of the way entirely so the game's
            // native skip path (MoviePlayerCamera.OnButtonDown, HUD_Controller.OnButtonDown)
            // receives Enter/Escape. We still fire deactivation callbacks below so
            // states that were active a frame ago clean up properly.
            bool cutsceneActive = CutsceneDetector.IsActive;

            // Global help key (Shift+/, i.e. "?"): read back the controls for whichever
            // context currently owns input. Detected once per frame; consumed by the
            // highest-priority active state below so it pre-empts that state's own keys.
            bool helpRequested = !cutsceneActive && HelpKeyPressed();
            bool helpHandled = false;

            // Global settings-menu toggle (Shift+S): open/close the modal mod-settings menu
            // regardless of which context owns input, mirroring the help key. Skipped while a
            // text field is being typed into (keyword/password box, save-name field) so the
            // capital S goes to the field instead of hijacking the keystroke.
            if (!cutsceneActive && SettingsKeyPressed() && !IsTextEntryActive())
            {
                SettingsMenuState.Toggle();
                // Consume the key so no state's HandleInput also processes it this frame.
                // Activation/deactivation callbacks still fire in the loop below.
                InputConsumedThisFrame = true;
            }

            for (int i = 0; i < states.Count; i++)
            {
                IAccessibilityState state = states[i];
                bool currentlyActive = !cutsceneActive && state.IsActive;
                bool wasActive = previousActiveState[state];

                // Fire activation/deactivation callbacks
                if (currentlyActive && !wasActive)
                {
                    state.OnActivated();
                }
                else if (!currentlyActive && wasActive)
                {
                    state.OnDeactivated();
                }

                previousActiveState[state] = currentlyActive;

                // Route input to highest-priority active state
                if (currentlyActive && !InputConsumedThisFrame)
                {
                    if (helpRequested)
                    {
                        ScreenReaderManager.SpeakInterrupt(state.GetHelpText());
                        InputConsumedThisFrame = true;
                        helpHandled = true;
                    }
                    else if (state.HandleInput())
                    {
                        InputConsumedThisFrame = true;
                        // Don't break - we still need to track activation state for remaining states
                    }
                }
            }

            // Help asked for but no state owned input this frame: give a generic answer
            // rather than swallowing the key silently.
            if (helpRequested && !helpHandled)
            {
                ScreenReaderManager.SpeakInterrupt(
                    "Press the help key in any menu or cursor to hear its controls. " +
                    "No accessibility context is active right now.");
                InputConsumedThisFrame = true;
            }
        }

        /// <summary>
        /// True on the frame the global help key (Shift+/) is pressed. Accepts both the
        /// main-row slash and the numpad divide key: on many non-US layouts "/" is only on
        /// the numpad (KeypadDivide), so checking KeyCode.Slash alone misses those keyboards.
        /// </summary>
        private static bool HelpKeyPressed()
        {
            if (!Input.GetKeyDown(KeyCode.Slash) && !Input.GetKeyDown(KeyCode.KeypadDivide)) return false;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>
        /// True on the frame the global settings key (Shift+S) is pressed.
        /// </summary>
        private static bool SettingsKeyPressed()
        {
            if (!Input.GetKeyDown(KeyCode.S)) return false;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>
        /// True when a mod text field is currently capturing typed characters (the
        /// conversation keyword/password box or the save-name field). The global
        /// settings hotkey defers to these so a typed capital S lands in the field.
        /// </summary>
        private static bool IsTextEntryActive()
        {
            return KeywordEntryState.blockUIInput || GenericMenuState.IsEditingTextField;
        }

        /// <summary>
        /// Check if any accessibility state is currently active.
        /// Used by UIFocusPatches to suppress legacy focus announcements
        /// when refactored states are handling their own announcements.
        /// </summary>
        public static bool IsAnyStateActive()
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].IsActive)
                    return true;
            }
            return false;
        }
    }
}
