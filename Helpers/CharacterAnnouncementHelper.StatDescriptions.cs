using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    // Stat- and trait-description builders for the info browser, plus the
    // CHA_DescriptionPanel reflection helpers that surface "+1 preview" /
    // "perks unlocked" text. See CharacterAnnouncementHelper.cs for the rest of
    // the partial class.
    public static partial class CharacterAnnouncementHelper
    {
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
    }
}
