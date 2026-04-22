using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for inventory system accessibility
    /// Provides screen reader announcements for inventory operations, item selection, and equipment changes
    /// </summary>
    public static class InventoryPatches
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

                    // Range — instance getter includes mod bonuses
                    int range = weapon.GetAttackRange();
                    if (range > 0)
                    {
                        details.Add($"Range {range}");
                    }

                    // Ranged-specific: loaded ammo and caliber
                    if (weapon is ItemInstance_WeaponRanged rangedInst
                        && weaponTemplate is ItemTemplate_WeaponRanged)
                    {
                        int clip = rangedInst.GetClipSize();
                        if (clip > 0)
                        {
                            details.Add($"Ammo {rangedInst.GetAmmoCount()} of {clip}");
                        }
                        string caliber = GetWeaponCaliberDisplay(weaponTemplate);
                        if (!string.IsNullOrEmpty(caliber))
                        {
                            details.Add($"Uses {caliber}");
                        }
                    }

                    // Crit bonus from template + mods (not the full PC-calculated crit %)
                    int critBonus = weapon.GetCriticalHitChanceBonus();
                    if (critBonus > 0)
                    {
                        details.Add($"Critical chance bonus {critBonus} percent");
                    }

                    // Penetration (template only — mods adjust via armorPenetration elsewhere)
                    if (weaponTemplate.armorPenetration > 0)
                    {
                        details.Add($"Penetration {weaponTemplate.armorPenetration}");
                    }

                    // Status-effect afflictor
                    string afflictor = BuildAfflictorLine(weapon);
                    if (!string.IsNullOrEmpty(afflictor))
                        details.Add(afflictor);
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

                    // Check if it's a medical/skill item
                    if (usableTemplate is ItemTemplate_UsableSkill skillTemplate && skillTemplate.itemEffects != null && skillTemplate.itemEffects.Count > 0)
                    {
                        // Find healing effects
                        foreach (var effect in skillTemplate.itemEffects)
                        {
                            if (effect.effectType.ToString() == "Heal" || effect.effectType.ToString() == "HealPercent")
                            {
                                if (effect.minHeal > 0 || effect.maxHeal > 0)
                                {
                                    if (effect.minHeal == effect.maxHeal)
                                    {
                                        details.Add($"Heals {effect.maxHeal} health");
                                    }
                                    else
                                    {
                                        details.Add($"Heals {effect.minHeal} to {effect.maxHeal} health");
                                    }
                                }

                                // Associated skill
                                if (!string.IsNullOrEmpty(skillTemplate.associatedSkill) && skillTemplate.associatedSkill != "NONE")
                                {
                                    string skillName = skillTemplate.associatedSkill.Replace("_", " ");
                                    details.Add($"Uses {skillName} skill");
                                }
                                break;
                            }
                        }
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

        private static string FormatStatModifier(string key, int value)
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
    }

    /// <summary>
    /// Patch for INV_DragDropItem.PopulateData - announces when item UI is created/updated
    /// This is called when inventory items are displayed in the grid
    /// </summary>
    [HarmonyPatch(typeof(INV_DragDropItem), "PopulateData")]
    public class INV_DragDropItem_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(INV_DragDropItem __instance, ItemInstance newItem, Inventory newOwnerInventory, PC pc, EquipmentSlot newSlot)
        {
            if (InventoryState.IsManagedNavigation) return;

            // Only announce if this item is currently selected
            if (UICamera.selectedObject == __instance.gameObject && newItem != null)
            {
                string announcement = InventoryPatches.FormatItemAnnouncement(newItem, detailed: true);
                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Patch for INV_DragDropItem OnEnable - announces when items become active
    /// Only announces if the item is selected when it becomes active
    /// </summary>
    [HarmonyPatch(typeof(INV_DragDropItem), "OnEnable")]
    public class INV_DragDropItem_OnEnable_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(INV_DragDropItem __instance)
        {
            // Wait a frame to check selection, as selection might happen after OnEnable
            // We'll rely on the focus system to handle this via UICamera patches
        }
    }

    // Removed: INV_MainPanel.PopulateData patch
    // No longer automatically announces inventory summary when switching characters
    // Users will hear individual items as they navigate instead

    /// <summary>
    /// Patch for InventoryGrid.SelectItem - announces when an item is selected in the grid
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "SelectItem")]
    public class InventoryGrid_SelectItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance, ItemInstance itemInstance, bool __result)
        {
            if (InventoryState.IsManagedNavigation) return;

            // If selection succeeded and we have a valid item
            if (__result && itemInstance != null)
            {
                string announcement = InventoryPatches.FormatItemAnnouncement(itemInstance, detailed: true);

                // Only announce if different from last item
                if (announcement != InventoryPatches.lastAnnouncedItem)
                {
                    InventoryPatches.lastAnnouncedItem = announcement;
                    ScreenReaderManager.Speak(announcement);
                }
            }
        }
    }

    /// <summary>
    /// Patch for InventoryGrid.SelectFirstItem - announces the first item when grid gets focus
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "SelectFirstItem")]
    public class InventoryGrid_SelectFirstItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance, bool __result)
        {
            if (InventoryState.IsManagedNavigation) return;

            // The focus patches will handle announcing the selected item
            // We just announce context
            if (__result)
            {
                ScreenReaderManager.Speak("Inventory grid");
            }
        }
    }

    /// <summary>
    /// Patch for Inventory.AddItem - announces when items are added to inventory
    /// </summary>
    [HarmonyPatch(typeof(Inventory), "AddItem")]
    public class Inventory_AddItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Inventory __instance, ItemInstance item, int numToAdd, int __result)
        {
            // Only announce for player inventories (not for vendors, containers, etc.)
            if (__instance is InventoryPlayer && item != null && __result > 0)
            {
                string itemName = UITextExtractor.CleanText(item.template.displayName);
                string announcement;

                if (__result > 1)
                {
                    announcement = $"Added {__result} {itemName}";
                }
                else
                {
                    announcement = $"Added {itemName}";
                }

                // Don't interrupt current speech for additions
                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Patch for Inventory.RemoveItemInstance - announces when items are removed from inventory
    /// </summary>
    [HarmonyPatch(typeof(Inventory), "RemoveItemInstance")]
    public class Inventory_RemoveItemInstance_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Inventory __instance, ItemInstance item, int numToRemove, bool __result)
        {
            // Only announce for player inventories and successful removals
            if (__instance is InventoryPlayer && item != null && __result && numToRemove > 0)
            {
                string itemName = UITextExtractor.CleanText(item.template.displayName);
                string announcement;

                if (numToRemove > 1)
                {
                    announcement = $"Removed {numToRemove} {itemName}";
                }
                else
                {
                    announcement = $"Removed {itemName}";
                }

                // Don't interrupt for removals
                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Patch for INV_EquipmentSlot.PopulateData(PC) - announces equipment slot changes when character is selected
    /// </summary>
    [HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(PC) })]
    public class INV_EquipmentSlot_PopulateData_PC_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(INV_EquipmentSlot __instance, PC newPC)
        {
            if (InventoryState.IsManagedNavigation) return;

            // Only announce if this slot is currently selected
            if (UICamera.selectedObject != null && __instance != null)
            {
                // Check if the selected object is a child of this slot
                GameObject selected = UICamera.selectedObject;
                if (selected.transform.IsChildOf(__instance.transform))
                {
                    INV_DragDropItem currentItem = __instance.GetCurrentItem(create: false);
                    ItemInstance item = currentItem?.GetItem();

                    string announcement = InventoryPatches.FormatEquipmentSlot(__instance.equipmentSlot, item);
                    ScreenReaderManager.Speak(announcement);
                }
            }
        }
    }

    /// <summary>
    /// Patch for INV_EquipmentSlot.PopulateData(ItemInstance_Equipment) - announces when item is equipped/unequipped
    /// </summary>
    [HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(ItemInstance_Equipment) })]
    public class INV_EquipmentSlot_PopulateData_Item_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(INV_EquipmentSlot __instance, ItemInstance_Equipment newItem)
        {
            if (InventoryState.IsManagedNavigation) return;

            // Announce equipment changes
            string announcement = InventoryPatches.FormatEquipmentSlot(__instance.equipmentSlot, newItem);

            // Don't interrupt for equipment changes (they happen during inventory management)
            ScreenReaderManager.Speak(announcement);
        }
    }

    /// <summary>
    /// Patch for InventoryGrid.Reposition - announces grid state after repositioning
    /// Only announces counts when significant changes occur
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), "Reposition")]
    public class InventoryGrid_Reposition_Patch
    {
        private static int lastItemCount = -1;

        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance)
        {
            if (InventoryState.IsManagedNavigation) return;

            // Count items in the grid
            Transform transform = __instance.transform;
            int currentItemCount = 0;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                {
                    INV_DragDropItem item = child.GetComponent<INV_DragDropItem>();
                    if (item != null && item.GetItem() != null)
                    {
                        currentItemCount++;
                    }
                }
            }

            // Only announce if count changed significantly (filter sorting might trigger this)
            if (currentItemCount != lastItemCount && lastItemCount != -1)
            {
                string announcement = $"{currentItemCount} items visible";
                ScreenReaderManager.Speak(announcement);
            }

            lastItemCount = currentItemCount;
        }
    }

    /// <summary>
    /// Patch for ItemInfoBox.SetItem - announces detailed item information when item is selected
    /// This is called when clicking/selecting an item to view its details, NOT on hover
    /// </summary>
    [HarmonyPatch(typeof(ItemInfoBox), "SetItem")]
    public class ItemInfoBox_SetItem_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemInfoBox __instance, ItemInstance item, PC currentPC)
        {
            if (InventoryState.IsManagedNavigation) return;

            if (item != null)
            {
                // Announce comprehensive item details
                string announcement = InventoryPatches.FormatDetailedItemInfo(item, currentPC);
                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Enhanced focus detection for inventory items
    /// Extends the existing UICamera focus patches to provide more detailed item information
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "SetSelection")]
    public class UICamera_SetSelection_InventoryExtension_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {
            if (InventoryState.IsManagedNavigation) return;
            if (go == null) return;

            // Check if the selected object is an inventory item
            INV_DragDropItem dragDropItem = go.GetComponent<INV_DragDropItem>();
            if (dragDropItem != null)
            {
                ItemInstance item = dragDropItem.GetItem();
                if (item != null)
                {
                    string announcement = InventoryPatches.FormatItemAnnouncement(item, detailed: true);

                    // Add grid position if available
                    if (item.inventoryGridX >= 0 && item.inventoryGridY >= 0)
                    {
                        announcement += $", position {item.inventoryGridX + 1}, {item.inventoryGridY + 1}";
                    }

                    // Add equipment slot context if this is in an equipment slot
                    if (dragDropItem.slot != EquipmentSlot.None)
                    {
                        announcement = InventoryPatches.FormatEquipmentSlot(dragDropItem.slot, item);
                    }

                    if (announcement != InventoryPatches.lastAnnouncedItem)
                    {
                        InventoryPatches.lastAnnouncedItem = announcement;
                        ScreenReaderManager.Speak(announcement);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Intercepts "Attach Mod" to open the ModItemMenu weapon selection popup
    /// instead of silently entering weaponSmith ASI mode (which relies on visual highlights).
    /// </summary>
    [HarmonyPatch(typeof(INV_DragDropItem), "AttemptToUse")]
    public class AttemptToUse_ModItemMenu_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(INV_DragDropItem __instance)
        {
            ItemInstance item = __instance.GetItem();
            if (item == null) return true; // let original handle null

            // Only intercept weapon mods
            if (!(item is ItemInstance_Mod)) return true;

            // Don't allow during combat (matches original check)
            if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return false;

            ItemTemplate_Mod modTemplate = item.template as ItemTemplate_Mod;
            if (modTemplate == null) return true;

            // Check if anyone has enough weaponSmith skill
            PC bestSmith = null;
            int bestSkill = -1;
            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            for (int i = 0; i < party.Count; i++)
            {
                int skill = party[i].pcStats.GetSkillLevel("weaponSmith");
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    bestSmith = party[i];
                }
            }

            int required = modTemplate.requiredStatValues.ContainsKey("weaponSmith")
                ? modTemplate.requiredStatValues["weaponSmith"] : 0;

            if (bestSkill < required)
            {
                ScreenReaderManager.SpeakInterrupt($"Insufficient weaponsmithing skill. Requires level {required}, party best is {bestSkill}");
                return false;
            }

            // Check if any weapons support this mod type
            if (!PCInventory.DoesPartyHaveWeaponsSupportingMods(modTemplate.slot))
            {
                ScreenReaderManager.SpeakInterrupt("No weapons in party support this mod type");
                return false;
            }

            // Open the ModItemMenu weapon selection popup
            PC modOwner = __instance.ownerPC ?? bestSmith;
            MonoBehaviourSingleton<GUIManager>.GetInstance().OpenModItemMenu(item as ItemInstance_Mod, modOwner);

            MelonLogger.Msg($"[InventoryPatches] Opened ModItemMenu for {UITextExtractor.CleanText(item.template.displayName)}");
            return false; // skip original AttemptToUse
        }
    }

    /// <summary>
    /// Announces context when ModItemMenu opens to show compatible weapons.
    /// </summary>
    [HarmonyPatch(typeof(ModItemMenu), "PopulateData", new Type[] { typeof(ItemInstance_Mod), typeof(PC) })]
    public class ModItemMenu_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ModItemMenu __instance, ItemInstance_Mod item)
        {
            if (item == null || item.template == null) return;

            string modName = UITextExtractor.CleanText(item.template.displayName);

            // Count compatible weapons in the grid
            int weaponCount = 0;
            if (__instance.inventoryGrid != null)
            {
                Transform gridTransform = __instance.inventoryGrid.transform;
                for (int i = 0; i < gridTransform.childCount; i++)
                {
                    Transform child = gridTransform.GetChild(i);
                    if (child != null && child.gameObject.activeSelf)
                    {
                        INV_DragDropItem dragDrop = child.GetComponent<INV_DragDropItem>();
                        if (dragDrop != null && dragDrop.GetItem() != null)
                            weaponCount++;
                    }
                }
            }

            string announcement = $"Select a weapon to attach {modName}. {weaponCount} compatible weapon{(weaponCount != 1 ? "s" : "")}. Use arrows to navigate, Enter to attach, Escape to cancel.";
            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[InventoryPatches] ModItemMenu opened: {weaponCount} weapons for {modName}");
        }
    }

    /// <summary>
    /// Adds a "Give to" button to the inventory context menu, enabling keyboard-accessible
    /// item transfers between party members (normally requires drag-and-drop).
    /// </summary>
    [HarmonyPatch(typeof(INV_DragDropItem), "OpenContextMenu")]
    public class OpenContextMenu_GiveTo_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(INV_DragDropItem __instance)
        {
            // Only add "Give to" in the character inventory screen, not loot/vendor
            if (__instance is VND_DragDropItem) return;
            if (__instance.ownerPC == null) return;

            // Must have party members to give to
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;
            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || party.Count <= 1) return;

            // Find the ItemInfoMenu that was just opened
            if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return;
            var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();
            if (!guiManager.IsItemInfoScreenOpen()) return;

            // Get the top screen as ItemInfoMenu
            var screensField = typeof(GUIManager).GetField("screens", BindingFlags.NonPublic | BindingFlags.Instance);
            if (screensField == null) return;
            var screens = screensField.GetValue(guiManager) as List<GUIScreen>;
            if (screens == null || screens.Count == 0) return;

            ItemInfoMenu itemInfoMenu = null;
            for (int i = screens.Count - 1; i >= 0; i--)
            {
                itemInfoMenu = screens[i] as ItemInfoMenu;
                if (itemInfoMenu != null) break;
            }
            if (itemInfoMenu == null) return;

            // Capture references for the closure
            INV_DragDropItem dragDropItem = __instance;

            itemInfoMenu.AddButton("Give to...", () =>
            {
                OnGiveToClicked(dragDropItem);
            });
        }

        private static void OnGiveToClicked(INV_DragDropItem dragDropItem)
        {
            // Close the context menu
            MonoBehaviourSingleton<GUIManager>.GetInstance().CloseTopMenu();

            if (dragDropItem == null || dragDropItem.GetItem() == null) return;

            ItemInstance item = dragDropItem.GetItem();
            PC ownerPC = dragDropItem.ownerPC;

            // Open a new ItemInfoMenu to show party member list
            ItemInfoMenu partyMenu = MonoBehaviourSingleton<GUIManager>.GetInstance().OpenItemInfoMenu(item, ownerPC);
            if (partyMenu == null) return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            for (int i = 0; i < party.Count; i++)
            {
                PC targetPC = party[i];
                if (targetPC == null) continue;
                if (targetPC == ownerPC) continue;

                string pcName = UITextExtractor.CleanText(
                    Language.Localize(targetPC.pcTemplate.displayName, false, false, string.Empty));

                // Check distance (same 20-unit check the game uses)
                bool inRange = true;
                if (ownerPC != null && targetPC != null)
                {
                    float sqrDist = (targetPC.transform.position - ownerPC.transform.position).sqrMagnitude;
                    if (sqrDist > 400f)
                        inRange = false;
                }

                partyMenu.AddButton(pcName, () =>
                {
                    OnPartyMemberSelected(dragDropItem, targetPC);
                }, inRange);
            }
        }

        private static void OnPartyMemberSelected(INV_DragDropItem dragDropItem, PC targetPC)
        {
            // Close the party member menu
            MonoBehaviourSingleton<GUIManager>.GetInstance().CloseTopMenu();

            if (dragDropItem == null || targetPC == null) return;

            ItemInstance item = dragDropItem.GetItem();
            if (item == null) return;

            // If stackable with quantity > 1, show quantity selection
            if (item.quantity > 1)
            {
                ShowQuantityMenu(dragDropItem, targetPC, item);
                return;
            }

            // Single item — transfer immediately
            ExecuteTransfer(dragDropItem, targetPC, 1);
        }

        private static void ShowQuantityMenu(INV_DragDropItem dragDropItem, PC targetPC, ItemInstance item)
        {
            int maxQty = item.quantity;
            string itemName = UITextExtractor.CleanText(
                Language.Localize(item.template.displayName, false, false, string.Empty));

            AskQuantityMenu qtyMenu = MonoBehaviourSingleton<GUIManager>.GetInstance().CreateAskQuantityMenu();
            qtyMenu.SetMessage(
                Language.Localize("<@>How Many?", false, false, string.Empty),
                itemName,
                maxQty,    // default to full stack
                1,         // min
                maxQty,    // max
                Language.Localize("<@>Okay", false, false, string.Empty),
                (ModalMessageMenu menu) =>
                {
                    AskQuantityMenu askMenu = menu as AskQuantityMenu;
                    if (askMenu != null)
                    {
                        int qty = UnityEngine.Mathf.Clamp(askMenu.GetQuantity(), 1, maxQty);
                        ExecuteTransfer(dragDropItem, targetPC, qty);
                    }
                },
                Language.Localize("<@>Cancel", false, false, string.Empty),
                null,  // no cancel callback needed
                0f     // no unit value (not trading)
            );
        }

        private static void ExecuteTransfer(INV_DragDropItem dragDropItem, PC targetPC, int transferQty)
        {
            if (dragDropItem == null || targetPC == null) return;

            ItemInstance item = dragDropItem.GetItem();
            if (item == null) return;

            PC ownerPC = dragDropItem.ownerPC;
            string itemName = UITextExtractor.CleanText(
                Language.Localize(item.template.displayName, false, false, string.Empty));
            string targetName = UITextExtractor.CleanText(
                Language.Localize(targetPC.pcTemplate.displayName, false, false, string.Empty));

            // Clamp to actual quantity available
            transferQty = Math.Min(transferQty, item.quantity);
            if (transferQty <= 0) return;

            // Check CNPC willingness
            if (ownerPC is CNPC cnpc)
            {
                Drama drama = cnpc.gameObject.GetComponent<Drama>();
                if (drama != null && !Drama.NotifyOnTradeItemsEvent(cnpc, "WillGiveItem", dragDropItem, null))
                {
                    ScreenReaderManager.SpeakInterrupt($"{UITextExtractor.CleanText(Language.Localize(ownerPC.pcTemplate.displayName, false, false, string.Empty))} refuses to give that item");
                    MelonLogger.Msg("[GiveTo] CNPC refused to give item");
                    return;
                }
            }

            // Unequip if the item is equipped
            if (dragDropItem.slot != EquipmentSlot.None && ownerPC != null)
            {
                ownerPC.inventory.UnEquip(dragDropItem.slot);
                ownerPC.inventory.RefreshEquipment();
            }

            bool transferringAll = transferQty >= item.quantity;

            // Print the trade message in the HUD (before removing, so owner info is still valid)
            INV_DragDropItem.PrintTradeMessage(dragDropItem, targetPC, transferQty);

            if (dragDropItem.ownerInventory != null)
            {
                dragDropItem.ownerInventory.RemoveItemInstance(item, transferQty, true);
                EventInfo_InventoryModified inventoryModified = ObjectPool.Get<EventInfo_InventoryModified>();
                inventoryModified.target = dragDropItem.ownerInventory;
                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(inventoryModified);
            }

            ItemInstance[] items = dragDropItem.RemoveItems(transferQty);
            targetPC.inventory.AddItems(items);
            targetPC.inventory.inventory.TriggerPCReceivedItemEvents(items);

            if (transferringAll)
            {
                // Destroy the now-empty drag drop widget
                if (dragDropItem.gameObject != null)
                {
                    UnityEngine.Object.Destroy(dragDropItem.gameObject, 0.05f);
                }
            }
            else
            {
                // Partial transfer — update the widget to show remaining quantity
                dragDropItem.UpdateText();
            }

            // Announce to screen reader
            string qtyText = transferQty > 1 ? $" x{transferQty}" : "";
            ScreenReaderManager.SpeakInterrupt($"Gave {itemName}{qtyText} to {targetName}");
            MelonLogger.Msg($"[GiveTo] Transferred {itemName} (x{transferQty}) from {ownerPC?.name ?? "?"} to {targetName}");

            // Check encumbrance
            float maxWeight = targetPC.pcStats.GetMaxWeight();
            float currentWeight = targetPC.inventory.WeightInPossession();
            if (currentWeight > maxWeight * 0.8f)
            {
                MonoBehaviourSingleton<TutorialManager>.GetInstance().TriggerTutorial("Encumbrance");
            }
        }
    }
}
