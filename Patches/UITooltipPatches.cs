using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for announcing tooltip content
    /// </summary>
    public static class UITooltipPatches
    {
        // Tooltip deduplication - prevent double announcements
        internal static string lastTooltipText = "";
        internal static float lastTooltipTime = 0f;
        internal const float TOOLTIP_DEBOUNCE_TIME = 0.5f; // Prevent same tooltip within 500ms
    }

    /// <summary>
    /// Harmony patch to announce UITooltip content
    /// Patches: protected virtual void SetText(string tooltipText)
    /// </summary>
    [HarmonyPatch(typeof(UITooltip), "SetText")]
    public class UITooltip_SetText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UITooltip __instance, string tooltipText)
        {
            if (__instance == null || string.IsNullOrEmpty(tooltipText)) return;

            // Clean and announce the tooltip text
            string cleanedText = UITextExtractor.CleanText(tooltipText);

            if (!string.IsNullOrEmpty(cleanedText))
            {
                MelonLogger.Msg($"[Tooltip] {cleanedText}");
                // Tooltips should NOT interrupt - they're informational
                ScreenReaderManager.Speak(cleanedText, interrupt: false);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce TextTooltip content
    /// Patches: public void SetText(string text)
    /// </summary>
    [HarmonyPatch(typeof(TextTooltip), "SetText")]
    public class TextTooltip_SetText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TextTooltip __instance, string text)
        {
            if (__instance == null || string.IsNullOrEmpty(text)) return;

            // Clean and announce the tooltip text
            string cleanedText = UITextExtractor.CleanText(text);

            if (!string.IsNullOrEmpty(cleanedText))
            {
                MelonLogger.Msg($"[TextTooltip] {cleanedText}");
                // Tooltips should NOT interrupt - they're informational
                ScreenReaderManager.Speak(cleanedText, interrupt: false);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce tooltips when they're set as the active popup
    /// Patches: public void SetPopup(GameObject popup, GameObject messageTarget = null)
    /// This catches tooltips created by TooltipCreator components (like in difficulty selection)
    /// </summary>
    [HarmonyPatch(typeof(TooltipManager), "SetPopup")]
    public class TooltipManager_SetPopup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject popup)
        {
            if (popup == null) return;

            string tooltipText = null;

            // Try to get TextTooltip component
            TextTooltip textTooltip = popup.GetComponent<TextTooltip>();
            if (textTooltip != null && textTooltip.label != null)
            {
                tooltipText = textTooltip.label.text;
            }

            // Try to get ItemInfoTooltip component if no TextTooltip
            if (string.IsNullOrEmpty(tooltipText))
            {
                ItemInfoTooltip itemTooltip = popup.GetComponent<ItemInfoTooltip>();
                if (itemTooltip != null && itemTooltip.nameLabel != null)
                {
                    tooltipText = itemTooltip.nameLabel.text;
                }
            }

            // Announce if we found tooltip text
            if (!string.IsNullOrEmpty(tooltipText))
            {
                string cleanedText = UITextExtractor.CleanText(tooltipText);
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    // Deduplication: don't announce same tooltip within debounce time
                    float currentTime = Time.unscaledTime;
                    bool isDifferentTooltip = cleanedText != UITooltipPatches.lastTooltipText;
                    bool enoughTimePassed = (currentTime - UITooltipPatches.lastTooltipTime) >= UITooltipPatches.TOOLTIP_DEBOUNCE_TIME;

                    if (isDifferentTooltip || enoughTimePassed)
                    {
                        UITooltipPatches.lastTooltipText = cleanedText;
                        UITooltipPatches.lastTooltipTime = currentTime;

                        // Tooltips should NOT interrupt - they're informational
                        ScreenReaderManager.Speak(cleanedText, interrupt: false);
                    }
                }
            }
        }
    }
}
