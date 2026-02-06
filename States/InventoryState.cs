using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation and announcements for inventory screens.
    /// Works alongside the game's existing UIButtonKeys navigation
    /// to provide screen reader announcements for items.
    /// </summary>
    public class InventoryState : IAccessibilityState
    {
        public string Name => "Inventory";
        public int Priority => 50;

        private GameObject lastSelectedObject;
        private string lastAnnouncedItem = "";
        private float lastAnnouncementTime;
        private const float ANNOUNCEMENT_COOLDOWN = 0.15f;

        public bool IsActive
        {
            get
            {
                // Check if we're in an inventory-related screen
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

                var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();
                if (!guiManager.IsAnyMenuActive()) return false;

                // Check for CharacterScreen with inventory panel active
                var charScreen = UnityEngine.Object.FindObjectOfType<CharacterScreen>();
                if (charScreen != null && charScreen.gameObject.activeInHierarchy)
                {
                    return true;
                }

                // Check for PopupInventoryMenu (loot containers)
                var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
                if (popupInv != null && popupInv.gameObject.activeInHierarchy)
                {
                    return true;
                }

                return false;
            }
        }

        public bool HandleInput()
        {
            // Monitor for selection changes and announce items
            CheckSelectionChanged();

            // Arrow keys - let the game handle navigation via UIButtonKeys
            // but ensure we announce when selection changes
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                // Schedule announcement check after the game processes navigation
                lastSelectedObject = null; // Force re-check next frame
                return false; // Don't consume - let game handle navigation
            }

            // Tab to announce current item details
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                AnnounceCurrentItemDetails();
                return true;
            }

            // R to read item description
            if (Input.GetKeyDown(KeyCode.R))
            {
                AnnounceItemDescription();
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            lastSelectedObject = null;
            lastAnnouncedItem = "";

            // Announce the screen type
            string announcement = "Inventory";

            var charScreen = UnityEngine.Object.FindObjectOfType<CharacterScreen>();
            if (charScreen != null && charScreen.gameObject.activeInHierarchy)
            {
                // Try to get current character name
                var pc = GetCurrentPC();
                if (pc != null && pc.pcTemplate != null)
                {
                    string name = UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                    announcement = $"{name} inventory";
                }
            }

            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv != null && popupInv.gameObject.activeInHierarchy)
            {
                announcement = "Loot container";
            }

            ScreenReaderManager.Speak(announcement, interrupt: true);
            MelonLogger.Msg($"[InventoryState] Activated: {announcement}");
        }

        public void OnDeactivated()
        {
            lastSelectedObject = null;
            lastAnnouncedItem = "";
            MelonLogger.Msg("[InventoryState] Deactivated");
        }

        private void CheckSelectionChanged()
        {
            GameObject selected = UICamera.selectedObject;
            if (selected == lastSelectedObject) return;

            lastSelectedObject = selected;

            if (selected == null) return;

            // Check if it's an inventory item
            INV_DragDropItem item = selected.GetComponent<INV_DragDropItem>();
            if (item != null)
            {
                AnnounceItem(item);
                return;
            }

            // Check if it's an equipment slot
            INV_EquipmentSlot slot = selected.GetComponent<INV_EquipmentSlot>();
            if (slot != null)
            {
                AnnounceEquipmentSlot(slot);
                return;
            }

            // Check for other interactive elements (buttons, etc.)
            UIButton button = selected.GetComponent<UIButton>();
            if (button != null)
            {
                UILabel label = selected.GetComponentInChildren<UILabel>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    string text = UITextExtractor.CleanText(label.text);
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReaderManager.Speak($"{text}, button", interrupt: true);
                    }
                }
            }
        }

        private void AnnounceItem(INV_DragDropItem dragDropItem)
        {
            if (dragDropItem == null) return;

            ItemInstance item = dragDropItem.GetItem();
            if (item == null || item.template == null) return;

            // Avoid rapid re-announcements
            if (Time.time - lastAnnouncementTime < ANNOUNCEMENT_COOLDOWN) return;
            lastAnnouncementTime = Time.time;

            string itemName = UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty));

            // Skip if same item was just announced
            if (itemName == lastAnnouncedItem) return;
            lastAnnouncedItem = itemName;

            List<string> parts = new List<string>();
            parts.Add(itemName);

            // Quantity if > 1
            if (item.quantity > 1)
            {
                parts.Add($"quantity {item.quantity}");
            }

            // Item type from GetTypeString
            string itemType = item.template.GetTypeString();
            if (!string.IsNullOrEmpty(itemType) && itemType != "Item")
            {
                parts.Add(itemType.ToLower());
            }

            // Equipped status
            if (dragDropItem.slot != EquipmentSlot.None)
            {
                parts.Add("equipped");
            }

            // New item
            if (item.isNew)
            {
                parts.Add("new");
            }

            ScreenReaderManager.Speak(string.Join(", ", parts.ToArray()), interrupt: true);
        }

        private void AnnounceEquipmentSlot(INV_EquipmentSlot slot)
        {
            if (slot == null) return;

            string slotName = GetSlotName(slot.equipmentSlot);

            INV_DragDropItem currentItem = slot.GetCurrentItem(false);
            if (currentItem != null)
            {
                ItemInstance item = currentItem.GetItem();
                if (item != null && item.template != null)
                {
                    string itemName = UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty));
                    ScreenReaderManager.Speak($"{slotName}: {itemName}", interrupt: true);
                    return;
                }
            }

            ScreenReaderManager.Speak($"{slotName}: empty", interrupt: true);
        }

        private void AnnounceCurrentItemDetails()
        {
            GameObject selected = UICamera.selectedObject;
            if (selected == null) return;

            INV_DragDropItem dragDropItem = selected.GetComponent<INV_DragDropItem>();
            if (dragDropItem == null) return;

            ItemInstance item = dragDropItem.GetItem();
            if (item == null || item.template == null) return;

            List<string> parts = new List<string>();

            string itemName = UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty));
            parts.Add(itemName);

            // Item type
            string itemType = item.template.GetTypeString();
            if (!string.IsNullOrEmpty(itemType) && itemType != "Item")
            {
                parts.Add(itemType.ToLower());
            }

            // Weight
            if (item.template.weight > 0)
            {
                parts.Add($"weight {item.template.weight:F1}");
            }

            // Price
            if (item.template.price > 0)
            {
                parts.Add($"value {item.template.price}");
            }

            // Quantity
            if (item.quantity > 1)
            {
                parts.Add($"quantity {item.quantity}");
            }

            // Tier
            if (item.template.tier > 0)
            {
                parts.Add($"tier {item.template.tier}");
            }

            ScreenReaderManager.Speak(string.Join(", ", parts.ToArray()), interrupt: true);
        }

        private void AnnounceItemDescription()
        {
            GameObject selected = UICamera.selectedObject;
            if (selected == null) return;

            INV_DragDropItem dragDropItem = selected.GetComponent<INV_DragDropItem>();
            if (dragDropItem == null) return;

            ItemInstance item = dragDropItem.GetItem();
            if (item == null || item.template == null) return;

            string description = UITextExtractor.CleanText(Language.Localize(item.template.description, false, false, string.Empty));
            if (string.IsNullOrEmpty(description))
            {
                ScreenReaderManager.Speak("No description available", interrupt: true);
            }
            else
            {
                ScreenReaderManager.Speak(description, interrupt: true);
            }
        }

        private string GetSlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "Head slot";
                case EquipmentSlot.Torso: return "Torso slot";
                case EquipmentSlot.Legs: return "Legs slot";
                case EquipmentSlot.Shoulders: return "Shoulders slot";
                case EquipmentSlot.WeaponR: return "Primary weapon";
                case EquipmentSlot.WeaponL: return "Secondary weapon";
                case EquipmentSlot.Trinket1: return "Trinket slot 1";
                case EquipmentSlot.Trinket2: return "Trinket slot 2";
                case EquipmentSlot.Trinket3: return "Trinket slot 3";
                case EquipmentSlot.UnderArmor: return "Armor slot";
                case EquipmentSlot.GasMask: return "Gas mask slot";
                case EquipmentSlot.RadSuit: return "Radiation suit slot";
                case EquipmentSlot.Canteen: return "Canteen slot";
                case EquipmentSlot.Packs: return "Backpack slot";
                default: return slot.ToString() + " slot";
            }
        }

        private PC GetCurrentPC()
        {
            // Try to get from CHA_InventoryPanel
            var chaPanel = UnityEngine.Object.FindObjectOfType<CHA_InventoryPanel>();
            if (chaPanel != null)
            {
                // Use reflection to get currentPC if needed
                var field = typeof(CHA_InventoryPanel).GetField("currentPC",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(chaPanel) as PC;
                }
            }

            // Fallback to first selected PC
            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                return MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
            }

            return null;
        }
    }
}
