using HarmonyLib;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for detecting UI focus changes
    /// </summary>
    public static class UIFocusPatches
    {
        private static string lastSpokenText = "";
        private static string lastSource = "";

        /// <summary>
        /// Handles focus change events from UI hooks
        /// </summary>
        internal static void HandleFocusChange(GameObject go, string source)
        {
            // Skip non-interactive visual elements (backgrounds, sprites, etc.)
            if (!UITextExtractor.IsInteractiveElement(go))
            {
                return;
            }

            string text = UITextExtractor.ExtractUIText(go);

            // Avoid double-speaking if both hooks fire for same element
            if (text == lastSpokenText && source != lastSource)
            {
                return;
            }

            lastSpokenText = text;
            lastSource = source;

            if (!string.IsNullOrEmpty(text))
            {
                ScreenReaderManager.Speak(text, interrupt: false);
            }
        }
    }

    /// <summary>
    /// Harmony patch to detect UI focus changes via UICamera.SetSelection
    /// Patches: protected static void SetSelection(GameObject go, ControlScheme scheme)
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "SetSelection")]
    public class UICamera_SetSelection_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {
            if (go == null) return;
            UIFocusPatches.HandleFocusChange(go, "SetSelection");
        }
    }

    /// <summary>
    /// Harmony patch to detect UI focus changes via UICamera.Notify
    /// Patches: public static void Notify(GameObject go, string funcName, object obj)
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "Notify")]
    public class UICamera_Notify_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go, string funcName, object obj)
        {
            // Only handle OnSelect(true) - when element gains focus
            if (funcName == "OnSelect" && obj is bool selected && selected && go != null)
            {
                UIFocusPatches.HandleFocusChange(go, "Notify");
            }
        }
    }
}
