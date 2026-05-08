File: Patches/InventoryPatches.cs — Harmony patches for inventory accessibility: item focus announcements, detailed item info formatting, equipment slot changes, add/remove events, mod attachment, and "Give to" party transfer.

namespace Wasteland2AccessibilityMod.Patches  (line 10)

// Static utility class providing item formatting helpers used across inventory patches.
public static class InventoryPatches  (line 16)
    internal static string lastAnnouncedItem  (line 18)

    // Formats a short item announcement (name, quantity, optional type/weight).
    internal static string FormatItemAnnouncement(ItemInstance item, bool detailed = false)  (line 23)

    // Formats comprehensive item details for screen reader (weapons, armor, ammo, consumables, mods, etc.).
    internal static string FormatDetailedItemInfo(ItemInstance item, PC pc = null)  (line 82)
        // note: includes damage, range brackets, firing modes, accuracy, crit, AP costs, jam chance, ammo, mod slots, stat modifiers, requirements, trait modifiers, description, value, weight.

    // Formats "SlotName slot, ItemInfo" or "SlotName slot, empty".
    internal static string FormatEquipmentSlot(EquipmentSlot slot, ItemInstance item)  (line 327)

    // Lines for each stat bonus in ItemTemplate_Equipment.stats; skips raw "armor" for armor pieces.
    internal static List<string> BuildModifierLines(ItemInstance item)  (line 344)

    // Lines for attribute requirements with "(met)"/"(not met)" annotation from the current PC.
    internal static List<string> BuildRequirementLines(ItemInstance item, PC currentPC)  (line 368)

    // "X% chance to apply Y [or Z]" for weapons with status effects; null if not applicable.
    internal static string BuildAfflictorLine(ItemInstance item)  (line 395)

    // Localized skill name for a weapon (e.g. "Assault Rifle"); null if unavailable.
    internal static string GetWeaponSkillDisplayName(ItemTemplate_Weapon wt)  (line 418)

    // Caliber display string for a ranged weapon; null if not applicable.
    internal static string GetWeaponCaliberDisplay(ItemTemplate_Weapon wt)  (line 431)

    // Replicates ItemInfoBox accuracy calculation for the given PC; returns -1 when not applicable.
    internal static int ComputeAccuracyPercent(ItemInstance_Weapon weapon, PC pc)  (line 445)
        // note: mirrors vanilla GetAccuracyPercent including mod bonuses, ranged hit bonus, trait multipliers, missing-attribute penalty.

    // Replicates ItemInfoBox crit calculation for the given PC; energy/thrown/RPG weapons return 0.
    internal static int ComputeCritChancePercent(ItemInstance_Weapon weapon, PC pc)  (line 478)

    // One line per UseEffects entry; skill-bonus-adjusted heal values when PC is supplied.
    internal static List<string> BuildConsumableEffectLines(ItemInstance_Usable item, PC pc)  (line 503)
        // note: mirrors ItemInfoBox.BuildFieldMedicString; also adds skill requirement and "Requirements not met" lines.

    // One line per mod slot showing installed mod name or "empty".
    internal static List<string> BuildModSlotLines(ItemInstance item)  (line 618)

    // Info lines for a weapon mod: slot type, stat bonuses, requirements, allowed-weapons list.
    internal static List<string> BuildWeaponModLines(ItemInstance_Mod item, PC pc)  (line 651)

    // "+N SkillName skill" line for XP-giver consumables (e.g. skill books).
    internal static string BuildXPGiverLine(ItemTemplate_UsableXPGiver xpGiver)  (line 712)

    // Lines for trait-specific item modifiers from pcTemplate.GetTraitItemModifierString.
    internal static List<string> BuildTraitItemModifierLines(ItemInstance item, PC pc)  (line 731)

    // Lines for energy weapon threshold damage multipliers; also notes no-jam / no-crit properties.
    internal static List<string> BuildEnergyWeaponLines(ItemTemplate_Weapon wt)  (line 759)

    private static string FormatModStatBonus(string key, float value)  (line 790)
        // note: flags positive values on negative-is-good stats as "(penalty)".

    // One line per firing mode: "ModeName: N AP, N ammo [, hit modifier] [(current)]".
    internal static List<string> BuildFiringModeLines(ItemInstance_WeaponRanged weapon)  (line 820)

    // Three range bracket lines (point-blank, optimal, maximum) with meters and %-to-hit modifiers.
    internal static List<string> BuildRangeBracketLines(ItemInstance_WeaponRanged weapon, PC pc)  (line 858)

    // AP-to-attack, AP-to-reload, chance-to-jam, and "Currently jammed" lines.
    internal static List<string> BuildWeaponOperationalLines(ItemInstance_Weapon weapon)  (line 898)

    // "Reserve ammo: N" line for ranged weapons using the PC's inventory caliber count.
    internal static string BuildReserveAmmoLine(ItemInstance_WeaponRanged weapon, PC pc)  (line 927)

    // Vendor-aware item sell value in dollars; mirrors ItemInfoBox.GetItemValue.
    internal static int ComputeItemValue(ItemInstance item)  (line 941)

    // Formats "+NUnit StatName [(penalty)]" for equipment stat modifiers.
    internal static string FormatStatModifier(string key, int value)  (line 962)

    private static string GetStatDisplayName(string key)  (line 993)
        // note: looks up display name via PCStatsManager, then PrintableNameHelper, then falls back to key.

