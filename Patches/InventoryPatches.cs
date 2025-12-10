using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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
                details.Add("Weapon");

                if (weapon.template is ItemTemplate_Weapon weaponTemplate)
                {
                    // Damage
                    if (weaponTemplate.minDamage > 0 || weaponTemplate.maxDamage > 0)
                    {
                        details.Add($"Damage {weaponTemplate.minDamage} to {weaponTemplate.maxDamage}");
                    }

                    // Range
                    if (weaponTemplate.attackRange > 0)
                    {
                        details.Add($"Range {weaponTemplate.attackRange}");
                    }

                    // Crit chance
                    if (weaponTemplate.criticalHitBonusChance > 0)
                    {
                        details.Add($"Critical chance {weaponTemplate.criticalHitBonusChance} percent");
                    }

                    // Penetration
                    if (weaponTemplate.armorPenetration > 0)
                    {
                        details.Add($"Penetration {weaponTemplate.armorPenetration}");
                    }
                }
            }
            else if (item is ItemInstance_Armor armor)
            {
                details.Add("Armor");

                if (armor.template is ItemTemplate_Equipment equipTemplate)
                {
                    int armorValue = equipTemplate.GetStat("AC");
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

            // Description
            if (!string.IsNullOrEmpty(item.template.description))
            {
                string desc = UITextExtractor.CleanText(item.template.description);
                details.Add(desc);
            }

            // Value
            int value = 0;
            if (item.template != null && item.template.price > 0)
            {
                value = Mathf.FloorToInt(item.template.price * 0.3f * item.quantity);
            }
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
            // Only announce if this item is currently selected
            if (UICamera.selectedObject == __instance.gameObject && newItem != null)
            {
                string announcement = InventoryPatches.FormatItemAnnouncement(newItem, detailed: true);
                ScreenReaderManager.Speak(announcement, interrupt: false);
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
            // If selection succeeded and we have a valid item
            if (__result && itemInstance != null)
            {
                string announcement = InventoryPatches.FormatItemAnnouncement(itemInstance, detailed: true);

                // Only announce if different from last item
                if (announcement != InventoryPatches.lastAnnouncedItem)
                {
                    InventoryPatches.lastAnnouncedItem = announcement;
                    ScreenReaderManager.Speak(announcement, interrupt: false);
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
            // The focus patches will handle announcing the selected item
            // We just announce context
            if (__result)
            {
                ScreenReaderManager.Speak("Inventory grid", interrupt: false);
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
                ScreenReaderManager.Speak(announcement, interrupt: false);
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
                ScreenReaderManager.Speak(announcement, interrupt: false);
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
                    ScreenReaderManager.Speak(announcement, interrupt: false);
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
            // Announce equipment changes
            string announcement = InventoryPatches.FormatEquipmentSlot(__instance.equipmentSlot, newItem);

            // Don't interrupt for equipment changes (they happen during inventory management)
            ScreenReaderManager.Speak(announcement, interrupt: false);
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
                ScreenReaderManager.Speak(announcement, interrupt: false);
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
            if (item != null)
            {
                // Announce comprehensive item details
                string announcement = InventoryPatches.FormatDetailedItemInfo(item, currentPC);
                ScreenReaderManager.Speak(announcement, interrupt: false);
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
                        ScreenReaderManager.Speak(announcement, interrupt: false);
                    }
                }
            }
        }
    }
}
