File: Patches/InventoryPatches.cs — Harmony patches for inventory accessibility: item focus announcements, detailed item info, equipment slot changes, add/remove events, mod attachment, and Give-to party transfer. Formatting helpers live in InventoryFormatting.cs.

namespace Wasteland2AccessibilityMod.Patches  (line 11)

[HarmonyPatch(typeof(INV_DragDropItem), "PopulateData")]
public class INV_DragDropItem_PopulateData_Patch  (line 23)
    [HarmonyPostfix] public static void Postfix(INV_DragDropItem __instance, ItemInstance newItem, Inventory newOwnerInventory, PC pc, EquipmentSlot newSlot)  (line 26)
        // note: only announces when the patched item is the currently selected UICamera object and IsManagedNavigation is false.

[HarmonyPatch(typeof(INV_DragDropItem), "OnEnable")]
public class INV_DragDropItem_OnEnable_Patch  (line 44)
    [HarmonyPostfix] public static void Postfix(INV_DragDropItem __instance)  (line 47)
        // note: stub — relies on focus/UICamera patches; no announcement here.

[HarmonyPatch(typeof(InventoryGrid), "SelectItem")]
public class InventoryGrid_SelectItem_Patch  (line 62)
    [HarmonyPostfix] public static void Postfix(InventoryGrid __instance, ItemInstance itemInstance, bool __result)  (line 65)
        // note: deduplicates via InventoryFormatting.lastAnnouncedItem; skips when IsManagedNavigation.

[HarmonyPatch(typeof(InventoryGrid), "SelectFirstItem")]
public class InventoryGrid_SelectFirstItem_Patch  (line 88)
    [HarmonyPostfix] public static void Postfix(InventoryGrid __instance, bool __result)  (line 91)
        // note: only speaks "Inventory grid" context; relies on focus patches for item detail.

[HarmonyPatch(typeof(Inventory), "AddItem")]
public class Inventory_AddItem_Patch  (line 107)
    [HarmonyPostfix] public static void Postfix(Inventory __instance, ItemInstance item, int numToAdd, int __result)  (line 111)
        // note: only announces for InventoryPlayer; uses Speak (not interrupt) to avoid stomping navigation.

[HarmonyPatch(typeof(Inventory), "RemoveItemInstance")]
public class Inventory_RemoveItemInstance_Patch  (line 137)
    [HarmonyPostfix] public static void Postfix(Inventory __instance, ItemInstance item, int numToRemove, bool __result)  (line 140)
        // note: only announces for InventoryPlayer on successful removal.

[HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(PC) })]
public class INV_EquipmentSlot_PopulateData_PC_Patch  (line 168)
    [HarmonyPostfix] public static void Postfix(INV_EquipmentSlot __instance, PC newPC)  (line 171)
        // note: only announces when the selected object is a child of this slot.

[HarmonyPatch(typeof(INV_EquipmentSlot), "PopulateData", new Type[] { typeof(ItemInstance_Equipment) })]
public class INV_EquipmentSlot_PopulateData_Item_Patch  (line 196)
    [HarmonyPostfix] public static void Postfix(INV_EquipmentSlot __instance, ItemInstance_Equipment newItem)  (line 199)

[HarmonyPatch(typeof(InventoryGrid), "Reposition")]
public class InventoryGrid_Reposition_Patch  (line 216)
    private static int lastItemCount  (line 218)
    [HarmonyPostfix] public static void Postfix(InventoryGrid __instance)  (line 221)
        // note: guards against PopupInventoryMenu and active CharacterInfoMenu to avoid double-announce; only speaks when item count changes from a known previous count.

[HarmonyPatch(typeof(ItemInfoBox), "SetItem")]
public class ItemInfoBox_SetItem_Patch  (line 268)
    [HarmonyPostfix] public static void Postfix(ItemInfoBox __instance, ItemInstance item, PC currentPC)  (line 271)

[HarmonyPatch(typeof(UICamera), "SetSelection")]
public class UICamera_SetSelection_InventoryExtension_Patch  (line 290)
    [HarmonyPostfix] public static void Postfix(GameObject go)  (line 293)
        // note: suppresses when any InputRouter state is active (avoids double-announce with GenericMenuState/ModItemMenu); appends grid position and re-formats as equipment slot when dragDropItem.slot != None.

[HarmonyPatch(typeof(INV_DragDropItem), "AttemptToUse")]
public class AttemptToUse_ModItemMenu_Patch  (line 337)
    [HarmonyPrefix] public static bool Prefix(INV_DragDropItem __instance)  (line 340)
        // note: intercepts weapon mods only; checks weaponSmith skill vs. requirement and party weapon compatibility; opens GUIManager.OpenModItemMenu instead of entering the visual weaponSmith ASI mode; skips original on success.

[HarmonyPatch(typeof(ModItemMenu), "PopulateData", new Type[] { typeof(ItemInstance_Mod), typeof(PC) })]
public class ModItemMenu_PopulateData_Patch  (line 397)
    [HarmonyPostfix] public static void Postfix(ModItemMenu __instance, ItemInstance_Mod item)  (line 400)
        // note: counts compatible weapons in the grid and announces "{N} compatible weapons, use arrows/Enter/Escape".

[HarmonyPatch(typeof(INV_DragDropItem), "OpenContextMenu")]
public class OpenContextMenu_GiveTo_Patch  (line 434)
    [HarmonyPostfix] public static void Postfix(INV_DragDropItem __instance)  (line 438)
        // note: injects a "Give to..." button into the ItemInfoMenu context menu; skips VND_DragDropItem and solo-party cases.

    private static void OnGiveToClicked(INV_DragDropItem dragDropItem)  (line 477)
        // note: opens a second ItemInfoMenu populated with party members as buttons; excludes the item's current owner; marks out-of-range members as disabled (> 20 units squared).

    private static void OnPartyMemberSelected(INV_DragDropItem dragDropItem, PC targetPC)  (line 517)
        // note: routes to ShowQuantityMenu for stackable items with quantity > 1; calls ExecuteTransfer directly for single items.

    private static void ShowQuantityMenu(INV_DragDropItem dragDropItem, PC targetPC, ItemInstance item)  (line 538)
        // note: opens AskQuantityMenu with default = full stack, min = 1, max = quantity.

    private static void ExecuteTransfer(INV_DragDropItem dragDropItem, PC targetPC, int transferQty)  (line 567)
        // note: checks CNPC willingness via Drama.NotifyOnTradeItemsEvent; unequips first if needed; calls RemoveItemInstance + AddItems; destroys widget on full transfer, UpdateText on partial; triggers encumbrance tutorial at 80% capacity.
