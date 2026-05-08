File: Patches/CombatMovementPatches.cs — Announces enemy movement during combat by capturing start position in StartedMoving and reading the move aloud in FinishedMoving.

namespace Wasteland2AccessibilityMod.Patches  (line 7)

// Records each mob's starting grid position keyed by instance; consumed by FinishedMoving.
[HarmonyPatch(typeof(Mob), "StartedMoving")]
class Mob_StartedMoving_Patch  (line 16)
    public static readonly Dictionary<Mob, Vector3> MoveStart  (line 19)

    [HarmonyPostfix]
    public static void Postfix(Mob __instance)  (line 21)
        // note: skips PC instances and mobs outside combat; stores currentSquare.id.

// Announces "X moves from A to B" after a mob finishes moving; respects FOW visibility.
[HarmonyPatch(typeof(Mob), "FinishedMoving", new[] { typeof(int) })]
class Mob_FinishedMoving_Patch  (line 41)
    [HarmonyPostfix]
    public static void Postfix(Mob __instance)  (line 43)
        // note: skips if destination is in fog of war; adds announcement to CombatLog.

    private static string GetMobName(Mob mob)  (line 86)
        // note: localizes template displayName; falls back to GameObject name.

    private static string FormatCoords(Vector3 id)  (line 98)
        // note: outputs "x, z" or "x, z, floor N" for multi-floor maps.
