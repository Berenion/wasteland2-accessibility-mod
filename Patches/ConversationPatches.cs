using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for conversation/dialogue system accessibility
    /// Patches ConversationHUD methods to announce dialogue text and response options.
    /// Note: When ConversationState is actively managing navigation, some announcements
    /// are suppressed to avoid duplicate speech output.
    /// </summary>

    // ============================================================================
    // PATCH 1: Hook AddText to read displayed NPC/Player dialogue text
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "AddText", new Type[] {
        typeof(string), typeof(float), typeof(ConversationHUD.NotifyDone),
        typeof(bool), typeof(bool)
    })]
    public class ConversationHUD_AddText_Patch
    {
        // Track last announced text to avoid duplicates
        private static string lastAnnouncedText = "";
        private static float lastAnnouncedTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(string p_value, bool isAppend, bool rangerSay)
        {
            try
            {
                // Skip if appending to existing text (we already announced the main text)
                if (isAppend)
                {
                    return;
                }

                // Clean the text
                string cleanedText = UITextExtractor.CleanText(p_value);

                if (string.IsNullOrEmpty(cleanedText))
                {
                    return;
                }

                // Skip player dialogue - we already read it when hovering on the option
                if (rangerSay)
                {
                    MelonLogger.Msg($"[Conversation] Skipping ranger say (already read on hover): {cleanedText}");
                    return;
                }

                // Check if this text has italic formatting or is gray (indicates descriptive/flavor text)
                bool isDescriptiveText = p_value.Contains("[i]") || p_value.Contains("</i>") ||
                                        p_value.Contains("[222222]") || p_value.Contains("[888888]");

                // If this is NPC dialogue and voiceover is playing,
                // skip TTS announcement for voiced dialogue but keep it for descriptive text
                if (!isDescriptiveText && VoiceoverHelper.IsVoiceoverPlaying())
                {
                    MelonLogger.Msg($"[Conversation] Skipping TTS for voiced dialogue: {cleanedText}");
                    return;
                }

                // Avoid duplicate announcements within 0.5 seconds
                float currentTime = UnityEngine.Time.time;
                if (cleanedText == lastAnnouncedText && (currentTime - lastAnnouncedTime) < 0.5f)
                {
                    return;
                }

                // Build announcement with speaker context
                string announcement;
                if (isDescriptiveText)
                {
                    // Descriptive/flavor text - announce immediately so it's not delayed
                    announcement = cleanedText;
                    ScreenReaderManager.Speak(announcement);
                }
                else
                {
                    // NPC dialogue (unvoiced) - wait for any voiceover to finish
                    announcement = cleanedText;
                    VoiceoverHelper.SpeakWithVoiceoverDelay(announcement, additionalDelay: 0.2f);
                }

                // Update tracking
                lastAnnouncedText = cleanedText;
                lastAnnouncedTime = currentTime;

                MelonLogger.Msg($"[Conversation] {(rangerSay ? "Ranger" : "NPC")}: {cleanedText}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.AddText patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 2: Hook AddButton to read available dialogue response options
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "AddButton")]
    public class ConversationHUD_AddButton_Patch
    {
        // Track announced buttons to avoid duplicates
        private static HashSet<string> announcedButtons = new HashSet<string>();
        private static float lastButtonAnnouncementTime = 0f;
        private static List<string> currentButtons = new List<string>();

        [HarmonyPostfix]
        public static void Postfix(KeywordInfo keywordInfo)
        {
            try
            {
                // Skip announcements when ConversationState is managing navigation
                if (ConversationState.IsManagingNavigation)
                {
                    return;
                }

                if (keywordInfo == null)
                {
                    return;
                }

                // Get the button text
                string buttonText = keywordInfo.text;
                string cleanedText = UITextExtractor.CleanText(buttonText);

                if (string.IsNullOrEmpty(cleanedText))
                {
                    return;
                }

                // Check if this is a new conversation (reset if enough time has passed)
                float currentTime = UnityEngine.Time.time;
                if (currentTime - lastButtonAnnouncementTime > 2.0f)
                {
                    announcedButtons.Clear();
                    currentButtons.Clear();
                }

                // Track this button
                string buttonKey = keywordInfo.id ?? cleanedText;
                if (announcedButtons.Contains(buttonKey))
                {
                    return; // Already announced this button
                }

                announcedButtons.Add(buttonKey);
                currentButtons.Add(cleanedText);
                lastButtonAnnouncementTime = currentTime;

                // Build announcement with context
                string announcement = cleanedText;

                // Add skill requirement information if applicable
                if (keywordInfo.isSkill)
                {
                    string skillName = keywordInfo.skillDisplayName;
                    int required = keywordInfo.skillRequired;
                    int player = keywordInfo.skillPlayer;

                    if (!string.IsNullOrEmpty(skillName))
                    {
                        skillName = UITextExtractor.CleanText(skillName);
                        announcement += $", {skillName}";

                        if (required > 0)
                        {
                            if (player >= required)
                            {
                                announcement += $" level {required}, available";
                            }
                            else
                            {
                                announcement += $" level {required} required, unavailable";
                            }
                        }
                    }
                }

                // Add "Goodbye" context
                if (keywordInfo.id == "Goodbye")
                {
                    announcement += ", ends conversation";
                }

                // Don't interrupt current speech - wait for voiceover to finish, then add delay
                VoiceoverHelper.SpeakWithVoiceoverDelay($"Response option: {announcement}",
                    additionalDelay: 0.5f);

                MelonLogger.Msg($"[Conversation] Button added: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.AddButton patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 3: Hook RemoveButton to track when options are removed
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "RemoveButton")]
    public class ConversationHUD_RemoveButton_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string keywordLabel)
        {
            try
            {
                // Log button removal (useful for debugging)
                MelonLogger.Msg($"[Conversation] Button removed: {keywordLabel}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.RemoveButton patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 4: Hook OnTopicPressed to announce when player selects a response
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "OnTopicPressed")]
    public class ConversationHUD_OnTopicPressed_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ConversationHUD __instance, UnityEngine.GameObject button)
        {
            try
            {
                // Skip selection announcements when ConversationState is managing navigation
                if (ConversationState.IsManagingNavigation)
                {
                    return;
                }

                if (button == null)
                {
                    return;
                }

                // Extract the button text
                UILabel label = button.GetComponentInChildren<UILabel>();
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    return;
                }

                string keywordText = UITextExtractor.CleanText(label.text);
                string fullResponseText = null;
                string skillInfo = null;

                // Find the corresponding ButtonInfo to get the full response text
                try
                {
                    var buttonListField = typeof(ConversationHUD).GetField("buttonList",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (buttonListField != null)
                    {
                        var buttonList = buttonListField.GetValue(__instance) as System.Collections.IList;
                        if (buttonList != null)
                        {
                            foreach (var btnInfo in buttonList)
                            {
                                if (btnInfo == null) continue;

                                var gobButtonField = btnInfo.GetType().GetField("gobButton");
                                var sayRangerTextField = btnInfo.GetType().GetField("sayRangerText");
                                var keywordInfoField = btnInfo.GetType().GetField("keywordInfo");

                                if (gobButtonField != null && keywordInfoField != null)
                                {
                                    var gobButton = gobButtonField.GetValue(btnInfo) as UnityEngine.GameObject;
                                    if (gobButton == button)
                                    {
                                        // Get the full response text
                                        if (sayRangerTextField != null)
                                        {
                                            string rawText = sayRangerTextField.GetValue(btnInfo) as string;
                                            if (!string.IsNullOrEmpty(rawText))
                                            {
                                                fullResponseText = UITextExtractor.CleanText(rawText);
                                            }
                                        }

                                        // Get skill information
                                        var keywordInfo = keywordInfoField.GetValue(btnInfo) as KeywordInfo;
                                        if (keywordInfo != null && keywordInfo.isSkill)
                                        {
                                            string skillName = UITextExtractor.CleanText(keywordInfo.skillDisplayName);
                                            if (!string.IsNullOrEmpty(skillName))
                                            {
                                                skillInfo = skillName;
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not get full response text for selected button: {ex.Message}");
                }

                // Build announcement
                string announcement;
                if (!string.IsNullOrEmpty(fullResponseText))
                {
                    announcement = $"Selected: {fullResponseText}";
                }
                else
                {
                    announcement = $"Selected: {keywordText}";
                }

                if (!string.IsNullOrEmpty(skillInfo))
                {
                    announcement += $", {skillInfo}";
                }

                ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[Conversation] {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.OnTopicPressed patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 5: Hook OnTopicMouseOver to announce when navigating/hovering options
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "OnTopicMouseOver")]
    public class ConversationHUD_OnTopicMouseOver_Patch
    {
        private static string lastHoveredButton = "";
        private static float lastHoverTime = 0f;

        [HarmonyPostfix]
        public static void Postfix(ConversationHUD __instance, UnityEngine.GameObject button)
        {
            try
            {
                // Skip hover announcements when ConversationState is managing navigation
                if (ConversationState.IsManagingNavigation)
                {
                    return;
                }

                if (button == null)
                {
                    return;
                }

                // Extract the button text
                UILabel label = button.GetComponentInChildren<UILabel>();
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    return;
                }

                string keywordText = UITextExtractor.CleanText(label.text);

                // Find the corresponding ButtonInfo to get the full response text
                string fullResponseText = null;
                string skillInfo = null;
                string additionalInfo = null;

                try
                {
                    var buttonListField = typeof(ConversationHUD).GetField("buttonList",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (buttonListField != null)
                    {
                        var buttonList = buttonListField.GetValue(__instance) as System.Collections.IList;
                        if (buttonList != null)
                        {
                            foreach (var btnInfo in buttonList)
                            {
                                if (btnInfo == null) continue;

                                var gobButtonField = btnInfo.GetType().GetField("gobButton");
                                var sayRangerTextField = btnInfo.GetType().GetField("sayRangerText");
                                var keywordInfoField = btnInfo.GetType().GetField("keywordInfo");

                                if (gobButtonField != null && keywordInfoField != null)
                                {
                                    var gobButton = gobButtonField.GetValue(btnInfo) as UnityEngine.GameObject;
                                    if (gobButton == button)
                                    {
                                        // Get the full response text the player will say
                                        if (sayRangerTextField != null)
                                        {
                                            string rawText = sayRangerTextField.GetValue(btnInfo) as string;
                                            if (!string.IsNullOrEmpty(rawText))
                                            {
                                                fullResponseText = UITextExtractor.CleanText(rawText);
                                            }
                                        }

                                        // Get skill information
                                        var keywordInfo = keywordInfoField.GetValue(btnInfo) as KeywordInfo;
                                        if (keywordInfo != null)
                                        {
                                            if (keywordInfo.isSkill)
                                            {
                                                string skillName = UITextExtractor.CleanText(keywordInfo.skillDisplayName);
                                                int required = keywordInfo.skillRequired;
                                                int player = keywordInfo.skillPlayer;

                                                if (!string.IsNullOrEmpty(skillName))
                                                {
                                                    skillInfo = skillName;

                                                    if (required > 0)
                                                    {
                                                        if (player >= required)
                                                        {
                                                            skillInfo += $" level {required}";
                                                        }
                                                        else
                                                        {
                                                            skillInfo += $" level {required} required, unavailable";
                                                        }
                                                    }
                                                }
                                            }

                                            if (keywordInfo.id == "Goodbye")
                                            {
                                                additionalInfo = "ends conversation";
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Could not get full response text for hovered button: {ex.Message}");
                }

                // Build the announcement
                string announcement;

                if (!string.IsNullOrEmpty(fullResponseText))
                {
                    // Use the full response text
                    announcement = fullResponseText;
                }
                else
                {
                    // Fallback to keyword text
                    announcement = keywordText;
                }

                // Add skill information
                if (!string.IsNullOrEmpty(skillInfo))
                {
                    announcement += $", {skillInfo}";
                }

                // Add additional context
                if (!string.IsNullOrEmpty(additionalInfo))
                {
                    announcement += $", {additionalInfo}";
                }

                // Prevent duplicate announcements within 0.3 seconds
                float currentTime = UnityEngine.Time.time;
                if (announcement == lastHoveredButton && (currentTime - lastHoverTime) < 0.3f)
                {
                    return;
                }

                lastHoveredButton = announcement;
                lastHoverTime = currentTime;

                // Announce immediately - this is navigation feedback
                ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[Conversation] Hovering: {announcement}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.OnTopicMouseOver patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 6: Hook Clear to announce when conversation options are cleared
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "Clear")]
    public class ConversationHUD_Clear_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                MelonLogger.Msg("[Conversation] Options cleared");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.Clear patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 7: Hook OnConversationStart to announce conversation beginning
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "OnConversationStart")]
    public class ConversationHUD_OnConversationStart_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ConversationHUD __instance)
        {
            try
            {
                // Get NPC name if available
                string npcName = "";
                if (__instance.npcNameLabel != null && !string.IsNullOrEmpty(__instance.npcNameLabel.text))
                {
                    npcName = UITextExtractor.CleanText(__instance.npcNameLabel.text);
                }

                string announcement = "Conversation started";
                if (!string.IsNullOrEmpty(npcName))
                {
                    announcement += $" with {npcName}";
                }

                VoiceoverHelper.SpeakWithVoiceoverDelay(announcement, additionalDelay: 0.3f);
                MelonLogger.Msg($"[Conversation] Started: {npcName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.OnConversationStart patch: {ex.Message}");
            }
        }
    }

    // ============================================================================
    // PATCH 8: Hook OnConversationEnd to announce conversation ending
    // ============================================================================
    [HarmonyPatch(typeof(ConversationHUD), "OnConversationEnd")]
    public class ConversationHUD_OnConversationEnd_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            try
            {
                ScreenReaderManager.Speak("Conversation ended");
                MelonLogger.Msg("[Conversation] Ended");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.OnConversationEnd patch: {ex.Message}");
            }
        }
    }
}
