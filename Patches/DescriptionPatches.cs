using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for reading examine/description text aloud
    /// </summary>
    [HarmonyPatch(typeof(HUD_Controller), "QueueTextDescription")]
    public class HUD_Controller_QueueTextDescription_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string newText, HUD_Controller.TextType textType, bool hasAudio)
        {
            try
            {
                // Skip empty text
                if (string.IsNullOrEmpty(newText)) return;

                // Skip if audio is already playing for this text (game handles it)
                // Actually, we want to speak even if hasAudio is true, since our screen reader
                // is separate from the game's audio system

                // Clean and speak the text
                string cleanedText = UITextExtractor.CleanText(newText);
                if (string.IsNullOrEmpty(cleanedText)) return;

                // Add context based on text type
                string prefix = GetTextTypePrefix(textType);
                string announcement = string.IsNullOrEmpty(prefix)
                    ? cleanedText
                    : $"{prefix}: {cleanedText}";

                MelonLogger.Msg($"Description text ({textType}): {cleanedText}");

                // Use non-interrupting speech so we don't cut off previous descriptions
                // that might be part of a multi-line examine
                ScreenReaderManager.Speak(announcement);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in QueueTextDescription_Patch: {ex.Message}");
            }
        }

        private static string GetTextTypePrefix(HUD_Controller.TextType textType)
        {
            switch (textType)
            {
                case HUD_Controller.TextType.SkillSuccess:
                    return "Success";
                case HUD_Controller.TextType.SkillFail:
                    return "Failed";
                case HUD_Controller.TextType.CombatDescription:
                    return "Combat";
                case HUD_Controller.TextType.Radio:
                    return "Radio";
                case HUD_Controller.TextType.System:
                    return "System";
                default:
                    return ""; // No prefix for Description, Conversation, etc.
            }
        }
    }
}
