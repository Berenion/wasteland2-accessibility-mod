using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

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

        // Tracks which keys were consumed this frame for selective suppression
        private static readonly HashSet<KeyCode> consumedKeys = new HashSet<KeyCode>();

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

            MelonLogger.Msg($"[InputRouter] Registered state: {state.Name} (priority {state.Priority})");
        }

        /// <summary>
        /// Called every frame from OnUpdate. Processes input through the priority chain.
        /// Must be called BEFORE game input processing (OnUpdate runs before Unity Update).
        /// </summary>
        public static void ProcessInput()
        {
            InputConsumedThisFrame = false;
            consumedKeys.Clear();

            // Reset suppression flags each frame
            InputSuppressor.Reset();

            // During a cutscene or movie, step out of the way entirely so the game's
            // native skip path (MoviePlayerCamera.OnButtonDown, HUD_Controller.OnButtonDown)
            // receives Enter/Escape. We still fire deactivation callbacks below so
            // states that were active a frame ago clean up properly.
            bool cutsceneActive = CutsceneDetector.IsActive;

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
                    if (state.HandleInput())
                    {
                        InputConsumedThisFrame = true;
                        // Don't break - we still need to track activation state for remaining states
                    }
                }
            }
        }

        /// <summary>
        /// Mark a specific key as consumed this frame.
        /// Called by state HandleInput implementations.
        /// </summary>
        public static void MarkKeyConsumed(KeyCode key)
        {
            consumedKeys.Add(key);
        }

        /// <summary>
        /// Check if a specific key was consumed by an accessibility state this frame.
        /// </summary>
        public static bool WasKeyConsumed(KeyCode key)
        {
            return consumedKeys.Contains(key);
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

        /// <summary>
        /// Check if any accessibility state that needs input suppression is currently active.
        /// Used by InputSuppressor to decide whether to block game input.
        /// </summary>
        public static bool IsAnyMenuStateActive()
        {
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i].IsActive && states[i].Priority >= 30)
                    return true;
            }
            return false;
        }
    }
}
