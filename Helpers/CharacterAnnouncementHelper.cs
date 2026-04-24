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
    public static class CharacterAnnouncementHelper
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

        // ========== Stat Descriptions ==========

        /// <summary>
        /// Builds an attribute/skill description as a list of individually-browsable lines.
        /// Each line = one fact (name, current value, full description, +1 preview, trait
        /// modifier, perks unlocked, skill cost, combat base stats, cross-character comparison).
        /// </summary>
        public static List<string> BuildStatDescriptionLines(GameObject obj)
        {
            var lines = new List<string>();
            if (obj == null) return lines;

            EnsureReflectionCached();

            string characteristicName = null;
            string levelText = null;
            string shortDesc = null;
            int currentLevel = 0;
            bool isAttribute = false;
            bool isSkill = false;
            CHA_SkillEditor skillEditor = null;

            var attrEditor = obj.GetComponent<CHA_AttributeEditor>();
            if (attrEditor != null)
            {
                isAttribute = true;
                characteristicName = attrEditor.attribute;
                levelText = attrEditor.valueLabel != null ? UITextExtractor.CleanText(attrEditor.valueLabel.text) : null;
                shortDesc = attrEditor.descriptionLabel != null ? UITextExtractor.CleanText(attrEditor.descriptionLabel.text) : null;
                if (attrEditorCurrentValueField != null)
                    currentLevel = (int)attrEditorCurrentValueField.GetValue(attrEditor);
                else if (!string.IsNullOrEmpty(levelText))
                    int.TryParse(levelText, out currentLevel);
            }

            skillEditor = obj.GetComponent<CHA_SkillEditor>();
            if (skillEditor != null)
            {
                isSkill = true;
                characteristicName = skillEditor.skillName;
                if (skillEditor.levelLabel != null)
                {
                    string raw = UITextExtractor.CleanText(skillEditor.levelLabel.text);
                    int.TryParse(raw, out currentLevel);
                    levelText = "Level " + raw;
                }
            }

            if (string.IsNullOrEmpty(characteristicName)) return lines;

            try
            {
                var baseStat = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetCharacteristic(characteristicName);
                if (baseStat == null) return lines;

                string name = UITextExtractor.CleanText(Language.Localize(baseStat.displayName, false, false, string.Empty));
                string fullDesc = UITextExtractor.CleanText(Language.Localize(baseStat.description, false, false, string.Empty));

                lines.Add(name);
                if (!string.IsNullOrEmpty(levelText))
                    lines.Add(levelText);
                if (!string.IsNullOrEmpty(shortDesc) && shortDesc != name)
                    lines.Add(shortDesc);
                if (!string.IsNullOrEmpty(fullDesc))
                    lines.Add(fullDesc);

                // Each ", "-separated section from the game's preview becomes its own line
                string preview = BuildNextLevelPreview(characteristicName, currentLevel, isAttribute, isSkill);
                AppendSplit(lines, preview);

                string traitMod = BuildTraitStatModifier(characteristicName, isAttribute, isSkill);
                if (!string.IsNullOrEmpty(traitMod))
                    lines.Add(traitMod);

                string perksUnlocked = BuildStatPerksUnlocked(characteristicName);
                AppendSplit(lines, perksUnlocked);

                if (isSkill)
                {
                    string cost = BuildSkillNextLevelCost(characteristicName);
                    if (!string.IsNullOrEmpty(cost))
                        lines.Add(cost);

                    string baseStats = BuildCombatSkillBaseStats(characteristicName);
                    if (!string.IsNullOrEmpty(baseStats))
                        lines.Add(baseStats);

                    string comparison = BuildSkillCrossCharacterComparison(skillEditor);
                    AppendSplit(lines, comparison);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error building stat description lines: {ex.Message}");
            }

            return lines;
        }

        /// <summary>
        /// Splits a comma-joined section into individual lines. Used so the +1 preview
        /// (which produces "Level N, +1 AP, +5% Hit, ...") browses as one line per delta.
        /// </summary>
        private static void AppendSplit(List<string> lines, string section)
        {
            if (string.IsNullOrEmpty(section)) return;
            // Preserve the leading label ("Next level: ", "Other rangers: ", "Perks: ") on the first piece
            string[] parts = section.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (string.IsNullOrEmpty(p)) continue;
                lines.Add(p);
            }
        }

        /// <summary>
        /// Back-compat: dump the description as one spoken string. New code should
        /// prefer BuildStatDescriptionLines + the info browser.
        /// </summary>
        public static void AnnounceStatDescription(GameObject obj)
        {
            var lines = BuildStatDescriptionLines(obj);
            if (lines == null || lines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No description available");
                return;
            }
            ScreenReaderManager.SpeakInterrupt(string.Join(". ", lines.ToArray()));
        }

        // ========== Description Panel Previews ==========

        private static CHA_DescriptionPanel FindAnyDescriptionPanel()
        {
            // The Build* methods we invoke don't depend on instance state,
            // so any CHA_DescriptionPanel in the scene works — active or not.
            // We try the in-game / character-creation menus' sub-panels first,
            // then fall back to a scene-wide scan including inactive objects.
            try
            {
                var infoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                if (infoMenu != null)
                {
                    if (infoMenu.attributePanel != null && infoMenu.attributePanel.descriptionPanel != null)
                        return infoMenu.attributePanel.descriptionPanel;
                    if (infoMenu.skillPanel != null && infoMenu.skillPanel.descriptionPanel != null)
                        return infoMenu.skillPanel.descriptionPanel;
                    if (infoMenu.traitPanel != null && infoMenu.traitPanel.descriptionPanel != null)
                        return infoMenu.traitPanel.descriptionPanel;
                    if (infoMenu.dossierPanel != null && infoMenu.dossierPanel.descriptionPanel != null)
                        return infoMenu.dossierPanel.descriptionPanel;
                }

                var charScreen = CharacterScreen.instance;
                if (charScreen != null)
                {
                    if (charScreen.attributePanel != null && charScreen.attributePanel.descriptionPanel != null)
                        return charScreen.attributePanel.descriptionPanel;
                    if (charScreen.skillPanel != null && charScreen.skillPanel.descriptionPanel != null)
                        return charScreen.skillPanel.descriptionPanel;
                    if (charScreen.traitsPanel != null && charScreen.traitsPanel.descriptionPanel != null)
                        return charScreen.traitsPanel.descriptionPanel;
                }
            }
            catch { }

            // Active first, then any
            var active = UnityEngine.Object.FindObjectOfType<CHA_DescriptionPanel>();
            if (active != null) return active;

            var all = Resources.FindObjectsOfTypeAll<CHA_DescriptionPanel>();
            return (all != null && all.Length > 0) ? all[0] : null;
        }

        /// <summary>
        /// Returns the localized "+1 preview" text the game's description panel shows
        /// when an attribute or skill is selected (delta from current level to current+1).
        /// </summary>
        private static string BuildNextLevelPreview(string statName, int currentLevel, bool isAttribute, bool isSkill)
        {
            try
            {
                var panel = FindAnyDescriptionPanel();
                if (panel == null) return "";

                int nextLevel = Mathf.Clamp(currentLevel + 1, 1, 10);
                string raw = "";

                if (isAttribute)
                {
                    if (descPanelBuildAttrUnlockMethod == null) return "";
                    var attr = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetAttribute(statName);
                    if (attr == null) return "";
                    raw = descPanelBuildAttrUnlockMethod.Invoke(panel, new object[] { attr, nextLevel }) as string;
                }
                else if (isSkill)
                {
                    if (descPanelBuildSkillUnlockMethod == null) return "";
                    var skill = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetSkill(statName);
                    if (skill == null) return "";
                    raw = descPanelBuildSkillUnlockMethod.Invoke(panel, new object[] { skill, nextLevel }) as string;
                }

                if (string.IsNullOrEmpty(raw)) return "";
                string clean = UITextExtractor.CleanText(raw).Trim();
                clean = clean.Replace("\r\n", "\n").Replace("\n", ", ");
                while (clean.Contains(", , ")) clean = clean.Replace(", , ", ", ");
                clean = clean.Trim(',', ' ');
                return string.IsNullOrEmpty(clean) ? "" : "Next level: " + clean;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error building level preview: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Returns trait-driven modifiers on the given stat (e.g. "+1 from Bookworm trait").
        /// Uses pcTemplate.GetTraitAttributeTooltipString / GetTraitSkillTooltipString.
        /// </summary>
        private static string BuildTraitStatModifier(string statName, bool isAttribute, bool isSkill)
        {
            try
            {
                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;
                if (pc == null || pc.pcTemplate == null) return "";

                string raw = "";
                if (isAttribute)
                {
                    var attr = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetAttribute(statName);
                    if (attr == null) return "";
                    raw = pc.pcTemplate.GetTraitAttributeTooltipString(attr);
                }
                else if (isSkill)
                {
                    var skill = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetSkill(statName);
                    if (skill == null) return "";
                    raw = pc.pcTemplate.GetTraitSkillTooltipString(skill);
                }

                if (string.IsNullOrEmpty(raw)) return "";
                return UITextExtractor.CleanText(raw).Trim();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Returns the "Perks unlocked at higher levels of this stat" text that the
        /// description panel shows, by reflection-calling BuildTraitUnlockString(string).
        /// </summary>
        private static string BuildStatPerksUnlocked(string statName)
        {
            try
            {
                if (descPanelBuildTraitUnlockByNameMethod == null) return "";
                var panel = FindAnyDescriptionPanel();
                if (panel == null) return "";
                string raw = descPanelBuildTraitUnlockByNameMethod.Invoke(panel, new object[] { statName }) as string;
                if (string.IsNullOrEmpty(raw)) return "";
                return UITextExtractor.CleanText(raw).Trim();
            }
            catch { return ""; }
        }

        /// <summary>
        /// Returns "Base Hit X%, Base Crit Y%" for combat skills — mirrors the
        /// CHA_WeaponStatsPanel side panel a sighted user sees when focusing a combat skill.
        /// Empty for non-combat skills.
        /// </summary>
        private static string BuildCombatSkillBaseStats(string skillName)
        {
            try
            {
                var skill = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetSkill(skillName);
                if (skill == null || skill.category != Skill.Category.Combat) return "";

                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;
                if (pc == null || pc.pcStats == null) return "";

                int hit = pc.pcStats.GetBaseChanceToHit(skillName, skillName == "atWeapons");
                int crit = pc.pcStats.GetBaseChanceToCriticalHitWithSkill(skillName);

                // Energy weapons use no critical hit calculation in the game's display
                if (skillName == "energyWeapons")
                    return $"Base Hit {hit}%";

                return $"Base Hit {hit}%, Base Crit {crit}%";
            }
            catch { return ""; }
        }

        /// <summary>
        /// Reads the cross-character comparison the game prepares on the skill editor's
        /// comparisonTooltipCreator (e.g. "Vargas: Level 4\nAngela: Level 3").
        /// </summary>
        private static string BuildSkillCrossCharacterComparison(CHA_SkillEditor editor)
        {
            try
            {
                if (editor == null || editor.comparisonTooltipCreator == null) return "";
                string raw = editor.comparisonTooltipCreator.text;
                if (string.IsNullOrEmpty(raw)) return "";
                string clean = UITextExtractor.CleanText(raw).Trim();
                if (string.IsNullOrEmpty(clean)) return "";
                // Game uses newlines between rangers — convert to comma list
                clean = clean.Replace("\n", ", ").Replace("\r", "");
                return "Other rangers: " + clean;
            }
            catch { return ""; }
        }

        /// <summary>
        /// Returns "Level N cost: K skill points" with an affordability hint for the next skill level.
        /// </summary>
        private static string BuildSkillNextLevelCost(string skillName)
        {
            try
            {
                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;
                if (pc == null || pc.pcTemplate == null) return "";

                int nextLevel = pc.pcTemplate.GetSkillLevel(skillName) + 1;
                if (nextLevel > Table_SkillLeveling.maxLevel) return "";

                int cost = Table_SkillLeveling.GetXPForLevel(nextLevel);
                int available = pc.pcTemplate.availableSkillPoints;

                string suffix = cost <= available ? "" : ", insufficient skill points";
                return $"Level {nextLevel} cost: {cost} skill point{(cost == 1 ? "" : "s")}{suffix}";
            }
            catch { return ""; }
        }

        // ========== Trait Descriptions ==========

        /// <summary>
        /// Reads the private Trait reference off a CHA_TraitEditor via the cached reflection.
        /// Returns null if unavailable.
        /// </summary>
        public static Trait GetTraitFromEditor(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            if (editor == null || traitEditorTraitField == null) return null;
            return traitEditorTraitField.GetValue(editor) as Trait;
        }

        public static string GetTraitDescription(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            try
            {
                if (traitEditorTraitField != null)
                {
                    var trait = traitEditorTraitField.GetValue(editor) as Trait;
                    if (trait != null)
                    {
                        string built = BuildTraitDescription(trait);
                        if (!string.IsNullOrEmpty(built))
                            return built;
                    }
                }

                // Fallback: tooltip text
                if (editor.tooltip != null)
                {
                    string tooltipText = editor.tooltip.text;
                    if (!string.IsNullOrEmpty(tooltipText))
                        return UITextExtractor.CleanText(tooltipText);
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error getting trait description: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds a full description for any Trait as one flat string — name, description,
        /// effects, requirements with met/unmet markers, and unlock list.
        /// Kept for callers that want the dump form; new code should prefer the line-list version.
        /// </summary>
        public static string BuildTraitDescription(Trait trait)
        {
            var lines = BuildTraitDescriptionLines(trait);
            return (lines == null || lines.Count == 0) ? null : string.Join(". ", lines.ToArray());
        }

        /// <summary>
        /// Builds a Trait description as a list of individually-browsable lines.
        /// Each line = one fact, suitable for Up/Down navigation in an info browser.
        /// </summary>
        public static List<string> BuildTraitDescriptionLines(Trait trait)
        {
            var lines = new List<string>();
            if (trait == null) return lines;

            try
            {
                string name = UITextExtractor.CleanText(
                    Language.Localize(trait.displayName, false, false, string.Empty));
                if (!string.IsNullOrEmpty(name))
                    lines.Add(name);

                if (!string.IsNullOrEmpty(trait.description))
                {
                    string desc = UITextExtractor.CleanText(
                        Language.Localize(trait.description, false, false, string.Empty));
                    if (!string.IsNullOrEmpty(desc))
                        lines.Add(desc);
                }

                if (!string.IsNullOrEmpty(trait.effectsDescription))
                {
                    string effects = UITextExtractor.CleanText(
                        Language.Localize(trait.effectsDescription, false, false, string.Empty));
                    if (!string.IsNullOrEmpty(effects))
                        lines.Add("Effects: " + effects);
                }

                var pc = MonoBehaviourSingleton<Game>.HasInstance()
                    ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                    : null;

                if (trait.requiredStatValues != null && trait.requiredStatValues.Count > 0)
                {
                    foreach (var kvp in trait.requiredStatValues)
                    {
                        string statDisplayName = MonoBehaviourSingleton<PCStatsManager>.GetInstance()
                            .GetCharacteristicDisplayName(kvp.Key);
                        string localized = UITextExtractor.CleanText(
                            Language.Localize(statDisplayName, false, false, string.Empty));
                        string mark = "";
                        if (pc != null && pc.pcStats != null)
                        {
                            int actual = pc.pcStats.GetCharacteristic(kvp.Key);
                            mark = actual >= kvp.Value ? " (met)" : " (not met)";
                        }
                        lines.Add($"Requires {kvp.Value} {localized}{mark}");
                    }
                }

                if (trait.requiredTraits != null && trait.requiredTraits.Length > 0)
                {
                    foreach (var reqTrait in trait.requiredTraits)
                    {
                        if (reqTrait == null) continue;
                        string reqName = UITextExtractor.CleanText(
                            Language.Localize(reqTrait.displayName, false, false, string.Empty));
                        string mark = "";
                        if (pc != null && pc.pcTemplate != null)
                            mark = pc.pcTemplate.HasTrait(reqTrait.name) ? " (met)" : " (not met)";
                        lines.Add($"Requires perk: {reqName}{mark}");
                    }
                }

                if (trait.subTraits != null && trait.subTraits.Length > 0)
                {
                    foreach (var subTrait in trait.subTraits)
                    {
                        if (subTrait == null) continue;
                        string subName = UITextExtractor.CleanText(
                            Language.Localize(subTrait.displayName, false, false, string.Empty));
                        lines.Add("Unlocks: " + subName);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error building trait description: {ex.Message}");
            }

            return lines;
        }

        // ========== Derived Stats ==========

        public static string FormatDerivedStatValue(int rawValue, DerivedStat.StatDisplayType displayType)
        {
            switch (displayType)
            {
                case DerivedStat.StatDisplayType.Percent:
                    return Mathf.Clamp(rawValue, 0, 100) + "%";
                case DerivedStat.StatDisplayType.ActionPoint:
                    return rawValue + " AP";
                case DerivedStat.StatDisplayType.CombatMovement:
                    float val = (float)rawValue / 100f;
                    return val.ToString("F1");
                case DerivedStat.StatDisplayType.Meters:
                    return rawValue + " meters";
                case DerivedStat.StatDisplayType.Pounds:
                    return rawValue + " lbs";
                default:
                    return rawValue.ToString();
            }
        }

        public static void AnnounceDerivedStat(PC pc, int index, bool interrupt = true)
        {
            if (index < 0 || index >= DerivedStatNames.Length) return;

            string statName = DerivedStatNames[index];

            try
            {
                var statsManager = MonoBehaviourSingleton<PCStatsManager>.GetInstance();
                DerivedStat stat = statsManager.GetStat(statName);
                if (stat == null)
                {
                    ScreenReaderManager.SpeakInterrupt("Unknown stat");
                    return;
                }

                string displayName = UITextExtractor.CleanText(
                    Language.Localize(stat.displayName, false, false, string.Empty));

                string valueText = "unknown";
                if (pc != null)
                {
                    int rawValue = pc.pcStats.GetDerivedStat(statName);
                    valueText = FormatDerivedStatValue(rawValue, stat.displayType);
                }

                string announcement = $"{displayName}, {valueText}, {index + 1} of {DerivedStatNames.Length}";
                if (interrupt)
                    ScreenReaderManager.SpeakInterrupt(announcement);
                else
                    ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[CharacterAnnouncementHelper] Derived stat [{index}]: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing derived stat: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Error reading stat");
            }
        }

        public static void AnnounceDerivedStatDescription(PC pc, int index)
        {
            if (index < 0 || index >= DerivedStatNames.Length) return;

            string statName = DerivedStatNames[index];

            try
            {
                var statsManager = MonoBehaviourSingleton<PCStatsManager>.GetInstance();
                BaseStat characteristic = statsManager.GetCharacteristic(statName);
                if (characteristic == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No description available");
                    return;
                }

                string name = UITextExtractor.CleanText(
                    Language.Localize(characteristic.displayName, false, false, string.Empty));
                string desc = UITextExtractor.CleanText(
                    Language.Localize(characteristic.description, false, false, string.Empty));

                var parts = new List<string>();
                parts.Add(name);
                if (!string.IsNullOrEmpty(desc))
                    parts.Add(desc);

                if (pc != null && pc.pcTemplate != null)
                {
                    string traitInfo = pc.pcTemplate.GetTraitBaseStatTooltipString(characteristic);
                    if (!string.IsNullOrEmpty(traitInfo))
                        parts.Add(UITextExtractor.CleanText(traitInfo));
                }

                ScreenReaderManager.SpeakInterrupt(string.Join(". ", parts.ToArray()));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error getting derived stat description: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("No description available");
            }
        }

        // ========== Header Snapshot (Character Info Menu) ==========

        /// <summary>
        /// Builds the header snapshot as a list of individually-browsable lines.
        /// Each line = one piece of info the always-visible CharacterInfoMenu header shows.
        /// Order: name+level+rank, HP, capacity, money, water, points-available, status effects.
        /// </summary>
        public static List<string> BuildHeaderSnapshotLines(PC pc)
        {
            var lines = new List<string>();
            if (pc == null) return lines;

            try
            {
                var tmpl = pc.pcTemplate;
                if (tmpl != null)
                {
                    string name = UITextExtractor.CleanText(Language.Localize(tmpl.displayName, false, false, string.Empty));
                    int level = pc.stats != null ? pc.stats.GetLevel() : tmpl.level;
                    string rank = UITextExtractor.CleanText(
                        Language.Localize(Table_Leveling.GetRank(level), false, false, string.Empty));
                    lines.Add($"{name}, level {level}, {rank}");
                }
            }
            catch { }

            try
            {
                if (pc.stats != null)
                {
                    float maxHP = pc.stats.GetMaxHP();
                    string hp = $"Health {Mathf.RoundToInt(pc.curHP)} of {Mathf.RoundToInt(maxHP)}";
                    if (pc.healthState != PC.HealthState.Healthy)
                        hp += $", {pc.healthState}";
                    lines.Add(hp);
                }
            }
            catch { }

            try
            {
                if (pc.pcStats != null)
                {
                    int cur = pc.pcStats.RecalculateCurrentWeight();
                    int max = Mathf.FloorToInt(pc.pcStats.GetMaxWeight());
                    string note = "";
                    if (max > 0)
                    {
                        float ratio = (float)cur / max;
                        if (ratio >= 1f) note = ", over encumbered";
                        else if (ratio >= 0.8f) note = ", near capacity";
                    }
                    lines.Add($"Capacity {cur} of {max} pounds{note}");
                }
            }
            catch { }

            try
            {
                if (MonoBehaviourSingleton<Game>.HasInstance())
                {
                    var game = MonoBehaviourSingleton<Game>.GetInstance();
                    lines.Add($"Money: {game.partyCurrency} dollars");
                    lines.Add($"Water: {game.water} of {game.GetMaxWater()}");
                }
            }
            catch { }

            try
            {
                var tmpl = pc.pcTemplate;
                if (tmpl != null)
                {
                    if (tmpl.availableAttributePoints > 0)
                        lines.Add($"{tmpl.availableAttributePoints} attribute point{(tmpl.availableAttributePoints == 1 ? "" : "s")} available");
                    if (tmpl.availableSkillPoints > 0)
                        lines.Add($"{tmpl.availableSkillPoints} skill point{(tmpl.availableSkillPoints == 1 ? "" : "s")} available");
                    if (tmpl.availableTraitPoints > 0)
                        lines.Add($"{tmpl.availableTraitPoints} perk point{(tmpl.availableTraitPoints == 1 ? "" : "s")} available");
                }
            }
            catch { }

            // Status effects — one line per effect, with full StatusEffectHelper detail
            try
            {
                if (pc.template != null && pc.template.statusEffects != null && pc.template.statusEffects.Count > 0)
                {
                    foreach (var eff in pc.template.statusEffects)
                    {
                        string line = StatusEffectHelper.BuildEffectLine(eff);
                        if (!string.IsNullOrEmpty(line))
                            lines.Add("Status: " + line);
                    }
                }
            }
            catch { }

            return lines;
        }

        /// <summary>
        /// Builds a short "points available" announcement, suitable for the auto-announcement
        /// when entering the Attributes / Skills / Perks tab.
        /// </summary>
        public static string BuildPointsAvailableHint(PC pc, CharacterInfoMenu.InfoPanel panel)
        {
            if (pc == null || pc.pcTemplate == null) return "";
            var tmpl = pc.pcTemplate;
            switch (panel)
            {
                case CharacterInfoMenu.InfoPanel.Attributes:
                    if (tmpl.availableAttributePoints > 0)
                        return $"{tmpl.availableAttributePoints} attribute point{(tmpl.availableAttributePoints == 1 ? "" : "s")} available";
                    break;
                case CharacterInfoMenu.InfoPanel.Skills:
                    if (tmpl.availableSkillPoints > 0)
                        return $"{tmpl.availableSkillPoints} skill point{(tmpl.availableSkillPoints == 1 ? "" : "s")} available";
                    break;
                case CharacterInfoMenu.InfoPanel.Traits:
                    if (tmpl.availableTraitPoints > 0)
                        return $"{tmpl.availableTraitPoints} perk point{(tmpl.availableTraitPoints == 1 ? "" : "s")} available";
                    break;
            }
            return "";
        }

        // ========== Combat Snapshot ==========

        /// <summary>
        /// Builds the combat snapshot as a list of individually-browsable lines.
        /// Mirrors the StatDisplayList "Combat" view plus armor.
        /// </summary>
        public static List<string> BuildCombatSnapshotLines(PC pc)
        {
            var lines = new List<string>();
            if (pc == null || pc.pcStats == null) return lines;
            var statsManager = MonoBehaviourSingleton<PCStatsManager>.GetInstance();

            // Damage (current weapon range)
            try
            {
                var weaponInst = pc.pcStats.GetWeaponInstance();
                var weaponTmpl = pc.pcStats.GetWeaponTemplate();
                if (weaponInst != null && weaponTmpl != null)
                {
                    Targetable.DamageMitigation mit = Targetable.DamageMitigation.None;
                    int min = pc.CalculateDamage(null, pc.transform.position, weaponInst.GetMinDamage(), out mit, weaponTmpl);
                    int max = pc.CalculateDamage(null, pc.transform.position, weaponInst.GetMaxDamage(), out mit, weaponTmpl);
                    string dmgText = (min == max) ? min.ToString() : $"{min} to {max}";
                    lines.Add($"Damage: {dmgText}");
                }
                else if (weaponTmpl != null)
                {
                    string dmgText = (weaponTmpl.minDamage == weaponTmpl.maxDamage)
                        ? weaponTmpl.minDamage.ToString()
                        : $"{weaponTmpl.minDamage} to {weaponTmpl.maxDamage}";
                    lines.Add($"Damage: {dmgText}");
                }
            }
            catch { }

            string[] combatStats = new string[]
            {
                PCStatsManager.chanceToHit,
                PCStatsManager.criticalHitChance,
                PCStatsManager.chanceToEvade,
                PCStatsManager.armor,
                PCStatsManager.attackRange,
                PCStatsManager.actionPoints,
                PCStatsManager.actionRechargeRate,
                PCStatsManager.combatSpeed,
            };
            foreach (var statName in combatStats)
            {
                try
                {
                    var characteristic = statsManager.GetCharacteristic(statName);
                    if (characteristic == null) continue;
                    string display = UITextExtractor.CleanText(
                        Language.Localize(characteristic.displayName, false, false, string.Empty));
                    int raw = pc.pcStats.GetCharacteristic(statName);
                    string value;
                    if (characteristic is DerivedStat ds)
                        value = FormatDerivedStatValue(raw, ds.displayType);
                    else
                        value = raw.ToString();
                    lines.Add($"{display}: {value}");
                }
                catch { }
            }

            return lines;
        }

        // ========== Character Summary ==========

        public static void AnnounceCharacterSummary(PC pc)
        {
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No character selected");
                return;
            }

            var parts = new List<string>();

            if (pc.pcTemplate != null)
            {
                string name = UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                parts.Add(name);

                string spec = pc.pcTemplate.GetLocalizedSpecialization();
                if (!string.IsNullOrEmpty(spec))
                    parts.Add(UITextExtractor.CleanText(spec));

                parts.Add($"Level {pc.pcTemplate.level}");
            }

            ScreenReaderManager.SpeakInterrupt(string.Join(", ", parts.ToArray()));
        }

        // ========== XP ==========

        public static string BuildXPAnnouncement(PC pc)
        {
            if (pc == null || pc.pcTemplate == null)
                return null;

            var tmpl = pc.pcTemplate;
            int level = tmpl.GetCurrentLevel();

            if (tmpl.IsAtMaxLevel())
                return $"Level {level}, max level. Experience {tmpl.GetXP()}";

            int xpCur = tmpl.GetXP();
            int xpNext = tmpl.GetXPForLevel(level + 1);
            string msg = $"Level {level}. Experience {xpCur} of {xpNext}";
            if (pc.CanLevelUp(ignoreHealthState: true))
                msg += ". Level up available";
            return msg;
        }

        public static void AnnounceXP(PC pc, bool interrupt = true)
        {
            string msg = BuildXPAnnouncement(pc);
            if (string.IsNullOrEmpty(msg))
            {
                ScreenReaderManager.SpeakInterrupt("No character selected");
                return;
            }
            if (interrupt) ScreenReaderManager.SpeakInterrupt(msg);
            else ScreenReaderManager.Speak(msg);
        }

        // ========== Value Adjustment ==========

        public static void AdjustAttribute(CHA_AttributeEditor editor, int direction, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (direction > 0)
            {
                if (editor.CanIncreaseValue())
                {
                    if (attrOnPlusClickedMethod != null)
                        attrOnPlusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Maximum");
                }
            }
            else
            {
                if (editor.CanDecreaseValue())
                {
                    if (attrOnMinusClickedMethod != null)
                        attrOnMinusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Minimum");
                }
            }
        }

        public static void AdjustSkill(CHA_SkillEditor editor, int direction, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (direction > 0)
            {
                if (skillOnPlusClickedMethod != null)
                {
                    skillOnPlusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
            }
            else
            {
                if (skillOnMinusClickedMethod != null)
                {
                    skillOnMinusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Invokes the trait's pressed callback (sets currentEditor in CHA_TraitsPanel)
        /// then toggles the checkbox. Required to maintain proper game state.
        /// </summary>
        public static void ToggleTrait(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (editor.checkboxButton != null && !editor.checkboxButton.isEnabled)
            {
                ScreenReaderManager.SpeakInterrupt("Locked");
                return;
            }

            // Must call pressedCallback BEFORE toggling checkbox
            if (pressedCallbackField != null)
            {
                var callback = pressedCallbackField.GetValue(editor) as Delegate;
                callback?.DynamicInvoke(editor);
            }

            bool before = editor.checkbox.value;
            editor.checkbox.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            bool after = editor.checkbox.value;
            if (before != after)
            {
                string state = after ? "selected" : "not selected";
                ScreenReaderManager.SpeakInterrupt(state);
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Cannot select");
            }
        }
    }
}
