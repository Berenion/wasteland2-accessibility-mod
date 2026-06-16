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
            ModLog.Debug("[CharacterAnnouncementHelper] Reflection cached");
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

                // Buffed/debuffed, naming *why* and always giving the magnitude.
                AppendAttributeBuffClause(parts, editor);

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

                // Buffed / debuffed / unlearned, naming the source for direct skill
                // modifiers. A skill can also be debuffed indirectly (its cap drops when
                // the associated attribute is debuffed); that has no flat source here, so
                // BuildStatSourceClause returns "" and we fall back to the bare word.
                string state = GetSkillState(editor);
                if (!string.IsNullOrEmpty(state))
                {
                    if (state == "buffed" || state == "debuffed")
                    {
                        var tmpl = GetSkillEditorPcStats(editor)?.GetPCTemplate();
                        string sources = BuildStatSourceClause(tmpl, editor.skillName, state == "debuffed");
                        parts.Add(string.IsNullOrEmpty(sources) ? state : $"{state} by {sources}");
                    }
                    else
                    {
                        parts.Add(state);
                    }
                }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] GetAttributeMaxValue failed: {ex.Message}"); }
            return 0;
        }

        /// <summary>
        /// Appends a "buffed by X" / "debuffed by X" clause for an attribute when the game has
        /// modified it. Detection is by the value label's colour — exactly what the game itself
        /// displays (CHA_AttributeEditor.SetAttribute sets valueLabel.color to GUIManager's
        /// buffed/debuffed colour), so it can never disagree with the on-screen state and needs
        /// no PC/template lookup. The magnitude/source is enrichment: name the flat source(s)
        /// from the live PC when we can, else fall back to the numeric delta. If neither can be
        /// resolved we still announce the bare "buffed"/"debuffed" word from the colour.
        /// </summary>
        private static void AppendAttributeBuffClause(List<string> parts, CHA_AttributeEditor editor)
        {
            try
            {
                if (string.IsNullOrEmpty(editor.attribute)) return;

                // 1. Authoritative detection: the colour the game painted the value.
                string word = null;
                bool wantNegative = false;
                if (editor.valueLabel != null)
                {
                    Color c = editor.valueLabel.color;
                    if (c == GUIManager.debuffedTextColor) { word = "debuffed"; wantNegative = true; }
                    else if (c == GUIManager.buffedTextColor) { word = "buffed"; wantNegative = false; }
                }
                if (word == null) return;   // not modified

                // 2. Enrichment: named source(s) from the live PC, else numeric magnitude.
                string sources = "";
                int delta = 0;
                var editorTmpl = GetAttributeEditorPcTemplate(editor);
                PC pc = editorTmpl != null ? editorTmpl.GetPC() : null;
                if (pc == null || pc.pcStats == null)
                {
                    pc = MonoBehaviourSingleton<Game>.HasInstance()
                        ? MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC()
                        : null;
                }
                if (pc != null && pc.pcStats != null)
                {
                    PCTemplate liveTmpl = pc.pcStats.GetPCTemplate();
                    if (liveTmpl != null)
                    {
                        delta = pc.pcStats.GetAttribute(editor.attribute) - liveTmpl.GetAttribute(editor.attribute);
                        sources = BuildStatSourceClause(liveTmpl, editor.attribute, wantNegative);
                    }
                }
                if (string.IsNullOrEmpty(sources) && delta != 0)
                    sources = Mathf.Abs(delta).ToString();   // numeric fallback when no flat source is nameable

                parts.Add(string.IsNullOrEmpty(sources) ? word : $"{word} by {sources}");
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] AppendAttributeBuffClause failed: {ex.Message}"); }
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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] GetSkillCap failed: {ex.Message}"); }
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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] GetSkillState failed: {ex.Message}"); }
            return "";
        }

        /// <summary>
        /// Names the active sources moving a stat in the given direction so a
        /// "debuffed"/"buffed" announcement can explain *why*. Scans the same three
        /// modifier sources PCStats.RecalculateAllStats sums — status effects,
        /// equipment, and active traits — for any flat contribution to <paramref name="statName"/>.
        /// Returns e.g. "Radiation Sickness minus 2, Leather Armor minus 1", or "" when
        /// no concrete flat source is found (e.g. the debuff comes from a trait percent
        /// modifier or, for skills, an attribute-cap drop the game applies separately).
        /// </summary>
        /// <param name="wantNegative">true to list debuff sources (negative), false for buffs (positive).</param>
        public static string BuildStatSourceClause(PCTemplate template, string statName, bool wantNegative)
        {
            if (template == null || string.IsNullOrEmpty(statName)) return "";
            var sources = new List<string>();

            // --- Status effects (radiation, injuries, drugs, etc.) ---
            try
            {
                if (template.statusEffects != null)
                {
                    foreach (var eff in template.statusEffects)
                    {
                        if (eff == null || eff.statEffects == null) continue;
                        int amt = 0;
                        foreach (var se in eff.statEffects)
                            if (se != null && se.statName == statName) amt += se.amount;
                        if (Wants(amt, wantNegative))
                            sources.Add(FormatStatSource(EffectDisplayName(eff), amt));
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildStatSourceClause status-effect section failed: {ex.Message}"); }

            // --- Equipment (armor/weapon penalties or bonuses) ---
            try
            {
                if (template.equipment != null)
                {
                    for (int i = 0; i < template.equipment.Length; i++)
                    {
                        // Slots 9 and 10 are skipped by GetEquipmentBonus itself.
                        if (i == 9 || i == 10) continue;
                        var eq = template.equipment[i];
                        if (eq == null) continue;
                        int bonus = eq.GetBonus(statName);
                        if (Wants(bonus, wantNegative))
                            sources.Add(FormatStatSource(EquipmentDisplayName(eq), bonus));
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildStatSourceClause equipment section failed: {ex.Message}"); }

            // --- Active traits / quirks ---
            try
            {
                var traits = template.GetActiveTraits();
                if (traits != null)
                {
                    foreach (var t in traits)
                    {
                        if (t == null) continue;
                        int amt = t.GetStat(statName);
                        if (Wants(amt, wantNegative))
                            sources.Add(FormatStatSource(TraitDisplayName(t), amt));
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildStatSourceClause trait section failed: {ex.Message}"); }

            return string.Join(", ", sources.ToArray());
        }

        private static bool Wants(int amount, bool wantNegative)
        {
            if (amount == 0) return false;
            return wantNegative ? amount < 0 : amount > 0;
        }

        private static string FormatStatSource(string name, int amount)
        {
            if (string.IsNullOrEmpty(name)) name = "unknown source";
            string sign = amount > 0 ? "plus" : "minus";
            return $"{name} {sign} {Math.Abs(amount)}";
        }

        private static string EffectDisplayName(StatusEffect eff)
        {
            try
            {
                if (!string.IsNullOrEmpty(eff.displayName))
                {
                    string n = UITextExtractor.CleanText(Language.Localize(eff.displayName, false, false, string.Empty));
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] EffectDisplayName failed: {ex.Message}"); }
            return eff != null ? eff.name : null;
        }

        private static string EquipmentDisplayName(ItemInstance_Equipment eq)
        {
            try
            {
                if (eq.template != null && !string.IsNullOrEmpty(eq.template.displayName))
                    return UITextExtractor.CleanText(Language.Localize(eq.template.displayName, false, false, string.Empty));
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] EquipmentDisplayName failed: {ex.Message}"); }
            return null;
        }

        private static string TraitDisplayName(Trait t)
        {
            try
            {
                if (!string.IsNullOrEmpty(t.displayName))
                    return UITextExtractor.CleanText(Language.Localize(t.displayName, false, false, string.Empty));
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] TraitDisplayName failed: {ex.Message}"); }
            return null;
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

                // Inline the short effects text so a quirk reads as "Name, available, +1
                // Intelligence, quirk" — the player can tell what it does without pressing I
                // (which still opens the full breakdown for the longer details).
                string inlineEffects = GetTraitInlineEffects(trait);
                if (!string.IsNullOrEmpty(inlineEffects))
                    parts.Add(inlineEffects);

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
        /// Short, one-line effects text for a trait/quirk, spoken inline on its navigation
        /// announcement. Prefers the concise effectsDescription (the mechanical summary,
        /// e.g. "+1 to Intelligence"); falls back to the flavour description. Newlines are
        /// flattened so it stays a single spoken clause. Returns "" for the "no quirk"
        /// placeholder or when neither field is set.
        /// </summary>
        private static string GetTraitInlineEffects(Trait trait)
        {
            if (trait == null) return "";
            try
            {
                string text = "";
                if (!string.IsNullOrEmpty(trait.effectsDescription))
                    text = UITextExtractor.CleanText(
                        Language.Localize(trait.effectsDescription, false, false, string.Empty));
                if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(trait.description))
                    text = UITextExtractor.CleanText(
                        Language.Localize(trait.description, false, false, string.Empty));
                if (string.IsNullOrEmpty(text)) return "";
                return text.Replace("\r\n", " ").Replace("\n", " ").Trim();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterAnnouncementHelper] Error building inline trait effects: {ex.Message}");
                return "";
            }
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
