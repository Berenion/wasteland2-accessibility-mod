File: WorldMapProximityAlert.cs — proximity threshold alerts as the world map review cursor moves; tracks POI approach, radiation entry/exit, and path radiation warnings

namespace Wasteland2AccessibilityMod  (line 7)

// Manages proximity warnings as the review cursor moves across the world map; tracks which items have been announced at each threshold to avoid repeats
class WorldMapProximityAlert  (line 12)  [public static]

    private const float FAR_THRESHOLD = 60f  (line 14)
    private const float MEDIUM_THRESHOLD = 30f  (line 15)
    private const float NEAR_THRESHOLD = 15f  (line 16)
    private const float RESET_DISTANCE = 80f  (line 19)

    private static Dictionary<int, float> announcedPOIs  (line 23)  // key=instance ID, value=closest threshold already announced
    private static HashSet<int> insideRadiationClouds  (line 26)
    private static HashSet<int> insideDiscoveryBoundary  (line 27)
    private static HashSet<int> insideEncounterZones  (line 30)

    // Check proximity to all known objects from the cursor position; returns combined announcement string or empty
    public static string CheckProximity(Vector3 cursorPosition)  (line 37)
        // note: calls CheckPOIProximity + CheckRadiationProximity; skips encounter zones (not visible to sighted players); calls CleanupDistantEntries

    // Check if a path (NavMesh corners) crosses any radiation clouds; returns warning string or empty
    public static string CheckPathForRadiation(Vector3[] pathCorners)  (line 67)
        // note: tests each segment via DoesSegmentCrossCloud; returns the highest radiation level found with direction from path start

    // Check if a specific point is inside any radiation cloud; returns radiation level (0 = none)
    public static int GetRadiationLevelAtPoint(Vector3 point)  (line 106)

    // Reset all tracking state; call when the world map state is deactivated
    public static void Reset()  (line 127)

    private static string CheckPOIProximity(Vector3 cursorPosition)  (line 135)
        // note: announces only the single closest POI that crossed a new threshold; uses announcedPOIs to suppress repeats; appends "within reach" at NEAR_THRESHOLD

    private static string CheckRadiationProximity(Vector3 cursorPosition)  (line 205)
        // note: tracks entry/exit of cloud AABB and expanded discovery boundary (1.5x size); announces "Entering"/"Leaving" on transitions

    private static string CheckEncounterZoneProximity(Vector3 cursorPosition)  (line 262)
        // note: method exists but is NOT called from CheckProximity — encounter zones excluded because they are editor-only gizmos invisible to sighted players

    private static void CleanupDistantEntries(Vector3 cursorPosition)  (line 295)
        // note: removes POIs from announcedPOIs dictionary when distance > RESET_DISTANCE so thresholds re-trigger if cursor returns

    private static bool IsPointInCloud(Vector3 point, WorldMapRadiationCloud cloud)  (line 337)
        // note: AABB test on X/Z plane using cloud.size (Vector2)

    private static bool IsPointInDiscoveryBoundary(Vector3 point, WorldMapRadiationCloud cloud)  (line 347)
        // note: same AABB but cloud.size scaled 1.5x in both axes

    private static bool IsPointInEncounterZone(Vector3 point, WorldMapRandomEncounterZone zone)  (line 359)

    // Line-segment vs AABB intersection test on the X/Z plane (Liang-Barsky algorithm)
    public static bool DoesSegmentCrossCloud(Vector3 a, Vector3 b, WorldMapRadiationCloud cloud)  (line 373)
        // note: first checks endpoint containment, then Liang-Barsky parametric clip on X and Z slabs separately

    private static float Vector2Distance(Vector3 a, Vector3 b)  (line 428)
