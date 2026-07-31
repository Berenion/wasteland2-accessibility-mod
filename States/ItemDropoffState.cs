using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation and screen reader support for the give-item screen
    /// (<see cref="ItemDropoffMenu"/>). This is the conversation-driven "hand an item
    /// to an NPC" popup a drama opens with cmd.tradeOpen(inv, mob, hideStock|hideTrade).
    /// It is a give-only sibling of the vendor screen: a player inventory column and an
    /// escrow ("items to give") column, backed by the same <see cref="Escrow"/> model
    /// ShopState reads. It is NOT a VendorScreen, so IsVendorScreenOpen() (and therefore
    /// ShopState) never catches it; that gap left the screen unnavigable and is what this
    /// state fixes.
    ///
    /// Two zones (Left/Right): "Your inventory" and "Items to give". Up/Down navigate,
    /// Enter moves the selected item across, G (or the finalize entry) hands everything
    /// over, Escape cancels. Priority 54 keeps it above ConversationState (52), which is
    /// otherwise still "active" (waiting) behind the popup.
    /// </summary>
    public class ItemDropoffState : AccessibilityStateBase
    {
        public override string Name => "ItemDropoff";
        public override int Priority => 54;

        public override string GetHelpText()
        {
            return "Give items. Left and Right switch between Your inventory and Items to give. " +
                   "Up and Down move within a list, Enter moves the selected item across. " +
                   "G gives the staged items, I opens item info, R reads the description, " +
                   "Escape cancels. If the NPC needs specific items they are announced when the screen opens.";
        }

        /// <summary>When true, passive patches should suppress their own announcements.</summary>
        public static bool IsManagedNavigation { get; private set; }

        private enum Zone { Inventory, Give }
        private static readonly Zone[] ZoneOrder = { Zone.Inventory, Zone.Give };

        // Finalize sentinel appended to the Give zone.
        private const string FinalizeMarker = "__GIVE_FINALIZE__";

        private Zone currentZone;
        private readonly List<object> currentList = new List<object>(); // Entry or FinalizeMarker
        private int currentIndex = -1;
        private bool isDirty = false;
        // Last inventory version this state rebuilt against.
        private int seenInventoryVersion = 0;
        private string lastAnnouncedText;

        // Info browser (mirrors ShopState).
        private bool isInfoBrowsing = false;
        private readonly List<string> infoLines = new List<string>();
        private int infoLineIndex = -1;

        private ItemDropoffMenu cachedMenu = null;

        /// <summary>An inventory or escrow item, tagged with which side it currently sits on.</summary>
        private class Entry
        {
            public ItemInstance Item;
            public bool IsGive; // true = currently staged in the give/escrow list
        }

        public override bool IsActive
        {
            get
            {
                var menu = GetMenu();
                // Only own input while the dropoff popup is the top menu — yield to any
                // quantity dialog or modal the game stacks on top of it.
                return menu != null && menu.gameObject.activeInHierarchy && menu.isTopMenu;
            }
        }

        public override bool HandleInput()
        {
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            if (isInfoBrowsing)
                return HandleInfoBrowserInput();

            // Any inventory change anywhere — including ones this mod did not perform —
            // invalidates the cached list. See Helpers/InventoryChangeTracker.
            if (Helpers.InventoryChangeTracker.HasChanged(ref seenInventoryVersion))
                isDirty = true;

            if (isDirty)
            {
                isDirty = false;
                RebuildList();
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) { NavigateList(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { NavigateList(1); return true; }

            if (Input.GetKeyDown(KeyCode.LeftArrow)) { SwitchZone(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { SwitchZone(1); return true; }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActOnCurrent();
                return true;
            }

            // G - give the staged items (shortcut for the finalize entry).
            if (Input.GetKeyDown(KeyCode.G)) { GiveStagedItems(); return true; }

            if (Input.GetKeyDown(KeyCode.I)) { OpenInfoBrowser(); return true; }
            if (Input.GetKeyDown(KeyCode.R)) { AnnounceDescription(); return true; }

            // F1-F7 - switch which party member's inventory is shown.
            if (HandlePartySwitch()) return true;

            if (Input.GetKeyDown(KeyCode.Escape)) { CloseMenu(); return true; }

            return false;
        }

        public override void OnActivated()
        {
            IsManagedNavigation = true;
            lastAnnouncedText = null;
            isDirty = false;
            isInfoBrowsing = false;
            cachedMenu = null;
            currentZone = Zone.Inventory;

            RebuildList();
            if (currentList.Count > 0)
                currentIndex = 0;

            string vendorName = GetVendorName();
            string requirements = BuildRequirementsText();

            string announcement = string.IsNullOrEmpty(vendorName)
                ? "Give items."
                : $"Give items to {vendorName}.";
            if (!string.IsNullOrEmpty(requirements))
                announcement += $" {requirements}.";
            announcement += $" Your inventory, {currentList.Count} items.";

            ScreenReaderManager.SpeakInterrupt(announcement);
            if (currentList.Count > 0)
                AnnounceCurrentItem(interrupt: false);

            ModLog.Debug($"[ItemDropoffState] Activated, {currentList.Count} items, requires: {requirements ?? "nothing specific"}");
        }

        public override void OnDeactivated()
        {
            IsManagedNavigation = false;
            currentList.Clear();
            currentIndex = -1;
            isDirty = false;
            isInfoBrowsing = false;
            cachedMenu = null;
            base.OnDeactivated();
        }

        #region List building / navigation

        private void RebuildList()
        {
            int previousIndex = currentIndex;
            currentList.Clear();

            Escrow escrow = GetActiveEscrow();

            if (currentZone == Zone.Inventory)
            {
                if (escrow != null && escrow.invPlayer != null)
                {
                    List<ItemInstance> items;
                    try { items = escrow.invPlayer.GetFilteredList(InventoryFilter.AllWithJunk); }
                    catch (Exception e) { MelonLogger.Warning($"[ItemDropoffState] GetFilteredList failed: {e.Message}"); items = null; }

                    if (items != null)
                    {
                        foreach (ItemInstance item in items)
                        {
                            if (item == null || item.template == null) continue;
                            currentList.Add(new Entry { Item = item, IsGive = false });
                        }
                    }
                }
            }
            else // Zone.Give
            {
                if (escrow != null && escrow.invEscrowPlayer != null)
                {
                    for (int i = 0; i < escrow.invEscrowPlayer.Count; i++)
                    {
                        ItemInstance item = escrow.invEscrowPlayer[i];
                        if (item == null || item.template == null) continue;
                        currentList.Add(new Entry { Item = item, IsGive = true });
                    }
                }
                currentList.Add(FinalizeMarker);
            }

            // Preserve index across a rebuild.
            if (previousIndex >= 0 && previousIndex < currentList.Count)
                currentIndex = previousIndex;
            else if (currentList.Count > 0)
                currentIndex = Math.Min(Math.Max(previousIndex, 0), currentList.Count - 1);
            else
                currentIndex = -1;
        }

        private void NavigateList(int direction)
        {
            if (currentList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt(GetZoneEmptyMessage());
                return;
            }

            int newIndex = currentIndex + direction;
            bool wrapped = false;
            if (newIndex < 0) { newIndex = currentList.Count - 1; wrapped = true; }
            else if (newIndex >= currentList.Count) { newIndex = 0; wrapped = true; }
            if (wrapped && currentList.Count > 1) MenuCue.PlayWrap();

            currentIndex = newIndex;
            AnnounceCurrentItem(interrupt: true);
        }

        private void SwitchZone(int direction)
        {
            int idx = Array.IndexOf(ZoneOrder, currentZone);
            int newIdx = idx + direction;
            bool wrapped = false;
            if (newIdx < 0) { newIdx = ZoneOrder.Length - 1; wrapped = true; }
            else if (newIdx >= ZoneOrder.Length) { newIdx = 0; wrapped = true; }
            if (wrapped) MenuCue.PlayWrap();

            currentZone = ZoneOrder[newIdx];
            currentIndex = 0;
            RebuildList();

            int itemCount = CountItems();
            string zoneName = currentZone == Zone.Inventory ? "Your inventory" : "Items to give";
            ScreenReaderManager.SpeakInterrupt($"{zoneName}, {itemCount} items");
            if (currentList.Count > 0)
                AnnounceCurrentItem(interrupt: false);
        }

        private int CountItems()
        {
            int n = 0;
            foreach (var o in currentList)
                if (o is Entry) n++;
            return n;
        }

        private string GetZoneEmptyMessage()
        {
            return currentZone == Zone.Inventory ? "Your inventory is empty" : "No items staged to give";
        }

        #endregion

        #region Actions

        private void ActOnCurrent()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count)
            {
                ScreenReaderManager.SpeakInterrupt("Nothing selected");
                return;
            }

            object current = currentList[currentIndex];

            if (current is string marker && marker == FinalizeMarker)
            {
                GiveStagedItems();
                return;
            }

            if (!(current is Entry entry) || entry.Item == null || entry.Item.template == null)
            {
                ScreenReaderManager.SpeakInterrupt("Cannot act on this");
                return;
            }

            ItemDropoffMenu menu = GetMenu();
            if (menu == null) return;

            // Bonded / non-transferable items can never be given; the game's
            // OnItemDoubleClicked would silently no-op, so say so instead.
            if (entry.Item.isOriginalProperty)
            {
                ScreenReaderManager.SpeakInterrupt("Cannot give this item");
                return;
            }

            InventoryGrid grid = entry.IsGive ? menu.playerEscrowGrid : menu.playerInventoryGrid;
            VND_DragDropItem dragItem = FindGridItem(entry.Item, grid);
            if (dragItem == null)
            {
                ScreenReaderManager.SpeakInterrupt("Cannot select this item");
                ModLog.Debug($"[ItemDropoffState] No grid drag item for {ItemName(entry.Item)} in zone {currentZone}");
                return;
            }

            string name = ItemName(entry.Item);
            // Stackable multi-quantity moves open the game's quantity dialog (a separate
            // modal), which this state yields to via isTopMenu.
            bool willPromptQuantity = entry.Item.isStackable && entry.Item.quantity > 1;

            menu.OnItemDoubleClicked(dragItem);
            isDirty = true;

            if (willPromptQuantity)
                ScreenReaderManager.SpeakInterrupt($"{name}, select quantity");
            else if (entry.IsGive)
                ScreenReaderManager.SpeakInterrupt($"Removed {name} from give");
            else
                ScreenReaderManager.SpeakInterrupt($"Added {name} to give");

            ModLog.Debug($"[ItemDropoffState] Moved {name} (wasGive={entry.IsGive})");
        }

        private void GiveStagedItems()
        {
            ItemDropoffMenu menu = GetMenu();
            if (menu == null) return;

            if (!AnyStagedItems())
            {
                ScreenReaderManager.SpeakInterrupt("Nothing to give. Move items into the give list first.");
                return;
            }

            try
            {
                // Same path the on-screen Give button drives (OnTradeClicked): commit the
                // escrow. The conversation drama detects the handover and usually closes
                // the popup itself.
                INV_DragDropItem.RevertCurrentMove();
                Escrow.ClearJunkFlags();
                Escrow.RequestTradeAll();

                if (menu.playerInventoryContainer != null) menu.playerInventoryContainer.Refresh();
                if (menu.playerEscrowContainer != null) menu.playerEscrowContainer.Refresh();

                isDirty = true;
                ScreenReaderManager.SpeakInterrupt("Items given");
                ModLog.Debug("[ItemDropoffState] Finalized give");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ItemDropoffState] Give failed: {e.Message}");
                ScreenReaderManager.SpeakInterrupt("Give failed");
            }
        }

        private void CloseMenu()
        {
            ItemDropoffMenu menu = GetMenu();
            if (menu == null) return;
            // "Back" maps to OnCloseClicked: cancels escrow (items return) and closes.
            menu.OnButtonDown("Back");
            ScreenReaderManager.SpeakInterrupt("Cancelled");
            ModLog.Debug("[ItemDropoffState] Closed dropoff menu");
        }

        private bool HandlePartySwitch()
        {
            for (int i = 0; i < 7; i++)
            {
                if (Input.GetKeyDown(KeyCode.F1 + i))
                {
                    SwitchToPartyMember(i);
                    return true;
                }
            }
            return false;
        }

        private void SwitchToPartyMember(int index)
        {
            ItemDropoffMenu menu = GetMenu();
            if (menu == null) return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt($"No party member {index + 1}");
                return;
            }

            // "Select Player N" handles range checks and the too-far modal for us.
            menu.OnButtonDown("Select Player " + (index + 1));
            isDirty = true;
            currentIndex = 0;

            PC pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
            if (pc != null && pc.pcTemplate != null)
            {
                string name = UITextExtractor.CleanText(
                    Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                ScreenReaderManager.SpeakInterrupt($"Selected {name}");
            }
            ModLog.Debug($"[ItemDropoffState] Switched to party member {index + 1}");
        }

        #endregion

        #region Info browser

        private bool HandleInfoBrowserInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (infoLines.Count == 0) return true;
                infoLineIndex--;
                if (infoLineIndex < 0) { infoLineIndex = infoLines.Count - 1; if (infoLines.Count > 1) MenuCue.PlayWrap(); }
                ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                return true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (infoLines.Count == 0) return true;
                infoLineIndex++;
                if (infoLineIndex >= infoLines.Count) { infoLineIndex = 0; if (infoLines.Count > 1) MenuCue.PlayWrap(); }
                ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I))
            {
                isInfoBrowsing = false;
                infoLines.Clear();
                infoLineIndex = -1;
                ScreenReaderManager.SpeakInterrupt("Closed item info");
                return true;
            }
            return true; // consume everything while browsing
        }

        private void OpenInfoBrowser()
        {
            ItemInstance item = GetCurrentItemInstance();
            if (item == null || item.template == null)
            {
                ScreenReaderManager.SpeakInterrupt("No item selected");
                return;
            }

            infoLines.Clear();
            PC pc = GetCurrentPC();
            ItemInfoBox infoBox = GetMenu()?.itemInfoBox;
            ItemInstance comparison = InventoryFormatting.ResolveEquippedComparisonItem(item, pc);
            infoLines.AddRange(InventoryFormatting.BuildItemInfoLines(
                item, pc, infoBox, equippedSlotName: null, comparisonEquipped: comparison));

            if (infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No information available");
                return;
            }

            isInfoBrowsing = true;
            infoLineIndex = 0;
            ScreenReaderManager.SpeakInterrupt(
                $"Item info: {infoLines[0]}, {infoLines.Count} lines, use up and down to browse, escape to close");
        }

        private void AnnounceDescription()
        {
            ItemInstance item = GetCurrentItemInstance();
            if (item == null || item.template == null)
            {
                ScreenReaderManager.SpeakInterrupt("No item selected");
                return;
            }
            if (!string.IsNullOrEmpty(item.template.description))
            {
                string desc = UITextExtractor.CleanText(
                    Language.Localize(item.template.description, false, false, string.Empty));
                if (!string.IsNullOrEmpty(desc))
                {
                    ScreenReaderManager.SpeakInterrupt(desc);
                    return;
                }
            }
            ScreenReaderManager.SpeakInterrupt("No description available");
        }

        #endregion

        #region Announcements

        private void AnnounceCurrentItem(bool interrupt)
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count)
            {
                string empty = GetZoneEmptyMessage();
                if (interrupt) ScreenReaderManager.SpeakInterrupt(empty); else ScreenReaderManager.Speak(empty);
                return;
            }

            string announcement = FormatCurrentAnnouncement();
            if (string.IsNullOrEmpty(announcement)) return;

            announcement += $", {currentIndex + 1} of {currentList.Count}";
            if (announcement == lastAnnouncedText) return;
            lastAnnouncedText = announcement;

            if (interrupt) ScreenReaderManager.SpeakInterrupt(announcement);
            else ScreenReaderManager.Speak(announcement);
        }

        private string FormatCurrentAnnouncement()
        {
            object current = currentList[currentIndex];

            if (current is string marker && marker == FinalizeMarker)
                return "Give these items, press Enter to hand them over";

            if (current is Entry entry && entry.Item != null)
            {
                string text = InventoryFormatting.FormatItemAnnouncement(entry.Item, detailed: true);
                if (entry.IsGive) text += ", staged to give";
                return text;
            }
            return null;
        }

        private string BuildRequirementsText()
        {
            ItemDropoffMenu menu = GetMenu();
            if (menu == null || menu.itemsRequired == null || menu.itemsRequired.Count == 0)
                return null;

            var parts = new List<string>();
            foreach (ItemRequired req in menu.itemsRequired)
            {
                if (req == null) continue;
                string name = null;
                if (req.itemTemplate != null)
                    name = UITextExtractor.CleanText(
                        Language.Localize(req.itemTemplate.displayName, false, false, string.Empty));
                else if (req.itemType != null)
                    name = req.itemType.Name;

                if (string.IsNullOrEmpty(name)) continue;
                parts.Add(req.count > 1 ? $"{req.count} {name}" : name);
            }

            if (parts.Count == 0) return null;
            return "Wants: " + string.Join(", ", parts.ToArray());
        }

        #endregion

        #region Helpers

        private ItemDropoffMenu GetMenu()
        {
            if (cachedMenu != null && cachedMenu.gameObject.activeInHierarchy)
                return cachedMenu;
            cachedMenu = SceneQueryCache.Find<ItemDropoffMenu>();
            return cachedMenu;
        }

        private Escrow GetActiveEscrow()
        {
            PC pc = GetCurrentPC();
            if (pc != null)
            {
                Escrow e = Escrow.GetEscrowForPC(pc);
                if (e != null) return e;
            }
            return Escrow.escrowList.Count > 0 ? Escrow.escrowList[0] : null;
        }

        private bool AnyStagedItems()
        {
            for (int i = 0; i < Escrow.escrowList.Count; i++)
            {
                var esc = Escrow.escrowList[i];
                if (esc.invEscrowPlayer != null && esc.invEscrowPlayer.Count > 0)
                    return true;
            }
            return false;
        }

        private VND_DragDropItem FindGridItem(ItemInstance item, InventoryGrid grid)
        {
            if (item == null || grid == null) return null;
            for (int i = 0; i < grid.transform.childCount; i++)
            {
                Transform t = grid.transform.GetChild(i);
                if (t == null || !t.gameObject.activeSelf) continue;
                var dd = t.GetComponent<VND_DragDropItem>();
                if (dd != null && dd.GetItem() == item)
                    return dd;
            }
            return null;
        }

        private ItemInstance GetCurrentItemInstance()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count) return null;
            return currentList[currentIndex] is Entry entry ? entry.Item : null;
        }

        private PC GetCurrentPC()
        {
            return MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
        }

        private string GetVendorName()
        {
            ItemDropoffMenu menu = GetMenu();
            if (menu != null && menu.vendorNameLabel != null && !string.IsNullOrEmpty(menu.vendorNameLabel.text))
                return UITextExtractor.CleanText(menu.vendorNameLabel.text);
            return null;
        }

        private static string ItemName(ItemInstance item)
        {
            if (item == null || item.template == null) return "item";
            return UITextExtractor.CleanText(
                Language.Localize(item.template.displayName, false, false, string.Empty));
        }

        #endregion
    }
}
