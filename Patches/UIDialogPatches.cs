using HarmonyLib;
using System;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patch to announce confirmation dialog content when it appears
    /// Patches: public virtual void SetMessage(string title, string msg, string buttonYesLabel, ModalButtonDelegate yesButtonDelegate, string buttonNoLabel, ModalButtonDelegate noButtonDelegate)
    /// </summary>
    [HarmonyPatch(typeof(ModalMessageMenu), "SetMessage", new Type[] {
        typeof(string), typeof(string), typeof(string),
        typeof(ModalMessageMenu.ModalButtonDelegate), typeof(string),
        typeof(ModalMessageMenu.ModalButtonDelegate)
    })]
    public class ModalMessageMenu_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string title, string msg, string buttonYesLabel, string buttonNoLabel)
        {
            // Build the full announcement: title + message + available options
            string announcement = "";

            if (!string.IsNullOrEmpty(title))
            {
                announcement += UITextExtractor.CleanText(title) + ". ";
            }

            if (!string.IsNullOrEmpty(msg))
            {
                announcement += UITextExtractor.CleanText(msg) + ". ";
            }

            // Announce available options
            if (!string.IsNullOrEmpty(buttonYesLabel))
            {
                announcement += "Options: " + buttonYesLabel;
                if (!string.IsNullOrEmpty(buttonNoLabel))
                {
                    announcement += " or " + buttonNoLabel;
                }
            }

            ScreenReaderManager.Speak(announcement, interrupt: false);
        }
    }
}
