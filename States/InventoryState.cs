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
    /// Full keyboard navigation and screen reader support for inventory screens.
    /// Handles two contexts:
    /// 1. CharacterInfoMenu inventory panel (equipment + backpack)
    /// 2. PopupInventoryMenu (loot containers)
    /// Priority 50 - same level as CharacterState/ConversationState.
    /// </summary>
    public class InventoryState : IAccessibilityState
    {
        public string Name => "Inventory";
        public int Priority => 50;

        /// <summary>
        /// When true, managed navigation is active and patch-based announcements should be suppressed.
        /// </summary>
        public static bool IsManagedNavigation { get; private set; }

        // Navigation zones
        private enum NavigationZone
        {
            Equipment,
            Backpack,
            ContainerItems
        }

        // Current state
        private NavigationZone currentZone;
        private List<object> currentList = new List<object>(); // INV_EquipmentSlot or INV_DragDropItem
        private int currentIndex = -1;
        private bool isDirty = false;

        // Context tracking
        private bool isCharacterInfoMenu = false;
        private bool isPopupInventoryMenu = false;

        // Suspended state — preserved across overlay deactivation (e.g. context menu)
        private NavigationZone suspendedZone;
        private int suspendedIndex = -1;
        private bool hasSuspendedState = false;
        private bool suspendedWasCharacterInfo = false;
        private bool suspendedWasPopupInventory = false;
        private int suspendedPopupInstanceId = 0;

        // Item info browser mode
        private bool isInfoBrowsing = false;
        private List<string> infoLines = new List<string>();
        private int infoLineIndex = -1;

        // Announcement tracking
        private string lastAnnouncedText = null;

        // Detects back-to-back loot windows (old popup destroyed, new created on same/next frame
        // without IsActive flipping false). When the instance id changes we reset announcement
        // state and announce the new container's first item.
        private int trackedPopupInstanceId = 0;

        // Reflection caches
        private static bool reflectionCached = false;
        private static MethodInfo openContextMenuMethod;
        private static FieldInfo charInfoCurrentPCField;
        private static FieldInfo popupInvPcSelectedField;
        private static MethodInfo inventoryContainerSetFilterMethod;

        // Equipment slot order (fixed, learnable)
        private static readonly string[] equipmentSlotFieldNames = new string[]
        {
            "headSlot",
            "torsoSlot",
            "armorSlot",
            "legSlot",
            "trinketSlot1",
            "weaponSlot1",
            "weaponSlot2",
            "ammoSlot1",
            "ammoSlot2",
            "canteenSlot",
            "radSuitSlot",
            "packSlot"
        };

        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

                var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();
                if (!guiManager.IsAnyMenuActive()) return false;

                // Yield to GenericMenuState when an overlay screen (e.g. ItemInfoMenu) is open
                if (guiManager.IsItemInfoScreenOpen()) return false;

                // Yield when ModItemMenu (weapon mod attachment popup) is open
                var modItemMenu = UnityEngine.Object.FindObjectOfType<ModItemMenu>();
                if (modItemMenu != null && modItemMenu.gameObject.activeInHierarchy) return false;

                // Yield when ModalMessageMenu is open (e.g. mod confirmation dialog)
                // This ensures inventory rebuilds after the dialog closes and items are consumed
                var modalMenu = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
                if (modalMenu != null && modalMenu.gameObject.activeInHierarchy) return false;

                // Check for PopupInventoryMenu (loot containers) FIRST — loot takes priority
                // because CharacterInfoMenu can coexist and would shadow the popup check
                var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
                if (popupInv != null && popupInv.gameObject.activeInHierarchy)
                {
                    return true;
                }

                // Check for CharacterInfoMenu with inventory panel active
                var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                if (charInfoMenu != null && charInfoMenu.gameObject.activeInHierarchy)
                {
                    var chaInvPanel = charInfoMenu.GetComponentInChildren<CHA_InventoryPanel>();
                    return chaInvPanel != null && chaInvPanel.gameObject.activeInHierarchy;
                }

                return false;
            }
        }

        public bool HandleInput()
        {
            // Suppress all game input - we handle everything
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

            // Detect back-to-back loot windows: the old PopupInventoryMenu can be destroyed
            // and a new one created without IsActive flipping false (same-frame swap). When
            // the instance id changes, treat it like a fresh activation for this container.
            DetectPopupInstanceChange();

            // Retry if list was empty — the game may populate the loot grid
            // after the popup becomes active (async container setup)
            if (currentList.Count == 0 && currentZone == NavigationZone.ContainerItems)
            {
                BuildContainerItemList();
                if (currentList.Count > 0)
                {
                    MelonLogger.Msg($"[InventoryState] Late grid population detected, found {currentList.Count} items");
                    AnnounceCurrentItem(interrupt: false);
                }
            }

            // Detect context
            DetectContext();

            if (isCharacterInfoMenu)
                return HandleCharacterInfoInput();

            if (isPopupInventoryMenu)
                return HandlePopupInventoryInput();

            return false;
        }

        public void OnActivated()
        {
            IsManagedNavigation = true;
            if (!reflectionCached) CacheReflection();

            lastAnnouncedText = null;
            isDirty = false;
            isInfoBrowsing = false;

            DetectContext();

            // Restore suspended state if returning from an overlay (e.g. context menu)
            // But discard it if the context changed (e.g. was character info, now loot popup)
            if (hasSuspendedState && suspendedWasCharacterInfo != isCharacterInfoMenu)
            {
                MelonLogger.Msg($"[InventoryState] Context changed (charInfo: {suspendedWasCharacterInfo}->{isCharacterInfoMenu}, popup: {suspendedWasPopupInventory}->{isPopupInventoryMenu}), discarding suspended state");
                hasSuspendedState = false;
            }
            if (hasSuspendedState && suspendedWasPopupInventory != isPopupInventoryMenu)
            {
                MelonLogger.Msg($"[InventoryState] Context changed (charInfo: {suspendedWasCharacterInfo}->{isCharacterInfoMenu}, popup: {suspendedWasPopupInventory}->{isPopupInventoryMenu}), discarding suspended state");
                hasSuspendedState = false;
            }
            // A different PopupInventoryMenu instance means the user opened a new loot
            // container (not a context-menu return). The new popup's grid hasn't populated
            // yet, so restoring would announce "Container is empty" before items appear.
            // Discard so the fresh-open path runs and HandleInput's late-grid-population
            // retry announces the first item once it's available.
            if (hasSuspendedState && suspendedWasPopupInventory && isPopupInventoryMenu)
            {
                int currentPopupId = GetPopupInstanceId();
                if (suspendedPopupInstanceId != 0 && currentPopupId != 0 && currentPopupId != suspendedPopupInstanceId)
                {
                    MelonLogger.Msg($"[InventoryState] New popup instance (suspended={suspendedPopupInstanceId}, current={currentPopupId}), discarding suspended state");
                    hasSuspendedState = false;
                }
            }

            if (hasSuspendedState)
            {
                hasSuspendedState = false;
                currentZone = suspendedZone;
                RebuildCurrentList();

                // Clamp index to valid range
                if (suspendedIndex >= 0 && suspendedIndex < currentList.Count)
                    currentIndex = suspendedIndex;
                else if (currentList.Count > 0)
                    currentIndex = Math.Min(suspendedIndex, currentList.Count - 1);
                else
                    currentIndex = -1;

                if (isPopupInventoryMenu)
                    trackedPopupInstanceId = GetPopupInstanceId();

                AnnounceCurrentItem(interrupt: true);
                MelonLogger.Msg($"[InventoryState] Restored from suspend, zone={currentZone}, index={currentIndex}, items={currentList.Count}");
                return;
            }

            if (isCharacterInfoMenu)
            {
                currentZone = NavigationZone.Equipment;
                BuildEquipmentSlotList();
                ScreenReaderManager.SpeakInterrupt("Inventory");
            }
            else if (isPopupInventoryMenu)
            {
                currentZone = NavigationZone.ContainerItems;
                BuildContainerItemList();
                trackedPopupInstanceId = GetPopupInstanceId();
                ScreenReaderManager.SpeakInterrupt("Loot");
            }

            if (currentList.Count > 0 && currentIndex < 0)
                currentIndex = 0;

            // Announce the first item after the zone header so users don't need to press
            // an arrow to hear what's in the container / inventory.
            if (currentList.Count > 0 && currentIndex >= 0)
                AnnounceCurrentItem(interrupt: false);

            MelonLogger.Msg($"[InventoryState] Activated, zone={currentZone}, items={currentList.Count}");
        }

        public void OnDeactivated()
        {
            // Suspend state so we can restore on reactivation (e.g. after context menu closes)
            suspendedZone = currentZone;
            suspendedIndex = currentIndex;
            hasSuspendedState = true;
            suspendedWasCharacterInfo = isCharacterInfoMenu;
            suspendedWasPopupInventory = isPopupInventoryMenu;
            suspendedPopupInstanceId = trackedPopupInstanceId;

            IsManagedNavigation = false;
            lastAnnouncedText = null;
            trackedPopupInstanceId = 0;
            currentList.Clear();
            isDirty = false;
            isInfoBrowsing = false;
            MelonLogger.Msg($"[InventoryState] Deactivated (suspended zone={suspendedZone}, index={suspendedIndex})");
        }

        #region Context Detection

        private void DetectContext()
        {
            isCharacterInfoMenu = false;
            isPopupInventoryMenu = false;

            // Check PopupInventoryMenu first — loot takes priority over CharacterInfoMenu
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv != null && popupInv.gameObject.activeInHierarchy)
            {
                isPopupInventoryMenu = true;
                return;
            }

            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu != null && charInfoMenu.gameObject.activeInHierarchy)
            {
                var chaInvPanel = charInfoMenu.GetComponentInChildren<CHA_InventoryPanel>();
                if (chaInvPanel != null && chaInvPanel.gameObject.activeInHierarchy)
                {
                    isCharacterInfoMenu = true;
                }
            }
        }

        #endregion

        #region CharacterInfoMenu Input

        private bool HandleCharacterInfoInput()
        {
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
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                SwitchZone();
                return true;
            }

            // Enter - open context menu
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OpenContextMenuOnCurrentItem();
                return true;
            }

            // E - quick equip/unequip
            if (Input.GetKeyDown(KeyCode.E))
            {
                QuickEquipUnequip();
                return true;
            }

            // Tab - detailed item info
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                AnnounceDetailedInfo();
                return true;
            }

            // R - read description
            if (Input.GetKeyDown(KeyCode.R))
            {
                AnnounceDescription();
                return true;
            }

            // F - cycle filter
            if (Input.GetKeyDown(KeyCode.F))
            {
                CycleFilter();
                return true;
            }

            // I - open item info browser
            if (Input.GetKeyDown(KeyCode.I))
            {
                OpenInfoBrowser();
                return true;
            }

            // C - read inventory context summary
            if (Input.GetKeyDown(KeyCode.C))
            {
                AnnounceInventoryContext();
                return true;
            }

            // F1-F7 - switch party member
            if (HandlePartySwitch())
                return true;

            // PageUp/PageDown - switch CharacterInfoMenu tabs
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                SwitchCharacterInfoTab(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                SwitchCharacterInfoTab(1);
                return true;
            }

            // Escape - close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInventory();
                return true;
            }

            return false;
        }

        #endregion

        #region PopupInventoryMenu Input

        private bool HandlePopupInventoryInput()
        {
            // Up/Down - navigate items
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

            // Left/Right - switch container
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SwitchContainer(-1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                SwitchContainer(1);
                return true;
            }

            // Enter - transfer item (loot screen uses transfer, not context menu)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TransferCurrentItem();
                return true;
            }

            // Tab - detailed info
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                AnnounceDetailedInfo();
                return true;
            }

            // R - read description
            if (Input.GetKeyDown(KeyCode.R))
            {
                AnnounceDescription();
                return true;
            }

            // F - cycle filter
            if (Input.GetKeyDown(KeyCode.F))
            {
                CycleFilter();
                return true;
            }

            // I - open item info browser
            if (Input.GetKeyDown(KeyCode.I))
            {
                OpenInfoBrowser();
                return true;
            }

            // C - read loot context summary
            if (Input.GetKeyDown(KeyCode.C))
            {
                AnnounceLootContext();
                return true;
            }

            // F1-F7 - switch destination party member
            if (HandlePartySwitch())
                return true;

            // T - take all
            if (Input.GetKeyDown(KeyCode.T))
            {
                TakeAll();
                return true;
            }

            // G - distribute all
            if (Input.GetKeyDown(KeyCode.G))
            {
                DistributeAll();
                return true;
            }

            // Escape - close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseLoot();
                return true;
            }

            return false;
        }

        #endregion

        #region List Building

        private void BuildEquipmentSlotList()
        {
            currentList.Clear();
            currentZone = NavigationZone.Equipment;

            var mainPanel = GetINV_MainPanel();
            if (mainPanel == null)
            {
                MelonLogger.Msg("[InventoryState] Could not find INV_MainPanel");
                return;
            }

            foreach (string fieldName in equipmentSlotFieldNames)
            {
                var field = typeof(INV_MainPanel).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var slot = field.GetValue(mainPanel) as INV_EquipmentSlot;
                    if (slot != null && slot.gameObject.activeInHierarchy)
                    {
                        currentList.Add(slot);
                    }
                }
            }

            if (currentIndex >= currentList.Count)
                currentIndex = currentList.Count > 0 ? currentList.Count - 1 : -1;
            if (currentIndex < 0 && currentList.Count > 0)
                currentIndex = 0;

            MelonLogger.Msg($"[InventoryState] Built equipment list: {currentList.Count} slots");
        }

        private void BuildBackpackItemList()
        {
            currentList.Clear();
            currentZone = NavigationZone.Backpack;

            var mainPanel = GetINV_MainPanel();
            if (mainPanel == null) return;

            InventoryGrid invGrid = mainPanel.inventoryGrid as InventoryGrid;
            if (invGrid != null)
            {
                // Use GetPositionSortedList for consistent ordering
                List<Transform> sorted = invGrid.GetPositionSortedList();
                foreach (Transform t in sorted)
                {
                    if (t == null || !t.gameObject.activeSelf) continue;
                    var item = t.GetComponent<INV_DragDropItem>();
                    if (item != null && item.GetItem() != null)
                    {
                        currentList.Add(item);
                    }
                }
            }
            else
            {
                // Fallback: iterate grid children
                var grid = mainPanel.inventoryGrid;
                if (grid != null)
                {
                    for (int i = 0; i < grid.transform.childCount; i++)
                    {
                        Transform child = grid.transform.GetChild(i);
                        if (child == null || !child.gameObject.activeSelf) continue;
                        var item = child.GetComponent<INV_DragDropItem>();
                        if (item != null && item.GetItem() != null)
                        {
                            currentList.Add(item);
                        }
                    }
                }
            }

            if (currentIndex >= currentList.Count)
                currentIndex = currentList.Count > 0 ? currentList.Count - 1 : -1;
            if (currentIndex < 0 && currentList.Count > 0)
                currentIndex = 0;

            MelonLogger.Msg($"[InventoryState] Built backpack list: {currentList.Count} items");
        }

        private void BuildContainerItemList()
        {
            currentList.Clear();
            currentZone = NavigationZone.ContainerItems;

            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            // Get items from inventoryContainer.table
            var container = popupInv.inventoryContainer;
            if (container == null || container.table == null) return;

            var table = container.table;
            List<Transform> sorted = table.GetSortedList();
            foreach (Transform t in sorted)
            {
                if (t == null || !t.gameObject.activeSelf) continue;
                var item = t.GetComponent<INV_DragDropItem>();
                if (item != null && item.GetItem() != null)
                {
                    currentList.Add(item);
                }
            }

            if (currentIndex >= currentList.Count)
                currentIndex = currentList.Count > 0 ? currentList.Count - 1 : -1;
            if (currentIndex < 0 && currentList.Count > 0)
                currentIndex = 0;

            MelonLogger.Msg($"[InventoryState] Built container item list: {currentList.Count} items");
        }

        private void RebuildCurrentList()
        {
            int previousIndex = currentIndex;
            switch (currentZone)
            {
                case NavigationZone.Equipment:
                    BuildEquipmentSlotList();
                    break;
                case NavigationZone.Backpack:
                    BuildBackpackItemList();
                    break;
                case NavigationZone.ContainerItems:
                    BuildContainerItemList();
                    break;
            }

            // Try to preserve index position
            if (previousIndex >= 0 && previousIndex < currentList.Count)
                currentIndex = previousIndex;
            else if (currentList.Count > 0)
                currentIndex = Math.Min(previousIndex, currentList.Count - 1);
            else
                currentIndex = -1;
        }

        #endregion

        #region Navigation

        private void NavigateList(int direction)
        {
            // If the list is empty but the loot grid hasn't populated yet, try to rebuild
            // before giving up — keeps us from announcing "Container is empty" in the brief
            // window between popup open and item population.
            if (currentList.Count == 0 && currentZone == NavigationZone.ContainerItems)
            {
                BuildContainerItemList();
                if (currentList.Count > 0 && currentIndex < 0)
                    currentIndex = 0;
            }

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
            // User pressed an arrow — always speak, even if the new item matches the
            // previous announcement (e.g. wrapping in a 1-item list).
            lastAnnouncedText = null;
            AnnounceCurrentItem(interrupt: true);
        }

        private void SwitchZone()
        {
            if (!isCharacterInfoMenu) return;

            if (currentZone == NavigationZone.Equipment)
            {
                BuildBackpackItemList();
                string announcement = $"Backpack, {currentList.Count} items";
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            else
            {
                BuildEquipmentSlotList();
                ScreenReaderManager.SpeakInterrupt("Equipment slots");
            }

            if (currentList.Count > 0 && currentIndex >= 0)
            {
                // Queue the first item announcement after the zone announcement
                AnnounceCurrentItem(interrupt: false);
            }
        }

        private void SwitchContainer(int direction)
        {
            if (!isPopupInventoryMenu) return;

            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            var containerButtons = popupInv.containerButtons;
            if (containerButtons == null || containerButtons.Count <= 1)
            {
                ScreenReaderManager.SpeakInterrupt("Only one container");
                return;
            }

            // Find current container index
            int currentContainerIdx = -1;
            for (int i = 0; i < containerButtons.Count; i++)
            {
                if (containerButtons[i] == popupInv.sourceContainerButton)
                {
                    currentContainerIdx = i;
                    break;
                }
            }

            int newIdx = currentContainerIdx + direction;
            if (newIdx < 0) newIdx = containerButtons.Count - 1;
            else if (newIdx >= containerButtons.Count) newIdx = 0;

            popupInv.SelectContainer(containerButtons[newIdx]);

            // Rebuild item list for new container
            isDirty = true;

            string containerName = GetContainerName();
            ScreenReaderManager.SpeakInterrupt($"Container: {containerName}");
        }

        #endregion

        #region Actions

        private void OpenContextMenuOnCurrentItem()
        {
            var dragDropItem = GetCurrentDragDropItem();
            if (dragDropItem == null)
            {
                ScreenReaderManager.SpeakInterrupt("No item selected");
                return;
            }

            if (openContextMenuMethod != null)
            {
                try
                {
                    openContextMenuMethod.Invoke(dragDropItem, null);
                    MelonLogger.Msg("[InventoryState] Opened context menu");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"[InventoryState] Failed to open context menu: {e.Message}");
                    ScreenReaderManager.SpeakInterrupt("Could not open context menu");
                }
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Context menu not available");
            }
        }

        private void QuickEquipUnequip()
        {
            if (!isCharacterInfoMenu) return;

            if (currentZone == NavigationZone.Backpack)
            {
                var item = GetCurrentDragDropItem();
                if (item == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No item selected");
                    return;
                }
                item.AttemptToEquip(false);
                isDirty = true;
                MelonLogger.Msg("[InventoryState] Attempted to equip item");
            }
            else if (currentZone == NavigationZone.Equipment)
            {
                if (currentIndex < 0 || currentIndex >= currentList.Count) return;
                var slot = currentList[currentIndex] as INV_EquipmentSlot;
                if (slot == null) return;

                var equipped = slot.GetCurrentItem(false);
                if (equipped != null)
                {
                    equipped.AttemptToUnequip();
                    isDirty = true;
                    MelonLogger.Msg("[InventoryState] Attempted to unequip item");
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Slot is empty");
                }
            }
        }

        private void TransferCurrentItem()
        {
            var dragDropItem = GetCurrentDragDropItem();
            if (dragDropItem == null)
            {
                ScreenReaderManager.SpeakInterrupt("No item selected");
                return;
            }

            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            // Use OnItemDoubleClicked which is the game's own transfer mechanism
            popupInv.OnItemDoubleClicked(dragDropItem.gameObject);
            isDirty = true;

            ItemInstance item = dragDropItem.GetItem();
            string itemName = item != null
                ? UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty))
                : "Item";
            ScreenReaderManager.SpeakInterrupt($"Transferred {itemName}");
            MelonLogger.Msg($"[InventoryState] Transferred item: {itemName}");
        }

        private void TakeAll()
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            popupInv.OnTakeAllClicked();
            isDirty = true;
            ScreenReaderManager.SpeakInterrupt("Taking all items");
        }

        private void DistributeAll()
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            popupInv.OnDistributeAllClicked();
            isDirty = true;
            ScreenReaderManager.SpeakInterrupt("Distributing all items");
        }

        private void CloseInventory()
        {
            hasSuspendedState = false; // Full close, don't restore
            // Prevent the "Back" event from bleeding into the next frame and opening the pause menu
            EventManager.ignoreNextBack = true;
            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu != null)
            {
                charInfoMenu.Close();
                MelonLogger.Msg("[InventoryState] Closed character info menu");
            }
        }

        private void CloseLoot()
        {
            hasSuspendedState = false; // Full close, don't restore
            // Prevent the "Back" event from bleeding into the next frame and opening the pause menu
            EventManager.ignoreNextBack = true;
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv != null)
            {
                popupInv.Close();
                MelonLogger.Msg("[InventoryState] Closed loot menu");
            }
        }

        private void SwitchCharacterInfoTab(int direction)
        {
            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu == null) return;

            if (direction > 0)
                charInfoMenu.GoToNextPanel();
            else
                charInfoMenu.GoToPreviousPanel();

            MelonLogger.Msg($"[InventoryState] Switched tab, direction={direction}");
        }

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
            if (isCharacterInfoMenu)
            {
                // CharacterInfoMenu uses INV_PartyList for party buttons, or direct party access
                SwitchPartyViaGameAPI(index);
            }
            else if (isPopupInventoryMenu)
            {
                // PopupInventoryMenu has its own pcContainerButtons
                SwitchPopupPartyMember(index);
            }
        }

        private void SwitchPartyViaGameAPI(int index)
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || party.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No party members available");
                return;
            }

            if (index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt($"No party member at position {index + 1}, {party.Count} available");
                return;
            }

            PC pc = party[index];
            if (pc == null) return;

            // Use the same mechanism as INV_PartyList.OnPCContainerClicked
            MonoBehaviourSingleton<InputManager>.GetInstance().ClearSelection();
            pc.MakeLeader();
            MonoBehaviourSingleton<InputManager>.GetInstance().AddToSelection(pc);

            isDirty = true;

            string pcName = UITextExtractor.CleanText(
                Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt($"Selected {pcName}");
            MelonLogger.Msg($"[InventoryState] Switched to party member {index + 1}: {pcName}");
        }

        private void SwitchPopupPartyMember(int index)
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return;

            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;
            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || party.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No party members available");
                return;
            }

            if (index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt($"No party member at position {index + 1}, {party.Count} available");
                return;
            }

            // Use the game's own OnButtonDown handler which properly updates pcSelected
            // via EventInfo_CharacterSelectionChanged -> OnPlayerSelected
            popupInv.OnButtonDown($"Select Player {index + 1}");
            isDirty = true;

            PC pc = party[index];
            string pcName = pc != null && pc.pcTemplate != null
                ? UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty))
                : $"Party member {index + 1}";
            ScreenReaderManager.SpeakInterrupt($"Selected {pcName}");
            MelonLogger.Msg($"[InventoryState] Popup party switch to {index + 1}: {pcName}");
        }

        private void CycleFilter()
        {
            InventoryContainer container = GetActiveInventoryContainer();
            if (container == null)
            {
                ScreenReaderManager.SpeakInterrupt("No filter available");
                return;
            }

            // Get current filter
            InventoryFilter currentFilter = container.GetFilter();

            // Cycle through useful filters
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

            // Use reflection to call SetFilter (protected)
            if (inventoryContainerSetFilterMethod != null)
            {
                try
                {
                    inventoryContainerSetFilterMethod.Invoke(container, new object[] { newFilter });
                    isDirty = true;

                    string filterName = GetFilterName(newFilter);
                    ScreenReaderManager.SpeakInterrupt($"Filter: {filterName}");
                    MelonLogger.Msg($"[InventoryState] Set filter to {newFilter}");
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"[InventoryState] Failed to set filter: {e.Message}");
                }
            }
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
            BuildInfoLines(item, GetCurrentPC());

            if (infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No information available");
                return;
            }

            isInfoBrowsing = true;
            infoLineIndex = 0;
            ScreenReaderManager.SpeakInterrupt($"Item info: {infoLines[0]}, {infoLines.Count} lines, use up and down to browse, escape to close");
        }

        private void BuildInfoLines(ItemInstance item, PC pc)
        {
            // Name
            string name = UITextExtractor.CleanText(Language.Localize(item.template.displayName, false, false, string.Empty));
            infoLines.Add($"Name: {name}");

            // Type — for weapons use the skill name ("Assault Rifle"), otherwise fall back to GetTypeString
            string typeStr = null;
            if (item is ItemInstance_Weapon)
                typeStr = InventoryFormatting.GetWeaponSkillDisplayName(item.template as ItemTemplate_Weapon);
            if (string.IsNullOrEmpty(typeStr))
                typeStr = item.template.GetTypeString();
            if (!string.IsNullOrEmpty(typeStr))
            {
                infoLines.Add($"Type: {typeStr}");
            }

            // Quantity
            if (item.quantity > 1)
            {
                infoLines.Add($"Quantity: {item.quantity}");
            }

            // Equipment slot if equipped
            if (currentZone == NavigationZone.Equipment && currentIndex >= 0 && currentIndex < currentList.Count)
            {
                var slot = currentList[currentIndex] as INV_EquipmentSlot;
                if (slot != null)
                {
                    infoLines.Add($"Equipped in: {GetSlotName(slot.equipmentSlot)}");
                }
            }

            // Weapon stats — use instance getters so weapon-mod bonuses are reflected
            if (item is ItemInstance_Weapon weapon && weapon.template is ItemTemplate_Weapon wt)
            {
                int minDmg = weapon.GetMinDamage();
                int maxDmg = weapon.GetMaxDamage();
                if (minDmg > 0 || maxDmg > 0)
                {
                    infoLines.Add(minDmg == maxDmg
                        ? $"Damage: {maxDmg}"
                        : $"Damage: {minDmg} to {maxDmg}");
                }

                // Ranged-specific: loaded ammo, reserve ammo, caliber, range brackets, firing modes
                if (weapon is ItemInstance_WeaponRanged rangedInst && wt is ItemTemplate_WeaponRanged)
                {
                    int clip = rangedInst.GetClipSize();
                    if (clip > 0)
                        infoLines.Add($"Ammo loaded: {rangedInst.GetAmmoCount()} of {clip}");

                    string reserveLine = InventoryFormatting.BuildReserveAmmoLine(rangedInst, pc);
                    if (!string.IsNullOrEmpty(reserveLine))
                        infoLines.Add(reserveLine);

                    string caliber = InventoryFormatting.GetWeaponCaliberDisplay(wt);
                    if (!string.IsNullOrEmpty(caliber))
                        infoLines.Add($"Uses: {caliber}");

                    foreach (string line in InventoryFormatting.BuildRangeBracketLines(rangedInst, pc))
                        infoLines.Add(line);

                    foreach (string line in InventoryFormatting.BuildFiringModeLines(rangedInst))
                        infoLines.Add(line);
                }
                else
                {
                    // Melee / thrown / RPG: raw max range only
                    int range = weapon.GetAttackRange();
                    if (range > 0)
                        infoLines.Add($"Range: {range}");
                }

                // PC-aware accuracy & crit (matches the on-screen ItemInfoBox values)
                int acc = InventoryFormatting.ComputeAccuracyPercent(weapon, pc);
                if (acc >= 0)
                    infoLines.Add($"Accuracy: {acc} percent");
                int crit = InventoryFormatting.ComputeCritChancePercent(weapon, pc);
                if (crit >= 0)
                    infoLines.Add($"Critical chance: {crit} percent");

                // Operational stats (AP to attack, AP to reload, chance to jam, jammed flag)
                foreach (string line in InventoryFormatting.BuildWeaponOperationalLines(weapon))
                    infoLines.Add(line);

                if (wt.armorPenetration > 0)
                    infoLines.Add($"Armor penetration: {wt.armorPenetration}");

                // Status-effect afflictor
                string afflictor = InventoryFormatting.BuildAfflictorLine(weapon);
                if (!string.IsNullOrEmpty(afflictor))
                    infoLines.Add(afflictor);

                // Energy-weapon notes + above/below threshold multipliers
                foreach (string line in InventoryFormatting.BuildEnergyWeaponLines(wt))
                    infoLines.Add(line);

                // Mod slots (installed mods + empty slots)
                foreach (string line in InventoryFormatting.BuildModSlotLines(weapon))
                    infoLines.Add(line);
            }

            // Armor stats
            if (item is ItemInstance_Armor armor && armor.template is ItemTemplate_Equipment eqt)
            {
                int armorValue = eqt.GetStat("armor");
                if (armorValue > 0)
                    infoLines.Add($"Armor value: {armorValue}");

                // Mod slots on armor (if any)
                foreach (string line in InventoryFormatting.BuildModSlotLines(armor))
                    infoLines.Add(line);
            }

            // Ammo stats
            if (item is ItemInstance_Ammo ammo && ammo.template is ItemTemplate_Ammo at)
            {
                string caliberDisplay = at.GetCaliberDisplayString();
                if (!string.IsNullOrEmpty(caliberDisplay))
                    infoLines.Add($"Caliber: {UITextExtractor.CleanText(caliberDisplay)}");
                if (at.damageMultiplier != 1f && at.damageMultiplier > 0)
                {
                    int dmgPct = UnityEngine.Mathf.RoundToInt((at.damageMultiplier - 1f) * 100f);
                    if (dmgPct != 0)
                        infoLines.Add($"Damage modifier: {dmgPct:+0;-#} percent");
                }
                if (at.expansionMultiplier != 1f && at.expansionMultiplier > 0)
                {
                    int expPct = UnityEngine.Mathf.RoundToInt((at.expansionMultiplier - 1f) * 100f);
                    if (expPct != 0)
                        infoLines.Add($"Expansion: {expPct:+0;-#} percent");
                }
                if (at.penetration > 0)
                    infoLines.Add($"Penetration: {at.penetration}");
                if (at.chanceToReduceArmor > 0)
                    infoLines.Add($"Armor reduction: {at.chanceToReduceArmor} percent chance, reduces by {at.armorReduction}");
            }

            // Usable/consumable stats
            if (item is ItemInstance_Usable usable && usable.template is ItemTemplate_Usable ut)
            {
                if (ut.actionPoints > 0)
                    infoLines.Add($"AP cost: {ut.actionPoints}");

                // All skill-driven effect types, with PC-skill-adjusted heal values
                foreach (string line in InventoryFormatting.BuildConsumableEffectLines(usable, pc))
                    infoLines.Add($"Effect: {line}");

                // Skill-book / XP-giver items (e.g. "+1 Surgeon skill")
                if (ut is ItemTemplate_UsableXPGiver xpGiver)
                {
                    string xpLine = InventoryFormatting.BuildXPGiverLine(xpGiver);
                    if (!string.IsNullOrEmpty(xpLine))
                        infoLines.Add($"Grants: {xpLine}");
                }

                if (ut.aoeRadius > 0)
                    infoLines.Add($"Area of effect: {ut.aoeRadius}");
                if (ut.isConsumedOnUse)
                    infoLines.Add("Consumed on use");
            }

            // Trinket info
            if (item is ItemInstance_Trinket)
            {
                infoLines.Add("Trinket");
            }

            // Weapon mod info (slot, stat bonuses, requirements, allowed weapons)
            if (item is ItemInstance_Mod modItem)
            {
                foreach (string line in InventoryFormatting.BuildWeaponModLines(modItem, pc))
                    infoLines.Add(line);
            }

            // Junk
            if (item.template.junk)
            {
                infoLines.Add("Junk item");
            }

            // New item flag
            if (item.isNew)
            {
                infoLines.Add("New item");
            }

            // Equipment stat modifiers (e.g. +3 Strength)
            foreach (string mod in InventoryFormatting.BuildModifierLines(item))
                infoLines.Add($"Modifier: {mod}");

            // Attribute requirements (annotated with met/not met for the current PC)
            foreach (string req in InventoryFormatting.BuildRequirementLines(item, pc))
                infoLines.Add(req);

            // Trait-specific item modifiers (e.g. Psychopath, Mysterious Stranger)
            foreach (string traitLine in InventoryFormatting.BuildTraitItemModifierLines(item, pc))
                infoLines.Add(traitLine);

            // Weight
            float weight = item.GetWeight();
            if (weight > 0)
            {
                if (item.quantity > 1)
                    infoLines.Add($"Weight: {weight:0.0} lbs each, {weight * item.quantity:0.0} lbs total");
                else
                    infoLines.Add($"Weight: {weight:0.0} lbs");
            }

            // Value — vendor-aware (applies barter adjustment when a vendor screen is open)
            int itemValue = InventoryFormatting.ComputeItemValue(item);
            if (itemValue > 0)
            {
                if (item.quantity > 1)
                {
                    int perUnit = Mathf.Max(1, itemValue / Mathf.Max(1, item.quantity));
                    infoLines.Add($"Value: ${perUnit} each, ${itemValue} total");
                }
                else
                {
                    infoLines.Add($"Value: ${itemValue}");
                }
            }

            // Tier
            if (item.template.tier > 0)
            {
                infoLines.Add($"Tier: {item.template.tier}");
            }

            // Description (can be multiline — split into separate lines)
            if (!string.IsNullOrEmpty(item.template.description))
            {
                string desc = UITextExtractor.CleanText(
                    Language.Localize(item.template.description, false, false, string.Empty));
                if (!string.IsNullOrEmpty(desc))
                {
                    infoLines.Add($"Description: {desc}");
                }
            }

            // Bonus string from ItemInfoBox — try to read from the actual UI
            try
            {
                var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                ItemInfoBox infoBox = null;
                if (charInfoMenu != null)
                {
                    // The CharacterInfoMenu's inventoryPanel has an ItemInfoBox
                    infoBox = charInfoMenu.GetComponentInChildren<ItemInfoBox>();
                }
                if (infoBox == null)
                {
                    var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
                    if (popupInv != null)
                    {
                        infoBox = popupInv.itemInfoBox ?? popupInv.descriptionPanel;
                    }
                }

                if (infoBox != null)
                {
                    // Read weapon-specific labels if visible
                    if (infoBox.damageLabel != null && !string.IsNullOrEmpty(infoBox.damageLabel.text)
                        && infoBox.damageLabel.gameObject.activeInHierarchy)
                    {
                        string dmg = UITextExtractor.CleanText(infoBox.damageLabel.text);
                        if (!string.IsNullOrEmpty(dmg))
                            infoLines.Add($"Damage display: {dmg}");
                    }
                    if (infoBox.rangeLabel != null && !string.IsNullOrEmpty(infoBox.rangeLabel.text)
                        && infoBox.rangeLabel.gameObject.activeInHierarchy)
                    {
                        string rng = UITextExtractor.CleanText(infoBox.rangeLabel.text);
                        if (!string.IsNullOrEmpty(rng))
                            infoLines.Add($"Range display: {rng}");
                    }
                    if (infoBox.accuracyLabel != null && !string.IsNullOrEmpty(infoBox.accuracyLabel.text)
                        && infoBox.accuracyLabel.gameObject.activeInHierarchy)
                    {
                        string acc = UITextExtractor.CleanText(infoBox.accuracyLabel.text);
                        if (!string.IsNullOrEmpty(acc))
                            infoLines.Add($"Accuracy: {acc}");
                    }
                    if (infoBox.armorLabel != null && !string.IsNullOrEmpty(infoBox.armorLabel.text)
                        && infoBox.armorLabel.gameObject.activeInHierarchy)
                    {
                        string arm = UITextExtractor.CleanText(infoBox.armorLabel.text);
                        if (!string.IsNullOrEmpty(arm))
                            infoLines.Add($"Armor display: {arm}");
                    }
                    if (infoBox.penetrationLabel != null && !string.IsNullOrEmpty(infoBox.penetrationLabel.text)
                        && infoBox.penetrationLabel.gameObject.activeInHierarchy)
                    {
                        string pen = UITextExtractor.CleanText(infoBox.penetrationLabel.text);
                        if (!string.IsNullOrEmpty(pen))
                            infoLines.Add($"Penetration display: {pen}");
                    }
                    if (infoBox.ammoLabel != null && !string.IsNullOrEmpty(infoBox.ammoLabel.text)
                        && infoBox.ammoLabel.gameObject.activeInHierarchy)
                    {
                        string ammoText = UITextExtractor.CleanText(infoBox.ammoLabel.text);
                        if (!string.IsNullOrEmpty(ammoText))
                            infoLines.Add($"Ammo: {ammoText}");
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[InventoryState] Error reading ItemInfoBox labels: {e.Message}");
            }

            // Comparison vs equipped item (suppressed when the focused item is itself equipped)
            if (currentZone != NavigationZone.Equipment)
            {
                AppendComparisonLines(item, pc);
            }
        }

        // Appends a "Compared to equipped X" section with stat deltas for weapons and
        // armor/wearables. Silently skips when no comparable equipped item exists.
        private void AppendComparisonLines(ItemInstance focused, PC pc)
        {
            bool isWeapon = focused is ItemInstance_Weapon;
            bool isArmorOrWearable = focused is ItemInstance_Armor || focused is ItemInstance_Wearable;
            if (!isWeapon && !isArmorOrWearable) return;

            ItemInstance equipped = GetEquippedComparisonItem(focused, pc);
            if (equipped == null) return;

            string equippedName = UITextExtractor.CleanText(
                Language.Localize(equipped.template.displayName, false, false, string.Empty));

            int headerIndex = infoLines.Count;
            infoLines.Add($"Compared to equipped {equippedName}:");

            if (isWeapon && equipped is ItemInstance_Weapon ew && focused is ItemInstance_Weapon fw)
            {
                AppendIntDiff("minimum damage", fw.GetMinDamage() - ew.GetMinDamage());
                AppendIntDiff("maximum damage", fw.GetMaxDamage() - ew.GetMaxDamage());
                AppendIntDiff("attack range", fw.GetAttackRange() - ew.GetAttackRange());

                int fAcc = InventoryFormatting.ComputeAccuracyPercent(fw, pc);
                int eAcc = InventoryFormatting.ComputeAccuracyPercent(ew, pc);
                if (fAcc >= 0 && eAcc >= 0)
                    AppendIntDiff("accuracy", fAcc - eAcc, " percent");

                int fCrit = InventoryFormatting.ComputeCritChancePercent(fw, pc);
                int eCrit = InventoryFormatting.ComputeCritChancePercent(ew, pc);
                if (fCrit >= 0 && eCrit >= 0)
                    AppendIntDiff("critical chance", fCrit - eCrit, " percent");

                var fwt = fw.template as ItemTemplate_Weapon;
                var ewt = ew.template as ItemTemplate_Weapon;
                if (fwt != null && ewt != null)
                    AppendIntDiff("armor penetration", fwt.armorPenetration - ewt.armorPenetration);

                AppendWeightDiff(focused.GetWeight() - equipped.GetWeight());
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
                    string line = InventoryFormatting.FormatStatModifier(key, diff);
                    if (!string.IsNullOrEmpty(line))
                        infoLines.Add(line);
                }

                AppendWeightDiff(focused.GetWeight() - equipped.GetWeight());
            }

            // No diffs emitted — replace the header with a clearer "identical" line.
            if (infoLines.Count == headerIndex + 1)
            {
                infoLines[headerIndex] = $"Identical stats to equipped {equippedName}";
            }
        }

        private void AppendIntDiff(string label, int diff, string units = "")
        {
            if (diff == 0) return;
            string sign = diff > 0 ? "+" : "";
            infoLines.Add($"{sign}{diff}{units} {label}");
        }

        private void AppendWeightDiff(float diff)
        {
            if (UnityEngine.Mathf.Abs(diff) < 0.05f) return;
            string sign = diff > 0 ? "+" : "";
            infoLines.Add($"{sign}{diff:0.0} pounds weight");
        }

        // Returns the equipped ItemInstance to compare the focused item against:
        //   - Weapons → INV_MainPanel.weaponSlot1 (always slot 1, never slot 2)
        //   - Armor/Wearable → equipped item in the slot that matches the focused
        //     item's ItemTemplate_Equipment.slot
        // Falls back to iterating pc.equipment when INV_MainPanel isn't present
        // (e.g. PopupInventoryMenu loot context). Returns null when nothing equipped.
        private ItemInstance GetEquippedComparisonItem(ItemInstance focused, PC pc)
        {
            if (focused == null || pc == null) return null;

            if (focused is ItemInstance_Weapon)
            {
                var mainPanel = GetINV_MainPanel();
                if (mainPanel != null && mainPanel.weaponSlot1 != null)
                {
                    var dragDrop = mainPanel.weaponSlot1.GetCurrentItem(false);
                    var item = dragDrop != null ? dragDrop.GetItem() : null;
                    if (item != null) return item;
                }

                // Fallback for loot-popup context: match by slot 1 weapon template.
                var slot1Tpl = pc.pcStats != null ? pc.pcStats.GetWeaponTemplate(false) : null;
                var equipment = pc.inventory != null ? pc.inventory.equipment : null;
                if (slot1Tpl != null && equipment != null)
                {
                    foreach (ItemInstance i in equipment)
                    {
                        if (i is ItemInstance_Weapon && i.template == slot1Tpl)
                            return i;
                    }
                }
                return null;
            }

            if (focused is ItemInstance_Armor || focused is ItemInstance_Wearable)
            {
                var focusedTpl = focused.template as ItemTemplate_Equipment;
                if (focusedTpl == null) return null;
                EquipmentSlot targetSlot = focusedTpl.slot;

                var mainPanel = GetINV_MainPanel();
                if (mainPanel != null)
                {
                    foreach (string fieldName in equipmentSlotFieldNames)
                    {
                        var field = typeof(INV_MainPanel).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                        if (field == null) continue;
                        var slot = field.GetValue(mainPanel) as INV_EquipmentSlot;
                        if (slot == null || slot.equipmentSlot != targetSlot) continue;
                        var dragDrop = slot.GetCurrentItem(false);
                        var item = dragDrop != null ? dragDrop.GetItem() : null;
                        if (item != null) return item;
                        break;
                    }
                }

                var equipment = pc.inventory != null ? pc.inventory.equipment : null;
                if (equipment != null)
                {
                    foreach (ItemInstance i in equipment)
                    {
                        if (i.template is ItemTemplate_Equipment t && t.slot == targetSlot)
                            return i;
                    }
                }
            }

            return null;
        }

        private ItemInstance GetCurrentItemInstance()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count) return null;

            object current = currentList[currentIndex];

            if (current is INV_EquipmentSlot slot)
            {
                var equipped = slot.GetCurrentItem(false);
                return equipped?.GetItem();
            }

            if (current is INV_DragDropItem dragDropItem)
            {
                return dragDropItem.GetItem();
            }

            return null;
        }

        #endregion

        #region Announcements

        private void AnnounceInventoryContext()
        {
            var pc = GetCurrentPC();
            string pcName = pc != null && pc.pcTemplate != null
                ? UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty))
                : "Unknown";

            string zoneName = currentZone == NavigationZone.Equipment ? "Equipment" : "Backpack";
            string info = $"{pcName} inventory, {zoneName} zone, {currentList.Count} items";

            // Add current filter
            var container = GetActiveInventoryContainer();
            if (container != null)
            {
                string filterName = GetFilterName(container.GetFilter());
                info += $", filter: {filterName}";
            }

            ScreenReaderManager.SpeakInterrupt(info);
        }

        private void AnnounceLootContext()
        {
            string containerName = GetContainerName();

            var pc = GetCurrentPC();
            string pcName = pc != null && pc.pcTemplate != null
                ? UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty))
                : "Unknown";

            string info = $"Loot: {containerName}, {currentList.Count} items, destination: {pcName}";
            ScreenReaderManager.SpeakInterrupt(info);
        }

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

        private void AnnounceDetailedInfo()
        {
            if (currentZone == NavigationZone.Equipment)
            {
                if (currentIndex < 0 || currentIndex >= currentList.Count) return;
                var slot = currentList[currentIndex] as INV_EquipmentSlot;
                if (slot == null) return;

                var equipped = slot.GetCurrentItem(false);
                if (equipped != null)
                {
                    ItemInstance item = equipped.GetItem();
                    if (item != null)
                    {
                        string details = InventoryFormatting.FormatDetailedItemInfo(item, GetCurrentPC());
                        ScreenReaderManager.SpeakInterrupt(details);
                        return;
                    }
                }
                ScreenReaderManager.SpeakInterrupt($"{GetSlotName(slot.equipmentSlot)}, empty");
            }
            else
            {
                var dragDropItem = GetCurrentDragDropItem();
                if (dragDropItem == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No item selected");
                    return;
                }

                ItemInstance item = dragDropItem.GetItem();
                if (item != null)
                {
                    string details = InventoryFormatting.FormatDetailedItemInfo(item, GetCurrentPC());
                    ScreenReaderManager.SpeakInterrupt(details);
                }
            }
        }

        private void AnnounceDescription()
        {
            ItemInstance item = null;

            if (currentZone == NavigationZone.Equipment)
            {
                if (currentIndex >= 0 && currentIndex < currentList.Count)
                {
                    var slot = currentList[currentIndex] as INV_EquipmentSlot;
                    if (slot != null)
                    {
                        var equipped = slot.GetCurrentItem(false);
                        if (equipped != null)
                            item = equipped.GetItem();
                    }
                }
            }
            else
            {
                var dragDropItem = GetCurrentDragDropItem();
                if (dragDropItem != null)
                    item = dragDropItem.GetItem();
            }

            if (item == null || item.template == null)
            {
                ScreenReaderManager.SpeakInterrupt("No description available");
                return;
            }

            string description = UITextExtractor.CleanText(
                Language.Localize(item.template.description, false, false, string.Empty));

            if (string.IsNullOrEmpty(description))
                ScreenReaderManager.SpeakInterrupt("No description available");
            else
                ScreenReaderManager.SpeakInterrupt(description);
        }

        private string FormatCurrentItemAnnouncement()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count)
                return null;

            object current = currentList[currentIndex];

            if (current is INV_EquipmentSlot slot)
            {
                var equipped = slot.GetCurrentItem(false);
                if (equipped != null)
                {
                    ItemInstance item = equipped.GetItem();
                    if (item != null)
                    {
                        string itemAnnouncement = InventoryFormatting.FormatItemAnnouncement(item, detailed: true);
                        return $"{GetSlotName(slot.equipmentSlot)}: {itemAnnouncement}";
                    }
                }
                return $"{GetSlotName(slot.equipmentSlot)}: empty";
            }

            if (current is INV_DragDropItem dragDropItem)
            {
                ItemInstance item = dragDropItem.GetItem();
                if (item != null)
                {
                    return InventoryFormatting.FormatItemAnnouncement(item, detailed: true);
                }
                return "Empty item";
            }

            return null;
        }

        #endregion

        #region Helpers

        private INV_MainPanel GetINV_MainPanel()
        {
            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu == null) return null;

            return charInfoMenu.GetComponentInChildren<INV_MainPanel>();
        }

        private INV_DragDropItem GetCurrentDragDropItem()
        {
            if (currentIndex < 0 || currentIndex >= currentList.Count) return null;

            if (currentList[currentIndex] is INV_DragDropItem item)
                return item;

            if (currentList[currentIndex] is INV_EquipmentSlot slot)
                return slot.GetCurrentItem(false);

            return null;
        }

        private PC GetCurrentPC()
        {
            if (isCharacterInfoMenu)
            {
                var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                if (charInfoMenu != null && charInfoCurrentPCField != null)
                {
                    return charInfoCurrentPCField.GetValue(charInfoMenu) as PC;
                }
            }
            else if (isPopupInventoryMenu)
            {
                var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
                if (popupInv != null && popupInvPcSelectedField != null)
                {
                    return popupInvPcSelectedField.GetValue(popupInv) as PC;
                }
            }

            // Fallback
            if (MonoBehaviourSingleton<Game>.HasInstance())
                return MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();

            return null;
        }

        private InventoryContainer GetActiveInventoryContainer()
        {
            if (isPopupInventoryMenu)
            {
                var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
                return popupInv?.inventoryContainer;
            }

            if (isCharacterInfoMenu)
            {
                // CharacterInfoMenu uses CHA_InventoryPanel which has an InventoryContainer
                var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                if (charInfoMenu != null)
                {
                    var invPanel = charInfoMenu.GetComponentInChildren<CHA_InventoryPanel>();
                    if (invPanel != null)
                    {
                        return invPanel.GetComponentInChildren<InventoryContainer>();
                    }
                }
            }

            return null;
        }

        private string GetContainerName()
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null) return "Unknown";

            if (popupInv.sourceLabel != null && !string.IsNullOrEmpty(popupInv.sourceLabel.text))
                return UITextExtractor.CleanText(popupInv.sourceLabel.text);

            return "Container";
        }

        private int GetPopupInstanceId()
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            return popupInv != null ? popupInv.GetInstanceID() : 0;
        }

        private void DetectPopupInstanceChange()
        {
            var popupInv = UnityEngine.Object.FindObjectOfType<PopupInventoryMenu>();
            if (popupInv == null || !popupInv.gameObject.activeInHierarchy) return;

            int currentId = popupInv.GetInstanceID();
            if (trackedPopupInstanceId == 0 || currentId == trackedPopupInstanceId) return;

            MelonLogger.Msg($"[InventoryState] Popup swap detected (old={trackedPopupInstanceId}, new={currentId}), re-announcing");
            trackedPopupInstanceId = currentId;
            lastAnnouncedText = null;
            currentZone = NavigationZone.ContainerItems;
            BuildContainerItemList();
            if (currentList.Count > 0 && currentIndex < 0)
                currentIndex = 0;

            ScreenReaderManager.SpeakInterrupt("Loot");
            if (currentList.Count > 0 && currentIndex >= 0)
                AnnounceCurrentItem(interrupt: false);
        }

        private string GetZoneEmptyMessage()
        {
            switch (currentZone)
            {
                case NavigationZone.Equipment: return "No equipment slots available";
                case NavigationZone.Backpack: return "Backpack is empty";
                case NavigationZone.ContainerItems: return "Container is empty";
                default: return "Empty";
            }
        }

        private string GetSlotName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return "Head";
                case EquipmentSlot.Torso: return "Torso";
                case EquipmentSlot.Legs: return "Legs";
                case EquipmentSlot.Shoulders: return "Shoulders";
                case EquipmentSlot.WeaponR: return "Primary weapon";
                case EquipmentSlot.WeaponL: return "Secondary weapon";
                case EquipmentSlot.Trinket1: return "Trinket 1";
                case EquipmentSlot.Trinket2: return "Trinket 2";
                case EquipmentSlot.Trinket3: return "Trinket 3";
                case EquipmentSlot.UnderArmor: return "Armor";
                case EquipmentSlot.GasMask: return "Gas mask";
                case EquipmentSlot.RadSuit: return "Radiation suit";
                case EquipmentSlot.Canteen: return "Canteen";
                case EquipmentSlot.Packs: return "Backpack";
                default: return slot.ToString();
            }
        }

        private string GetFilterName(InventoryFilter filter)
        {
            switch (filter)
            {
                case InventoryFilter.AllWithJunk: return "All items";
                case InventoryFilter.All: return "All except junk";
                case InventoryFilter.Weapon: return "Weapons";
                case InventoryFilter.Armor: return "Armor";
                case InventoryFilter.Ammo: return "Ammo";
                case InventoryFilter.Trinket: return "Trinkets";
                case InventoryFilter.Crafting: return "Crafting";
                case InventoryFilter.Misc: return "Miscellaneous";
                case InventoryFilter.Favorites: return "Favorites";
                case InventoryFilter.Junk: return "Junk";
                case InventoryFilter.Consumables: return "Consumables";
                default: return filter.ToString();
            }
        }

        #endregion

        #region Reflection

        private static void CacheReflection()
        {
            reflectionCached = true;

            // INV_DragDropItem.OpenContextMenu (protected)
            openContextMenuMethod = typeof(INV_DragDropItem).GetMethod("OpenContextMenu",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (openContextMenuMethod == null)
                MelonLogger.Warning("[InventoryState] Could not find INV_DragDropItem.OpenContextMenu");

            // CharacterInfoMenu.currentPC (private)
            charInfoCurrentPCField = typeof(CharacterInfoMenu).GetField("currentPC",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (charInfoCurrentPCField == null)
                MelonLogger.Warning("[InventoryState] Could not find CharacterInfoMenu.currentPC");

            // PopupInventoryMenu.pcSelected (protected)
            popupInvPcSelectedField = typeof(PopupInventoryMenu).GetField("pcSelected",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (popupInvPcSelectedField == null)
                MelonLogger.Warning("[InventoryState] Could not find PopupInventoryMenu.pcSelected");

            // InventoryContainer.SetFilter (protected)
            inventoryContainerSetFilterMethod = typeof(InventoryContainer).GetMethod("SetFilter",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (inventoryContainerSetFilterMethod == null)
                MelonLogger.Warning("[InventoryState] Could not find InventoryContainer.SetFilter");

            MelonLogger.Msg("[InventoryState] Reflection cached");
        }

        #endregion
    }
}
