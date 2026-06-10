using System;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Tracks whether a cutscene or movie is currently playing so the mod can
    /// step out of the way and let the game's native skip path handle Enter/Escape:
    ///   - MoviePlayerCamera.OnButtonDown cancels fullscreen movies (intro, epilogue)
    ///   - HUD_Controller.OnButtonDown opens the pause menu over drama cutscenes
    ///
    /// Movie state comes from the game's own EventInfo_MovieStarted/Ended events.
    /// Drama cutscene state is read directly from Drama.isCutsceneOn.
    /// </summary>
    public static class CutsceneDetector
    {
        private static bool isMoviePlaying;
        private static bool subscribed;

        public static bool IsActive
        {
            get
            {
                EnsureSubscribed();
                return isMoviePlaying || Drama.isCutsceneOn;
            }
        }

        private static void EnsureSubscribed()
        {
            if (subscribed) return;
            if (!MonoBehaviourSingleton<EventManager>.HasInstance()) return;

            try
            {
                var em = MonoBehaviourSingleton<EventManager>.GetInstance();
                em.Subscribe(typeof(EventInfo_MovieStarted), OnMovieStarted);
                em.Subscribe(typeof(EventInfo_MovieEnded), OnMovieEnded);
                subscribed = true;
                ModLog.Debug("[CutsceneDetector] Subscribed to movie events");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CutsceneDetector] Subscribe failed: {ex.Message}");
            }
        }

        private static void OnMovieStarted(EventInfoBase e)
        {
            isMoviePlaying = true;
            ModLog.Debug("[CutsceneDetector] Movie started — mod input bypassed");
        }

        private static void OnMovieEnded(EventInfoBase e)
        {
            isMoviePlaying = false;
            ModLog.Debug("[CutsceneDetector] Movie ended — mod input resumed");
        }
    }
}
