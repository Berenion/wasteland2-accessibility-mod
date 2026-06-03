using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Full keyboard navigation and screen reader support for the vendor/shop screen.
    /// Handles buying and selling with NPCs via the VendorScreen.
    /// Four navigation zones: PlayerInventory, Escrow, VendorInventory, Filters.
    /// Priority 50 - same level as InventoryState (mutually exclusive).
    /// </summary>
    public class ShopState : AccessibilityStateBase
    {
        public override string Name => "Shop";
        public override int Priority => 50;

        /// <summary>
        /// When true, managed navigation is active and patch-based announcements should be suppressed.
        /// </summary>
        public static bool IsManagedNavigation { get; private set; }

        // Navigation zones
        private enum ShopZone
        {
            PlayerInventory,
            Escrow,
            VendorInventory,
            Filters
        }

        // Zone order for Left/Right cycling
        private static readonly ShopZone[] ZoneOrder = new ShopZone[]
        {
            ShopZone.PlayerInventory,
            ShopZone.Escrow,
            ShopZone.VendorInventory,
            ShopZone.Filters
        };

        // Current state
        private ShopZone currentZone;
        private List<object> currentList = new List<object>(); // ShopItemEntry, EscrowEntry, string (escrow markers), or GameObject (filters)
        private int currentIndex = -1;
        private bool isDirty = false;

        // Suspended state for overlays (quantity dialogs, modals)
        private ShopZone suspendedZone;
        private int suspendedIndex = -1;
        private bool hasSuspendedState = false;

        // Info browser mode (reuses InventoryState's pattern)
        private bool isInfoBrowsing = false;
        private List<string> infoLines = new List<string>();
        private int infoLineIndex = -1;

        // Announcement tracking
        private string lastAnnouncedText = null;

        // Sentinel values for escrow list
        private const string EscrowSummaryMarker = "__ESCROW_SUMMARY__";
        private const string EscrowFinalizeMarker = "__ESCROW_FINALIZE__";

        /// <summary>
        /// Wrapper for items in the escrow list, read directly from the Escrow data model.
        /// </summary>
        private class EscrowEntry
        {
            public ItemInstance Item;
            public bool IsSelling; // true = player item being sold, false = vendor item being bought
            public Escrow ParentEscrow;
        }

        /// <summary>
        /// Wrapper for player/vendor inventory items, read directly from the Inventory data model.
        /// Reading from the data model avoids stale positionCache and grid timing issues that
        /// caused the displayed list to lag behind reality after a sell/buy.
        /// </summary>
        private class ShopItemEntry
        {
            public ItemInstance Item;
            public Inventory OwnerInventory;
            public PC OwnerPC; // null for vendor items
            public bool IsVendor;
        }

        // Cached VendorScreen reference
        private VendorScreen cachedVendorScreen = null;

        // Reflection caches
        private static bool reflectionCached = false;
        private static MethodInfo tryGetDestInventoryMethod;
        private static MethodInfo setFilterMethod;
        private static FieldInfo inventoryContainerFilterField;
        private static MethodInfo onSellJunkClickedMethod;

        public override bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

                var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();
                if (!guiManager.IsAnyMenuActive()) return false;

                // Yield to GenericMenuState when an overlay screen is open
                if (guiManager.IsItemInfoScreenOpen()) return false;

                return guiManager.IsVendorScreenOpen();
            }
        }

        public override bool HandleInput()
        {
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            // Info browser mode intercepts all input
            if (isInfoBrowsing)
                return HandleInfoBrowserInput();

            // Rebuild lists if dirty
            if (isDirty)
            {
                isDirty = false;
                RebuildCurrentList();
            }

            // Up/Down - navigate within current zone
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateList(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateList(1);
                return true;
            }

            // Left/Right - switch zones
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SwitchZone(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SwitchZone(1);
                return true;
            }

            // Enter - context-dependent action
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                PerformAction();
                return true;
            }

            // I - detailed item info browser
            if (Input.GetKeyDown(KeyCode.I))
            {
                OpenInfoBrowser();
                return true;
            }

            // R - read description
            if (Input.GetKeyDown(KeyCode.R))
            {
                AnnounceDescription();
                return true;
            }

            // S - read scrap balance
            if (Input.GetKeyDown(KeyCode.S))
            {
                AnnounceScrapBalance();
                return true;
            }

            // J - sell all junk
            if (Input.GetKeyDown(KeyCode.J))
            {
                SellAllJunk();
                return true;
            }

            // F - cycle filter (when in Filters zone or any item zone)
            if (Input.GetKeyDown(KeyCode.F))
            {
                CycleFilter();
                return true;
            }

            // F1-F7 - switch party member
            if (HandlePartySwitch())
                return true;

            // Escape - close shop
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseShop();
                return true;
            }

            return false;
        }

        public override void OnActivated()
        {
            IsManagedNavigation = true;
            if (!reflectionCached) CacheReflection();

            lastAnnouncedText = null;
            isDirty = false;
            isInfoBrowsing = false;
            cachedVendorScreen = null;

            // Restore suspended state if returning from an overlay
            if (hasSuspendedState)
            {
                hasSuspendedState = false;
                currentZone = suspendedZone;
                RebuildCurrentList();

                if (suspendedIndex >= 0 && suspendedIndex < currentList.Count)
                    currentIndex = suspendedIndex;
                else if (currentList.Count > 0)
                    currentIndex = Math.Min(suspendedIndex, currentList.Count - 1);
                else
                    currentIndex = -1;

                AnnounceCurrentItem(interrupt: true);
                MelonLogger.Msg($"[ShopState] Restored from suspend, zone={currentZone}, index={currentIndex}");
                return;
            }

            // Start in vendor inventory (buy mode) by default
            var vendorScreen = GetVendorScreen();
            if (vendorScreen != null)
            {
                // Ensure we're in buy mode so vendor items are visible
                if (vendorScreen.isSelling)
                {
                    vendorScreen.SetSellMode(false);
                }

                string vendorName = GetVendorName(vendorScreen);
                int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;

                currentZone = ShopZone.VendorInventory;
                BuildVendorItemList();

                string announcement = $"Shop, {vendorName}, {scrap} scrap, {currentList.Count} items";
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            else
            {
                currentZone = ShopZone.VendorInventory;
                currentList.Clear();
                ScreenReaderManager.SpeakInterrupt("Shop");
            }

            if (currentList.Count > 0 && currentIndex < 0)
                currentIndex = 0;

            MelonLogger.Msg($"[ShopState] Activated, zone={currentZone}, items={currentList.Count}");
        }

        public override void OnDeactivated()
        {
            suspendedZone = currentZone;
            suspendedIndex = currentIndex;
            hasSuspendedState = true;

            IsManagedNavigation = false;
            lastAnnouncedText = null;
            currentList.Clear();
            isDirty = false;
            isInfoBrowsing = false;
            cachedVendorScreen = null;
            MelonLogger.Msg($"[ShopState] Deactivated (suspended zone={suspendedZone}, index={suspendedIndex})");
        }

        #region Zone Switching

        private void SwitchZone(int direction)
        {
            int currentZoneIdx = Array.IndexOf(ZoneOrder, currentZone);
            int newZoneIdx = currentZoneIdx + direction;

            // Wrap around
            if (newZoneIdx < 0) newZoneIdx = ZoneOrder.Length - 1;
            else if (newZoneIdx >= ZoneOrder.Length) newZoneIdx = 0;

            ShopZone newZone = ZoneOrder[newZoneIdx];
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            // Activate the correct grid based on zone
            if (newZone == ShopZone.PlayerInventory || newZone == ShopZone.Escrow)
            {
                // Need sell mode active to see player inventory
                if (!vendorScreen.isSelling)
                    vendorScreen.SetSellMode(true);
            }
            else if (newZone == ShopZone.VendorInventory)
            {
                // Need buy mode active to see vendor inventory
                if (vendorScreen.isSelling)
                    vendorScreen.SetSellMode(false);
            }

            currentZone = newZone;
            RebuildCurrentList();

            string zoneName = GetZoneName(currentZone);
            string zoneAnnouncement = $"{zoneName}, {currentList.Count} items";

            if (currentZone == ShopZone.Escrow)
            {
                // Count actual items (not summary/finalize markers)
                int itemCount = 0;
                foreach (var entry in currentList)
                {
                    if (entry is EscrowEntry)
                        itemCount++;
                }
                zoneAnnouncement = $"{zoneName}, {itemCount} items in escrow";
            }

            ScreenReaderManager.SpeakInterrupt(zoneAnnouncement);

            if (currentList.Count > 0 && currentIndex >= 0)
            {
                AnnounceCurrentItem(interrupt: false);
            }
        }

        private string GetZoneName(ShopZone zone)
        {
            switch (zone)
            {
                case ShopZone.PlayerInventory: return "Your inventory";
                case ShopZone.Escrow: return "Escrow";
                case ShopZone.VendorInventory: return "Vendor inventory";
                case ShopZone.Filters: return "Filters";
                default: return "Unknown";
            }
        }

        #endregion

        #region List Building

        private void BuildPlayerItemList()
        {
            currentList.Clear();
            currentZone = ShopZone.PlayerInventory;

            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            var container = vendorScreen.playerInventoryContainer;
            if (container == null) return;

            InventoryFilter filter = container.GetFilter();

            if (container.inventory != null)
            {
                PC ownerPC = GetCurrentPC();
                AddInventoryItemsToList(container.inventory, filter, ownerPC, isVendor: false, excludeNoDrop: true);
            }
            else
            {
                // "All Squad Members" mode: container has no single inventory; iterate party
                var game = MonoBehaviourSingleton<Game>.GetInstance();
                if (game != null && game.party != null)
                {
                    for (int i = 0; i < game.party.Count; i++)
                    {
                        PC pc = game.party[i];
                        if (pc != null && vendorScreen.IsPCWithinRange(pc) && pc.inventory != null && pc.inventory.inventory != null)
                        {
                            AddInventoryItemsToList(pc.inventory.inventory, filter, pc, isVendor: false, excludeNoDrop: true);
                        }
                    }
                }
            }

            ClampIndex();
            MelonLogger.Msg($"[ShopState] Built player item list: {currentList.Count} items (filter={filter})");
        }

        private void BuildVendorItemList()
        {
            currentList.Clear();
            currentZone = ShopZone.VendorInventory;

            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            var container = vendorScreen.vendorInventoryContainer;
            if (container == null) return;

            InventoryFilter filter = container.GetFilter();

            if (container.inventory != null)
            {
                AddInventoryItemsToList(container.inventory, filter, null, isVendor: true, excludeNoDrop: false);
            }

            ClampIndex();
            MelonLogger.Msg($"[ShopState] Built vendor item list: {currentList.Count} items (filter={filter})");
        }

        private void AddInventoryItemsToList(Inventory inv, InventoryFilter filter, PC ownerPC, bool isVendor, bool excludeNoDrop)
        {
            if (inv == null) return;
            List<ItemInstance> items;
            try
            {
                items = inv.GetFilteredList(filter);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[ShopState] GetFilteredList failed: {e.Message}");
                return;
            }
            if (items == null) return;
            foreach (ItemInstance item in items)
            {
                if (item == null || item.template == null) continue;
                // Player inventory in vendor view excludes partyNoDrop items (matches game's ConsistencyCheck)
                if (excludeNoDrop && item.template.partyNoDrop) continue;

                currentList.Add(new ShopItemEntry
                {
                    Item = item,
                    OwnerInventory = inv,
                    OwnerPC = ownerPC,
                    IsVendor = isVendor
                });
            }
        }

        private VND_DragDropItem FindGridDragDropItem(ItemInstance item, InventoryGrid grid)
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

        private void BuildEscrowList()
        {
            currentList.Clear();
            currentZone = ShopZone.Escrow;

            // Read directly from the Escrow data model — the container UI may not be populated
            for (int e = 0; e < Escrow.escrowList.Count; e++)
            {
                Escrow escrow = Escrow.escrowList[e];

                // Player escrow items (items the player is selling)
                for (int i = 0; i < escrow.invEscrowPlayer.Count; i++)
                {
                    ItemInstance item = escrow.invEscrowPlayer[i];
                    if (item != null)
                    {
                        currentList.Add(new EscrowEntry
                        {
                            Item = item,
                            IsSelling = true,
                            ParentEscrow = escrow
                        });
                    }
                }

                // Vendor escrow items (items the player is buying)
                for (int i = 0; i < escrow.invEscrowVendor.Count; i++)
                {
                    ItemInstance item = escrow.invEscrowVendor[i];
                    if (item != null)
                    {
                        currentList.Add(new EscrowEntry
                        {
                            Item = item,
                            IsSelling = false,
                            ParentEscrow = escrow
                        });
                    }
                }
            }

            // Add summary and finalize markers at the end
            currentList.Add(EscrowSummaryMarker);
            currentList.Add(EscrowFinalizeMarker);

            ClampIndex();
            int itemCount = currentList.Count - 2; // exclude markers
            MelonLogger.Msg($"[ShopState] Built escrow list: {itemCount} items + 2 markers");
        }

        private void BuildFilterList()
        {
            currentList.Clear();
            currentZone = ShopZone.Filters;

            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            // Get the active container's filter grid
            VND_InventoryContainer activeContainer = vendorScreen.isSelling
                ? vendorScreen.playerInventoryContainer
                : vendorScreen.vendorInventoryContainer;

            if (activeContainer == null || activeContainer.filterGrid == null) return;

            UIGrid filterGrid = activeContainer.filterGrid;
            foreach (Transform child in filterGrid.transform)
            {
                if (child != null && child.gameObject.activeSelf)
                {
                    currentList.Add(child.gameObject);
                }
            }

            ClampIndex();
            MelonLogger.Msg($"[ShopState] Built filter list: {currentList.Count} filters");
        }

        private void RebuildCurrentList()
        {
            int previousIndex = currentIndex;
            switch (currentZone)
            {
                case ShopZone.PlayerInventory:
                    BuildPlayerItemList();
                    break;
                case ShopZone.VendorInventory:
                    BuildVendorItemList();
                    break;
                case ShopZone.Escrow:
                    BuildEscrowList();
                    break;
                case ShopZone.Filters:
                    BuildFilterList();
                    break;
            }

            // Preserve index
            if (previousIndex >= 0 && previousIndex < currentList.Count)
                currentIndex = previousIndex;
            else if (currentList.Count > 0)
                currentIndex = Math.Min(previousIndex, currentList.Count - 1);
            else
                currentIndex = -1;
        }

        private void ClampIndex()
        {
            if (currentIndex >= currentList.Count)
                currentIndex = currentList.Count > 0 ? currentList.Count - 1 : -1;
            if (currentIndex < 0 && currentList.Count > 0)
                currentIndex = 0;
        }

        #endregion

        #region Navigation

        private void NavigateList(int direction)
        {
            if (currentList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt(GetZoneEmptyMessage());
                return;
            }

            int newIndex = currentIndex + direction;

            // Wrap around
            if (newIndex < 0)
                newIndex = currentList.Count - 1;
            else if (newIndex >= currentList.Count)
                newIndex = 0;

            currentIndex = newIndex;
            AnnounceCurrentItem(interrupt: true);
        }

        private string GetZoneEmptyMessage()
        {
            switch (currentZone)
            {
                case ShopZone.PlayerInventory: return "Your inventory is empty";
                case ShopZone.VendorInventory: return "Vendor has no items";
                case ShopZone.Escrow: return "Escrow is empty";
                case ShopZone.Filters: return "No filters available";
                default: return "Empty";
            }
        }

        #endregion

        #region Actions

        private void PerformAction()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count)
            {
                ScreenReaderManager.SpeakInterrupt("Nothing selected");
                return;
            }

            object current = currentList[currentIndex];

            // Escrow summary marker - just re-announce it
            if (current is string str && str == EscrowSummaryMarker)
            {
                AnnounceEscrowSummary();
                return;
            }

            // Escrow finalize marker - execute the trade
            if (current is string str2 && str2 == EscrowFinalizeMarker)
            {
                FinalizeTrade();
                return;
            }

            // Filter button - activate it
            if (current is GameObject filterObj)
            {
                ActivateFilter(filterObj);
                return;
            }

            // Escrow item - remove from escrow
            if (current is EscrowEntry escrowEntry)
            {
                RemoveFromEscrow(escrowEntry);
                return;
            }

            // Player/vendor inventory item: move to escrow
            if (current is ShopItemEntry shopEntry)
            {
                TradeItem(shopEntry);
                return;
            }

            ScreenReaderManager.SpeakInterrupt("Cannot act on this");
        }

        private void TradeItem(ShopItemEntry entry)
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            ItemInstance item = entry?.Item;
            if (item == null || item.template == null)
            {
                ScreenReaderManager.SpeakInterrupt("No item");
                return;
            }

            if (item.isOriginalProperty)
            {
                ScreenReaderManager.SpeakInterrupt("Cannot trade this item");
                return;
            }

            Inventory ownerInventory = entry.OwnerInventory;
            if (ownerInventory == null)
            {
                ScreenReaderManager.SpeakInterrupt("Cannot trade this item");
                return;
            }

            string itemName = UITextExtractor.CleanText(
                Language.Localize(item.template.displayName, false, false, string.Empty));

            if (tryGetDestInventoryMethod == null)
            {
                ScreenReaderManager.SpeakInterrupt("Trade not available");
                MelonLogger.Warning("[ShopState] TryGetDestInventory method not found");
                return;
            }

            try
            {
                object[] args = new object[] { ownerInventory, null, null };
                bool found = (bool)tryGetDestInventoryMethod.Invoke(vendorScreen, args);

                if (!found)
                {
                    ScreenReaderManager.SpeakInterrupt("Cannot trade this item");
                    return;
                }

                Inventory dstInv = (Inventory)args[1];
                VND_InventoryContainer dstContainer = (VND_InventoryContainer)args[2];

                // Validate the trade
                if (!dstInv.WillTakeItemFrom(item, ownerInventory) || !ownerInventory.WillGiveItemTo(item, dstInv))
                {
                    ScreenReaderManager.SpeakInterrupt("Cannot trade this item");
                    return;
                }

                // For stackable items with quantity > 1, open the game's quantity dialog.
                // AskForQuantity needs an INV_DragDropItem reference, so look it up from the active grid.
                if (item.isStackable && item.quantity > 1)
                {
                    InventoryGrid grid = entry.IsVendor
                        ? vendorScreen.vendorInventoryGrid
                        : vendorScreen.playerInventoryGrid;
                    VND_DragDropItem vndItem = FindGridDragDropItem(item, grid);
                    VND_DragDropContainer vndDDContainer = dstContainer.dragDropContainer != null
                        ? dstContainer.dragDropContainer
                        : dstContainer.GetComponentInChildren<VND_DragDropContainer>();
                    if (vndItem != null && vndDDContainer != null)
                    {
                        vndDDContainer.AskForQuantity(vndItem);
                        ScreenReaderManager.SpeakInterrupt($"{itemName}, select quantity");
                        return;
                    }
                    // If we couldn't find the grid item, fall through and transfer the whole stack.
                    MelonLogger.Msg($"[ShopState] No grid item for stackable {itemName}, transferring whole stack");
                }

                // Unequip if currently equipped on the owning PC
                if (entry.OwnerPC != null && entry.OwnerPC.inventory != null)
                {
                    var equipment = entry.OwnerPC.inventory.equipment;
                    if (equipment != null)
                    {
                        for (int slot = 0; slot < equipment.Length; slot++)
                        {
                            if (slot == (int)EquipmentSlot.None) continue;
                            if (equipment[slot] == item)
                            {
                                entry.OwnerPC.inventory.UnEquip((EquipmentSlot)slot);
                                break;
                            }
                        }
                    }
                }

                // Move via the data model. Refresh() on the containers cleans up the stale UI items.
                int qty = item.quantity;
                ownerInventory.RemoveItemInstance(item, qty, removeActualInstance: true);
                ownerInventory.UpdateTotalValue();
                dstInv.AddItems(new[] { item });

                // Refresh containers (do NOT finalize — items stay in escrow until user confirms)
                vendorScreen.playerInventoryContainer.Refresh();
                vendorScreen.playerEscrowContainer.Refresh();
                vendorScreen.vendorInventoryContainer.Refresh();
                vendorScreen.vendorEscrowContainer.Refresh();
                Escrow.UpdateTotalValue();

                isDirty = true;

                if (entry.IsVendor)
                    ScreenReaderManager.SpeakInterrupt($"Added {itemName} to buy");
                else
                    ScreenReaderManager.SpeakInterrupt($"Added {itemName} to sell");

                MelonLogger.Msg($"[ShopState] Moved item to escrow: {itemName}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ShopState] Failed to move item to escrow: {e.Message}");
                ScreenReaderManager.SpeakInterrupt("Trade failed");
            }
        }

        private void RemoveFromEscrow(EscrowEntry entry)
        {
            if (entry == null || entry.Item == null || entry.ParentEscrow == null)
            {
                ScreenReaderManager.SpeakInterrupt("Nothing to remove");
                return;
            }

            string itemName = UITextExtractor.CleanText(
                Language.Localize(entry.Item.template.displayName, false, false, string.Empty));

            try
            {
                Inventory escrowInv;
                Inventory sourceInv;

                if (entry.IsSelling)
                {
                    // Move from player escrow back to player inventory
                    escrowInv = entry.ParentEscrow.invEscrowPlayer;
                    sourceInv = entry.ParentEscrow.invPlayer;
                }
                else
                {
                    // Move from vendor escrow back to vendor inventory
                    escrowInv = entry.ParentEscrow.invEscrowVendor;
                    sourceInv = entry.ParentEscrow.invVendor;
                }

                // Move the item back
                escrowInv.Remove(entry.Item);
                sourceInv.Add(entry.Item);
                Escrow.UpdateTotalValue();

                // Refresh containers
                var vendorScreen = GetVendorScreen();
                if (vendorScreen != null)
                {
                    vendorScreen.playerInventoryContainer.Refresh();
                    vendorScreen.playerEscrowContainer.Refresh();
                    vendorScreen.vendorInventoryContainer.Refresh();
                    vendorScreen.vendorEscrowContainer.Refresh();
                }

                isDirty = true;

                string action = entry.IsSelling ? "Removed from sell" : "Removed from buy";
                ScreenReaderManager.SpeakInterrupt($"{action}: {itemName}");
                MelonLogger.Msg($"[ShopState] Removed from escrow: {itemName}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ShopState] Failed to remove from escrow: {e.Message}");
                ScreenReaderManager.SpeakInterrupt("Failed to remove item");
            }
        }

        private void FinalizeTrade()
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            try
            {
                // Check if there's anything to trade
                float vendorEscrowVal = Escrow.GetTotalVendorEscrowValue();
                float playerEscrowVal = Escrow.GetTotalPlayerEscrowValue();

                bool hasItems = false;
                for (int i = 0; i < Escrow.escrowList.Count; i++)
                {
                    if (Escrow.escrowList[i].invEscrowPlayer.Count > 0 ||
                        Escrow.escrowList[i].invEscrowVendor.Count > 0)
                    {
                        hasItems = true;
                        break;
                    }
                }

                if (!hasItems)
                {
                    ScreenReaderManager.SpeakInterrupt("Nothing to trade");
                    return;
                }

                // Check if player has enough scrap
                int netCost = Mathf.CeilToInt(vendorEscrowVal - playerEscrowVal);
                int partyCurrency = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;

                if (netCost > 0 && partyCurrency < netCost)
                {
                    ScreenReaderManager.SpeakInterrupt($"Not enough scrap. Need {netCost}, have {partyCurrency}");
                    // Cancel escrow — items go back to their owners
                    Escrow.CancelAll();
                    vendorScreen.playerInventoryContainer.Refresh();
                    vendorScreen.vendorInventoryContainer.Refresh();
                    isDirty = true;
                    return;
                }

                // Execute the trade directly (bypassing OnTradeButtonClicked which we've patched out)
                INV_DragDropItem.RevertCurrentMove();
                Escrow.ClearJunkFlags();
                Escrow.RequestTradeAll();

                // Refresh all containers
                vendorScreen.playerInventoryContainer.Refresh();
                vendorScreen.playerEscrowContainer.Refresh();
                vendorScreen.vendorInventoryContainer.Refresh();
                vendorScreen.vendorEscrowContainer.Refresh();

                isDirty = true;

                int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
                ScreenReaderManager.SpeakInterrupt($"Trade complete, {scrap} scrap remaining");
                MelonLogger.Msg("[ShopState] Trade finalized");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[ShopState] Failed to finalize trade: {e.Message}");
                ScreenReaderManager.SpeakInterrupt("Trade failed");
            }
        }

        private void ActivateFilter(GameObject filterObj)
        {
            if (filterObj == null) return;

            // Click the filter button
            filterObj.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            isDirty = true;

            string filterName = GetFilterButtonName(filterObj);
            ScreenReaderManager.SpeakInterrupt($"Filter: {filterName}");
            MelonLogger.Msg($"[ShopState] Activated filter: {filterName}");
        }

        private void SellAllJunk()
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            if (onSellJunkClickedMethod != null)
            {
                try
                {
                    onSellJunkClickedMethod.Invoke(vendorScreen, new object[] { null });
                    isDirty = true;

                    int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
                    ScreenReaderManager.SpeakInterrupt($"Sold junk, {scrap} scrap");
                    MelonLogger.Msg("[ShopState] Sold all junk");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"[ShopState] Failed to sell junk: {e.Message}");
                    ScreenReaderManager.SpeakInterrupt("Could not sell junk");
                }
            }
        }

        private void CloseShop()
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            // Cancel all escrow and close — use OnButtonDown which handles audio too
            vendorScreen.OnButtonDown("Back");

            hasSuspendedState = false;
            MelonLogger.Msg("[ShopState] Closed shop");
        }

        private void CycleFilter()
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            // Determine which container is active
            VND_InventoryContainer activeContainer = vendorScreen.isSelling
                ? vendorScreen.playerInventoryContainer
                : vendorScreen.vendorInventoryContainer;

            if (activeContainer == null) return;

            // Get current filter via reflection
            InventoryFilter currentFilter = InventoryFilter.AllWithJunk;
            if (inventoryContainerFilterField != null)
            {
                currentFilter = (InventoryFilter)inventoryContainerFilterField.GetValue(activeContainer);
            }

            // Cycle through filters
            InventoryFilter[] filterOrder = new InventoryFilter[]
            {
                InventoryFilter.AllWithJunk,
                InventoryFilter.Weapon,
                InventoryFilter.Armor,
                InventoryFilter.Ammo,
                InventoryFilter.Consumables,
                InventoryFilter.Misc,
                InventoryFilter.Junk
            };

            int currentIdx = Array.IndexOf(filterOrder, currentFilter);
            int nextIdx = (currentIdx + 1) % filterOrder.Length;
            InventoryFilter newFilter = filterOrder[nextIdx];

            if (setFilterMethod != null)
            {
                try
                {
                    setFilterMethod.Invoke(activeContainer, new object[] { newFilter });
                    isDirty = true;

                    string filterName = GetFilterName(newFilter);
                    ScreenReaderManager.SpeakInterrupt($"Filter: {filterName}");
                    MelonLogger.Msg($"[ShopState] Set filter to {newFilter}");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"[ShopState] Failed to set filter: {e.Message}");
                }
            }
        }

        #endregion

        #region Party Switching

        private bool HandlePartySwitch()
        {
            for (int i = 0; i < 7; i++)
            {
                KeyCode key = KeyCode.F1 + i;
                if (Input.GetKeyDown(key))
                {
                    SwitchToPartyMember(i);
                    return true;
                }
            }
            return false;
        }

        private void SwitchToPartyMember(int index)
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt($"No party member {index + 1}");
                return;
            }

            PC pc = party[index];
            if (pc == null) return;

            // Check if PC is within range
            if (!vendorScreen.IsPCWithinRange(pc))
            {
                string pcName = UITextExtractor.CleanText(
                    Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                ScreenReaderManager.SpeakInterrupt($"{pcName} is too far away");
                return;
            }

            // Switch to this party member
            // Use CheckSelectCharacter which handles audio, selection, and range checks
            MonoBehaviourSingleton<InputManager>.GetInstance().ClearSelection();
            pc.MakeLeader();
            MonoBehaviourSingleton<InputManager>.GetInstance().AddToSelection(pc);
            vendorScreen.SelectPlayer(pc);

            isDirty = true;

            string name = UITextExtractor.CleanText(
                Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt($"Selected {name}");
            MelonLogger.Msg($"[ShopState] Switched to party member {index + 1}: {name}");
        }

        #endregion

        #region Info Browser

        private bool HandleInfoBrowserInput()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (infoLines.Count == 0) return true;
                infoLineIndex--;
                if (infoLineIndex < 0) infoLineIndex = infoLines.Count - 1;
                ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (infoLines.Count == 0) return true;
                infoLineIndex++;
                if (infoLineIndex >= infoLines.Count) infoLineIndex = 0;
                ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                if (infoLines.Count > 0)
                {
                    infoLineIndex = 0;
                    ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                if (infoLines.Count > 0)
                {
                    infoLineIndex = infoLines.Count - 1;
                    ScreenReaderManager.SpeakInterrupt($"{infoLines[infoLineIndex]}, {infoLineIndex + 1} of {infoLines.Count}");
                }
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

            // Consume all other keys while browsing
            return true;
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
            BuildInfoLines(item);

            if (infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No information available");
                return;
            }

            isInfoBrowsing = true;
            infoLineIndex = 0;
            ScreenReaderManager.SpeakInterrupt($"Item info: {infoLines[0]}, {infoLines.Count} lines, use up and down to browse, escape to close");
        }

        private void BuildInfoLines(ItemInstance item)
        {
            PC pc = GetCurrentPC();

            // Shop-specific trade price lines, inserted where the default "Value" line
            // would otherwise appear in the shared block.
            List<string> priceLines = BuildPriceInfoLines(item);

            // The vendor screen's ItemInfoBox carries the same visible labels a sighted
            // player reads, so scrape it the same way the inventory browser does.
            ItemInfoBox infoBox = null;
            var vendorScreen = GetVendorScreen();
            if (vendorScreen != null)
                infoBox = vendorScreen.itemInfoBox;

            // Compare a vendor item the user might buy (or a player item) against what the
            // selected PC currently has equipped.
            ItemInstance comparisonEquipped = InventoryFormatting.ResolveEquippedComparisonItem(item, pc);

            var lines = InventoryFormatting.BuildItemInfoLines(
                item, pc, infoBox,
                equippedSlotName: null,
                comparisonEquipped: comparisonEquipped,
                valueLinesOverride: priceLines);

            infoLines.AddRange(lines);
        }

        private List<string> BuildPriceInfoLines(ItemInstance item)
        {
            var priceLines = new List<string>();
            if (item.template.price <= 0) return priceLines;

            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return priceLines;

            // Try to get the actual trade value via the Escrow system
            PC pc = GetCurrentPC();
            Escrow escrow = pc != null ? Escrow.GetEscrowForPC(pc) : null;

            if (escrow != null)
            {
                Inventory ownerInventory = GetCurrentOwnerInventory();
                if (ownerInventory != null)
                {
                    float tradeValue = escrow.TradeValueOfItem(item, ownerInventory, true);
                    int price = Mathf.FloorToInt(tradeValue);

                    if (currentZone == ShopZone.VendorInventory)
                        priceLines.Add($"Buy price: {price} scrap");
                    else if (currentZone == ShopZone.PlayerInventory)
                        priceLines.Add($"Sell price: {price} scrap");
                    else
                        priceLines.Add($"Trade value: {price} scrap");

                    if (item.quantity > 1)
                        priceLines.Add($"Total: {price * item.quantity} scrap");

                    return priceLines;
                }
            }

            // Fallback: base price
            priceLines.Add($"Base price: {item.template.price} scrap");
            return priceLines;
        }

        #endregion

        #region Announcements

        private void AnnounceCurrentItem(bool interrupt)
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count)
            {
                string emptyMsg = GetZoneEmptyMessage();
                if (interrupt)
                    ScreenReaderManager.SpeakInterrupt(emptyMsg);
                else
                    ScreenReaderManager.Speak(emptyMsg);
                return;
            }

            string announcement = FormatCurrentItemAnnouncement();
            if (string.IsNullOrEmpty(announcement)) return;

            // Add position info
            announcement += $", {currentIndex + 1} of {currentList.Count}";

            if (announcement == lastAnnouncedText) return;
            lastAnnouncedText = announcement;

            if (interrupt)
                ScreenReaderManager.SpeakInterrupt(announcement);
            else
                ScreenReaderManager.Speak(announcement);
        }

        private string FormatCurrentItemAnnouncement()
        {
            object current = currentList[currentIndex];

            // Escrow summary
            if (current is string str && str == EscrowSummaryMarker)
            {
                return FormatEscrowSummary();
            }

            // Escrow finalize
            if (current is string str2 && str2 == EscrowFinalizeMarker)
            {
                return "Finalize trade, press Enter to confirm";
            }

            // Filter button
            if (current is GameObject filterObj)
            {
                return GetFilterButtonName(filterObj);
            }

            // Escrow entry (from data model)
            if (current is EscrowEntry escrowEntry)
            {
                ItemInstance item = escrowEntry.Item;
                if (item == null) return "Empty";

                string baseAnnouncement = InventoryFormatting.FormatItemAnnouncement(item, detailed: true);

                // Add price from escrow
                if (item.template.price > 0)
                {
                    Inventory escrowInv = escrowEntry.IsSelling
                        ? escrowEntry.ParentEscrow.invEscrowPlayer
                        : escrowEntry.ParentEscrow.invEscrowVendor;
                    float tradeValue = escrowEntry.ParentEscrow.TradeValueOfItem(item, escrowInv, true);
                    int price = Mathf.FloorToInt(tradeValue);
                    baseAnnouncement += $", {price} scrap";
                    if (item.quantity > 1)
                        baseAnnouncement += $" each, {price * item.quantity} total";
                }

                baseAnnouncement += escrowEntry.IsSelling ? ", selling" : ", buying";
                return baseAnnouncement;
            }

            // Player/vendor inventory item (from data model)
            if (current is ShopItemEntry shopEntry)
            {
                ItemInstance item = shopEntry.Item;
                if (item == null) return "Empty";

                string baseAnnouncement = InventoryFormatting.FormatItemAnnouncement(item, detailed: true);

                string priceStr = GetItemPriceString(item, shopEntry.OwnerInventory);
                if (!string.IsNullOrEmpty(priceStr))
                    baseAnnouncement += $", {priceStr}";

                return baseAnnouncement;
            }

            return null;
        }

        private string GetItemPriceString(ItemInstance item, Inventory ownerInventory)
        {
            if (item.template.price <= 0) return null;

            PC pc = GetCurrentPC();
            Escrow escrow = pc != null ? Escrow.GetEscrowForPC(pc) : null;

            if (escrow != null && ownerInventory != null)
            {
                float tradeValue = escrow.TradeValueOfItem(item, ownerInventory, true);
                int price = Mathf.FloorToInt(tradeValue);

                if (item.quantity > 1)
                    return $"{price} scrap each, {price * item.quantity} total";
                return $"{price} scrap";
            }

            // Fallback
            return $"{item.template.price} scrap base";
        }

        private void AnnounceScrapBalance()
        {
            int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
            float barterAdj = Escrow.GetPCBarterSkillAdjustment();
            int barterPct = Mathf.FloorToInt(barterAdj * 100f);

            string announcement = $"{scrap} scrap";
            if (barterPct > 0)
                announcement += $", barter bonus {barterPct} percent";

            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private void AnnounceEscrowSummary()
        {
            string summary = FormatEscrowSummary();
            ScreenReaderManager.SpeakInterrupt(summary);
        }

        private string FormatEscrowSummary()
        {
            float vendorEscrowValue = Escrow.GetTotalVendorEscrowValue();
            float playerEscrowValue = Escrow.GetTotalPlayerEscrowValue();
            int partyCurrency = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;

            int buyingCost = Mathf.CeilToInt(vendorEscrowValue);
            int sellingValue = Mathf.CeilToInt(playerEscrowValue);
            int netCost = buyingCost - sellingValue;

            string summary = $"Transaction summary: Buying {buyingCost} scrap, Selling {sellingValue} scrap";

            if (netCost > 0)
                summary += $", Net cost {netCost} scrap";
            else if (netCost < 0)
                summary += $", Net gain {-netCost} scrap";
            else
                summary += ", Even trade";

            summary += $", Your scrap {partyCurrency}";

            if (netCost > 0 && netCost > partyCurrency)
                summary += ", WARNING not enough scrap";

            return summary;
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

        #region Helpers

        private string GetFilterButtonName(GameObject filterObj)
        {
            // First try TextTooltipCreator — filter buttons use tooltips, not labels
            var tooltip = filterObj.GetComponent<TextTooltipCreator>();
            if (tooltip != null && !string.IsNullOrEmpty(tooltip.text))
            {
                return UITextExtractor.CleanText(tooltip.text);
            }

            // Second, match against the known filter button fields on the container
            var vendorScreen = GetVendorScreen();
            if (vendorScreen != null)
            {
                VND_InventoryContainer activeContainer = vendorScreen.isSelling
                    ? vendorScreen.playerInventoryContainer
                    : vendorScreen.vendorInventoryContainer;

                if (activeContainer != null)
                {
                    if (activeContainer.filterAllButton != null && filterObj == activeContainer.filterAllButton.gameObject)
                        return "All";
                    if (activeContainer.filterWeaponButton != null && filterObj == activeContainer.filterWeaponButton.gameObject)
                        return "Weapons";
                    if (activeContainer.filterArmorButton != null && filterObj == activeContainer.filterArmorButton.gameObject)
                        return "Armor";
                    if (activeContainer.filterAmmoButton != null && filterObj == activeContainer.filterAmmoButton.gameObject)
                        return "Ammo";
                    if (activeContainer.filterConsumablesButton != null && filterObj == activeContainer.filterConsumablesButton.gameObject)
                        return "Consumables";
                    if (activeContainer.filterMiscButton != null && filterObj == activeContainer.filterMiscButton.gameObject)
                        return "Miscellaneous";
                    if (activeContainer.filterJunkButton != null && filterObj == activeContainer.filterJunkButton.gameObject)
                        return "Junk";
                }
            }

            // Last resort: try UILabel child
            UILabel label = filterObj.GetComponentInChildren<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
                return UITextExtractor.CleanText(label.text);

            return "Filter";
        }

        private VendorScreen GetVendorScreen()
        {
            if (cachedVendorScreen != null && cachedVendorScreen.gameObject.activeInHierarchy)
                return cachedVendorScreen;

            cachedVendorScreen = UnityEngine.Object.FindObjectOfType<VendorScreen>();
            return cachedVendorScreen;
        }

        private string GetVendorName(VendorScreen vendorScreen)
        {
            if (vendorScreen.mobNameLabel != null && !string.IsNullOrEmpty(vendorScreen.mobNameLabel.text))
            {
                return UITextExtractor.CleanText(vendorScreen.mobNameLabel.text);
            }
            return "Vendor";
        }

        private PC GetCurrentPC()
        {
            var vendorScreen = GetVendorScreen();
            if (vendorScreen == null) return null;

            // pcSelected is private; try the game's selected PC
            return MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
        }

        private ItemInstance GetCurrentItemInstance()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count) return null;

            object current = currentList[currentIndex];

            if (current is EscrowEntry escrowEntry)
                return escrowEntry.Item;

            if (current is ShopItemEntry shopEntry)
                return shopEntry.Item;

            return null;
        }

        private Inventory GetCurrentOwnerInventory()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count) return null;
            object current = currentList[currentIndex];
            if (current is ShopItemEntry shopEntry) return shopEntry.OwnerInventory;
            if (current is EscrowEntry escrowEntry)
                return escrowEntry.IsSelling ? (Inventory)escrowEntry.ParentEscrow.invEscrowPlayer : escrowEntry.ParentEscrow.invEscrowVendor;
            return null;
        }

        private string GetFilterName(InventoryFilter filter)
        {
            switch (filter)
            {
                case InventoryFilter.All: return "All";
                case InventoryFilter.AllWithJunk: return "All";
                case InventoryFilter.Weapon: return "Weapons";
                case InventoryFilter.Armor: return "Armor";
                case InventoryFilter.Ammo: return "Ammo";
                case InventoryFilter.Consumables: return "Consumables";
                case InventoryFilter.Misc: return "Miscellaneous";
                case InventoryFilter.Junk: return "Junk";
                case InventoryFilter.Favorites: return "Favorites";
                default: return filter.ToString();
            }
        }

        #endregion

        #region Reflection

        private static void CacheReflection()
        {
            reflectionCached = true;

            // VendorScreen.TryGetDestInventory(Inventory, out Inventory, out VND_InventoryContainer) - protected
            tryGetDestInventoryMethod = typeof(VendorScreen).GetMethod("TryGetDestInventory",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (tryGetDestInventoryMethod == null)
                MelonLogger.Warning("[ShopState] Could not find VendorScreen.TryGetDestInventory");

            // VendorScreen.OnSellJunkClicked(GameObject) - public but we cache it anyway
            onSellJunkClickedMethod = typeof(VendorScreen).GetMethod("OnSellJunkClicked",
                BindingFlags.Public | BindingFlags.Instance,
                null, new Type[] { typeof(GameObject) }, null);
            if (onSellJunkClickedMethod == null)
                MelonLogger.Warning("[ShopState] Could not find VendorScreen.OnSellJunkClicked");

            // InventoryContainer.SetFilter(InventoryFilter) - protected
            setFilterMethod = typeof(InventoryContainer).GetMethod("SetFilter",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null, new Type[] { typeof(InventoryFilter) }, null);
            if (setFilterMethod == null)
                MelonLogger.Warning("[ShopState] Could not find InventoryContainer.SetFilter");

            // InventoryContainer.filter - protected field
            inventoryContainerFilterField = typeof(InventoryContainer).GetField("filter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (inventoryContainerFilterField == null)
                MelonLogger.Warning("[ShopState] Could not find InventoryContainer.filter");

            MelonLogger.Msg("[ShopState] Reflection cache complete");
        }

        #endregion
    }
}
