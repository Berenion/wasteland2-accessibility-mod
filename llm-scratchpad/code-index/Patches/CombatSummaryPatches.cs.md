File: Patches/CombatSummaryPatches.cs — Reads the end-of-combat XP summary aloud before the game's animated type-out begins; uses a Prefix because the original removes entries from the kill list.

namespace Wasteland2AccessibilityMod.Patches  (line 7)

// Reads "Combat summary. EnemyName, N XP. … Total: N XP" before the game clears the kill list.
[HarmonyPatch(typeof(HUD_CombatSummary), "OnCombatEnded")]
class HUD_CombatSummary_OnCombatEnded_Patch  (line 15)
    [HarmonyPrefix]
    public static void Prefix()  (line 18)
        // note: iterates CombatManager.killList to build the full report before the original method processes it.