// Announces newly populated item when it is the currently selected UI object; suppressed when InventoryState is active.
[HarmonyPatch(typeof(INV_DragDropItem), "PopulateData")]
class INV_DragDropItem_PopulateData_Patch  (line 1013)
    [HarmonyPostfix]
    public static void Postfix(INV_DragDropItem __instance, ItemInstance newItem, Inventory newOwnerInventory, PC pc, EquipmentSlot newSlot)  (line 1017)

// Empty stub — selection is delegated to UICamera focus patches.
[HarmonyPatch(typeof(INV_DragDropItem), "OnEnable")]
class INV_DragDropItem_OnEnable_Patch  (line 1034)
    [HarmonyPostfix]
    public static void Postfix(INV_DragDropItem __instance)  (line 1038)

// Announces selected item with details when InventoryGrid.SelectItem succeeds; deduplicates against lastAnnouncedItem.
[HarmonyPatch(typeof(InventoryGrid), "SelectItem")]
class InventoryGrid_SelectItem_Patch  (line 1052)
    [HarmonyPostfix]
    public static void Postfix(InventoryGrid __instance, ItemInstance itemInstance, bool __result)  (line 1056)

// Announces "Inventory grid" context when SelectFirstItem succeeds.
[HarmonyPatch(typeof(InventoryGrid), "SelectFirstItem")]
class InventoryGrid_SelectFirstItem_Patch  (line 1079)
    [HarmonyPostfix]
    public static void Postfix(InventoryGrid __instance, bool __result)  (line 1082)

// Announces "Added N ItemName" when items are added to a player inventory.
[HarmonyPatch(typeof(Inventory), "AddItem")]
class Inventory_AddItem_Patch  (line 1099)
    [HarmonyPostfix]
    public static void Postfix(Inventory __instance, ItemInstance item, int numToAdd, int __result)  (line 1102)

// Announces "Removed N ItemName" when items are removed from a player inventory.
[HarmonyPatch(typeof(Inventory), "RemoveItemInstance")]
class Inventory_RemoveItemInstance_Patch  (line 1129)
    [HarmonyPostfix]
    public static void Postfix(Inventory __instance, ItemInstance item, int numToRemove, bool __result)  (line 1132)

// Announces equipment slot content when a character is selected and the slot is focused.
[HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(PC) })]
class INV_EquipmentSlot_PopulateData_PC_Patch  (line 1158)
    [HarmonyPostfix]
    public static void Postfix(INV_EquipmentSlot __instance, PC newPC)  (line 1162)

