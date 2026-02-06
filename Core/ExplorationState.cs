using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Handles keyboard input during exploration mode.
    /// Provides interactable cycling ([ ]), category filtering (PageUp/Down),
    /// announcement repeat (\), direction toggle (=), and scrap query (').
    /// Lowest priority (10) - only handles input when no menus or cursor are active.
    /// </summary>
    public class ExplorationState : IAccessibilityState
    {
        public string Name => "Exploration";
        public int Priority => 10;

        public bool IsActive
        {
            get
            {
                // Must have game instance
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;

                Game game = MonoBehaviourSingleton<Game>.GetInstance();

                // Only active during gameplay or random encounters
                if (game.state != GameState.Gameplay && game.state != GameState.RandomEncounter)
                    return false;

                // Not during conversations or cutscenes
                if (Drama.isConversationOn || Drama.isCutsceneOn) return false;

                // Not during combat
                if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                    MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                    return false;

                // Not when menus are open
                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
                    return false;

                // Not when input is frozen
                if (MonoBehaviourSingleton<InputManager>.HasInstance() &&
                    MonoBehaviourSingleton<InputManager>.GetInstance().IsInputFrozen())
                    return false;

                return true;
            }
        }

        public bool HandleInput()
        {
            // Camera lock toggle (F10) - works in exploration mode
            if (Input.GetKeyDown(KeyCode.F10))
            {
                CameraLock.Toggle();
                return true;
            }

            // Next interactable (])
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.CycleNext();
                return true;
            }

            // Previous interactable ([)
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.CyclePrevious();
                return true;
            }

            // Repeat last announcement (\)
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.RepeatLastAnnouncement();
                return true;
            }

            // Toggle direction format (=)
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                ModConfig.ToggleClockPositions();
                return true;
            }

            // Announce party scrap (')
            if (Input.GetKeyDown(KeyCode.Quote))
            {
                AnnouncePartyScrap();
                return true;
            }

            // Next category (Page Down)
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                NavigationManager.NextCategory();
                return true;
            }

            // Previous category (Page Up)
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                NavigationManager.PreviousCategory();
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            // No special activation behavior needed
        }

        public void OnDeactivated()
        {
            // No special deactivation behavior needed
        }

        private static void AnnouncePartyScrap()
        {
            try
            {
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

                int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
                string announcement = $"{scrap} scrap";

                MelonLogger.Msg($"Announcing party scrap: {scrap}");
                ScreenReaderManager.Speak(announcement, interrupt: true);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error announcing scrap: {ex.Message}");
            }
        }
    }
}
