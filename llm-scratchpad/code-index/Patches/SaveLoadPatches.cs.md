File: Patches/SaveLoadPatches.cs — Forces the save/load screen to always sort saves by time descending (newest first) by patching InitializeSort and PopulateData.

namespace Wasteland2AccessibilityMod.Patches  (line 7)

// Resets the static sortMode field to TypeDescending before InitializeSort runs so the sort is always newest-first.
[HarmonyPatch(typeof(SaveLoadScreen), "InitializeSort")]
class SaveLoadScreen_InitializeSort_Patch  (line 20)
    private static FieldInfo sortModeField  (line 22)

    [HarmonyPrefix]
    public static void Prefix()  (line 25)
        // note: uses reflection to set private static sortMode field; caches FieldInfo after first lookup.

// Configures the UIGrid's sorted flag and sort function before PopulateData calls Reposition().
[HarmonyPatch(typeof(SaveLoadScreen), "PopulateData")]
class SaveLoadScreen_PopulateData_Patch  (line 41)
    [HarmonyPrefix]
    public static void Prefix(SaveLoadScreen __instance)  (line 44)
        // note: uses reflection to access saveGrid; sets sorted=true and sort function to SaveGameListEntry.SortByTimeDescending.
