File: Helpers/CharacterAnnouncementHelper.StatDescriptions.cs — stat and trait description builders; surfaces game's +1 preview, perks-unlocked, and cross-character comparison text via CHA_DescriptionPanel reflection.

namespace Wasteland2AccessibilityMod.Helpers  (line 6)

public static partial class CharacterAnnouncementHelper  (line 12)

    // Builds an attribute/skill description as a list of individually-browsable lines (name, value, full desc, +1 preview, trait modifier, perks unlocked, skill cost, combat base stats, cross-character comparison).
    public static List<string> BuildStatDescriptionLines(GameObject obj)  (line 21)
        // note: detects CHA_AttributeEditor or CHA_SkillEditor on the object; each ", "-separated game preview section becomes its own browsable line via AppendSplit.

    // Splits a comma-joined section into individual lines so +1 preview items browse one per delta.
    private static void AppendSplit(List<string> lines, string section)  (line 117)

    // Back-compat: dumps description as one spoken string. New code should prefer BuildStatDescriptionLines.
    public static void AnnounceStatDescription(GameObject obj)  (line 134)

    private static CHA_DescriptionPanel FindAnyDescriptionPanel()  (line 147)
        // note: tries CharacterInfoMenu and CharacterScreen sub-panels first; falls back to scene-wide scan including inactive objects via Resources.FindObjectsOfTypeAll.

    // Returns the localized "+1 preview" text the game's description panel shows when an attribute or skill is selected.
    private static string BuildNextLevelPreview(string statName, int currentLevel, bool isAttribute, bool isSkill)  (line 193)
        // note: calls descPanelBuildAttrUnlockMethod or descPanelBuildSkillUnlockMethod via reflection; normalizes newlines to comma-separated.

    // Returns trait-driven modifiers on the given stat using pcTemplate.GetTraitAttributeTooltipString / GetTraitSkillTooltipString.
    private static string BuildTraitStatModifier(string statName, bool isAttribute, bool isSkill)  (line 236)

    // Returns the "Perks unlocked at higher levels" text by reflection-calling BuildTraitUnlockString(string).
    private static string BuildStatPerksUnlocked(string statName)  (line 268)

    // Returns "Base Hit X%, Base Crit Y%" for combat skills; empty for non-combat skills.
    private static string BuildCombatSkillBaseStats(string skillName)  (line 286)
        // note: energy weapons omit the crit portion to match the game's display.

    // Reads the cross-character comparison from the skill editor's comparisonTooltipCreator; converts newlines to comma list.
    private static string BuildSkillCrossCharacterComparison(CHA_SkillEditor editor)  (line 316)

    // Returns "Level N cost: K skill points" with an affordability hint for the next skill level.
    private static string BuildSkillNextLevelCost(string skillName)  (line 335)

    // Reads the private Trait reference off a CHA_TraitEditor via the cached reflection field.
    public static Trait GetTraitFromEditor(CHA_TraitEditor editor)  (line 362)

    public static string GetTraitDescription(CHA_TraitEditor editor)  (line 369)
        // note: calls BuildTraitDescription; falls back to editor.tooltip.text if the reflection field is unavailable.

    // Builds a full trait description as one flat string (name, description, effects, requirements with met/unmet markers, unlock list).
    public static string BuildTraitDescription(Trait trait)  (line 407)

    // Builds a Trait description as a list of individually-browsable lines; each line = one fact.
    public static List<string> BuildTraitDescriptionLines(Trait trait)  (line 417)
        // note: annotates each requiredStatValue and requiredTrait with "(met)" or "(not met)" by querying the current PC's pcStats/pcTemplate.
