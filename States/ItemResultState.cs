using System.Collections.Generic;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Handles the Field Strip result popup (PopupItemResultMenu) that appears after
    /// breaking down a weapon for parts. The popup is purely informational — it lists
    /// the items the working PC received — and is dismissed with a single "Back" action.
    ///
    /// Priority 56: above the 50-54 inventory/shop cluster (the popup overlays the
    /// inventory, which would otherwise stay active and trap input) and above
    /// GenericMenuState (55), but below KeypadState (58) and DialogState (70).
    ///
    /// Without this state the popup is silent and InputSuppressor (set by the still-active
    /// InventoryState) prevents the game from routing Back to close it.
    /// </summary>
    public class ItemResultState : AccessibilityStateBase
    {
        public override string Name => "ItemResult";
        public override int Priority => 56;

        private PopupItemResultMenu cachedMenu;

        public override bool IsActive
        {
            get
            {
                var menu = UnityEngine.Object.FindObjectOfType<PopupItemResultMenu>();
                return menu != null && menu.gameObject.activeInHierarchy;
            }
        }

        public override bool HandleInput()
        {
            // Modal popup: trap all input so the underlying inventory doesn't react,
            // and set the suppressors ourselves (the lower-priority InventoryState's
            // HandleInput won't run while we consume input).
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            if (cachedMenu == null || !cachedMenu.gameObject.activeInHierarchy)
            {
                cachedMenu = UnityEngine.Object.FindObjectOfType<PopupItemResultMenu>();
                if (cachedMenu == null) return true;
            }

            // Enter / Space / Escape all dismiss the popup (mirrors its single "Back" action).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            {
                cachedMenu.Close();
                cachedMenu = null;
            }

            return true;
        }

        public override void OnActivated()
        {
            cachedMenu = UnityEngine.Object.FindObjectOfType<PopupItemResultMenu>();
            if (cachedMenu != null)
                ScreenReaderManager.SpeakInterrupt(BuildAnnouncement(cachedMenu));
            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            cachedMenu = null;
            base.OnDeactivated();
        }

        private static string BuildAnnouncement(PopupItemResultMenu menu)
        {
            string title = menu.titleLabel != null
                ? UITextExtractor.CleanText(menu.titleLabel.text)
                : "Field Strip Result";
            string message = menu.messageLabel != null
                ? UITextExtractor.CleanText(menu.messageLabel.text)
                : string.Empty;

            // The displayed result items live in the popup's InventoryContainer as
            // INV_DragDropItem widgets (populated by SetInventory before the screen
            // is pushed). Read them the same way the inventory navigation does.
            var itemStrings = new List<string>();
            if (menu.inventoryContainer != null)
            {
                var widgets = menu.inventoryContainer
                    .GetComponentsInChildren<INV_DragDropItem>(includeInactive: true);
                foreach (var widget in widgets)
                {
                    if (widget == null) continue;
                    var item = widget.GetItem();
                    if (item == null || item.template == null) continue;
                    itemStrings.Add(InventoryFormatting.FormatItemAnnouncement(item));
                }
            }

            string itemsText = itemStrings.Count > 0
                ? string.Join(", ", itemStrings.ToArray())
                : "nothing";

            string body = string.IsNullOrEmpty(message)
                ? $"received: {itemsText}"
                : $"{message} {itemsText}";

            return $"{title}. {body}. Press Enter or Escape to close.";
        }
    }
}
