File: States/InventoryState.cs — accessibility state for inventory screens (CharacterInfoMenu equipment+backpack and PopupInventoryMenu loot containers), including an item info browser.

namespace Wasteland2AccessibilityMod.States  (line 8)

class InventoryState : IAccessibilityState  (line 17)
    // Priority 50. Yields to GenericMenuState when ItemInfoMenu, ModItemMenu, or ModalMessageMenu is open.
    // PopupInventoryMenu (loot) takes priority over CharacterInfoMenu when both are present.

    public string Name => "Inventory"  (line 19)
    public int Priority => 50  (line 20)

    // When true, managed navigation is active and patch-based announcements should be suppressed.
    public static bool IsManagedNavigation { get; private set; }  (line 25)

    private enum NavigationZone  (line 28)
        Equipment
        Backpack
        ContainerItems

    // Current navigation state
    private NavigationZone currentZone  (line 36)
    private List<object> currentList  (line 37)   // INV_EquipmentSlot or INV_DragDropItem
    private int currentIndex  (line 38)
    private bool isDirty  (line 39)

    // Context flags
    private bool isCharacterInfoMenu  (line 42)
    private bool isPopupInventoryMenu  (line 43)

    // Suspended state — preserved across overlay deactivation (e.g. context menu)
    private NavigationZone suspendedZone  (line 46)
    private int suspendedIndex  (line 47)
    private bool hasSuspendedState  (line 48)
    private bool suspendedWasCharacterInfo  (line 49)
    private bool suspendedWasPopupInventory  (line 50)
    private int suspendedPopupInstanceId  (line 51)

    // Item info browser mode
    private bool isInfoBrowsing  (line 54)
    private List<string> infoLines  (line 55)
    private int infoLineIndex  (line 56)

    private string lastAnnouncedText  (line 59)

    // Tracks PopupInventoryMenu instance ID to detect back-to-back loot windows on same frame
    private int trackedPopupInstanceId  (line 64)

    // Reflection caches (all static, populated once by CacheReflection)
    private static bool reflectionCached  (line 67)
    private static MethodInfo openContextMenuMethod  (line 68)   // INV_DragDropItem.OpenContextMenu
    private static FieldInfo charInfoCurrentPCField  (line 69)   // CharacterInfoMenu.currentPC
    private static FieldInfo charInfoPcContainerButtonsField  (line 70)
    private static FieldInfo popupPcContainerButtonsField  (line 71)
    private static FieldInfo inventoryContainerFilterField  (line 72)
    private static MethodInfo inventoryContainerSetFilterMethod  (line 73)   // InventoryContainer.SetFilter
    private static FieldInfo charInfoCurrentPanelField  (line 74)   // CharacterInfoMenu.currentPanel

    // Fixed equipment slot traversal order (12 named field names on INV_MainPanel)
    private static readonly string[] equipmentSlotFieldNames  (line 77)

    public bool IsActive { get; }  (line 93)
    // note: checks PopupInventoryMenu first (loot priority), then CharacterInfoMenu+CHA_InventoryPanel.
    //       Returns false while ItemInfoMenu, ModItemMenu, or ModalMessageMenu is active.

    public bool HandleInput()  (line 134)
    // note: suppresses all game input. Routes to HandleInfoBrowserInput() when isInfoBrowsing.
    //       Calls DetectPopupInstanceChange() each frame to catch same-frame loot-window swaps.
    //       Retries BuildContainerItemList() when list is empty (async container population).
    //       Dispatches to HandleCharacterInfoInput() or HandlePopupInventoryInput().

    public void OnActivated()  (line 181)
    // note: restores suspended state (zone, index) when returning from an overlay; discards it
    //       when context changed or a different PopupInventoryMenu instance is now active.
    //       Fresh activation: announces zone header, then first item.

    public void OnDeactivated()  (line 266)
    // note: saves current zone/index as suspended state for overlay-return restoration.

    // ---- Context Detection ----

    private void DetectContext()  (line 287)
    // note: sets isCharacterInfoMenu / isPopupInventoryMenu. Popup checked first (priority).

    // ---- CharacterInfoMenu Input ----  (region, line 313)

    private bool HandleCharacterInfoInput()  (line 315)
    // Keybinds: Up/Down=navigate, Left/Right=SwitchZone, Enter=context menu, E=quick equip/unequip,
    //           Tab=detailed info, R=description, F=cycle filter, I=info browser,
    //           C=inventory context summary, F1-F7=party switch, PageUp/PageDown=tab switch, Esc=close.

    // ---- PopupInventoryMenu Input ----  (region, line 413)

    private bool HandlePopupInventoryInput()  (line 415)
    // Keybinds: Up/Down=navigate, Left/Right=switch container, Enter=transfer item,
    //           Tab=detailed info, R=description, F=cycle filter, I=info browser,
    //           C=loot context summary, F1-F7=destination party switch, T=take all,
    //           G=distribute all, Esc=close loot.

    // ---- List Building ----  (region, line 514)

    private void BuildEquipmentSlotList()  (line 516)
    // note: iterates equipmentSlotFieldNames via reflection on INV_MainPanel; only active slots included.

    private void BuildBackpackItemList()  (line 549)
    // note: prefers InventoryGrid.GetPositionSortedList() for consistent order; falls back to child scan.

    private void BuildContainerItemList()  (line 599)
    // note: reads PopupInventoryMenu.inventoryContainer.table.GetSortedList().

    private void RebuildCurrentList()  (line 631)
    // Dispatches to the correct Build* method for currentZone; tries to preserve currentIndex.

    // ---- Navigation ----  (region, line 658)

    private void NavigateList(int direction)  (line 660)
    // note: wraps around. Tries BuildContainerItemList() first if list is empty (async population guard).
    //       Clears lastAnnouncedText so the same item is re-announced after a wrap.

    private void SwitchZone()  (line 693)
    // Toggles between Equipment and Backpack zones (CharacterInfoMenu only); announces zone + first item.

    private void SwitchContainer(int direction)  (line 716)
    // Cycles through PopupInventoryMenu.containerButtons; calls popupInv.SelectContainer(); sets isDirty.

    // ---- Actions ----  (region, line 756)

    private void OpenContextMenuOnCurrentItem()  (line 758)
    // Invokes INV_DragDropItem.OpenContextMenu via reflection.

    private void QuickEquipUnequip()  (line 786)
    // Backpack zone: calls item.AttemptToEquip(). Equipment zone: calls equipped.AttemptToUnequip().

    private void TransferCurrentItem()  (line 822)
    // Calls PopupInventoryMenu.OnItemDoubleClicked(); announces transferred item name.

    private void TakeAll()  (line 846)
    // Calls PopupInventoryMenu.OnTakeAllClicked().

    private void DistributeAll()  (line 856)
    // Calls PopupInventoryMenu.OnDistributeAllClicked().

    private void CloseInventory()  (line 866)
    // note: sets EventManager.ignoreNextBack to prevent Escape from opening pause menu on next frame.

    private void CloseLoot()  (line 879)
    // note: same ignoreNextBack guard as CloseInventory.

    private void SwitchCharacterInfoTab(int direction)  (line 892)
    // Calls charInfoMenu.GoToNextPanel() or GoToPreviousPanel().

    private bool HandlePartySwitch()  (line 905)
    // Detects F1–F7 key presses and calls SwitchToPartyMember(index).

    private void SwitchToPartyMember(int index)  (line 919)
    // Dispatches to SwitchPartyViaGameAPI (CharacterInfoMenu) or SwitchPopupPartyMember (loot).

    private void SwitchPartyViaGameAPI(int index)  (line 933)
    // note: uses pc.MakeLeader() + InputManager.AddToSelection() — same mechanism as INV_PartyList.

    private void SwitchPopupPartyMember(int index)  (line 966)
    // note: uses popupInv.OnButtonDown("Select Player N") to update pcSelected via the game's event.

    private void CycleFilter()  (line 998)
    // Cycles through 7 InventoryFilter values; calls InventoryContainer.SetFilter via reflection.
    // note: state machine over fixed filterOrder array; uses reflection for protected SetFilter.

    // ---- Info Browser ----  (region, line 1047)

    private bool HandleInfoBrowserInput()  (line 1049)
    // Handles Up/Down/Home/End navigation within infoLines; Escape or I closes browser.
    // note: consumes all keys while browsing (returns true for any unhandled key).

    private void OpenInfoBrowser()  (line 1102)
    // Builds infoLines from current item, enters isInfoBrowsing mode, announces first line.

    private void BuildInfoLines(ItemInstance item, PC pc)  (line 1125)
    // Assembles all readable item properties into infoLines list in announcement order:
    //   Name, Type, Quantity, Equipped-in slot, weapon stats (damage/ammo/range/accuracy/crit/
    //   firing modes/mods), armor stats, ammo stats, usable/consumable effects, trinket,
    //   weapon mod lines, junk flag, new flag, modifiers, requirements, trait modifiers,
    //   weight, value, tier, description, ItemInfoBox display labels, comparison vs equipped.
    // note: delegates heavily to InventoryPatches static helpers. Falls back to ItemInfoBox
    //       label scraping for display values that don't have clean accessor paths.

    private void AppendComparisonLines(ItemInstance focused, PC pc)  (line 1447)
    // Appends a "Compared to equipped X" section with signed stat deltas.
    // note: suppressed when currentZone == Equipment (focused item IS the equipped one).
    //       Replaces header with "Identical stats to…" when no deltas are found.

    private void AppendIntDiff(string label, int diff, string units = "")  (line 1519)
    // Appends a signed delta line; silently skips when diff == 0.

    private void AppendWeightDiff(float diff)  (line 1526)
    // Appends signed weight delta; skips when |diff| < 0.05.

    private ItemInstance GetEquippedComparisonItem(ItemInstance focused, PC pc)  (line 1539)
    // Returns the currently equipped item to compare against focused:
    //   Weapon → weaponSlot1; Armor/Wearable → slot matching ItemTemplate_Equipment.slot.
    // note: falls back to iterating pc.inventory.equipment when INV_MainPanel isn't accessible
    //       (e.g. loot context). Returns null when nothing relevant is equipped.

    private ItemInstance GetCurrentItemInstance()  (line 1603)
    // Unwraps currentList[currentIndex] — handles both INV_EquipmentSlot and INV_DragDropItem.

    // ---- Announcements ----  (region, line 1625)

    private void AnnounceInventoryContext()  (line 1627)
    // Speaks: "<PC name> inventory, <zone>, <count> items, filter: <filter name>".

    private void AnnounceLootContext()  (line 1648)
    // Speaks: "Loot: <container name>, <count> items, destination: <PC name>".

    private void AnnounceCurrentItem(bool interrupt)  (line 1661)
    // Speaks formatted item announcement + position ("N of M"); skips duplicate text.

    private void AnnounceDetailedInfo()  (line 1688)
    // Calls InventoryPatches.FormatDetailedItemInfo() for equipment or backpack item.

    private void AnnounceDescription()  (line 1727)
    // Reads item.template.description through localization; speaks "No description available" if empty.

    private string FormatCurrentItemAnnouncement()  (line 1766)
    // Returns slot-prefixed announcement for equipment slots ("Head: item name")
    // or plain item announcement for backpack/container items.

    // ---- Helpers ----  (region, line 1803)

    private INV_MainPanel GetINV_MainPanel()  (line 1805)
    // Finds CharacterInfoMenu in scene and returns its child INV_MainPanel component.

    private INV_DragDropItem GetCurrentDragDropItem()  (line 1813)
    // Returns INV_DragDropItem for current list entry; also unwraps from INV_EquipmentSlot.

    private PC GetCurrentPC()  (line 1826)
    // Gets PC from CharacterInfoMenu.currentPC (reflection) or PopupInventoryMenu.pcSelected (reflection).
    // Falls back to Game.GetFirstSelectedPC().

    private InventoryContainer GetActiveInventoryContainer()  (line 1856)
    // Returns inventoryContainer from popup or CHA_InventoryPanel child of CharacterInfoMenu.

    private string GetContainerName()  (line 1881)
    // Reads PopupInventoryMenu.sourceLabel.text; returns "Container" if unavailable.

    private int GetPopupInstanceId()  (line 1892)
    // Returns PopupInventoryMenu.GetInstanceID(); 0 if not found.

    private void DetectPopupInstanceChange()  (line 1898)
    // Called each frame; when instance ID changes (same-frame swap), resets state and re-announces.

    private string GetZoneEmptyMessage()  (line 1919)
    // Switch over NavigationZone → empty-state message string.

    private string GetSlotName(EquipmentSlot slot)  (line 1930)
    // Switch over EquipmentSlot enum → display string (e.g. WeaponR → "Primary weapon").

    private string GetFilterName(InventoryFilter filter)  (line 1952)
    // Switch over InventoryFilter enum → display string.

    // ---- Reflection ----  (region, line 1973)

    private static void CacheReflection()  (line 1975)
    // Populates all static MethodInfo/FieldInfo caches (6 members across 4 game types).
    // note: uses reflection throughout because targets are protected/private game members.
