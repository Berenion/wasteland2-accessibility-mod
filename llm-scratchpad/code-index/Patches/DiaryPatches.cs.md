File: Patches/DiaryPatches.cs — Reads diary/letter/note content aloud when opened or when pages change; BeekersDiaryMenu is the GUIScreen used for all readable items.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Announces title and entry text when a diary/note is first opened.
[HarmonyPatch(typeof(BeekersDiaryMenu), "PopulateData")]
class BeekersDiaryMenu_PopulateData_Patch  (line 11)
    [HarmonyPostfix]
    public static void Postfix(BeekersDiaryMenu __instance)  (line 14)
        // note: concatenates title and entry with ". " separator; speaks with SpeakInterrupt.

// Announces current page entry text when navigating multi-page diaries.
[HarmonyPatch(typeof(BeekersDiaryMenu), "SetPage")]
class BeekersDiaryMenu_SetPage_Patch  (line 52)
    [HarmonyPostfix]
    public static void Postfix(BeekersDiaryMenu __instance)  (line 55)
        // note: reads only the entry text (title stays the same across pages).
