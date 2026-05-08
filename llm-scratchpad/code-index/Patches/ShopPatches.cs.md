File: Patches/ShopPatches.cs — Harmony patches for vendor/shop accessibility: mode announcements, trade blocking during managed navigation, and item transfer announcements.

namespace Wasteland2AccessibilityMod.Patches  (line 7)

// Announces "Sell mode" / "Buy mode" when vendor mode changes; suppressed when ShopState is managing.
[HarmonyPatch(typeof(VendorScreen), "SetSellMode")]
class VendorScreen_SetSellMode_Patch  (line 18)
    [HarmonyPostfix]
    public static void Postfix(VendorScreen __instance, bool sellMode)  (line 21)

// Blocks OnTradeButtonClicked (auto-finalize after each item move) when ShopState is managing navigation.
[HarmonyPatch(typeof(VendorScreen), "OnTradeButtonClicked")]
class VendorScreen_OnTradeButtonClicked_Patch  (line 37)
    [HarmonyPrefix]
    public static bool Prefix()  (line 40)
        // note: returns false when ShopState.IsManagedNavigation is true, keeping items in escrow only.

// Announces "Trade complete, N scrap remaining" after DoTrade when not managed.
[HarmonyPatch(typeof(VendorScreen), "DoTrade")]
class VendorScreen_DoTrade_Patch  (line 51)
    [HarmonyPostfix]
    public static void Postfix()  (line 54)

// Announces "Trade failed" via SpeakInterrupt when Escrow.NotifyTradeFailed fires.
[HarmonyPatch(typeof(Escrow), "NotifyTradeFailed")]
class Escrow_NotifyTradeFailed_Patch  (line 66)
    [HarmonyPostfix]
    public static void Postfix()  (line 69)

// Announces "Added to sell/buy: ItemName" when an item is moved to escrow; suppressed when ShopState is managing.
[HarmonyPatch(typeof(VND_DragDropItem), "AttemptToTrade")]
class VND_DragDropItem_AttemptToTrade_Patch  (line 82)
    [HarmonyPostfix]
    public static void Postfix(VND_DragDropItem __instance)  (line 85)
