using System.Collections.Generic;
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
        private static readonly List<string> combatLog = new List<string>();
        private const int MAX_LOG_ENTRIES = 100;

        public static List<string> CombatLog => combatLog;

        // Monotonic count of description lines actually announced. Used by
        // MapCursorState to detect when an examine produced no description text
        // (the game's ExamineDescriptionObject path falls through to DEFAULTCASE,
        // which yields nothing for EXAMINE) so it can say "No description available"
        // instead of leaving the user with silence. Emission is synchronous with
        // the examine call (Drama.descriptionText -> Action_DescriptionText), so a
        // before/after comparison around the call is reliable.
        private static int announcedCount;
        public static int AnnouncedCount => announcedCount;

        public static void ClearLog()
        {
            combatLog.Clear();
        }

        [HarmonyPostfix]
        public static void Postfix(string newText, HUD_Controller.TextType textType, bool hasAudio)
        {
            try
            {
                // Skip empty text
                if (string.IsNullOrEmpty(newText)) return;

                // During active conversations, ConversationPatches.AddText handles dialogue text
                // to avoid duplicate announcements (both patches fire from EmitToTextWindow)
                if (Drama.isConversationOn && textType == HUD_Controller.TextType.Conversation)
                {
                    return;
                }

                // Clean and speak the text
                string cleanedText = UITextExtractor.CleanText(newText);
                if (string.IsNullOrEmpty(cleanedText)) return;

                // Add context based on text type
                string prefix = GetTextTypePrefix(textType);
                string announcement = string.IsNullOrEmpty(prefix)
                    ? cleanedText
                    : $"{prefix}: {cleanedText}";

                MelonLogger.Msg($"Description text ({textType}): {cleanedText}");

                announcedCount++;

                // Store in combat log for review
                combatLog.Add(announcement);
                if (combatLog.Count > MAX_LOG_ENTRIES)
                    combatLog.RemoveAt(0);

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
