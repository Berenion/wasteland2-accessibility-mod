using MelonLoader;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Manages tactical pause state. Freezes game time (Time.timeScale = 0) via
    /// Game.Pause()/Resume() while keeping mod input active, allowing the player
    /// to move the review cursor and queue orders on frozen enemies.
    /// </summary>
    public static class TacticalPauseManager
    {
        public static bool IsPaused { get; private set; }

        public static void TogglePause()
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }

        private static void Pause()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            MonoBehaviourSingleton<Game>.GetInstance().Pause();
            IsPaused = true;

            ScreenReaderManager.SpeakInterrupt("Tactical pause");
            MelonLogger.Msg("[TacticalPause] Paused");
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
        /// to prevent getting stuck in a paused state.
        /// </summary>
        public static void ForceResumeIfPaused()
        {
            if (!IsPaused) return;

            if (MonoBehaviourSingleton<Game>.HasInstance())
                MonoBehaviourSingleton<Game>.GetInstance().Resume();

            IsPaused = false;
            MelonLogger.Msg("[TacticalPause] Force-resumed (context changed)");
        }
    }
}
