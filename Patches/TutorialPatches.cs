using HarmonyLib;
using MelonLoader;
using System;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for tutorial system accessibility
    /// Patches TUT_TutorialPopup to announce tutorial content
    /// </summary>

    // ============================================================================
    // PATCH 1: Hook PopulateData to announce tutorial title and message
    // ============================================================================
    [HarmonyPatch(typeof(TUT_TutorialPopup), "PopulateData")]
    public class TUT_TutorialPopup_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TUT_TutorialPopup __instance, TutorialEntryTemplate template)
        {
            try
            {
                if (template == null)
                {
                    return;
                }

                // Get the title from titleLabel
                string title = null;
                if (__instance.titleLabel != null && !string.IsNullOrEmpty(__instance.titleLabel.text))
                {
                    title = UITextExtractor.CleanText(__instance.titleLabel.text);
                }

                // Get the message from messageLabel
                string message = null;
                if (__instance.messageLabel != null && !string.IsNullOrEmpty(__instance.messageLabel.text))
                {
                    message = UITextExtractor.CleanText(__instance.messageLabel.text);
                }

                // Build the announcement
                string announcement = "Tutorial";

                if (!string.IsNullOrEmpty(title))
                {
                    announcement += $": {title}";
                }

                if (!string.IsNullOrEmpty(message))
                {
                    announcement += $". {message}";
                }

                // Check if there's a next page
                if (!string.IsNullOrEmpty(template.nextPageName))
                {
                    announcement += ". Press Enter or A button for next page";
                }
                else
                {
                    announcement += ". Press Enter or A button to close";
                }

                // Announce the tutorial
                ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[Tutorial] {title}: {message}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TUT_TutorialPopup.PopulateData patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 2: Hook OnOkayClicked to announce when tutorial is closed
    // ============================================================================
    [HarmonyPatch(typeof(TUT_TutorialPopup), "OnOkayClicked")]
    public class TUT_TutorialPopup_OnOkayClicked_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(TUT_TutorialPopup __instance)
        {
            try
            {
                // Check if going to next page or closing
                if (__instance.tutorial != null && !string.IsNullOrEmpty(__instance.tutorial.nextPageName))
                {
                    // Will go to next page, don't announce closure
                    var nextTutorial = MonoBehaviourSingleton<TutorialManager>.GetInstance()
                        .GetTutorial(__instance.tutorial.nextPageName);

                    if (nextTutorial != null && !__instance.checkbox.value)
                    {
                        // Next page exists and tutorials not disabled, don't announce
                        return;
                    }
                }

                // Tutorial is closing
                ScreenReaderManager.Speak("Tutorial closed");
                MelonLogger.Msg("[Tutorial] Closed");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TUT_TutorialPopup.OnOkayClicked patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 3: Hook OnCheckboxToggled to announce when tutorials are disabled
    // ============================================================================
    [HarmonyPatch(typeof(TUT_TutorialPopup), "OnCheckboxToggled")]
    public class TUT_TutorialPopup_OnCheckboxToggled_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TUT_TutorialPopup __instance)
        {
            try
            {
                if (__instance.checkbox != null)
                {
                    if (__instance.checkbox.value)
                    {
                        ScreenReaderManager.Speak("Tutorials disabled");
                        MelonLogger.Msg("[Tutorial] Tutorials disabled");
                    }
                    else
                    {
                        ScreenReaderManager.Speak("Tutorials enabled");
                        MelonLogger.Msg("[Tutorial] Tutorials enabled");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in TUT_TutorialPopup.OnCheckboxToggled patch: {ex.Message}");
            }
        }
    }
}
