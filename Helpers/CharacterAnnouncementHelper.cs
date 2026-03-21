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
        internal static FieldInfo attrEditorCurrentValueField;
        internal static FieldInfo traitEditorTraitField;
        internal static FieldInfo pressedCallbackField;
        internal static MethodInfo skillOnPlusClickedMethod;
        internal static MethodInfo skillOnMinusClickedMethod;
        internal static MethodInfo attrOnPlusClickedMethod;
        internal static MethodInfo attrOnMinusClickedMethod;

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
            skillOnPlusClickedMethod = typeof(CHA_SkillEditor).GetMethod("OnPlusClicked", flags);
            skillOnMinusClickedMethod = typeof(CHA_SkillEditor).GetMethod("OnMinusClicked", flags);

            attrEditorCurrentValueField = typeof(CHA_AttributeEditor).GetField("currentValue", flags);
            attrOnPlusClickedMethod = typeof(CHA_AttributeEditor).GetMethod("OnPlusClicked", flags);
            attrOnMinusClickedMethod = typeof(CHA_AttributeEditor).GetMethod("OnMinusClicked", flags);

            traitEditorTraitField = typeof(CHA_TraitEditor).GetField("trait", flags);
            pressedCallbackField = typeof(CHA_TraitEditor).GetField("pressedCallback", flags);

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

                return $"{name}, {value}, attribute";
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

                return $"{name}, level {value}, skill";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing skill: {ex.Message}");
                return "Skill";
            }
        }

        public static string GetTraitEditorAnnouncement(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            try
            {
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

                if (string.IsNullOrEmpty(name))
                    name = "Unknown trait";

                string checkedState = editor.checkbox != null ? (editor.checkbox.value ? "selected" : "not selected") : "";

                return $"{name}, {checkedState}, quirk";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error announcing trait: {ex.Message}");
                return "Trait";
            }
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
        /// Announces the full description for an attribute or skill editor GameObject.
        /// </summary>
        public static void AnnounceStatDescription(GameObject obj)
        {
            if (obj == null)
            {
                ScreenReaderManager.SpeakInterrupt("No description available");
                return;
            }

            string characteristicName = null;
            string levelText = null;
            string shortDesc = null;

            var attrEditor = obj.GetComponent<CHA_AttributeEditor>();
            if (attrEditor != null)
            {
                characteristicName = attrEditor.attribute;
                levelText = attrEditor.valueLabel != null ? UITextExtractor.CleanText(attrEditor.valueLabel.text) : null;
                shortDesc = attrEditor.descriptionLabel != null ? UITextExtractor.CleanText(attrEditor.descriptionLabel.text) : null;
            }

            var skillEditor = obj.GetComponent<CHA_SkillEditor>();
            if (skillEditor != null)
            {
                characteristicName = skillEditor.skillName;
                levelText = skillEditor.levelLabel != null ? "Level " + UITextExtractor.CleanText(skillEditor.levelLabel.text) : null;
            }

            if (string.IsNullOrEmpty(characteristicName))
            {
                ScreenReaderManager.SpeakInterrupt("No description available");
                return;
            }

            try
            {
                var baseStat = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetCharacteristic(characteristicName);
                if (baseStat == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No description available");
                    return;
                }

                string name = UITextExtractor.CleanText(Language.Localize(baseStat.displayName, false, false, string.Empty));
                string fullDesc = UITextExtractor.CleanText(Language.Localize(baseStat.description, false, false, string.Empty));

                var parts = new List<string>();
                parts.Add(name);
                if (!string.IsNullOrEmpty(levelText))
                    parts.Add(levelText);
                if (!string.IsNullOrEmpty(shortDesc))
                    parts.Add(shortDesc);
                if (!string.IsNullOrEmpty(fullDesc))
                    parts.Add(fullDesc);

                ScreenReaderManager.SpeakInterrupt(string.Join(". ", parts.ToArray()));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error getting stat description: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("No description available");
            }
        }

        // ========== Trait Descriptions ==========

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
                        var parts = new List<string>();

                        if (!string.IsNullOrEmpty(trait.description))
                        {
                            string desc = UITextExtractor.CleanText(
                                Language.Localize(trait.description, false, false, string.Empty));
                            if (!string.IsNullOrEmpty(desc))
                                parts.Add(desc);
                        }

                        if (!string.IsNullOrEmpty(trait.effectsDescription))
                        {
                            string effects = UITextExtractor.CleanText(
                                Language.Localize(trait.effectsDescription, false, false, string.Empty));
                            if (!string.IsNullOrEmpty(effects))
                                parts.Add(effects);
                        }

                        if (trait.requiredStatValues != null && trait.requiredStatValues.Count > 0)
                        {
                            var reqParts = new List<string>();
                            reqParts.Add("Requirements:");
                            foreach (var kvp in trait.requiredStatValues)
                            {
                                string statDisplayName = MonoBehaviourSingleton<PCStatsManager>.GetInstance()
                                    .GetCharacteristicDisplayName(kvp.Key);
                                string localized = UITextExtractor.CleanText(
                                    Language.Localize(statDisplayName, false, false, string.Empty));
                                reqParts.Add($"{kvp.Value} {localized}");
                            }
                            parts.Add(string.Join(", ", reqParts.ToArray()));
                        }

                        if (trait.requiredTraits != null && trait.requiredTraits.Length > 0)
                        {
                            var traitNames = new List<string>();
                            foreach (var reqTrait in trait.requiredTraits)
                            {
                                if (reqTrait != null)
                                {
                                    string name = UITextExtractor.CleanText(
                                        Language.Localize(reqTrait.displayName, false, false, string.Empty));
                                    traitNames.Add(name);
                                }
                            }
                            if (traitNames.Count > 0)
                                parts.Add("Requires: " + string.Join(", ", traitNames.ToArray()));
                        }

                        if (trait.subTraits != null && trait.subTraits.Length > 0)
                        {
                            var unlockNames = new List<string>();
                            foreach (var subTrait in trait.subTraits)
                            {
                                if (subTrait != null)
                                {
                                    string name = UITextExtractor.CleanText(
                                        Language.Localize(subTrait.displayName, false, false, string.Empty));
                                    unlockNames.Add(name);
                                }
                            }
                            if (unlockNames.Count > 0)
                                parts.Add("Unlocks: " + string.Join(", ", unlockNames.ToArray()));
                        }

                        if (parts.Count > 0)
                            return string.Join(". ", parts.ToArray());
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
