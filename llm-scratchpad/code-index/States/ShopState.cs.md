File: States/ShopState.cs — keyboard nav and screen reader support for the vendor/shop screen, covering a four-zone escrow trade flow

namespace Wasteland2AccessibilityMod.States  (line 9)

class ShopState : IAccessibilityState  (line 17)

    // IAccessibilityState identity
    public string Name => "Shop"  (line 19)
    public int Priority => 50  (line 20)

    // When true, managed navigation is active and patch-based announcements should be suppressed
    public static bool IsManagedNavigation { get; private set; }  (line 25)

    // Private enum ShopZone  (line 28)
    //   PlayerInventory, Escrow, VendorInventory, Filters
    private static readonly ShopZone[] ZoneOrder  (line 37)
    // note: Left/Right zone cycling uses this array for wrap-around order

    // Current navigation state
    private ShopZone currentZone  (line 46)
    private List<object> currentList  (line 47)
    // note: heterogeneous list — holds VND_DragDropItem, EscrowEntry, string sentinels, or GameObject (filters)
    private int currentIndex  (line 48)
    private bool isDirty  (line 49)

    // Overlay suspension state (quantity dialogs, modals)
    private ShopZone suspendedZone  (line 52)
    private int suspendedIndex  (line 53)
    private bool hasSuspendedState  (line 54)

    // Info browser (line-list mode, same pattern as InventoryState)
    private bool isInfoBrowsing  (line 57)
    private List<string> infoLines  (line 58)
    private int infoLineIndex  (line 59)

    private string lastAnnouncedText  (line 62)

    // Sentinel string constants for escrow list virtual entries
    private const string EscrowSummaryMarker = "__ESCROW_SUMMARY__"  (line 65)
    private const string EscrowFinalizeMarker = "__ESCROW_FINALIZE__"  (line 66)

    // Private nested class EscrowEntry  (line 71)
    //   ItemInstance Item
    //   bool IsSelling  — true = player selling, false = player buying
    //   Escrow ParentEscrow

    // Cached VendorScreen reference
    private VendorScreen cachedVendorScreen  (line 79)

    // Reflection caches (static, one-time)
    private static bool reflectionCached  (line 82)
    private static MethodInfo tryGetDestInventoryMethod  (line 83)
    private static MethodInfo setFilterMethod  (line 84)
    private static FieldInfo inventoryContainerFilterField  (line 85)
    private static MethodInfo onSellJunkClickedMethod  (line 86)

    // Active when VendorScreen is open; yields when ItemInfoScreen is open
    public bool IsActive { get; }  (line 88)

    // Handles info browser mode first; rebuilds list if dirty; dispatches Up/Down nav, Left/Right zone switch, Enter=action, I=info browser, R=description, S=scrap balance, J=sell junk, F=cycle filter, F1-F7=party switch, Escape=close
    public bool HandleInput()  (line 104)

    // Enters buy mode, announces vendor name and scrap; restores suspended state if returning from an overlay
    public void OnActivated()  (line 201)

    // Saves current position to suspension fields; clears transient state
    public void OnDeactivated()  (line 262)

    // --- Zone Switching (#region line 277) ---

    // Cycles ZoneOrder by direction with wrap-around; switches vendor screen buy/sell mode as needed; rebuilds list; announces zone name and item count
    private void SwitchZone(int direction)  (line 279)

    // Maps ShopZone to display string
    private string GetZoneName(ShopZone zone)  (line 332)

    // --- List Building (#region line 346) ---

    // Collects INV_DragDropItem components from vendorScreen.playerInventoryGrid sorted by position
    private void BuildPlayerItemList()  (line 348)

    // Collects INV_DragDropItem components from vendorScreen.vendorInventoryGrid sorted by position
    private void BuildVendorItemList()  (line 376)

    // Reads directly from Escrow.escrowList data model (not the UI); adds EscrowEntry objects plus EscrowSummaryMarker and EscrowFinalizeMarker sentinels at the end
    // note: bypasses the container UI because it may not be populated; always ends with two sentinel strings
    private void BuildEscrowList()  (line 402)

    // Collects filter button GameObjects from the active container's filterGrid
    private void BuildFilterList()  (line 452)

    // Dispatches to the appropriate Build*List method; preserves or clamps currentIndex
    private void RebuildCurrentList()  (line 480)

    // Clamps currentIndex to valid range; sets to 0 if list is non-empty and index was -1
    private void ClampIndex()  (line 508)

    // --- Navigation (#region line 518) ---

    // Wraps currentIndex within currentList; announces after move
    private void NavigateList(int direction)  (line 520)

    // Returns zone-specific "empty" message
    private string GetZoneEmptyMessage()  (line 540)

    // --- Actions (#region line 554) ---

    // Dispatches on type of currentList[currentIndex]: string sentinel → summary/finalize, GameObject → filter, EscrowEntry → remove from escrow, INV_DragDropItem → trade item
    // note: big type-dispatch switch disguised as if-chain
    private void PerformAction()  (line 556)

    // Casts to VND_DragDropItem; uses reflected TryGetDestInventory to find destination; asks quantity for stacks; moves item to escrow inventory; refreshes all containers
    // note: uses reflection (tryGetDestInventoryMethod); does NOT finalize the trade — just moves to escrow
    private void TradeItem(INV_DragDropItem dragDropItem)  (line 604)

    // Moves item from escrow inventory back to source inventory; refreshes containers
    private void RemoveFromEscrow(EscrowEntry entry)  (line 722)

    // Checks escrow totals, validates scrap, calls Escrow.RequestTradeAll(); cancels if insufficient scrap
    private void FinalizeTrade()  (line 779)

    // Sends OnClick to filter GameObject; sets isDirty
    private void ActivateFilter(GameObject filterObj)  (line 846)

    // Invokes reflected VendorScreen.OnSellJunkClicked(null); sets isDirty
    private void SellAllJunk()  (line 859)

    // Calls vendorScreen.OnButtonDown("Back"); clears hasSuspendedState
    private void CloseShop()  (line 883)

    // Reads current filter via reflected inventoryContainerFilterField; cycles through seven InventoryFilter values; applies via reflected setFilterMethod
    // note: uses reflection for both read and write of the filter field
    private void CycleFilter()  (line 895)

    // --- Party Switching (#region line 950) ---

    // Loops F1-F7 KeyCodes; returns true if a matching key was pressed
    private bool HandlePartySwitch()  (line 952)

    // Calls pc.MakeLeader(), InputManager.AddToSelection, vendorScreen.SelectPlayer; announces name
    private void SwitchToPartyMember(int index)  (line 966)

    // --- Info Browser (#region line 1007) ---

    // Handles Up/Down/Home/End line navigation; Escape/I closes; blocks all other keys
    private bool HandleInfoBrowserInput()  (line 1009)

    // Gets current ItemInstance, builds info lines, enters browsing mode
    private void OpenInfoBrowser()  (line 1062)

    // Populates infoLines with name, type, quantity, price, weapon/armor/ammo/usable stats, weight, tier, description, and ItemInfoBox labels
    // note: contains nested type-checking chain (ItemInstance_Weapon, _Armor, _Ammo, _Usable, _Trinket); also tries to read from vendorScreen.itemInfoBox
    private void BuildInfoLines(ItemInstance item)  (line 1085)

    // Computes buy/sell price via Escrow.TradeValueOfItem; falls back to template base price
    private void AddPriceInfoLines(ItemInstance item)  (line 1231)

    // Reads visible stat labels (damage, range, accuracy, armor, penetration, ammo) from ItemInfoBox and appends non-empty ones to infoLines
    private void AddInfoBoxLabels(ItemInfoBox infoBox)  (line 1269)

    // --- Announcements (#region line 1317) ---

    // Appends "N of M" position; deduplicates against lastAnnouncedText before speaking
    private void AnnounceCurrentItem(bool interrupt)  (line 1319)

    // Dispatches on current item type to format the appropriate announcement string
    // note: type-dispatch chain matching the same types as PerformAction
    private string FormatCurrentItemAnnouncement()  (line 1346)

    // Returns price string via Escrow.TradeValueOfItem for the current PC; falls back to base price
    private string GetItemPriceString(ItemInstance item, INV_DragDropItem dragDropItem)  (line 1412)

    // Reads partyCurrency and barter adjustment; speaks combined scrap/barter string
    private void AnnounceScrapBalance()  (line 1433)

    // Speaks FormatEscrowSummary()
    private void AnnounceEscrowSummary()  (line 1446)

    // Builds "buying X, selling Y, net cost/gain, your scrap N [WARNING]" summary string
    private string FormatEscrowSummary()  (line 1452)

    // Reads current item description from template and speaks it
    private void AnnounceDescription()  (line 1479)

    // --- Helpers (#region line 1503) ---

    // Tries TextTooltipCreator first, then matches against named filter button fields on the container, then falls back to UILabel child
    private string GetFilterButtonName(GameObject filterObj)  (line 1505)

    // Returns cached VendorScreen if still active in hierarchy; otherwise calls FindObjectOfType
    private VendorScreen GetVendorScreen()  (line 1549)

    // Reads vendorScreen.mobNameLabel; falls back to "Vendor"
    private string GetVendorName(VendorScreen vendorScreen)  (line 1558)

    // Gets the first selected PC from the Game singleton (pcSelected is private on VendorScreen)
    private PC GetCurrentPC()  (line 1567)

    // Returns ItemInstance from EscrowEntry or INV_DragDropItem at currentIndex
    private ItemInstance GetCurrentItemInstance()  (line 1576)

    // Returns currentList[currentIndex] cast to INV_DragDropItem, or null
    private INV_DragDropItem GetCurrentDragDropItem()  (line 1591)

    // Maps InventoryFilter enum to display string
    // note: switch with 9 cases
    private string GetFilterName(InventoryFilter filter)  (line 1597)

    // --- Reflection (#region line 1616) ---

    // Caches TryGetDestInventory (protected instance), OnSellJunkClicked (public instance), SetFilter (protected instance), and filter field (private instance) via reflection
    // note: all four targets use reflection because they are protected or private; logs warnings for any that can't be found
    private static void CacheReflection()  (line 1618)
