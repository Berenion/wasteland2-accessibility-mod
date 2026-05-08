File: Helpers/CharacterAnnouncementHelper.cs — reflection cache, DerivedStatNames, and per-control announcement builders for attribute/skill/trait editors and generic GameObjects. Shared by CharacterState and CharacterInfoState.

namespace Wasteland2AccessibilityMod.Helpers  (line 7)

public static partial class CharacterAnnouncementHelper  (line 26)
    // Split across four partial files. This file owns: reflection cache fields,
    // DerivedStatNames, EnsureReflectionCached, per-control announcement builders,
    // and the buff/debuff/cap helpers those builders use.

    private static bool reflectionCached  (line 29)
    internal static FieldInfo skillEditorCurrentValueField  (line 30)
    internal static FieldInfo skillEditorPcStatsField  (line 31)
    internal static FieldInfo skillEditorIsLearnedField  (line 32)
    internal static FieldInfo attrEditorCurrentValueField  (line 33)
    internal static FieldInfo attrEditorPcTemplateField  (line 34)
    internal static FieldInfo attrEditorBaseValueField  (line 35)
    internal static FieldInfo attrEditorMaxValueField  (line 36)
    internal static FieldInfo traitEditorTraitField  (line 37)
    internal static FieldInfo pressedCallbackField  (line 38)
    internal static MethodInfo skillOnPlusClickedMethod  (line 39)
    internal static MethodInfo skillOnMinusClickedMethod  (line 40)
    internal static MethodInfo attrOnPlusClickedMethod  (line 41)
    internal static MethodInfo attrOnMinusClickedMethod  (line 42)
    internal static MethodInfo descPanelBuildAttrUnlockMethod  (line 45)
    internal static MethodInfo descPanelBuildSkillUnlockMethod  (line 46)
    internal static MethodInfo descPanelBuildTraitUnlockByNameMethod  (line 47)
    internal static MethodInfo descPanelBuildTraitReqMethod  (line 48)

    // The 10 derived stat names in the same order the game displays them.
    public static readonly string[] DerivedStatNames  (line 53)

    public static void EnsureReflectionCached()  (line 68)
        // note: resolves private fields/methods on CHA_SkillEditor, CHA_AttributeEditor, CHA_TraitEditor, and CHA_DescriptionPanel; picks the (string) overload of BuildTraitUnlockString by parameter inspection.

    public static string GetAttributeEditorAnnouncement(CHA_AttributeEditor editor)  (line 112)
        // note: reads currentValue via reflection; appends "of N" cap when maxValue < 10; appends buffed/debuffed from GetAttributeBuffState.

    public static string GetSkillEditorAnnouncement(CHA_SkillEditor editor)  (line 156)
        // note: reads level from levelLabel first, falls back to reflection; appends "of N" cap when skillCap < 10; appends buffed/debuffed/unlearned from GetSkillState.

    private static PCStats GetSkillEditorPcStats(CHA_SkillEditor editor)  (line 194)

    private static PCTemplate GetAttributeEditorPcTemplate(CHA_AttributeEditor editor)  (line 200)

    // Returns the attribute's max cap (10 minus trait penalties), or 0 if unknown.
    private static int GetAttributeMaxValue(CHA_AttributeEditor editor)  (line 209)

    // Returns "buffed" / "debuffed" / "" depending on whether the attribute's current value differs from the template base value.
    private static string GetAttributeBuffState(CHA_AttributeEditor editor)  (line 230)

    // Returns the skill's max level (capped by associated attribute and trait penalties), or 0 if unknown.
    private static int GetSkillCap(CHA_SkillEditor editor)  (line 252)

    // Returns "buffed" / "debuffed" / "unlearned" / "" matching the game's CheckLevelColor logic.
    private static string GetSkillState(CHA_SkillEditor editor)  (line 271)
        // note: "unlearned" is only returned outside character creation when the party lacks the skill.

    public static string GetTraitEditorAnnouncement(CHA_TraitEditor editor)  (line 295)
        // note: appends availability state from GetTraitAvailabilityState; falls back to "selected"/"not selected".

    private static string ResolveTraitName(CHA_TraitEditor editor, Trait trait)  (line 327)
        // note: tries trait.displayName, then traitName field, then nameButton label, then nameLabel; returns "Unknown trait" if all fail.

    // Mirrors CHA_TraitsPanel.OnTraitEditorPressed availability logic so the user learns whether the perk is purchasable before trying to toggle it.
    private static string GetTraitAvailabilityState(CHA_TraitEditor editor, Trait trait, bool isChecked, bool isLocked)  (line 353)
        // note: checks perk points via CHA_TraitsPanel.GetPointsRemaining and pcTemplate.CanAcquireTrait.

    // Gets announcement text for a generic GameObject by component type; handles CHA_AttributeEditor, CHA_SkillEditor, CHA_TraitEditor, UIButton, UILabel.
    public static string GetControlAnnouncement(GameObject obj)  (line 398)
        // note: does NOT handle CharacterScreen-specific types such as CHA_PartyEntry, CHA_PremadeCharacterEntry, UIInput, UIPopupList.
