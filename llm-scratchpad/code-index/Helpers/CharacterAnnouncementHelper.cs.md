File: Helpers/CharacterAnnouncementHelper.cs — shared announcement helpers for character-related UI; used by CharacterState (character creation) and CharacterInfoState (in-game character sheet).

namespace Wasteland2AccessibilityMod.Helpers  (line 7)

static class CharacterAnnouncementHelper  (line 13)
    // Centralises all text-building for character attributes, skills, traits, derived stats, header snapshot, combat snapshot, XP, and value adjustments.

    // ---- Reflection caches ----
    private static bool reflectionCached  (line 16)
    internal static FieldInfo skillEditorCurrentValueField  (line 17)
    internal static FieldInfo skillEditorPcStatsField  (line 18)
    internal static FieldInfo skillEditorIsLearnedField  (line 19)
    internal static FieldInfo attrEditorCurrentValueField  (line 20)
    internal static FieldInfo attrEditorPcTemplateField  (line 21)
    internal static FieldInfo attrEditorBaseValueField  (line 22)
    internal static FieldInfo attrEditorMaxValueField  (line 23)
    internal static FieldInfo traitEditorTraitField  (line 24)
    internal static FieldInfo pressedCallbackField  (line 25)
    internal static MethodInfo skillOnPlusClickedMethod  (line 26)
    internal static MethodInfo skillOnMinusClickedMethod  (line 27)
    internal static MethodInfo attrOnPlusClickedMethod  (line 28)
    internal static MethodInfo attrOnMinusClickedMethod  (line 29)
    internal static MethodInfo descPanelBuildAttrUnlockMethod  (line 32)
    internal static MethodInfo descPanelBuildSkillUnlockMethod  (line 33)
    internal static MethodInfo descPanelBuildTraitUnlockByNameMethod  (line 34)
    internal static MethodInfo descPanelBuildTraitReqMethod  (line 35)

    // The 10 derived stat names in the same order the game displays them.
    public static readonly string[] DerivedStatNames  (line 40)

    // Caches all reflection fields/methods for CHA_SkillEditor, CHA_AttributeEditor, CHA_TraitEditor, CHA_DescriptionPanel in one pass.
    public static void EnsureReflectionCached()  (line 55)
        // note: selects BuildTraitUnlockString(string) overload specifically to avoid ambiguity with the (Trait) overload.

    // ---- Control Announcements ----

    // Builds "Name, value[/max], [buffed|debuffed], rating, attribute" announcement string.
    public static string GetAttributeEditorAnnouncement(CHA_AttributeEditor editor)  (line 99)

    // Builds "Name, level N[/cap], [buffed|debuffed|unlearned], skill" announcement string.
    public static string GetSkillEditorAnnouncement(CHA_SkillEditor editor)  (line 143)

    // ---- Buff/Debuff/Cap Helpers ----

    private static PCStats GetSkillEditorPcStats(CHA_SkillEditor editor)  (line 181)
    private static PCTemplate GetAttributeEditorPcTemplate(CHA_AttributeEditor editor)  (line 187)

    // Returns attribute max cap (10 minus trait penalties); 0 if unknown.
    private static int GetAttributeMaxValue(CHA_AttributeEditor editor)  (line 196)

    // Returns "buffed" / "debuffed" / "" by comparing pcStats current value against template base.
    private static string GetAttributeBuffState(CHA_AttributeEditor editor)  (line 217)

    // Returns skill max level capped by associated attribute and trait penalties; 0 if unknown.
    private static int GetSkillCap(CHA_SkillEditor editor)  (line 239)

    // Returns "buffed" / "debuffed" / "unlearned" / "" matching the game's CheckLevelColor logic.
    private static string GetSkillState(CHA_SkillEditor editor)  (line 258)
        // note: "unlearned" requires the party not to have the skill and xpLevel == 0, but only outside character creation.

    // Builds "Name, [purchased|locked|available|requirements not met|...], quirk" announcement string.
    public static string GetTraitEditorAnnouncement(CHA_TraitEditor editor)  (line 282)

    private static string ResolveTraitName(CHA_TraitEditor editor, Trait trait)  (line 314)

    // Mirrors CHA_TraitsPanel.OnTraitEditorPressed availability logic to tell the user if a perk is purchasable.
    private static string GetTraitAvailabilityState(CHA_TraitEditor editor, Trait trait, bool isChecked, bool isLocked)  (line 340)
        // note: reads perk points from the containing CHA_TraitsPanel via NGUITools.FindInParents.

    // Gets announcement text for a generic GameObject; dispatches to attribute/skill/trait/button/label handlers.
    // Does NOT handle CharacterScreen-specific types (CHA_PartyEntry, CHA_PremadeCharacterEntry, UIInput, UIPopupList).
    public static string GetControlAnnouncement(GameObject obj)  (line 385)

    // ---- Stat Descriptions ----

    // Builds attribute/skill description as a list of individually-browsable lines: name, level, short desc, full desc, +1 preview, trait modifier, perks unlocked, skill cost, combat base stats, cross-character comparison.
    public static List<string> BuildStatDescriptionLines(GameObject obj)  (line 431)

    // Splits a comma-joined section into individual lines; preserves leading label on first piece.
    private static void AppendSplit(List<string> lines, string section)  (line 526)

    // Back-compat single-string dump; new code should prefer BuildStatDescriptionLines + info browser.
    public static void AnnounceStatDescription(GameObject obj)  (line 543)

    // ---- Description Panel Previews ----

    private static CHA_DescriptionPanel FindAnyDescriptionPanel()  (line 556)
        // note: searches CharacterInfoMenu, CharacterScreen, then scene-wide (including inactive objects) to find any CHA_DescriptionPanel whose Build* methods can be invoked without instance state.

    // Returns the localized "+1 preview" text from the description panel for attribute or skill (delta to current+1).
    private static string BuildNextLevelPreview(string statName, int currentLevel, bool isAttribute, bool isSkill)  (line 602)

    // Returns trait-driven stat modifiers (e.g. "+1 from Bookworm") via pcTemplate tooltip string methods.
    private static string BuildTraitStatModifier(string statName, bool isAttribute, bool isSkill)  (line 645)

    // Returns perks-unlocked-at-higher-levels text via reflection-calling BuildTraitUnlockString(string).
    private static string BuildStatPerksUnlocked(string statName)  (line 678)

    // Returns "Base Hit X%, Base Crit Y%" for combat skills; empty for non-combat skills.
    private static string BuildCombatSkillBaseStats(string skillName)  (line 697)
        // note: energy weapons omit crit line to match game's display.

    // Reads cross-character skill comparison from comparisonTooltipCreator, converting newlines to comma list.
    private static string BuildSkillCrossCharacterComparison(CHA_SkillEditor editor)  (line 725)

    // Returns "Level N cost: K skill points[, insufficient skill points]" for the next skill level.
    private static string BuildSkillNextLevelCost(string skillName)  (line 744)

    // ---- Trait Descriptions ----

    // Reads the private Trait field off a CHA_TraitEditor via reflection cache; returns null if unavailable.
    public static Trait GetTraitFromEditor(CHA_TraitEditor editor)  (line 771)

    public static string GetTraitDescription(CHA_TraitEditor editor)  (line 778)

    // Builds full Trait description as flat string (name, desc, effects, requirements, unlocks); new code should prefer BuildTraitDescriptionLines.
    public static string BuildTraitDescription(Trait trait)  (line 816)

    // Builds Trait description as individually-browsable lines; each line = one fact.
    public static List<string> BuildTraitDescriptionLines(Trait trait)  (line 826)

    // ---- Derived Stats ----

    public static string FormatDerivedStatValue(int rawValue, DerivedStat.StatDisplayType displayType)  (line 911)
        // note: large switch over StatDisplayType enum values (Percent, ActionPoint, CombatMovement, Meters, Pounds, default).

    // Announces a single derived stat by index into DerivedStatNames; uses interrupt or queue based on parameter.
    public static void AnnounceDerivedStat(PC pc, int index, bool interrupt = true)  (line 931)

    // Announces the description (name + full desc + trait modifier) for a derived stat by index.
    public static void AnnounceDerivedStatDescription(PC pc, int index)  (line 971)

    // Builds the full derived-stats list as browsable lines in DerivedStatNames order, formatted "name: value".
    public static List<string> BuildDerivedStatLines(PC pc)  (line 1017)

    // ---- Header Snapshot (Character Info Menu) ----

    // Builds header snapshot as browsable lines: name+level+rank, HP, capacity, money, water, points available, status effects.
    public static List<string> BuildHeaderSnapshotLines(PC pc)  (line 1052)

    // Builds a short "N points available" hint for auto-announcement when entering Attributes/Skills/Perks tab.
    public static string BuildPointsAvailableHint(PC pc, CharacterInfoMenu.InfoPanel panel)  (line 1150)

    // ---- Combat Snapshot ----

    // Builds combat snapshot as browsable lines: damage, hit chance, crit, evade, armor, range, AP, recharge, speed.
    public static List<string> BuildCombatSnapshotLines(PC pc)  (line 1178)

    // ---- Character Summary ----

    // Announces "Name, specialization, Level N" for the given PC.
    public static void AnnounceCharacterSummary(PC pc)  (line 1242)

    // ---- XP ----

    // Builds "Level N. Experience X of Y[. Level up available]" or max-level variant.
    public static string BuildXPAnnouncement(PC pc)  (line 1269)

    public static void AnnounceXP(PC pc, bool interrupt = true)  (line 1288)

    // ---- Value Adjustment ----

    // Invokes OnPlusClicked/OnMinusClicked via reflection; speaks "Maximum"/"Minimum" if boundary hit.
    public static void AdjustAttribute(CHA_AttributeEditor editor, int direction, Action announceCallback)  (line 1302)

    // Invokes OnPlusClicked/OnMinusClicked via reflection on CHA_SkillEditor.
    public static void AdjustSkill(CHA_SkillEditor editor, int direction, Action announceCallback)  (line 1335)

    // Invokes the trait's pressedCallback then toggles the checkbox; must call callback first to maintain CHA_TraitsPanel.currentEditor state.
    public static void ToggleTrait(CHA_TraitEditor editor)  (line 1362)
        // note: fires pressedCallback via DynamicInvoke before SendMessage("OnClick") to keep CHA_TraitsPanel.currentEditor in sync.
