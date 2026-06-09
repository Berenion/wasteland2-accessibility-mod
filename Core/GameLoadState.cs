using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Tracks the most recent map/level load so announcement code can suppress the
    /// burst of events the game fires while a level initializes. The most audible
    /// case is inventory: a new game populates every ranger's starting loadout, and
    /// loading a save bulk-restores all carried items — both flood the screen reader
    /// with "Added X" lines the player never asked for.
    ///
    /// Driven by the FOWSystem.LoadMap postfix (the same hook FOWHelper uses), which
    /// fires inside the level-load coroutine right where inventories get populated.
    /// </summary>
    public static class GameLoadState
    {
        /// <summary>
        /// Realtime (wall-clock, so it advances even while the game is paused or a
        /// load frame hangs) grace window after a load during which item adds are
        /// treated as load noise rather than user-initiated pickups.
        /// </summary>
        private const float InventoryLoadGraceSeconds = 3f;

        private static float lastLoadRealtime = float.NegativeInfinity;

        /// <summary>Called from the FOWSystem.LoadMap postfix when a map finishes loading.</summary>
        public static void NotifyMapLoaded()
        {
            lastLoadRealtime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// True while we're within the post-load grace window — i.e. an item add is
        /// most likely the game populating inventories on load rather than the player
        /// picking something up.
        /// </summary>
        public static bool IsBulkInventoryWindow()
        {
            if (lastLoadRealtime == float.NegativeInfinity) return false;
            return (Time.realtimeSinceStartup - lastLoadRealtime) < InventoryLoadGraceSeconds;
        }
    }
}