// Announces equipment slot changes when an item is equipped/unequipped.
[HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(ItemInstance_Equipment) })]
class INV_EquipmentSlot_PopulateData_Item_Patch  (line 1186)
    [HarmonyPostfix]
    public static void Postfix(INV_EquipmentSlot __instance, ItemInstance_Equipment newItem)  (line 1190)

// Announces visible item count when the grid is repositioned and the count changes.
[HarmonyPatch(typeof(InventoryGrid), "Reposition")]
class InventoryGrid_Reposition_Patch  (line 1206)
    private static int lastItemCount  (line 1209)

    [HarmonyPostfix]
    public static void Postfix(InventoryGrid __instance)  (line 1212)
        // note: skips when PopupInventoryMenu or active CharacterInfoMenu is present to avoid double-announce with InventoryState.

// Announces detailed item info when ItemInfoBox.SetItem is called (item clicked, not hovered).
[HarmonyPatch(typeof(ItemInfoBox), "SetItem")]
class ItemInfoBox_SetItem_Patch  (line 1259)
    [HarmonyPostfix]
    public static void Postfix(ItemInfoBox __instance, ItemInstance item, PC currentPC)  (line 1263)

// Extended UICamera.SetSelection handler that announces INV_DragDropItem details including grid position and equipment slot context.
[HarmonyPatch(typeof(UICamera), "SetSelection")]
class UICamera_SetSelection_InventoryExtension_Patch  (line 1280)
    [HarmonyPostfix]
    public static void Postfix(GameObject go)  (line 1284)
        // note: suppressed when InventoryState or any InputRouter state is active.

// Intercepts INV_DragDropItem.AttemptToUse for weapon mods to open ModItemMenu instead of entering visual-only weaponSmith ASI mode.
[HarmonyPatch(typeof(INV_DragDropItem), "AttemptToUse")]
class AttemptToUse_ModItemMenu_Patch  (line 1328)
    [HarmonyPrefix]
    public static bool Prefix(INV_DragDropItem __instance)  (line 1332)
        // note: checks weaponSmith skill requirement and whether any party weapons support the mod slot; speaks error if not met; returns false to skip original.

// Announces mod name and compatible weapon count when ModItemMenu opens.
[HarmonyPatch(typeof(ModItemMenu), "PopulateData", new Type[] { typeof(ItemInstance_Mod), typeof(PC) })]
class ModItemMenu_PopulateData_Patch  (line 1388)
    [HarmonyPostfix]
    public static void Postfix(ModItemMenu __instance, ItemInstance_Mod item)  (line 1392)

// Adds a "Give to" button to the inventory context menu for keyboard-accessible party transfers (drag-and-drop alternative).
[HarmonyPatch(typeof(INV_DragDropItem), "OpenContextMenu")]
class OpenContextMenu_GiveTo_Patch  (line 1425)
    [HarmonyPostfix]
    public static void Postfix(INV_DragDropItem __instance)  (line 1429)
        // note: injects "Give to..." button into ItemInfoMenu via reflection on GUIManager.screens; skips VND_DragDropItem.

    private static void OnGiveToClicked(INV_DragDropItem dragDropItem)  (line 1468)
        // note: closes context menu, opens party-member selection menu.

    private static void OnPartyMemberSelected(INV_DragDropItem dragDropItem, PC targetPC)  (line 1508)
        // note: shows quantity menu for stacks; calls ExecuteTransfer for singles.

    private static void ShowQuantityMenu(INV_DragDropItem dragDropItem, PC targetPC, ItemInstance item)  (line 1529)
        // note: opens AskQuantityMenu with min=1, max=item.quantity.

    private static void ExecuteTransfer(INV_DragDropItem dragDropItem, PC targetPC, int transferQty)  (line 1558)
        // note: checks CNPC willingness, unequips if needed, removes from owner inventory, adds to target, announces result, triggers encumbrance tutorial if >80% carry weight.
