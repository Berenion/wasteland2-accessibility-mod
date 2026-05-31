using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Manages tactical pause state. Freezes game time (Time.timeScale = 0) via
    /// Game.Pause()/Resume() while keeping mod input active, allowing the player
    /// to move the review cursor and queue orders on frozen enemies.
    ///
    /// Also auto-pauses while an inventory-style screen is open so screen-reader
    /// users can manage items without being attacked. The auto-pause is reversed
    /// when the screen closes — unless the user had manually paused before opening
    /// it (in which case the pause persists and the user resumes with Space).
    /// </summary>
    public static class TacticalPauseManager
    {
        public static bool IsPaused { get; private set; }

        // True when WE auto-paused for an open inventory/shop screen. Lets us tell
        // "menu safety pause" apart from "user pressed Space" so we know whether
        // to auto-resume on menu close.
        private static bool menuSafetyPaused;

        public static void TogglePause()
        {
            if (IsPaused)
            {
                // Manual unpause overrides any safety auto-pause — clear the flag
                // so the per-frame Tick doesn't immediately re-pause us.
                menuSafetyPaused = false;
                Resume();
            }
            else
            {
                if (TryPause())
                    menuSafetyPaused = false;
            }
        }

        /// <summary>
        /// Per-frame maintenance. Auto-pauses while a safety menu is open and
        /// auto-resumes once it closes (only if we were the ones who paused).
        /// </summary>
        public static void Tick()
        {
            bool safetyMenuOpen = IsSafetyMenuOpen();

            if (safetyMenuOpen)
            {
                if (!IsPaused && !menuSafetyPaused)
                {
                    if (TryPause())
                    {
                        menuSafetyPaused = true;
                        MelonLogger.Msg("[TacticalPause] Auto-paused for inventory menu safety");
                    }
                }
            }
            else if (menuSafetyPaused)
            {
                if (IsPaused)
                {
                    Resume();
                    MelonLogger.Msg("[TacticalPause] Auto-resumed (inventory menu closed)");
                }
                menuSafetyPaused = false;
            }
        }

        /// <summary>
        /// True when an inventory/loot/vendor screen is open and the player is
        /// managing items — these are the cases we auto-pause for.
        /// </summary>
        public static bool IsSafetyMenuOpen()
        {
            // Combat is turn-based — Time.timeScale freeze is meaningless and may
            // interfere with combat-end transitions. Skip auto-pause here.
            if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                return false;

            // Loot containers (most dangerous — player standing over a body)
            var popupInv = Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv != null && popupInv.gameObject.activeInHierarchy)
                return true;

            // Player inventory / character info screen
            var charInfo = Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfo != null && charInfo.gameObject.activeInHierarchy)
                return true;

            // Vendor trading screen
            if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                MonoBehaviourSingleton<GUIManager>.GetInstance().IsVendorScreenOpen())
                return true;

            return false;
        }

        private static bool TryPause()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;

            Game game = MonoBehaviourSingleton<Game>.GetInstance();

            // Game.Pause() is reference-counted via pauseCounter. During scene loads
            // the game calls Pause(forLoad: true) which sets pauseCounter = 9999.
            // If we stack our own Pause() on top, the counter never unwinds back to 0
            // and Time.timeScale stays frozen forever — stranding every mod hotkey.
            // Refuse to pause while the game is already paused by something else.
            if (game.IsPaused())
            {
                ScreenReaderManager.SpeakInterrupt("Still loading, try again");
                MelonLogger.Msg("[TacticalPause] Refused — game already paused (pauseCounter>0)");
                return false;
            }

            game.Pause();
            IsPaused = true;
            ScreenReaderManager.SpeakInterrupt("Tactical pause");
            MelonLogger.Msg("[TacticalPause] Paused");
            return true;
        }

        private static void Resume()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            MonoBehaviourSingleton<Game>.GetInstance().Resume();
            IsPaused = false;

            ScreenReaderManager.SpeakInterrupt("Resumed");
            MelonLogger.Msg("[TacticalPause] Resumed");
        }

        /// <summary>
        /// Force-resume if paused. Called when entering menus, combat, etc.
        /// to prevent getting stuck in a paused state. Skipped automatically
        /// when a safety menu is open — auto-pause should hold across the menu.
        /// </summary>
        public static void ForceResumeIfPaused()
        {
            if (!IsPaused) return;
            if (IsSafetyMenuOpen()) return;

            if (MonoBehaviourSingleton<Game>.HasInstance())
                MonoBehaviourSingleton<Game>.GetInstance().Resume();

            IsPaused = false;
            menuSafetyPaused = false;
            MelonLogger.Msg("[TacticalPause] Force-resumed (context changed)");
        }
    }
}
