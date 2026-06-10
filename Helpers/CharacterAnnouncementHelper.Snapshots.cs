using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    // Derived stats, header / combat snapshots, character summary, and XP
    // announcements. See CharacterAnnouncementHelper.cs for the rest of the
    // partial class.
    public static partial class CharacterAnnouncementHelper
    {
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
                ModLog.Debug($"[CharacterAnnouncementHelper] Derived stat [{index}]: {announcement}");
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

        /// <summary>
        /// Builds the derived-stats list as individually-browsable lines, in DerivedStatNames order.
        /// One line per stat, formatted "{name}: {value}".
        /// </summary>
        public static List<string> BuildDerivedStatLines(PC pc)
        {
            var lines = new List<string>();
            if (!MonoBehaviourSingleton<PCStatsManager>.HasInstance()) return lines;
            var statsManager = MonoBehaviourSingleton<PCStatsManager>.GetInstance();

            for (int i = 0; i < DerivedStatNames.Length; i++)
            {
                string statName = DerivedStatNames[i];
                try
                {
                    DerivedStat stat = statsManager.GetStat(statName);
                    if (stat == null) continue;
                    string displayName = UITextExtractor.CleanText(
                        Language.Localize(stat.displayName, false, false, string.Empty));
                    string valueText = "unknown";
                    if (pc != null)
                    {
                        int rawValue = pc.pcStats.GetDerivedStat(statName);
                        valueText = FormatDerivedStatValue(rawValue, stat.displayType);
                    }
                    lines.Add($"{displayName}: {valueText}");
                }
                catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildDerivedStatLines failed for '{statName}': {ex.Message}"); }
            }
            return lines;
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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines name/level/rank failed: {ex.Message}"); }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines health failed: {ex.Message}"); }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines capacity failed: {ex.Message}"); }

            try
            {
                if (MonoBehaviourSingleton<Game>.HasInstance())
                {
                    var game = MonoBehaviourSingleton<Game>.GetInstance();
                    lines.Add($"Money: {game.partyCurrency} dollars");
                    lines.Add($"Water: {game.water} of {game.GetMaxWater()}");
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines money/water failed: {ex.Message}"); }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines points-available failed: {ex.Message}"); }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildHeaderSnapshotLines status effects failed: {ex.Message}"); }

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
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildCombatSnapshotLines damage failed: {ex.Message}"); }

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
                catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildCombatSnapshotLines failed for '{statName}': {ex.Message}"); }
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

        // ========== Party Member Info (browsable panel) ==========

        /// <summary>
        /// Builds the browsable party-member info panel shared by CombatState and MapCursorState.
        /// Health / Weapon / Status sections are identical in both; the combat context adds AP,
        /// Stance and a weapon "jammed" note, while the exploration context shows Level/XP instead.
        /// Single source of truth so the same character is never described two ways.
        /// </summary>
        public static List<string> BuildPartyMemberInfoLines(PC pc, bool combat)
        {
            var lines = new List<string>();
            if (pc == null) return lines;

            // --- Health ---
            try
            {
                float maxHP = pc.stats.GetMaxHP();
                float hpPercent = maxHP > 0 ? (pc.curHP / maxHP) * 100f : 0;
                string healthLine = "Health: " + Mathf.RoundToInt(pc.curHP) + " of " + Mathf.RoundToInt(maxHP)
                    + " (" + hpPercent.ToString("F0") + "%)";
                if (pc.healthState != PC.HealthState.Healthy)
                    healthLine += ", " + pc.healthState.ToString();
                lines.Add(healthLine);
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines health section failed: {ex.Message}"); }

            if (combat)
            {
                // --- AP ---
                try
                {
                    lines.Add("AP: " + pc.combatActionPointsRemaining + " of " + pc.stats.GetActionPoints());
                }
                catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines AP section failed: {ex.Message}"); }

                // --- Stance / Cover ---
                try
                {
                    var stanceParts = new List<string>();
                    if (pc.isCrouching) stanceParts.Add("crouching");
                    if (pc.inCover)
                        stanceParts.Add(pc.coverType == Cover.CoverType.Tall ? "tall cover" : "short cover");
                    if (pc.isHidden) stanceParts.Add("hidden");
                    if (stanceParts.Count > 0)
                        lines.Add("Stance: " + string.Join(", ", stanceParts.ToArray()));
                }
                catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines stance section failed: {ex.Message}"); }
            }
            else
            {
                // --- Level / XP ---
                try
                {
                    string xpLine = BuildXPAnnouncement(pc);
                    if (!string.IsNullOrEmpty(xpLine))
                        lines.Add(xpLine);
                }
                catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines XP section failed: {ex.Message}"); }
            }

            // --- Weapon ---
            try
            {
                var weaponInstance = pc.pcStats.GetWeaponInstance();
                if (weaponInstance != null && weaponInstance.template != null)
                {
                    string weaponName = UITextExtractor.CleanText(
                        Language.Localize(weaponInstance.template.displayName, false, false, string.Empty));
                    string weaponLine = "Weapon: " + weaponName;

                    var ranged = weaponInstance as ItemInstance_WeaponRanged;
                    if (ranged != null)
                    {
                        weaponLine += ", " + ranged.GetAmmoCount() + " of " + ranged.GetClipSize() + " ammo";
                        if (combat && pc.IsJammed()) weaponLine += ", jammed";
                    }
                    lines.Add(weaponLine);
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines weapon section failed: {ex.Message}"); }

            // --- Status Effects ---
            try
            {
                if (pc.template != null && pc.template.statusEffects != null
                    && pc.template.statusEffects.Count > 0)
                {
                    foreach (var effect in pc.template.statusEffects)
                    {
                        string line = StatusEffectHelper.BuildEffectLine(effect);
                        if (!string.IsNullOrEmpty(line))
                            lines.Add(line);
                    }
                }
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterAnnouncementHelper] BuildPartyMemberInfoLines status effects section failed: {ex.Message}"); }

            return lines;
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
    }
}
