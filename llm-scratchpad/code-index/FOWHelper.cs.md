File: FOWHelper.cs — shared fog-of-war visibility and teleporter activation tracking used across navigation features

namespace Wasteland2AccessibilityMod  (line 5)

class FOWHelper  (line 11)  [public static]
    // Wraps FOWSystem visibility checks and guards against the stale-buffer window after LoadMap.

    private const float NewTeleporterDetectionRange = 10f  (line 18)
    private static HashSet<InteractableNexus> seenInactive  (line 25)
    private static HashSet<InteractableNexus> recentlyActivated  (line 26)
    private static HashSet<InteractableNexus> knownTeleporters  (line 33)
    private const float LoadMapGraceSeconds = 1.0f  (line 44)
    private static float lastLoadMapRealtime  (line 45)
    private static bool sawUnpausedFrameSinceLoadMap  (line 46)

    public static bool IsVisibleThroughFOW(Vector3 position)  (line 48)

    // Returns true once FOWSystem has had real time and at least one unpaused frame since the last LoadMap call.
    public static bool IsFOWReady()  (line 60)
        // note: callers should bail out of scanning while false to avoid the stale-buffer window where all explored cells report visible

    // Called from the FOWSystem.LoadMap Harmony postfix; resets readiness state so next scan waits for FOW to converge.
    public static void NotifyFOWMapLoaded()  (line 71)

    // Per-frame tick; records whether we've seen an unpaused frame since last LoadMap (FOW UpdateBuffer requires Time.timeScale > 0).
    public static void Tick()  (line 84)

    private static bool IsNearParty(Vector3 position, float range)  (line 90)

    private static float lastTrackDiagTime  (line 111)

    // Call periodically to track teleporter nexuses that transition from inactive to active (e.g. perception-revealed ShortcutDoors).
    public static void UpdateActivationTracking()  (line 113)
        // note: iterates InteractableNexus.interactables; emits verbose diagnostics every 5s; populates recentlyActivated for IsPerceptionGated

    // Clears activation tracking state; call on scene/map change.
    public static void ClearActivationTracking()  (line 164)

    // Directly marks a nexus as recently activated, bypassing the teleporter destination filter in IsPerceptionGated.
    public static void MarkAsRecentlyActivated(InteractableNexus nexus)  (line 176)

    // Returns true if the interactable should be filtered out (perception-gated or unexplored-teleporter destination).
    public static bool IsPerceptionGated(InteractableNexus nexus)  (line 202)
        // note: two independent filter cases — (1) skob.difficulty > None and not yet perceived; (2) teleporter dest > 100 units away in unexplored FOW, unless recentlyActivated
