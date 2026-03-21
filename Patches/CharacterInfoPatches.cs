using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Patches to make the C key open CharacterInfoMenu to the Attributes tab
    /// instead of the default Inventory tab. Since both C and I are mapped to
    /// the same "Character" input event, we check the raw key state to determine
    /// which key triggered the menu opening.
    /// </summary>
    public static class CharacterInfoPatches
    {
        /// <summary>
        /// Prefix on GUIManager.ToggleCharacterInfoMenu — sets the openToAttributes flag
        /// if the C key is currently held (as opposed to the I key).
        /// </summary>
        [HarmonyPatch(typeof(GUIManager), "ToggleCharacterInfoMenu")]
        public class GUIManager_ToggleCharacterInfoMenu_Patch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                // Check if C key is pressed (not I) — both map to "Character" input
                // If C is pressed, we want to open to Attributes tab
                if (Input.GetKey(KeyCode.C) && !Input.GetKey(KeyCode.I))
                {
                    CharacterInfoState.openToAttributes = true;
                }
            }
        }

        /// <summary>
        /// Postfix on CharacterInfoMenu.OnEnable — switches to Attributes tab
        /// if the openToAttributes flag was set by the prefix above.
        /// </summary>
        [HarmonyPatch(typeof(CharacterInfoMenu), "OnEnable")]
        public class CharacterInfoMenu_OnEnable_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(CharacterInfoMenu __instance)
            {
                if (CharacterInfoState.openToAttributes)
                {
                    CharacterInfoState.openToAttributes = false;
                    __instance.ShowPanel(CharacterInfoMenu.InfoPanel.Attributes);
                    MelonLogger.Msg("[CharacterInfoPatches] Opened to Attributes tab (C key)");
                }
            }
        }
    }
}
