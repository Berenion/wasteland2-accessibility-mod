using HarmonyLib;
using UnityEngine;
using MelonLoader;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for vendor/shop screen accessibility.
    /// Provides screen reader announcements for trade events when
    /// ShopState's managed navigation is not active.
    /// </summary>

    /// <summary>
    /// Patch VendorScreen.SetSellMode to announce mode changes when not managed.
    /// </summary>
    [HarmonyPatch(typeof(VendorScreen), "SetSellMode")]
    public class VendorScreen_SetSellMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VendorScreen __instance, bool sellMode)
        {
            if (ShopState.IsManagedNavigation) return;

            string mode = sellMode ? "Sell mode" : "Buy mode";
            ScreenReaderManager.Speak(mode);
        }
    }

    /// <summary>
    /// Patch VendorScreen.OnTradeButtonClicked to block auto-finalize when managed.
    /// The game calls this after every item move (via OnItemDoubleClicked and OnItemDropped).
    /// When ShopState is managing navigation, we want items to go to escrow only —
    /// the user finalizes explicitly from the Escrow zone.
    /// </summary>
    [HarmonyPatch(typeof(VendorScreen), "OnTradeButtonClicked")]
    public class VendorScreen_OnTradeButtonClicked_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            // Return false = skip the original method when managed
            return !ShopState.IsManagedNavigation;
        }
    }

    /// <summary>
    /// Patch VendorScreen.DoTrade to announce successful trades when not managed.
    /// </summary>
    [HarmonyPatch(typeof(VendorScreen), "DoTrade")]
    public class VendorScreen_DoTrade_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (ShopState.IsManagedNavigation) return;

            int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
            ScreenReaderManager.Speak($"Trade complete, {scrap} scrap remaining");
        }
    }

    /// <summary>
    /// Patch Escrow.NotifyTradeFailed to announce trade failures.
    /// </summary>
    [HarmonyPatch(typeof(Escrow), "NotifyTradeFailed")]
    public class Escrow_NotifyTradeFailed_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            ScreenReaderManager.SpeakInterrupt("Trade failed");
        }
    }

    /// <summary>
    /// Patch VND_DragDropItem.PopulateData to announce items when selected and not managed.
    /// VND_DragDropItem extends INV_DragDropItem, so the base PopulateData patch may already
    /// fire. This patch adds vendor-specific context.
    /// </summary>
    [HarmonyPatch(typeof(VND_DragDropItem), "AttemptToTrade")]
    public class VND_DragDropItem_AttemptToTrade_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VND_DragDropItem __instance)
        {
            if (ShopState.IsManagedNavigation) return;

            ItemInstance item = __instance.GetItem();
            if (item != null)
            {
                string itemName = UITextExtractor.CleanText(
                    Language.Localize(item.template.displayName, false, false, string.Empty));
                string action = __instance.isPlayerOwned ? "Added to sell" : "Added to buy";
                ScreenReaderManager.Speak($"{action}: {itemName}");
            }
        }
    }
}
