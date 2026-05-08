File: Patches/CharacterCreationPatches.cs — Harmony patches for character creation UI: difficulty selection, skill/attribute/trait description panels, and selection focus announcements.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Announces difficulty description text and difficulty name/position when a difficulty is selected.
[HarmonyPatch(typeof(DifficultySelectionMenu), "SelectDifficulty")]
class DifficultySelectionMenu_SelectDifficulty_Patch  (line 14)
    [HarmonyPostfix]
    public static void Postfix(DifficultySelectionMenu __instance, int difficultyLevel)  (line 17)
        // note: reads name, position (e.g. "2 of 4"), and full description label text.

// Announces skill name, level, cost (reasonLabel), and description when SetSkill fires.
[HarmonyPatch(typeof(CHA_DescriptionPanel), "SetSkill")]
class CHA_DescriptionPanel_SetSkill_Patch  (line 51)
    [HarmonyPostfix]
    public static void Postfix(CHA_DescriptionPanel __instance, int level)  (line 54)

// Announces attribute name, level, and description when SetAttribute fires.
[HarmonyPatch(typeof(CHA_DescriptionPanel), "SetAttribute")]
class CHA_DescriptionPanel_SetAttribute_Patch  (line 101)
    [HarmonyPostfix]
    public static void Postfix(CHA_DescriptionPanel __instance, int level)  (line 104)

// Announces trait name, description, bonus effects, and cost when SetTrait fires.
[HarmonyPatch(typeof(CHA_DescriptionPanel), "SetTrait")]
class CHA_DescriptionPanel_SetTrait_Patch  (line 141)
    [HarmonyPostfix]
    public static void Postfix(CHA_DescriptionPanel __instance)  (line 144)

// Announces skill name and level when a skill editor row gains focus.
[HarmonyPatch(typeof(CHA_SkillEditor), "OnSelect")]
class CHA_SkillEditor_OnSelect_Patch  (line 195)
    [HarmonyPostfix]
    public static void Postfix(CHA_SkillEditor __instance, bool isSelected)  (line 198)
        // note: only announces when isSelected is true.

// Announces attribute name, numeric value, and rating label when an attribute editor row gains focus.
[HarmonyPatch(typeof(CHA_AttributeEditor), "OnSelect")]
class CHA_AttributeEditor_OnSelect_Patch  (line 234)
    [HarmonyPostfix]
    public static void Postfix(CHA_AttributeEditor __instance, bool isSelected)  (line 237)
        // note: only announces when isSelected is true.
