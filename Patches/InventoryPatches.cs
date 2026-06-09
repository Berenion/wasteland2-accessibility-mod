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
    // Harmony patches for inventory accessibility: item focus announcements,
    // detailed item info, equipment slot changes, add/remove events, mod
    // attachment, and "Give to" party transfer. Formatting helpers live in
    // InventoryFormatting.cs.

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
                string announcement = InventoryFormatting.FormatItemAnnouncement(newItem, detailed: true);
                ScreenReaderManager.Speak(announcement);
            }
        }
    }

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
                string announcement = InventoryFormatting.FormatItemAnnouncement(itemInstance, detailed: true);

                // Only announce if different from last item
                if (announcement != InventoryFormatting.lastAnnouncedItem)
                {
                    InventoryFormatting.lastAnnouncedItem = announcement;
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
                // Suppress the burst of adds the game fires while populating inventories
                // on a fresh start or a save load — that's the "starting item spam," not
                // a pickup the player initiated.
                if (GameLoadState.IsBulkInventoryWindow()) return;

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

                    string announcement = InventoryFormatting.FormatEquipmentSlot(__instance.equipmentSlot, item);
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
            string announcement = InventoryFormatting.FormatEquipmentSlot(__instance.equipmentSlot, newItem);

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

            // Skip when a PopupInventoryMenu (loot) or CharacterInfoMenu is in the scene —
            // InventoryState handles those, but its IsManagedNavigation flag flips on a frame
            // *after* the popup's first Reposition. We use FindObjectOfType instead of
            // GetComponentInParent because during popup instantiation the grid's parent
            // chain isn't reliably walkable yet (parent activeInHierarchy timing), so the
            // transform-based guard misses and the user hears "0 items visible" /
            // "N items visible" before the managed loot announcement.
            if (UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>() != null) return;
            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu != null && charInfoMenu.gameObject.activeInHierarchy) return;

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
                string announcement = InventoryFormatting.FormatDetailedItemInfo(item, currentPC);
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
            // Suppress when an InputRouter state is active (e.g. GenericMenuState handling
            // ModItemMenu's weapon-selection grid) — that state announces focused items itself
            // with richer owner/equipment context, and we don't want a double-announce.
            if (InputRouter.IsAnyStateActive()) return;
            if (go == null) return;

            // Check if the selected object is an inventory item
            INV_DragDropItem dragDropItem = go.GetComponent<INV_DragDropItem>();
            if (dragDropItem != null)
            {
                ItemInstance item = dragDropItem.GetItem();
                if (item != null)
                {
                    string announcement = InventoryFormatting.FormatItemAnnouncement(item, detailed: true);

                    // Add grid position if available
                    if (item.inventoryGridX >= 0 && item.inventoryGridY >= 0)
                    {
                        announcement += $", position {item.inventoryGridX + 1}, {item.inventoryGridY + 1}";
                    }

                    // Add equipment slot context if this is in an equipment slot
                    if (dragDropItem.slot != EquipmentSlot.None)
                    {
                        announcement = InventoryFormatting.FormatEquipmentSlot(dragDropItem.slot, item);
                    }

                    if (announcement != InventoryFormatting.lastAnnouncedItem)
                    {
                        InventoryFormatting.lastAnnouncedItem = announcement;
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
