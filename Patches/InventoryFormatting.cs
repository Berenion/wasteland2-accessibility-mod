using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Formatting and calculation helpers for inventory item announcements.
    /// Replicates ItemInfoBox / WeaponRangeTooltip / AttackModePanel logic so a
    /// blind player can hear the same numbers a sighted player sees.
    /// Used by InventoryPatches (this directory), InventoryState, and ShopState.
    /// </summary>
    public static class InventoryFormatting
    {
        internal static string lastAnnouncedItem = "";

        /// <summary>
        /// Formats an ItemInstance for screen reader announcement
        /// </summary>
        internal static string FormatItemAnnouncement(ItemInstance item, bool detailed = false)
        {
            if (item == null || item.template == null)
                return "Empty slot";

            string itemName = UITextExtractor.CleanText(item.template.displayName);
            string announcement = itemName;

            // Add quantity if stackable and more than 1
            if (item.quantity > 1)
            {
                announcement = $"{itemName}, quantity {item.quantity}";
            }

            if (detailed)
            {
                // Add item type information
                if (item is ItemInstance_Weapon)
                {
                    announcement += ", weapon";
                }
                else if (item is ItemInstance_Armor)
                {
                    announcement += ", armor";
                }
                else if (item is ItemInstance_Usable)
                {
                    announcement += ", consumable";
                }
                else if (item is ItemInstance_Ammo)
                {
                    announcement += ", ammo";
                }
                else if (item is ItemInstance_Component)
                {
                    announcement += ", crafting component";
                }

                // Add weight
                float weight = item.GetWeight();
                if (weight > 0)
                {
                    announcement += $", weight {weight:0.0}";
                }

                // Add equipped status
                if (item is ItemInstance_Equipment equipment)
                {
                    // Check if this item is currently equipped
                    // This will be checked in the context of the owning character
                }
            }

            return announcement;
        }

        /// <summary>
        /// Formats comprehensive item details for screen reader (called on selection, not hover)
        /// </summary>
        internal static string FormatDetailedItemInfo(ItemInstance item, PC pc = null)
        {
            if (item == null || item.template == null)
                return "No item selected";

            List<string> details = new List<string>();

            // Basic info
            string itemName = UITextExtractor.CleanText(item.template.displayName);
            details.Add(itemName);

            // Quantity
            if (item.quantity > 1)
            {
                details.Add($"Quantity {item.quantity}");
            }

            // Item type
            if (item is ItemInstance_Weapon weapon)
            {
                ItemTemplate_Weapon weaponTemplate = weapon.template as ItemTemplate_Weapon;
                string skillType = GetWeaponSkillDisplayName(weaponTemplate);
                details.Add(string.IsNullOrEmpty(skillType) ? "Weapon" : skillType);

                if (weaponTemplate != null)
                {
                    // Damage — instance getter includes weapon-mod bonuses
                    int minDmg = weapon.GetMinDamage();
                    int maxDmg = weapon.GetMaxDamage();
                    if (minDmg > 0 || maxDmg > 0)
                    {
                        details.Add(minDmg == maxDmg
                            ? $"Damage {maxDmg}"
                            : $"Damage {minDmg} to {maxDmg}");
                    }

                    // Ranged-specific: loaded ammo, reserve ammo, caliber, range brackets, firing modes
                    if (weapon is ItemInstance_WeaponRanged rangedInst
                        && weaponTemplate is ItemTemplate_WeaponRanged)
                    {
                        int clip = rangedInst.GetClipSize();
                        if (clip > 0)
                        {
                            details.Add($"Ammo {rangedInst.GetAmmoCount()} of {clip}");
                        }
                        string reserveLine = BuildReserveAmmoLine(rangedInst, pc);
                        if (!string.IsNullOrEmpty(reserveLine))
                            details.Add(reserveLine);
                        string caliber = GetWeaponCaliberDisplay(weaponTemplate);
                        if (!string.IsNullOrEmpty(caliber))
                        {
                            details.Add($"Uses {caliber}");
                        }
                        details.AddRange(BuildRangeBracketLines(rangedInst, pc));
                        details.AddRange(BuildFiringModeLines(rangedInst));
                    }
                    else
                    {
                        // Melee / thrown / RPG: raw max range only
                        int range = weapon.GetAttackRange();
                        if (range > 0)
                            details.Add($"Range {range}");
                    }

                    // PC-aware accuracy & crit (mirrors what sighted users see in ItemInfoBox)
                    int acc = ComputeAccuracyPercent(weapon, pc);
                    if (acc >= 0)
                        details.Add($"Accuracy {acc} percent");
                    int crit = ComputeCritChancePercent(weapon, pc);
                    if (crit >= 0)
                        details.Add($"Critical chance {crit} percent");

                    // Operational stats (AP to attack, AP to reload, chance to jam, jammed flag)
                    details.AddRange(BuildWeaponOperationalLines(weapon));

                    // Penetration (template only — mods adjust via armorPenetration elsewhere)
                    if (weaponTemplate.armorPenetration > 0)
                    {
                        details.Add($"Penetration {weaponTemplate.armorPenetration}");
                    }

                    // Status-effect afflictor
                    string afflictor = BuildAfflictorLine(weapon);
                    if (!string.IsNullOrEmpty(afflictor))
                        details.Add(afflictor);

                    // Energy-weapon notes + threshold multipliers
                    details.AddRange(BuildEnergyWeaponLines(weaponTemplate));

                    // Mod slots (installed mods + empty slots)
                    details.AddRange(BuildModSlotLines(weapon));
                }
            }
            else if (item is ItemInstance_Armor armor)
            {
                details.Add("Armor");

                if (armor.template is ItemTemplate_Equipment equipTemplate)
                {
                    int armorValue = equipTemplate.GetStat("armor");
                    if (armorValue > 0)
                    {
                        details.Add($"Armor value {armorValue}");
                    }
                }

                // Mod slots on armor (e.g. trinket-like slots) — same readout as weapons
                details.AddRange(BuildModSlotLines(armor));
            }
            else if (item is ItemInstance_Ammo ammo)
            {
                details.Add("Ammunition");

                if (ammo.template is ItemTemplate_Ammo ammoTemplate)
                {
                    // Caliber
                    string caliberDisplay = ammoTemplate.GetCaliberDisplayString();
                    if (!string.IsNullOrEmpty(caliberDisplay))
                    {
                        caliberDisplay = UITextExtractor.CleanText(caliberDisplay);
                        details.Add(caliberDisplay);
                    }

                    // Damage multiplier
                    if (ammoTemplate.damageMultiplier != 1f && ammoTemplate.damageMultiplier > 0)
                    {
                        int damagePercent = Mathf.RoundToInt((ammoTemplate.damageMultiplier - 1f) * 100f);
                        if (damagePercent != 0)
                        {
                            details.Add($"Damage {damagePercent:+0;-#} percent");
                        }
                    }

                    // Expansion multiplier (shown in the accuracy slot on the info box)
                    if (ammoTemplate.expansionMultiplier != 1f && ammoTemplate.expansionMultiplier > 0)
                    {
                        int expansionPercent = Mathf.RoundToInt((ammoTemplate.expansionMultiplier - 1f) * 100f);
                        if (expansionPercent != 0)
                        {
                            details.Add($"Expansion {expansionPercent:+0;-#} percent");
                        }
                    }

                    // Penetration
                    if (ammoTemplate.penetration > 0)
                    {
                        details.Add($"Penetration {ammoTemplate.penetration}");
                    }

                    // Armor reduction
                    if (ammoTemplate.chanceToReduceArmor > 0)
                    {
                        details.Add($"{ammoTemplate.chanceToReduceArmor} percent chance to reduce armor by {ammoTemplate.armorReduction}");
                    }
                }
            }
            else if (item is ItemInstance_Usable usableItem)
            {
                details.Add("Consumable item");

                if (usableItem.template is ItemTemplate_Usable usableTemplate)
                {
                    // Action point cost
                    if (usableTemplate.actionPoints > 0)
                    {
                        details.Add($"Costs {usableTemplate.actionPoints} action points");
                    }

                    // Skill-driven effects (heal amounts use PC's skill-bonus-adjusted values)
                    details.AddRange(BuildConsumableEffectLines(usableItem, pc));

                    // Skill-book / XP-giver items (e.g. "+1 Surgeon skill")
                    if (usableTemplate is ItemTemplate_UsableXPGiver xpGiver)
                    {
                        string xpLine = BuildXPGiverLine(xpGiver);
                        if (!string.IsNullOrEmpty(xpLine)) details.Add(xpLine);
                    }

                    // AoE radius
                    if (usableTemplate.aoeRadius > 0)
                    {
                        details.Add($"Area of effect radius {usableTemplate.aoeRadius}");
                    }

                    // Consumed on use
                    if (usableTemplate.isConsumedOnUse)
                    {
                        details.Add("Consumed when used");
                    }
                }
            }
            else if (item is ItemInstance_Mod modItem)
            {
                // Weapon mod: slot, stat bonuses, requirements, allowed-weapons list
                details.AddRange(BuildWeaponModLines(modItem, pc));
            }
            else if (item is ItemInstance_Component)
            {
                details.Add("Crafting component");
            }
            else if (item is ItemInstance_Trinket)
            {
                details.Add("Trinket");
            }
            else if (item.template.junk)
            {
                details.Add("Junk item");
            }

            // Equipment stat modifiers (e.g. +3 Strength, +5 Coordination)
            details.AddRange(BuildModifierLines(item));

            // Attribute requirements (e.g. "Requires 5 Coordination (met)")
            details.AddRange(BuildRequirementLines(item, pc));

            // Trait-specific item modifiers (e.g. Psychopath, Mysterious Stranger)
            details.AddRange(BuildTraitItemModifierLines(item, pc));

            // Description
            if (!string.IsNullOrEmpty(item.template.description))
            {
                string desc = UITextExtractor.CleanText(item.template.description);
                details.Add(desc);
            }

            // Value (vendor-aware when a vendor screen is open)
            int value = ComputeItemValue(item);
            if (value > 0)
            {
                details.Add($"Value {value} dollars");
            }

            // Weight
            float weight = item.GetWeight() * item.quantity;
            if (weight > 0)
            {
                details.Add($"Weight {weight:0.0} pounds");
            }

            return string.Join(", ", details.ToArray());
        }

        /// <summary>
        /// Formats equipment slot information
        /// </summary>
        internal static string FormatEquipmentSlot(EquipmentSlot slot, ItemInstance item)
        {
            string slotName = slot.ToString();

            if (item == null)
            {
                return $"{slotName} slot, empty";
            }

            string itemInfo = FormatItemAnnouncement(item, detailed: false);
            return $"{slotName} slot, {itemInfo}";
        }

        /// <summary>
        /// Lines for each entry in ItemTemplate_Equipment.stats (mirrors ItemInfoBox.BuildBonusString).
        /// Empty list if the item isn't equipment or has no stat bonuses.
        /// </summary>
        internal static List<string> BuildModifierLines(ItemInstance item)
        {
            var result = new List<string>();
            if (item == null) return result;
            var eqt = item.template as ItemTemplate_Equipment;
            if (eqt == null || eqt.stats == null) return result;

            // Skip the raw "armor" value for armor pieces — already announced separately.
            bool isArmor = item is ItemInstance_Armor;

            foreach (var kvp in eqt.stats)
            {
                if (isArmor && kvp.Key == "armor") continue;
                string line = FormatStatModifier(kvp.Key, kvp.Value);
                if (!string.IsNullOrEmpty(line))
                    result.Add(line);
            }
            return result;
        }

        /// <summary>
        /// Lines for each entry in ItemTemplate_Equipment.requiredAttributeValues, annotated with
        /// whether the current PC meets the requirement (mirrors ItemInfoBox.BuildRequirementString).
        /// </summary>
        internal static List<string> BuildRequirementLines(ItemInstance item, PC currentPC)
        {
            var result = new List<string>();
            if (item == null) return result;
            var eqt = item.template as ItemTemplate_Equipment;
            if (eqt == null || eqt.requiredAttributeValues == null) return result;

            if (currentPC == null && MonoBehaviourSingleton<Game>.HasInstance())
                currentPC = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();

            foreach (var kvp in eqt.requiredAttributeValues)
            {
                string attrName = GetStatDisplayName(kvp.Key);
                string suffix = "";
                if (currentPC != null)
                {
                    int pcVal = currentPC.pcStats.GetCharacteristic(kvp.Key);
                    suffix = pcVal >= kvp.Value ? " (met)" : " (not met)";
                }
                result.Add($"Requires {kvp.Value} {attrName}{suffix}");
            }
            return result;
        }

        /// <summary>
        /// Returns "X% chance to apply Y [or Z]" for weapons with status effects, or null.
        /// </summary>
        internal static string BuildAfflictorLine(ItemInstance item)
        {
            var wt = item?.template as ItemTemplate_Weapon;
            if (wt == null || wt.statusEffect == null || wt.statusEffect.Length == 0) return null;
            if (wt.percentToApplyStatusEffect <= 0) return null;

            var effectNames = new List<string>();
            for (int i = 0; i < wt.statusEffect.Length; i++)
            {
                var se = wt.statusEffect[i];
                if (se == null) continue;
                string name = UITextExtractor.CleanText(
                    Language.Localize(se.displayName, false, false, string.Empty));
                if (!string.IsNullOrEmpty(name)) effectNames.Add(name);
            }
            if (effectNames.Count == 0) return null;

            return $"{wt.percentToApplyStatusEffect} percent chance to apply {string.Join(" or ", effectNames.ToArray())}";
        }

        /// <summary>
        /// Localized display name of a weapon's skill (e.g. "Assault Rifle"). Null if unavailable.
        /// </summary>
        internal static string GetWeaponSkillDisplayName(ItemTemplate_Weapon wt)
        {
            if (wt == null || string.IsNullOrEmpty(wt.skill)) return null;
            if (!MonoBehaviourSingleton<PCStatsManager>.HasInstance()) return null;
            var skill = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetSkill(wt.skill);
            if (skill == null || string.IsNullOrEmpty(skill.displayName)) return null;
            return UITextExtractor.CleanText(
                Language.Localize(skill.displayName, false, false, string.Empty));
        }

        /// <summary>
        /// Caliber display string for a ranged weapon, or null.
        /// </summary>
        internal static string GetWeaponCaliberDisplay(ItemTemplate_Weapon wt)
        {
            var rt = wt as ItemTemplate_WeaponRanged;
            if (rt == null) return null;
            string s = ItemTemplate_Ammo.GetCaliberDisplayString(rt.caliber);
            return string.IsNullOrEmpty(s) ? null : UITextExtractor.CleanText(s);
        }

        /// <summary>
        /// Replicates ItemInfoBox.OnWeaponSelected accuracy calculation for the given PC
        /// (lines 474-496 of ItemInfoBox.cs). Returns -1 when not applicable (e.g. no PC).
        /// Thrown / RPG weapons return 100. Includes weapon-mod bonuses, ranged hit bonus,
        /// trait multipliers, and the missing-attribute penalty.
        /// </summary>
        internal static int ComputeAccuracyPercent(ItemInstance_Weapon weapon, PC pc)
        {
            if (weapon == null) return -1;
            var wt = weapon.template as ItemTemplate_Weapon;
            if (wt == null) return -1;
            if (pc == null) return -1;

            if (wt.weaponType == WeaponType.Thrown || wt.weaponType == WeaponType.RPG)
                return 100;

            int skillLevel = pc.pcStats.GetSkillLevel(wt.skill);
            int num = Table_ChanceToHit.GetValue(wt.skill, skillLevel) + weapon.GetChanceToHit();

            if (weapon is ItemInstance_WeaponRanged)
                num += pc.pcStats.GetDerivedStat(PCStatsManager.bonusRangedHitChance);

            var activeTraits = pc.pcTemplate.GetActiveTraits();
            for (int i = 0; i < activeTraits.Count; i++)
            {
                var trait = activeTraits[i];
                float statPercent = trait.GetStatPercent(PCStatsManager.chanceToHit);
                num += trait.GetStat(PCStatsManager.chanceToHit);
                num = Mathf.RoundToInt((float)num * statPercent);
            }

            int penalty = Table_MissingAttributeRequirements.GetMissingWeaponAttributePenalty(wt, pc.pcTemplate);
            return num - penalty;
        }

        /// <summary>
        /// Replicates ItemInfoBox.OnWeaponSelected crit calculation for the given PC
        /// (lines 464-472). Energy weapons / Thrown / RPG return 0.
        /// </summary>
        internal static int ComputeCritChancePercent(ItemInstance_Weapon weapon, PC pc)
        {
            if (weapon == null) return -1;
            var wt = weapon.template as ItemTemplate_Weapon;
            if (wt == null) return -1;
            if (pc == null) return -1;

            if (wt is ItemTemplate_WeaponEnergy
                || wt is ItemTemplate_WeaponMeleeEnergy
                || wt.weaponType == WeaponType.Thrown
                || wt.weaponType == WeaponType.RPG)
                return 0;

            float f = pc.pcStats.GetBaseChanceToCriticalHitWithSkill(wt.skill)
                      + weapon.GetCriticalHitChanceBonus();
            return Mathf.RoundToInt(f);
        }

        /// <summary>
        /// One line per UseEffects entry in a consumable, with skill-bonus-adjusted heal
        /// values when a PC is supplied. Mirrors ItemInfoBox.BuildFieldMedicString.
        /// Also adds the "Requires X skill of Y" and "Requirements not met" lines when
        /// applicable.
        /// </summary>
        internal static List<string> BuildConsumableEffectLines(ItemInstance_Usable item, PC pc)
        {
            var result = new List<string>();
            if (item == null) return result;
            var skillTemplate = item.template as ItemTemplate_UsableSkill;
            if (skillTemplate == null || skillTemplate.itemEffects == null) return result;

            int skillLevel = 0;
            if (pc != null
                && !string.IsNullOrEmpty(skillTemplate.associatedSkill)
                && skillTemplate.associatedSkill != ItemTemplate_UsableSkill.NONESKILL)
            {
                skillLevel = pc.pcStats.GetSkillLevel(skillTemplate.associatedSkill);
            }

            for (int i = 0; i < skillTemplate.itemEffects.Count; i++)
            {
                var ee = skillTemplate.itemEffects[i];
                switch (ee.effectType)
                {
                    case ItemTemplate_UsableSkill.UseEffects.Heal:
                    {
                        int hMin = ItemTemplate_UsableSkill.CalcHealMin(ee, skillLevel, includeSkillBonus: true);
                        int hMax = ItemTemplate_UsableSkill.CalcHealMax(ee, skillLevel, includeSkillBonus: true);
                        if (hMin > 0 || hMax > 0)
                        {
                            result.Add(hMin == hMax
                                ? $"Heals {hMax} points of CON"
                                : $"Heals {hMin} to {hMax} points of CON");
                        }
                        break;
                    }
                    case ItemTemplate_UsableSkill.UseEffects.HealPercent:
                    {
                        float pct = ItemTemplate_UsableSkill.CalcHealPCT(ee, skillLevel, includeSkillBonus: true);
                        if (pct > 0)
                            result.Add($"Heals {Mathf.RoundToInt(pct)} percent of CON");
                        break;
                    }
                    case ItemTemplate_UsableSkill.UseEffects.Resurrect:
                    {
                        float during = ItemTemplate_UsableSkill.CalcRezRecoveringPCT(ee, skillLevel, includeSkillBonus: true);
                        float after = ItemTemplate_UsableSkill.CalcRezRecoveredPCT(ee, skillLevel, includeSkillBonus: true);
                        result.Add($"Resurrects: heals {Mathf.RoundToInt(during)} percent of CON during recovery, {Mathf.RoundToInt(after)} percent on recovering");
                        break;
                    }
                    case ItemTemplate_UsableSkill.UseEffects.StatusEffect:
                    {
                        var se = ee.statusEffect;
                        if (se != null)
                        {
                            string name = UITextExtractor.CleanText(
                                Language.Localize(se.displayName, false, false, string.Empty));
                            if (!string.IsNullOrEmpty(name))
                                result.Add($"Applies {name}");
                        }
                        break;
                    }
                    case ItemTemplate_UsableSkill.UseEffects.KillEffect:
                    {
                        var se = ee.statusEffect;
                        if (se != null)
                        {
                            string name = UITextExtractor.CleanText(
                                Language.Localize(se.displayName, false, false, string.Empty));
                            if (!string.IsNullOrEmpty(name))
                                result.Add($"Removes {name}");
                        }
                        break;
                    }
                    case ItemTemplate_UsableSkill.UseEffects.KillEffectClass:
                    {
                        string effectName = null;
                        try
                        {
                            if (ee.statusEffect != null && pc != null && pc.template != null)
                                effectName = StatusEffect.GetEffectTypeDisplayName(ee.statusEffect, pc.template.mobType);
                            else
                                effectName = StatusEffect.GetEffectTypeDisplayName(ee.effectClass);
                        }
                        catch { /* fall through */ }
                        if (!string.IsNullOrEmpty(effectName))
                            result.Add($"Removes all {UITextExtractor.CleanText(effectName)} effects");
                        break;
                    }
                }
            }

            // Required skill level
            int required = skillTemplate.GetSkillRequired();
            if (required > 0 && required < 99)
            {
                string skillKey = skillTemplate.associatedSkill;
                string skillDisplay = skillKey;
                if (PrintableNameHelper.PrintNames != null
                    && PrintableNameHelper.PrintNames.ContainsKey(skillKey))
                {
                    skillDisplay = UITextExtractor.CleanText(
                        Language.Localize(PrintableNameHelper.PrintNames[skillKey], false, false, string.Empty));
                }
                result.Add($"Requires {skillDisplay} skill of {required}");
            }

            // Requirements-not-met flag
            if (pc != null && !skillTemplate.CanUse(pc))
                result.Add("Requirements not met");

            return result;
        }

        /// <summary>
        /// Lines describing the weapon-mod slots of an equipment item: one line per
        /// slot the template accepts, showing either the installed mod name or "empty".
        /// Mirrors the visual mod-slot row in ItemInfoBox.SetModDisplay.
        /// </summary>
        internal static List<string> BuildModSlotLines(ItemInstance item)
        {
            var result = new List<string>();
            var equipInst = item as ItemInstance_Equipment;
            if (equipInst == null) return result;

            foreach (ModSlot slot in Enum.GetValues(typeof(ModSlot)))
            {
                if (!equipInst.CanUseMod(slot)) continue;

                string slotName = UITextExtractor.CleanText(
                    Language.Localize(ItemTemplate_Mod.GetSlotString(slot), false, false, string.Empty));
                if (string.IsNullOrEmpty(slotName)) slotName = slot.ToString();

                ItemInstance_Mod mod = equipInst.GetMod(slot);
                if (mod != null && mod.template != null)
                {
                    string modName = UITextExtractor.CleanText(
                        Language.Localize(mod.template.displayName, false, false, string.Empty));
                    result.Add($"{slotName} slot: {modName}");
                }
                else
                {
                    result.Add($"{slotName} slot: empty");
                }
            }
            return result;
        }

        /// <summary>
        /// Info lines for a weapon mod item: stat bonuses (mirrors ItemInfoBox.BuildModBonusString),
        /// slot type, weaponSmith and other attribute requirements, and the allowed-weapon list.
        /// </summary>
        internal static List<string> BuildWeaponModLines(ItemInstance_Mod item, PC pc)
        {
            var result = new List<string>();
            if (item == null) return result;
            var template = item.template as ItemTemplate_Mod;
            if (template == null) return result;

            string slotName = UITextExtractor.CleanText(
                Language.Localize(template.GetSlotString(), false, false, string.Empty));
            if (!string.IsNullOrEmpty(slotName))
                result.Add($"Weapon mod, {slotName} slot");

            // Stat bonuses (SerializableDictionary_StringFloat)
            if (template.stats != null)
            {
                foreach (var kvp in template.stats)
                {
                    string line = FormatModStatBonus(kvp.Key, kvp.Value);
                    if (!string.IsNullOrEmpty(line))
                        result.Add(line);
                }
            }

            // Required stat values (e.g. weaponSmith, intelligence)
            if (template.requiredStatValues != null)
            {
                foreach (var kvp in template.requiredStatValues)
                {
                    string attrName = GetStatDisplayName(kvp.Key);
                    string suffix = "";
                    if (pc != null)
                    {
                        int pcVal = kvp.Key == "weaponSmith"
                            ? pc.pcStats.GetSkillLevel(kvp.Key)
                            : pc.pcStats.GetCharacteristic(kvp.Key);
                        suffix = pcVal >= kvp.Value ? " (met)" : " (not met)";
                    }
                    result.Add($"Requires {kvp.Value} {attrName}{suffix}");
                }
            }

            // Allowed weapons
            string[] allowed = template.GetAllowedWeapons();
            if (allowed != null && allowed.Length > 0)
            {
                var cleaned = new List<string>();
                for (int i = 0; i < allowed.Length; i++)
                {
                    string a = UITextExtractor.CleanText(allowed[i]);
                    if (!string.IsNullOrEmpty(a)) cleaned.Add(a);
                }
                if (cleaned.Count > 0)
                    result.Add($"Usable on: {string.Join(", ", cleaned.ToArray())}");
            }
            return result;
        }

        /// <summary>
        /// Line describing an XP-giver consumable (e.g. skill book) — "+1 Surgeon skill"
        /// or "+10 Surgeon skill" for maxSkillOut items.
        /// </summary>
        internal static string BuildXPGiverLine(ItemTemplate_UsableXPGiver xpGiver)
        {
            if (xpGiver == null || string.IsNullOrEmpty(xpGiver.skillName)) return null;
            int levels = xpGiver.maxSkillOut ? 10 : 1;
            string skillDisplay = xpGiver.skillName;
            if (MonoBehaviourSingleton<PCStatsManager>.HasInstance())
            {
                skillDisplay = UITextExtractor.CleanText(
                    MonoBehaviourSingleton<PCStatsManager>.GetInstance()
                        .GetCharacteristicDisplayName(xpGiver.skillName));
            }
            return $"+{levels} {skillDisplay} skill";
        }

        /// <summary>
        /// Lines for each acquired trait on the PC that has a custom tooltip for the item
        /// (mirrors ItemInfoBox using pcTemplate.GetTraitItemModifierString). Colour codes
        /// and NGUI markers are stripped.
        /// </summary>
        internal static List<string> BuildTraitItemModifierLines(ItemInstance item, PC pc)
        {
            var result = new List<string>();
            if (item == null || pc == null || pc.pcTemplate == null) return result;

            string raw;
            try { raw = pc.pcTemplate.GetTraitItemModifierString(item); }
            catch { return result; }
            if (string.IsNullOrEmpty(raw)) return result;

            string clean = UITextExtractor.CleanText(raw);
            if (string.IsNullOrEmpty(clean)) return result;

            // Each trait contributes one "TraitName: modifier" segment separated by newlines.
            string[] lines = clean.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].Trim();
                if (!string.IsNullOrEmpty(l))
                    result.Add($"Trait: {l}");
            }
            return result;
        }

        /// <summary>
        /// Lines for energy weapons: descriptive note and above/below armor threshold
        /// damage multipliers. Mirrors ItemInfoBox.OnWeaponSelected lines 503-547.
        /// </summary>
        internal static List<string> BuildEnergyWeaponLines(ItemTemplate_Weapon wt)
        {
            var result = new List<string>();
            if (wt == null) return result;
            bool isRangedEnergy = wt is ItemTemplate_WeaponEnergy;
            bool isMeleeEnergy = wt is ItemTemplate_WeaponMeleeEnergy;
            if (!isRangedEnergy && !isMeleeEnergy) return result;

            if (isRangedEnergy)
                result.Add("Energy weapons cannot jam or inflict critical hits");
            else
                result.Add("Melee energy weapons cannot inflict critical hits");

            float above, below;
            if (isRangedEnergy)
            {
                var ew = wt as ItemTemplate_WeaponEnergy;
                above = ew.energyNoPenetrationMultiplier;
                below = ew.energyPenetratedMultiplier;
            }
            else
            {
                // ItemInfoBox reads the same field twice for melee — not a bug we need to "fix"
                var mw = wt as ItemTemplate_WeaponMeleeEnergy;
                above = mw.energyPenetratedMultiplier;
                below = mw.energyPenetratedMultiplier;
            }
            result.Add($"Threshold damage: above {above:0.0}x, below {below:0.0}x");
            return result;
        }

        private static string FormatModStatBonus(string key, float value)
        {
            string displayName = GetStatDisplayName(key);
            // Negative-is-good stats (jam chance, reload AP, etc.) — flag positive as penalty.
            bool isNegativeStat = false;
            if (MonoBehaviourSingleton<PCStatsManager>.HasInstance())
            {
                BaseStat stat = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetCharacteristic(key);
                if (stat is DerivedStat ds && ds.isNegative) isNegativeStat = true;
            }
            switch (key)
            {
                case "actionPointAttack":
                case "chanceToJam":
                case "actionPointReload":
                    isNegativeStat = true;
                    break;
            }

            string valueStr = value.ToString("0.##");
            if (key == "chanceToJam") valueStr += "%";
            string sign = value > 0 ? "+" : "";
            string goodBad = (isNegativeStat && value > 0) ? " (penalty)" : "";
            return $"{sign}{valueStr} {displayName}{goodBad}";
        }

        /// <summary>
        /// One line per firing mode: "Single: 3 AP, 1 ammo", annotating the currently-selected
        /// mode with " (current)". Mirrors how sighted players see AttackModePanel.
        /// </summary>
        internal static List<string> BuildFiringModeLines(ItemInstance_WeaponRanged weapon)
        {
            var result = new List<string>();
            if (weapon == null) return result;
            var wt = weapon.template as ItemTemplate_WeaponRanged;
            if (wt == null || wt.firingModeInfos == null || wt.firingModeInfos.Length == 0) return result;

            int currentIdx = weapon.firingModeIndex;
            int currentAttackAp = weapon.GetActionPointsToAttack();
            for (int i = 0; i < wt.firingModeInfos.Length; i++)
            {
                var info = wt.firingModeInfos[i];
                if (info == null) continue;

                // Use the same ammoCost-based rule as GetFiringModeName, but compute per-mode.
                string modeName;
                if (info.ammoCost == 1) modeName = "Single";
                else if (info.ammoCost == wt.clipSize) modeName = "Full auto";
                else modeName = $"Burst ({info.ammoCost} shots)";

                // Only the current firing mode's AP cost includes installed-mod AP bonuses.
                // For other modes, fall back to the template AP cost.
                int apCost = (i == currentIdx) ? currentAttackAp : info.actionPointCost;

                string line = $"{modeName}: {apCost} AP, {info.ammoCost} ammo";
                if (info.chanceToHitPenalty != 0)
                    line += $", {info.chanceToHitPenalty:+0;-#} percent to hit";
                if (i == currentIdx) line += " (current)";
                result.Add(line);
            }
            return result;
        }

        /// <summary>
        /// Three lines for the three range brackets of a ranged weapon:
        /// point-blank, optimal, maximum. Each line shows meters and the %-to-hit
        /// modifier at that range. Mirrors WeaponRangeTooltip.SetWeapon.
        /// </summary>
        internal static List<string> BuildRangeBracketLines(ItemInstance_WeaponRanged weapon, PC pc)
        {
            var result = new List<string>();
            if (weapon == null) return result;
            var wt = weapon.template as ItemTemplate_WeaponRanged;
            if (wt == null) return result;

            int pbRange = weapon.GetPointBlankRange();
            int optRange = weapon.GetOptimalRange();
            int maxRange = weapon.GetAttackRange();

            int pbCth = wt.hitChanceAtPointBlankRange + weapon.GetPointBlankChanceToHit();
            int optCth = 0;
            int maxCth = wt.hitChancePastOptimalRange + weapon.GetOutsideOptimalChanceToHit();

            // Apply point-blank trait adjustments (matches tooltip when a PC is supplied).
            if (pc != null && pc.pcTemplate != null)
            {
                try
                {
                    var traits = pc.pcTemplate.GetActiveTraits();
                    for (int i = 0; i < traits.Count; i++)
                        traits[i].OnPCCalculateWeaponPointBlankChanceToHit(pc, weapon, ref pbCth);
                }
                catch { /* trait hook may throw on headless weapons — ignore */ }
            }

            result.Add($"Point blank: up to {pbRange} meters, {pbCth:+0;-#} percent to hit");
            result.Add($"Optimal: {pbRange} to {optRange} meters, {optCth:+0;-#} percent to hit");
            if (optRange != maxRange)
                result.Add($"Maximum: {optRange} to {maxRange} meters, {maxCth:+0;-#} percent to hit");
            else
                result.Add($"Maximum: {maxRange} meters, {maxCth:+0;-#} percent to hit");
            return result;
        }

        /// <summary>
        /// Operational per-weapon stats: AP to attack (current firing mode), AP to reload
        /// (ranged only), and chance-to-jam (ranged only). Includes mod bonuses.
        /// </summary>
        internal static List<string> BuildWeaponOperationalLines(ItemInstance_Weapon weapon)
        {
            var result = new List<string>();
            if (weapon == null) return result;

            int attackAp = weapon.GetActionPointsToAttack();
            if (attackAp > 0)
                result.Add($"{attackAp} AP to attack");

            if (weapon is ItemInstance_WeaponRanged ranged)
            {
                int reloadAp = ranged.GetActionPointsToReload();
                if (reloadAp > 0)
                    result.Add($"{reloadAp} AP to reload");

                int jam = ranged.GetChanceToJam();
                if (jam > 0)
                    result.Add($"{jam} percent chance to jam");

                if (ranged.IsJammed())
                    result.Add("Currently jammed");
            }
            return result;
        }

        /// <summary>
        /// Line showing the PC's reserve ammo for this weapon's caliber (sum across inventory),
        /// or null if the weapon isn't ranged / PC is missing.
        /// </summary>
        internal static string BuildReserveAmmoLine(ItemInstance_WeaponRanged weapon, PC pc)
        {
            if (weapon == null || pc == null || pc.inventory == null) return null;
            var wt = weapon.template as ItemTemplate_WeaponRanged;
            if (wt == null) return null;

            int reserves = pc.inventory.GetAmmoCountForCaliber(wt.caliber);
            return $"Reserve ammo: {reserves}";
        }

        /// <summary>
        /// Vendor-aware item value. Matches ItemInfoBox.GetItemValue behavior for the
        /// non-vendor and player-selling paths (we don't have ownerInventory context here).
        /// </summary>
        internal static int ComputeItemValue(ItemInstance item)
        {
            if (item == null || item.template == null) return 0;
            if (item is ItemInstance_Currency) return item.quantity;

            float price = item.template.price;
            if (price <= 0f) return 0;

            price *= 0.3f;
            price = Mathf.Max(1f, Mathf.Floor(price + 0.5f));
            price *= item.quantity;

            if (MonoBehaviourSingleton<GUIManager>.HasInstance()
                && MonoBehaviourSingleton<GUIManager>.GetInstance().IsVendorScreenOpen()
                && MonoBehaviourSingleton<Game>.HasInstance())
            {
                price *= 1f + MonoBehaviourSingleton<Game>.GetInstance().GetHighestBarterSkillAdjustment();
            }
            return Mathf.Max(1, Mathf.FloorToInt(price + 0.5f));
        }

        internal static string FormatStatModifier(string key, int value)
        {
            string displayName = GetStatDisplayName(key);
            string unit = "";
            bool isNegativeStat = false;

            if (MonoBehaviourSingleton<PCStatsManager>.HasInstance())
            {
                BaseStat stat = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetCharacteristic(key);
                if (stat != null)
                {
                    unit = stat.GetUnitString() ?? "";
                    if (stat is DerivedStat ds && ds.isNegative) isNegativeStat = true;
                }
            }

            // combatSpeed displays as a decimal fraction of 100 (e.g. 150 → "1.5")
            string valueStr;
            if (key == PCStatsManager.combatSpeed)
                valueStr = ((float)value / 100f).ToString("0.0");
            else
                valueStr = value.ToString();

            string sign = value > 0 ? "+" : "";
            // For negative stats (e.g. noiseRadius), a positive value is a penalty — note that.
            string modifier = $"{sign}{valueStr}{unit}";
            string goodBad = "";
            if (isNegativeStat && value > 0) goodBad = " (penalty)";
            return $"{modifier} {displayName}{goodBad}";
        }

        private static string GetStatDisplayName(string key)
        {
            if (MonoBehaviourSingleton<PCStatsManager>.HasInstance())
            {
                BaseStat stat = MonoBehaviourSingleton<PCStatsManager>.GetInstance().GetCharacteristic(key);
                if (stat != null && !string.IsNullOrEmpty(stat.displayName))
                    return UITextExtractor.CleanText(
                        Language.Localize(stat.displayName, false, false, string.Empty));
            }
            if (PrintableNameHelper.PrintNames != null && PrintableNameHelper.PrintNames.ContainsKey(key))
                return UITextExtractor.CleanText(
                    Language.Localize(PrintableNameHelper.PrintNames[key], false, false, string.Empty));
            return key;
        }

        /// <summary>
        /// Builds the full multi-line item detail block shared by the inventory and
        /// shop info browsers. Mirrors the stats a sighted player sees in the on-screen
        /// <see cref="ItemInfoBox"/> so both screens read identically.
        ///
        /// Context differences are supplied by the caller:
        ///   infoBox            — on-screen ItemInfoBox to scrape display labels from (may be null)
        ///   equippedSlotName   — adds an "Equipped in: X" line when non-null (inventory equipment zone)
        ///   comparisonEquipped — equipped item to diff against, or null to skip the comparison section
        ///   valueLinesOverride — replaces the default vendor-aware "Value" line; the shop passes its
        ///                        own buy/sell price lines here, null uses the default valuation
        /// </summary>
        internal static List<string> BuildItemInfoLines(
            ItemInstance item,
            PC pc,
            ItemInfoBox infoBox = null,
            string equippedSlotName = null,
            ItemInstance comparisonEquipped = null,
            List<string> valueLinesOverride = null)
        {
            var lines = new List<string>();
            if (item == null || item.template == null) return lines;

            // Name
            string name = UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty));
            lines.Add($"Name: {name}");

            // Type — for weapons use the skill name ("Assault Rifle"), otherwise fall back to GetTypeString
            string typeStr = null;
            if (item is ItemInstance_Weapon)
                typeStr = GetWeaponSkillDisplayName(item.template as ItemTemplate_Weapon);
            if (string.IsNullOrEmpty(typeStr))
                typeStr = item.template.GetTypeString();
            if (!string.IsNullOrEmpty(typeStr))
                lines.Add($"Type: {typeStr}");

            // Quantity
            if (item.quantity > 1)
                lines.Add($"Quantity: {item.quantity}");

            // Equipment slot if equipped
            if (!string.IsNullOrEmpty(equippedSlotName))
                lines.Add($"Equipped in: {equippedSlotName}");

            // Weapon stats — use instance getters so weapon-mod bonuses are reflected
            if (item is ItemInstance_Weapon weapon && weapon.template is ItemTemplate_Weapon wt)
            {
                int minDmg = weapon.GetMinDamage();
                int maxDmg = weapon.GetMaxDamage();
                if (minDmg > 0 || maxDmg > 0)
                {
                    lines.Add(minDmg == maxDmg
                        ? $"Damage: {maxDmg}"
                        : $"Damage: {minDmg} to {maxDmg}");
                }

                // Ranged-specific: loaded ammo, reserve ammo, caliber, range brackets, firing modes
                if (weapon is ItemInstance_WeaponRanged rangedInst && wt is ItemTemplate_WeaponRanged)
                {
                    int clip = rangedInst.GetClipSize();
                    if (clip > 0)
                        lines.Add($"Ammo loaded: {rangedInst.GetAmmoCount()} of {clip}");

                    string reserveLine = BuildReserveAmmoLine(rangedInst, pc);
                    if (!string.IsNullOrEmpty(reserveLine))
                        lines.Add(reserveLine);

                    string caliber = GetWeaponCaliberDisplay(wt);
                    if (!string.IsNullOrEmpty(caliber))
                        lines.Add($"Uses: {caliber}");

                    foreach (string line in BuildRangeBracketLines(rangedInst, pc))
                        lines.Add(line);

                    foreach (string line in BuildFiringModeLines(rangedInst))
                        lines.Add(line);
                }
                else
                {
                    // Melee / thrown / RPG: raw max range only
                    int range = weapon.GetAttackRange();
                    if (range > 0)
                        lines.Add($"Range: {range}");
                }

                // PC-aware accuracy & crit (matches the on-screen ItemInfoBox values)
                int acc = ComputeAccuracyPercent(weapon, pc);
                if (acc >= 0)
                    lines.Add($"Accuracy: {acc} percent");
                int crit = ComputeCritChancePercent(weapon, pc);
                if (crit >= 0)
                    lines.Add($"Critical chance: {crit} percent");

                // Operational stats (AP to attack, AP to reload, chance to jam, jammed flag)
                foreach (string line in BuildWeaponOperationalLines(weapon))
                    lines.Add(line);

                if (wt.armorPenetration > 0)
                    lines.Add($"Armor penetration: {wt.armorPenetration}");

                // Status-effect afflictor
                string afflictor = BuildAfflictorLine(weapon);
                if (!string.IsNullOrEmpty(afflictor))
                    lines.Add(afflictor);

                // Energy-weapon notes + above/below threshold multipliers
                foreach (string line in BuildEnergyWeaponLines(wt))
                    lines.Add(line);

                // Mod slots (installed mods + empty slots)
                foreach (string line in BuildModSlotLines(weapon))
                    lines.Add(line);
            }

            // Armor stats
            if (item is ItemInstance_Armor armor && armor.template is ItemTemplate_Equipment eqt)
            {
                int armorValue = eqt.GetStat("armor");
                if (armorValue > 0)
                    lines.Add($"Armor value: {armorValue}");

                // Mod slots on armor (if any)
                foreach (string line in BuildModSlotLines(armor))
                    lines.Add(line);
            }

            // Ammo stats
            if (item is ItemInstance_Ammo ammo && ammo.template is ItemTemplate_Ammo at)
            {
                string caliberDisplay = at.GetCaliberDisplayString();
                if (!string.IsNullOrEmpty(caliberDisplay))
                    lines.Add($"Caliber: {UITextExtractor.CleanText(caliberDisplay)}");
                if (at.damageMultiplier != 1f && at.damageMultiplier > 0)
                {
                    int dmgPct = Mathf.RoundToInt((at.damageMultiplier - 1f) * 100f);
                    if (dmgPct != 0)
                        lines.Add($"Damage modifier: {dmgPct:+0;-#} percent");
                }
                if (at.expansionMultiplier != 1f && at.expansionMultiplier > 0)
                {
                    int expPct = Mathf.RoundToInt((at.expansionMultiplier - 1f) * 100f);
                    if (expPct != 0)
                        lines.Add($"Expansion: {expPct:+0;-#} percent");
                }
                if (at.penetration > 0)
                    lines.Add($"Penetration: {at.penetration}");
                if (at.chanceToReduceArmor > 0)
                    lines.Add($"Armor reduction: {at.chanceToReduceArmor} percent chance, reduces by {at.armorReduction}");
            }

            // Usable/consumable stats
            if (item is ItemInstance_Usable usable && usable.template is ItemTemplate_Usable ut)
            {
                if (ut.actionPoints > 0)
                    lines.Add($"AP cost: {ut.actionPoints}");

                // All skill-driven effect types, with PC-skill-adjusted heal values
                foreach (string line in BuildConsumableEffectLines(usable, pc))
                    lines.Add($"Effect: {line}");

                // Skill-book / XP-giver items (e.g. "+1 Surgeon skill")
                if (ut is ItemTemplate_UsableXPGiver xpGiver)
                {
                    string xpLine = BuildXPGiverLine(xpGiver);
                    if (!string.IsNullOrEmpty(xpLine))
                        lines.Add($"Grants: {xpLine}");
                }

                if (ut.aoeRadius > 0)
                    lines.Add($"Area of effect: {ut.aoeRadius}");
                if (ut.isConsumedOnUse)
                    lines.Add("Consumed on use");
            }

            // Trinket info
            if (item is ItemInstance_Trinket)
                lines.Add("Trinket");

            // Weapon mod info (slot, stat bonuses, requirements, allowed weapons)
            if (item is ItemInstance_Mod modItem)
            {
                foreach (string line in BuildWeaponModLines(modItem, pc))
                    lines.Add(line);
            }

            // Junk
            if (item.template.junk)
                lines.Add("Junk item");

            // New item flag
            if (item.isNew)
                lines.Add("New item");

            // Equipment stat modifiers (e.g. +3 Strength)
            foreach (string mod in BuildModifierLines(item))
                lines.Add($"Modifier: {mod}");

            // Attribute requirements (annotated with met/not met for the current PC)
            foreach (string req in BuildRequirementLines(item, pc))
                lines.Add(req);

            // Trait-specific item modifiers (e.g. Psychopath, Mysterious Stranger)
            foreach (string traitLine in BuildTraitItemModifierLines(item, pc))
                lines.Add(traitLine);

            // Weight
            float weight = item.GetWeight();
            if (weight > 0)
            {
                if (item.quantity > 1)
                    lines.Add($"Weight: {weight:0.0} lbs each, {weight * item.quantity:0.0} lbs total");
                else
                    lines.Add($"Weight: {weight:0.0} lbs");
            }

            // Value / price — the shop passes its own buy/sell lines; everyone else gets the
            // default vendor-aware valuation.
            if (valueLinesOverride != null)
            {
                lines.AddRange(valueLinesOverride);
            }
            else
            {
                int itemValue = ComputeItemValue(item);
                if (itemValue > 0)
                {
                    if (item.quantity > 1)
                    {
                        int perUnit = Mathf.Max(1, itemValue / Mathf.Max(1, item.quantity));
                        lines.Add($"Value: ${perUnit} each, ${itemValue} total");
                    }
                    else
                    {
                        lines.Add($"Value: ${itemValue}");
                    }
                }
            }

            // Tier
            if (item.template.tier > 0)
                lines.Add($"Tier: {item.template.tier}");

            // Description (can be multiline — split into separate lines)
            if (!string.IsNullOrEmpty(item.template.description))
            {
                string desc = UITextExtractor.CleanText(
                    Language.Localize(item.template.description, false, false, string.Empty));
                if (!string.IsNullOrEmpty(desc))
                    lines.Add($"Description: {desc}");
            }

            // Display labels scraped from the on-screen ItemInfoBox (if one was supplied)
            AppendInfoBoxLabels(lines, infoBox);

            // Comparison vs the equipped item (when one was supplied)
            if (comparisonEquipped != null)
                AppendComparisonLines(lines, item, comparisonEquipped, pc);

            return lines;
        }

        /// <summary>
        /// Reads the visible weapon/armor display labels off an on-screen ItemInfoBox and
        /// appends them to <paramref name="lines"/>. No-op when infoBox is null.
        /// </summary>
        internal static void AppendInfoBoxLabels(List<string> lines, ItemInfoBox infoBox)
        {
            if (infoBox == null) return;
            try
            {
                AppendInfoBoxLabel(lines, "Damage display", infoBox.damageLabel);
                AppendInfoBoxLabel(lines, "Range display", infoBox.rangeLabel);
                AppendInfoBoxLabel(lines, "Accuracy", infoBox.accuracyLabel);
                AppendInfoBoxLabel(lines, "Armor display", infoBox.armorLabel);
                AppendInfoBoxLabel(lines, "Penetration display", infoBox.penetrationLabel);
                AppendInfoBoxLabel(lines, "Ammo", infoBox.ammoLabel);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Warning($"[InventoryFormatting] Error reading ItemInfoBox labels: {e.Message}");
            }
        }

        private static void AppendInfoBoxLabel(List<string> lines, string prefix, UILabel label)
        {
            if (label == null || string.IsNullOrEmpty(label.text) || !label.gameObject.activeInHierarchy)
                return;
            string text = UITextExtractor.CleanText(label.text);
            if (!string.IsNullOrEmpty(text))
                lines.Add($"{prefix}: {text}");
        }

        /// <summary>
        /// Appends a "Compared to equipped X" section with stat deltas for weapons and
        /// armor/wearables. No-op when the items aren't comparable or are the same instance.
        /// </summary>
        internal static void AppendComparisonLines(List<string> lines, ItemInstance focused, ItemInstance equipped, PC pc)
        {
            if (focused == null || equipped == null || focused == equipped) return;

            bool isWeapon = focused is ItemInstance_Weapon;
            bool isArmorOrWearable = focused is ItemInstance_Armor || focused is ItemInstance_Wearable;
            if (!isWeapon && !isArmorOrWearable) return;

            string equippedName = UITextExtractor.CleanText(
                Language.Localize(equipped.template.displayName, false, false, string.Empty));

            int headerIndex = lines.Count;
            lines.Add($"Compared to equipped {equippedName}:");

            if (isWeapon && equipped is ItemInstance_Weapon ew && focused is ItemInstance_Weapon fw)
            {
                AppendIntDiff(lines, "minimum damage", fw.GetMinDamage() - ew.GetMinDamage());
                AppendIntDiff(lines, "maximum damage", fw.GetMaxDamage() - ew.GetMaxDamage());
                AppendIntDiff(lines, "attack range", fw.GetAttackRange() - ew.GetAttackRange());

                int fAcc = ComputeAccuracyPercent(fw, pc);
                int eAcc = ComputeAccuracyPercent(ew, pc);
                if (fAcc >= 0 && eAcc >= 0)
                    AppendIntDiff(lines, "accuracy", fAcc - eAcc, " percent");

                int fCrit = ComputeCritChancePercent(fw, pc);
                int eCrit = ComputeCritChancePercent(ew, pc);
                if (fCrit >= 0 && eCrit >= 0)
                    AppendIntDiff(lines, "critical chance", fCrit - eCrit, " percent");

                var fwt = fw.template as ItemTemplate_Weapon;
                var ewt = ew.template as ItemTemplate_Weapon;
                if (fwt != null && ewt != null)
                    AppendIntDiff(lines, "armor penetration", fwt.armorPenetration - ewt.armorPenetration);

                AppendWeightDiff(lines, focused.GetWeight() - equipped.GetWeight());
            }
            else if (isArmorOrWearable && focused.template is ItemTemplate_Equipment fTpl
                     && equipped.template is ItemTemplate_Equipment eTpl)
            {
                // Union of stat keys from both templates — covers armor, rad resistance,
                // attribute bonuses, etc. Reuses FormatStatModifier so display names and
                // negative-stat penalty annotations match the rest of the info browser.
                var keys = new HashSet<string>();
                if (fTpl.stats != null)
                {
                    foreach (var kvp in fTpl.stats) keys.Add(kvp.Key);
                }
                if (eTpl.stats != null)
                {
                    foreach (var kvp in eTpl.stats) keys.Add(kvp.Key);
                }
                foreach (var key in keys)
                {
                    int diff = fTpl.GetStat(key) - eTpl.GetStat(key);
                    if (diff == 0) continue;
                    string line = FormatStatModifier(key, diff);
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                }

                AppendWeightDiff(lines, focused.GetWeight() - equipped.GetWeight());
            }

            // No diffs emitted — replace the header with a clearer "identical" line.
            if (lines.Count == headerIndex + 1)
            {
                lines[headerIndex] = $"Identical stats to equipped {equippedName}";
            }
        }

        private static void AppendIntDiff(List<string> lines, string label, int diff, string units = "")
        {
            if (diff == 0) return;
            string sign = diff > 0 ? "+" : "";
            lines.Add($"{sign}{diff}{units} {label}");
        }

        private static void AppendWeightDiff(List<string> lines, float diff)
        {
            if (Mathf.Abs(diff) < 0.05f) return;
            string sign = diff > 0 ? "+" : "";
            lines.Add($"{sign}{diff:0.0} pounds weight");
        }

        /// <summary>
        /// Resolves the equipped item on <paramref name="pc"/> to compare a focused item
        /// against, working purely off the PC's equipment array (no inventory-screen UI).
        ///   - Weapons → the PC's slot-1 weapon (falls back to the first equipped weapon)
        ///   - Armor/Wearable → the equipped item occupying the focused item's slot
        /// Returns null when nothing comparable is equipped.
        /// </summary>
        internal static ItemInstance ResolveEquippedComparisonItem(ItemInstance focused, PC pc)
        {
            if (focused == null || pc == null || pc.inventory == null) return null;
            var equipment = pc.inventory.equipment;
            if (equipment == null) return null;

            if (focused is ItemInstance_Weapon)
            {
                var slot1Tpl = pc.pcStats != null ? pc.pcStats.GetWeaponTemplate(false) : null;
                if (slot1Tpl != null)
                {
                    foreach (ItemInstance i in equipment)
                        if (i != null && i is ItemInstance_Weapon && i.template == slot1Tpl)
                            return i;
                }
                foreach (ItemInstance i in equipment)
                    if (i is ItemInstance_Weapon) return i;
                return null;
            }

            if (focused is ItemInstance_Armor || focused is ItemInstance_Wearable)
            {
                var focusedTpl = focused.template as ItemTemplate_Equipment;
                if (focusedTpl == null) return null;
                EquipmentSlot targetSlot = focusedTpl.slot;
                foreach (ItemInstance i in equipment)
                    if (i != null && i.template is ItemTemplate_Equipment t && t.slot == targetSlot)
                        return i;
            }

            return null;
        }
    }
}
