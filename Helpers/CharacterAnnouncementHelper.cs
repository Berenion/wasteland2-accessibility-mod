using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Shared announcement helpers for character-related UI components.
    /// Used by both CharacterState (character creation) and CharacterInfoState (in-game character sheet).
    /// </summary>
    /// <remarks>
    /// Split across multiple files via <c>partial</c>. This file contains:
    /// reflection cache + DerivedStatNames + EnsureReflectionCached + per-control
    /// announcement builders (attribute / skill / trait editor; generic GameObject)
    /// + the buff/debuff/cap helpers those announcements use.
    ///
    /// Other partials:
    /// <list type="bullet">
    ///   <item><c>CharacterAnnouncementHelper.StatDescriptions.cs</c> — stat &amp; trait description builders, description-panel previews</item>
    ///   <item><c>CharacterAnnouncementHelper.Snapshots.cs</c> — derived stats, header / combat snapshots, character summary, XP</item>
    ///   <item><c>CharacterAnnouncementHelper.ValueAdjustment.cs</c> — AdjustAttribute / AdjustSkill / ToggleTrait</item>
    /// </list>
    /// </remarks>
    public static partial class CharacterAnnouncementHelper
    {
        // Reflection caches
        private static bool reflectionCached = false;
        internal static FieldInfo skillEditorCurrentValueField;
        internal static FieldInfo skillEditorPcStatsField;
        internal static FieldInfo skillEditorIsLearnedField;
        internal static FieldInfo attrEditorCurrentValueField;
        internal static FieldInfo attrEditorPcTemplateField;
        internal static FieldInfo attrEditorBaseValueField;
        internal static FieldInfo attrEditorMaxValueField;
        internal static FieldInfo traitEditorTraitField;
        internal static FieldInfo pressedCallbackField;
        internal static MethodInfo skillOnPlusClickedMethod;
        internal static MethodInfo skillOnMinusClickedMethod;
        internal static MethodInfo attrOnPlusClickedMethod;
        internal static MethodInfo attrOnMinusClickedMethod;

        // CHA_DescriptionPanel private build methods (for description previews)
        internal static MethodInfo descPanelBuildAttrUnlockMethod;
        internal static MethodInfo descPanelBuildSkillUnlockMethod;
        internal static MethodInfo descPanelBuildTraitUnlockByNameMethod;
        internal static MethodInfo descPanelBuildTraitReqMethod;

        /// <summary>
        /// The 10 derived stat names in the same order the game displays them.
        /// </summary>
        public static readonly string[] DerivedStatNames = new string[]
        {
            PCStatsManager.actionPoints,
            PCStatsManager.bonusRangedHitChance,
            PCStatsManager.criticalHitChance,
            PCStatsManager.actionRechargeRate,
            PCStatsManager.chanceToEvade,
            PCStatsManager.hitPoints,
            PCStatsManager.armor,
            PCStatsManager.combatSpeed,
            PCStatsManager.skillPointsPerLevel,
            PCStatsManager.maxWeight,
            PCStatsManager.conPerLevel
        };

        public static void EnsureReflectionCached()
        {
            if (reflectionCached) return;

            var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

            skillEditorCurrentValueField = typeof(CHA_SkillEditor).GetField("currentValue", flags);
            skillEditorPcStatsField = typeof(CHA_SkillEditor).GetField("pcStats", flags);
            skillEditorIsLearnedField = typeof(CHA_SkillEditor).GetField("isLearnedSkillEditor", flags);
            skillOnPlusClickedMethod = typeof(CHA_SkillEditor).GetMethod("OnPlusClicked", flags);
            skillOnMinusClickedMethod = typeof(CHA_SkillEditor).GetMethod("OnMinusClicked", flags);

            attrEditorCurrentValueField = typeof(CHA_AttributeEditor).GetField("currentValue", flags);
            attrEditorPcTemplateField = typeof(CHA_AttributeEditor).GetField("pcTemplate", flags);
            attrEditorBaseValueField = typeof(CHA_AttributeEditor).GetField("baseValue", flags);
            attrEditorMaxValueField = typeof(CHA_AttributeEditor).GetField("maxValue", flags);
            attrOnPlusClickedMethod = typeof(CHA_AttributeEditor).GetMethod("OnPlusClicked", flags);
            attrOnMinusClickedMethod = typeof(CHA_AttributeEditor).GetMethod("OnMinusClicked", flags);

            traitEditorTraitField = typeof(CHA_TraitEditor).GetField("trait", flags);
            pressedCallbackField = typeof(CHA_TraitEditor).GetField("pressedCallback", flags);

            // CHA_DescriptionPanel private string-builders (used to surface +1 deltas, perks-unlocked etc.)
            descPanelBuildAttrUnlockMethod = typeof(CHA_DescriptionPanel).GetMethod("BuildAttributeUnlockString", flags);
            descPanelBuildSkillUnlockMethod = typeof(CHA_DescriptionPanel).GetMethod("BuildSkillUnlockString", flags);
            descPanelBuildTraitReqMethod = typeof(CHA_DescriptionPanel).GetMethod("BuildTraitRequirementsString", flags);
            // BuildTraitUnlockString has multiple overloads — we want the (string) one
            foreach (var m in typeof(CHA_DescriptionPanel).GetMethods(flags))
            {
                if (m.Name != "BuildTraitUnlockString") continue;
                var p = m.GetParameters();
                if (p.Length == 1 && p[0].ParameterType == typeof(string))
                {
                    descPanelBuildTraitUnlockByNameMethod = m;
                    break;
                }
            }

            reflectionCached = true;
            MelonLogger.Msg("[CharacterAnnouncementHelper] Reflection cached");
        }

        // ========== Control Announcements ==========

        public static string GetAttributeEditorAnnouncement(CHA_AttributeEditor editor)
        {
            EnsureReflectionCached();
            try
            {
                string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.attribute;
                int value = 0;
                if (attrEditorCurrentValueField != null)
                    value = (int)attrEditorCurrentValueField.GetValue(editor);
                else if (editor.valueLabel != null)
                    int.TryParse(UITextExtractor.CleanText(editor.valueLabel.text), out value);

                var parts = new List<string>();

                // Value, optionally with cap if reduced
                int maxValue = GetAttributeMaxValue(editor);
                if (maxValue > 0 && maxValue < 10)
                    parts.Add($"{name}, {value} of {maxValue}");
                else
                    parts.Add($"{name}, {value}");

                // Buffed/debuffed (compare current pcStats value vs template base)
                string buffState = GetAttributeBuffState(editor);
                if (!string.IsNullOrEmpty(buffState))
                    parts.Add(buffState);

                // Textual rating from descriptionLabel ("Excellent" / "Good" / "Average" / "Poor")
                if (editor.descriptionLabel != null && !string.IsNullOrEmpty(editor.descriptionLabel.text))
                {
                    string rating = UITextExtractor.CleanText(editor.descriptionLabel.text);
                    if (!string.IsNullOrEmpty(rating))
                        parts.Add(rating);
                }

                parts.Add("attribute");
                return string.Join(", ", parts.ToArray());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing attribute: {ex.Message}");
                return "Attribute";
            }
        }

        public static string GetSkillEditorAnnouncement(CHA_SkillEditor editor)
        {
            EnsureReflectionCached();
            try
            {
                string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.skillName;
                int value = 0;
                if (editor.levelLabel != null)
                    int.TryParse(UITextExtractor.CleanText(editor.levelLabel.text), out value);
                else if (skillEditorCurrentValueField != null)
                    value = (int)skillEditorCurrentValueField.GetValue(editor);

                var parts = new List<string>();

                // Level, optionally with cap if reduced below 10
                int skillCap = GetSkillCap(editor);
                if (skillCap > 0 && skillCap < 10)
                    parts.Add($"{name}, level {value} of {skillCap}");
                else
                    parts.Add($"{name}, level {value}");

                // Buffed / debuffed / unlearned
                string state = GetSkillState(editor);
                if (!string.IsNullOrEmpty(state))
                    parts.Add(state);

                parts.Add("skill");
                return string.Join(", ", parts.ToArray());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing skill: {ex.Message}");
                return "Skill";
            }
        }

        // ========== Buff/Debuff/Cap Helpers ==========

        private static PCStats GetSkillEditorPcStats(CHA_SkillEditor editor)
        {
            if (skillEditorPcStatsField == null) return null;
            return skillEditorPcStatsField.GetValue(editor) as PCStats;
        }

        private static PCTemplate GetAttributeEditorPcTemplate(CHA_AttributeEditor editor)
        {
            if (attrEditorPcTemplateField == null) return null;
            return attrEditorPcTemplateField.GetValue(editor) as PCTemplate;
        }

        /// <summary>
        /// Returns the attribute's max cap (10 minus trait penalties), or 0 if unknown.
        /// </summary>
        private static int GetAttributeMaxValue(CHA_AttributeEditor editor)
        {
            try
            {
                if (attrEditorMaxValueField != null)
                {
                    var v = attrEditorMaxValueField.GetValue(editor);
                    if (v is int max && max > 0) return max;
                }
                var tmpl = GetAttributeEditorPcTemplate(editor);
                if (tmpl != null && !string.IsNullOrEmpty(editor.attribute))
                    return 10 - tmpl.GetTraitStatEffect(editor.attribute);
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Returns "buffed" / "debuffed" / "" depending on whether the attribute's current
        /// value (pcStats with bonuses) differs from the template base value.
        /// </summary>
        private static string GetAttributeBuffState(CHA_AttributeEditor editor)
        {
            try
            {
                var tmpl = GetAttributeEditorPcTemplate(editor);
                if (tmpl == null || string.IsNullOrEmpty(editor.attribute)) return "";
                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;
                if (pc == null || pc.pcStats == null) return "";
                int current = pc.pcStats.GetAttribute(editor.attribute);
                int baseVal = tmpl.GetAttribute(editor.attribute);
                if (current > baseVal) return "buffed";
                if (current < baseVal) return "debuffed";
            }
            catch { }
            return "";
        }

        /// <summary>
        /// Returns the skill's max level (capped by associated attribute and trait penalties), or 0 if unknown.
        /// </summary>
        private static int GetSkillCap(CHA_SkillEditor editor)
        {
            try
            {
                var pcStats = GetSkillEditorPcStats(editor);
                if (pcStats == null || string.IsNullOrEmpty(editor.skillName)) return 0;
                var skill = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetSkill(editor.skillName);
                if (skill == null) return 0;
                int cap = skill.GetSkillLimit(pcStats.GetAttribute(skill.associatedAttribute))
                          - pcStats.GetPCTemplate().GetTraitStatEffect(editor.skillName);
                return Mathf.Clamp(cap, 0, 10);
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Returns "buffed" / "debuffed" / "unlearned" / "" matching the game's CheckLevelColor logic.
        /// </summary>
        private static string GetSkillState(CHA_SkillEditor editor)
        {
            try
            {
                var pcStats = GetSkillEditorPcStats(editor);
                if (pcStats == null || string.IsNullOrEmpty(editor.skillName)) return "";

                int xp = pcStats.GetSkillXP(editor.skillName);
                int xpLevel = Table_SkillLeveling.GetSkillLevelFromXp(editor.skillName, xp);
                int displayedLevel = pcStats.GetSkillLevel(editor.skillName);

                bool inCC = MonoBehaviourSingleton<HUD_Controller>.HasInstance() == false
                            && MonoBehaviourSingleton<HUD_WorldMapController>.HasInstance() == false;

                if (xpLevel == 0 && !inCC && !MonoBehaviourSingleton<Game>.GetInstance().DoesPartyHaveSkill(editor.skillName))
                    return "unlearned";

                if (displayedLevel > xpLevel) return "buffed";
                if (displayedLevel < xpLevel) return "debuffed";
            }
            catch { }
            return "";
        }

        public static string GetTraitEditorAnnouncement(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            try
            {
                Trait trait = traitEditorTraitField != null
                    ? traitEditorTraitField.GetValue(editor) as Trait
                    : null;

                string name = ResolveTraitName(editor, trait);
                bool isChecked = editor.checkbox != null && editor.checkbox.value;
                bool isLocked = editor.checkboxButton != null && !editor.checkboxButton.isEnabled && !isChecked;

                var parts = new List<string> { name };

                // Lifecycle state — match what a sighted user sees from the description panel
                string availability = GetTraitAvailabilityState(editor, trait, isChecked, isLocked);
                if (!string.IsNullOrEmpty(availability))
                    parts.Add(availability);
                else
                    parts.Add(isChecked ? "selected" : "not selected");

                parts.Add("quirk");
                return string.Join(", ", parts.ToArray());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing trait: {ex.Message}");
                return "Trait";
            }
        }

        private static string ResolveTraitName(CHA_TraitEditor editor, Trait trait)
        {
            if (trait != null && !string.IsNullOrEmpty(trait.displayName))
                return UITextExtractor.CleanText(Language.Localize(trait.displayName, false, false, string.Empty));

            string name = editor.traitName;
            if (string.IsNullOrEmpty(name))
            {
                var nameBtn = editor.nameButton;
                if (nameBtn != null)
                {
                    var label = nameBtn.GetComponentInChildren<UILabel>();
                    if (label != null)
                        name = UITextExtractor.CleanText(label.text);
                }
            }
            // The "no quirk" placeholder
            if (editor.nameLabel != null && !string.IsNullOrEmpty(editor.nameLabel.text) && string.IsNullOrEmpty(name))
                name = UITextExtractor.CleanText(editor.nameLabel.text);
            return string.IsNullOrEmpty(name) ? "Unknown trait" : name;
        }

        /// <summary>
        /// Mirrors CHA_TraitsPanel.OnTraitEditorPressed availability logic so the user
        /// learns whether the perk is purchasable before trying to toggle it.
        /// </summary>
        private static string GetTraitAvailabilityState(CHA_TraitEditor editor, Trait trait, bool isChecked, bool isLocked)
        {
            try
            {
                if (trait == null)
                    return ""; // "no quirk" placeholder — no availability text

                if (isChecked)
                    return "purchased";

                bool inCC = MonoBehaviourSingleton<HUD_Controller>.HasInstance() == false
                            && MonoBehaviourSingleton<HUD_WorldMapController>.HasInstance() == false;
                if (inCC)
                    return isLocked ? "locked" : "available";

                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;
                if (pc == null || pc.pcTemplate == null)
                    return isLocked ? "locked" : "";

                if (pc.pcTemplate.HasTrait(trait.name))
                    return "purchased";

                // Check perk points remaining via the panel that owns this editor
                int pointsRemaining = -1;
                var panel = NGUITools.FindInParents<CHA_TraitsPanel>(editor.gameObject);
                if (panel != null) pointsRemaining = panel.GetPointsRemaining();

                if (pointsRemaining == 0)
                    return "insufficient perk points";

                if (!pc.pcTemplate.CanAcquireTrait(trait))
                    return "requirements not met";

                return isLocked ? "locked" : "available";
            }
            catch { return isLocked ? "locked" : ""; }
        }

        /// <summary>
        /// Gets announcement text for a generic GameObject based on its component type.
        /// Handles CHA_AttributeEditor, CHA_SkillEditor, CHA_TraitEditor, UIButton, UILabel.
        /// Does NOT handle CharacterScreen-specific types (CHA_PartyEntry, CHA_PremadeCharacterEntry, UIInput, UIPopupList).
        /// </summary>
        public static string GetControlAnnouncement(GameObject obj)
        {
            if (obj == null) return null;

            var attrEditor = obj.GetComponent<CHA_AttributeEditor>();
            if (attrEditor != null)
                return GetAttributeEditorAnnouncement(attrEditor);

            var skillEditor = obj.GetComponent<CHA_SkillEditor>();
            if (skillEditor != null)
                return GetSkillEditorAnnouncement(skillEditor);

            var traitEditor = obj.GetComponent<CHA_TraitEditor>();
            if (traitEditor != null)
                return GetTraitEditorAnnouncement(traitEditor);

            // Generic button
            var button = obj.GetComponent<UIButton>();
            if (button != null)
            {
                UILabel btnLabel = obj.GetComponentInChildren<UILabel>();
                string text = btnLabel != null ? UITextExtractor.CleanText(btnLabel.text) : "";
                if (string.IsNullOrEmpty(text))
                {
                    text = obj.name.Replace("Button", "").Replace("button", "").Trim();
                    if (string.IsNullOrEmpty(text)) text = obj.name;
                }
                return $"{text}, button";
            }

            // Last resort: label text
            UILabel anyLabel = obj.GetComponentInChildren<UILabel>();
            if (anyLabel != null)
                return UITextExtractor.CleanText(anyLabel.text);

            return obj.name;
        }
    }
}
