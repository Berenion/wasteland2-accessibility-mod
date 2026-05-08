File: Patches/CharacterSelectionPatches.cs — Announces the selected player character name when PC.MakeLeader is called (character switching outside combat).

namespace Wasteland2AccessibilityMod.Patches  (line 4)

// Announces character name when party leader changes; skips if the same PC was already leader.
[HarmonyPatch(typeof(PC), "MakeLeader")]
class PC_MakeLeader_Patch  (line 11)
    private static PC previousLeader  (line 13)

    [HarmonyPostfix]
    public static void Postfix(PC __instance)  (line 15)
        // note: verifies new leader via Game.pcLeader; compares to previousLeader to avoid duplicate announcements.

    private static string GetCharacterName(PC pc)  (line 43)
        // note: checks template.displayName, pcTemplate.displayName, then falls back to GameObject name.
