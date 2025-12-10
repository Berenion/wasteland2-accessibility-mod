using HarmonyLib;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patch to announce toggle (checkbox) state changes
    /// Patches: private void Set(bool state)
    /// </summary>
    [HarmonyPatch(typeof(UIToggle), "Set")]
    public class UIToggle_Set_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UIToggle __instance, bool state)
        {
            if (__instance == null) return;

            // Only announce if this toggle is part of the currently focused UI
            GameObject toggleGO = __instance.gameObject;

            // Get the toggle's label
            UILabel label = toggleGO.GetComponentInChildren<UILabel>();
            string labelText = "";
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = UITextExtractor.CleanText(label.text);
            }
            else
            {
                // Fallback to GameObject name
                labelText = toggleGO.name;
            }

            // Announce the toggle state
            string stateText = state ? "On" : "Off";
            string announcement = $"{labelText}: {stateText}";

            ScreenReaderManager.Speak(announcement, interrupt: false);
        }
    }
}
