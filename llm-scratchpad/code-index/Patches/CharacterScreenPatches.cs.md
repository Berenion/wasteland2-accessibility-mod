File: Patches/CharacterScreenPatches.cs — Harmony patches for character screen stat/attribute/skill panels; also blocks UIInput keyboard processing when an accessibility state is active.

namespace Wasteland2AccessibilityMod.Patches  (line 6)

// Static helpers and shared state for character screen announcement deduplication.
public static class CharacterScreenPatches  (line 13)
    internal static string lastAnnouncedStat  (line 14)

    // Formats a stat name for readability, expanding abbreviations (AP, CON, HP).
    internal static string FormatStatName(string statName)  (line 19)

// Announces remaining attribute points when the attribute panel is populated for a PC.
[HarmonyPatch(typeof(CHA_AttributePanel), "PopulateData", new Type[] { typeof(PC) })]
class CHA_AttributePanel_PopulateData_Patch  (line 40)
    [HarmonyPostfix]
    public static void Postfix(CHA_AttributePanel __instance, PC player)  (line 43)

// Announces action points when the stat display panel is updated and focus is inside it.
[HarmonyPatch(typeof(CHA_StatDisplayPanel), "PopulateData")]
class CHA_StatDisplayPanel_PopulateData_Patch  (line 62)
    [HarmonyPostfix]
    public static void Postfix(CHA_StatDisplayPanel __instance, PC pc)  (line 65)
        // note: only announces when the selected object is a child of this panel.

// Announces stat name and value when a stat's value changes and it is the selected object.
[HarmonyPatch(typeof(CHA_StatDisplay), "SetValue")]
class CHA_StatDisplay_SetValue_Patch  (line 87)
    [HarmonyPostfix]
    public static void Postfix(CHA_StatDisplay __instance, int newValue)  (line 90)
        // note: deduplicates against lastAnnouncedStat.

// Announces stat name and value when a stat display is focused/selected.
[HarmonyPatch(typeof(CHA_StatDisplay), "SetSelected")]
class CHA_StatDisplay_SetSelected_Patch  (line 116)
    [HarmonyPostfix]
    public static void Postfix(CHA_StatDisplay __instance, bool selected)  (line 119)
        // note: deduplicates against lastAnnouncedStat.

// Announces remaining skill points when the skill panel is populated.
[HarmonyPatch(typeof(CHA_SkillPanel), "PopulateData")]
class CHA_SkillPanel_PopulateData_Patch  (line 145)
    [HarmonyPostfix]
    public static void Postfix(CHA_SkillPanel __instance, PC pc)  (line 148)

// Announces attribute name and new value when the attribute editor's current value changes and it is focused.
[HarmonyPatch(typeof(CHA_AttributeEditor), "SetCurrentValue")]
class CHA_AttributeEditor_SetCurrentValue_Patch  (line 167)
    [HarmonyPostfix]
    public static void Postfix(CHA_AttributeEditor __instance, int newValue)  (line 170)

// Announces skill name and new level when the skill editor's current value changes and it is focused.
[HarmonyPatch(typeof(CHA_SkillEditor), "SetCurrentValue")]
class CHA_SkillEditor_SetCurrentValue_Patch  (line 189)
    [HarmonyPostfix]
    public static void Postfix(CHA_SkillEditor __instance, int newValue)  (line 192)

// Blocks UIInput.ProcessEvent when CharacterState, CharacterInfoState, or GenericMenuState is navigating.
[HarmonyPatch(typeof(UIInput), "ProcessEvent")]
class UIInput_ProcessEvent_Patch  (line 213)
    [HarmonyPrefix]
    public static bool Prefix()  (line 216)
        // note: returns false (skips original) when any relevant blockUIInput flag is true.

// Blocks UIInput.Update when CharacterState, CharacterInfoState, or GenericMenuState is navigating.
[HarmonyPatch(typeof(UIInput), "Update")]
class UIInput_Update_Patch  (line 228)
    [HarmonyPrefix]
    public static bool Prefix()  (line 231)
        // note: prevents silent text entry into fields during keyboard navigation.
