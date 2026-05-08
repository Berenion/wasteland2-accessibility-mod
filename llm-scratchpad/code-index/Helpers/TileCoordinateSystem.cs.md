File: Helpers/TileCoordinateSystem.cs — converts world positions to/from combat grid tiles using CombatAStar.fullMap via reflection; provides distance and range text in tiles or meters.

namespace Wasteland2AccessibilityMod.Helpers  (line 7)

static class TileCoordinateSystem  (line 9)
    // Reflects CombatAStar.fullMap (private Dictionary<Vector3, CombatAStarNode>) and caches it per CombatAStar instance; falls back to world-distance / SquareSize when grid is unavailable.

    public const float SquareSize = 1.6f  (line 10)

    private static FieldInfo fullMapField  (line 12)
    private static Dictionary<Vector3, CombatAStarNode> cachedFullMap  (line 13)
    private static CombatAStar cachedInstance  (line 14)

    // True when CombatAStar is available and fullMap contains at least one node.
    public static bool IsGridAvailable { get; }  (line 16)

    // Returns the CombatAStarNode at worldPos; falls back to nearest node scan if exact id lookup misses (handles multi-floor scenes).
    public static CombatAStarNode GetTileAtPosition(Vector3 worldPos)  (line 24)
        // note: exact miss triggers O(n) scan over all nodes; returns null if nearest node is > SquareSize away.

    // Returns the node for a Mob; prefers mob.currentSquare during active combat.
    public static CombatAStarNode GetTileForMob(Mob mob)  (line 53)

    // Returns Chebyshev tile distance between two world positions; falls back to Euclidean / SquareSize.
    public static int GetTileDistance(Vector3 a, Vector3 b)  (line 67)
        // note: uses Mathf.Max(dx, dz) — Chebyshev distance, matching how the game counts movement tiles.

    // Returns a user-facing distance string ("N tile(s)" or "N meters") controlled by ModConfig.UseTileDistances.
    public static string GetDistanceText(Vector3 fromPos, Vector3 toPos)  (line 80)

    // Converts a float meter value to a tile count via SquareSize rounding.
    public static int MetersToTiles(float meters)  (line 92)

    // Returns a user-facing range string ("N tile(s)" or "N meters") controlled by ModConfig.UseTileDistances.
    public static string GetRangeText(float meters)  (line 97)

    // Reflects and caches CombatAStar.fullMap; invalidates cache when the CombatAStar instance changes.
    private static bool TryGetFullMap(out Dictionary<Vector3, CombatAStarNode> map)  (line 108)
        // note: uses reflection to access private "fullMap" field; logs error and returns false permanently if the field is not found.
