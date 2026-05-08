File: Core/TacticalPauseManager.cs — manages a mod-owned tactical pause via Game.Pause()/Resume(), guarding against stacking on top of existing game pauses.

namespace Wasteland2AccessibilityMod.Core  (line 4)

static class TacticalPauseManager  (line 11)
    // Freezes game time via Game.Pause()/Resume(); refuses to pause if the game is already paused (e.g. during scene loads) to avoid unwinding pauseCounter issues.

    public static bool IsPaused { get; private set; }  (line 12)

    public static void TogglePause()  (line 14)
    private static void Pause()  (line 22)
        // note: checks game.IsPaused() before calling game.Pause(); if already paused (pauseCounter > 0), announces "Still loading" and returns without stacking another pause — avoids permanent freeze from counter overflow.
    private static void Resume()  (line 47)

    // Force-resumes if paused; called when entering menus, combat, etc. to prevent getting stuck.
    public static void ForceResumeIfPaused()  (line 62)
