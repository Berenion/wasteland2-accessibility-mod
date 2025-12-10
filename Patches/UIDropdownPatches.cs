using HarmonyLib;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patch to announce dropdown menu options as they're highlighted
    /// Patches: private void Highlight(UILabel lbl, bool instant)
    /// </summary>
    [HarmonyPatch(typeof(UIPopupList), "Highlight")]
    public class UIPopupList_Highlight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILabel lbl)
        {
            if (lbl != null && !string.IsNullOrEmpty(lbl.text))
            {
                string cleanedText = UITextExtractor.CleanText(lbl.text);
                ScreenReaderManager.Speak(cleanedText, interrupt: false);
            }
        }
    }
}
