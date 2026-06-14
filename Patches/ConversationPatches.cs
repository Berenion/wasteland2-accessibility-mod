using HarmonyLib;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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
    // PATCH 0: Hook BubbleTextManager.Print to track whether current line has VO
    // ============================================================================
    [HarmonyPatch(typeof(BubbleTextManager), "Print", new Type[] {
        typeof(BubbleTextKind), typeof(GameObject), typeof(string), typeof(string),
        typeof(GameObject), typeof(float), typeof(BubbleTextManager.NotifyBubbleText),
        typeof(string), typeof(bool), typeof(Texture2D), typeof(Texture2D), typeof(bool)
    })]
    public class BubbleTextManager_Print_Patch
    {
        /// <summary>
        /// True if the most recently printed bubble text has voiceover audio.
        /// Reset each time Print() is called. Consumed by AddText patch.
        /// </summary>
        public static bool LastPrintHadAudio { get; private set; }

        /// <summary>
        /// The BubbleTextKind of the most recently printed bubble text.
        /// </summary>
        public static BubbleTextKind LastPrintTextKind { get; private set; }

        /// <summary>
        /// True if the bubble about to emit shows a "click anywhere to continue" prompt —
        /// i.e. the game is gating the conversation on the player advancing it. Only known
        /// at emit time (AddClickToContinue runs after Print), so it's set from the
        /// EmitToTextWindow prefix; the Print postfix leaves it false.
        /// </summary>
        public static bool LastPrintHadClickToContinue { get; private set; }

        /// <summary>
        /// Records the textKind, whether the audio name maps to a real audio file, and
        /// whether the bubble gates on click-to-continue, so the AddText patch can classify
        /// the line. Shared by the Print postfix and the EmitToTextWindow prefix.
        /// </summary>
        public static void SetMetadata(BubbleTextKind textKind, string audioName, bool hasClickToContinue = false)
        {
            LastPrintTextKind = textKind;
            LastPrintHadClickToContinue = hasClickToContinue;
            // audioName "__" is a placeholder used when no actual voice file exists
            bool hasAudioName = !string.IsNullOrEmpty(audioName) && audioName.Length > 0 && audioName != "__";

            // Even if audioName is set, verify the audio file actually exists in the audio system.
            // Many Director's Cut lines have audioName entries but no actual audio files.
            if (hasAudioName)
            {
                try
                {
                    LastPrintHadAudio = AudioManager.IsValidAudioID(audioName);
                    if (!LastPrintHadAudio)
                    {
                        ModLog.Debug($"[BubbleTextPrint] Audio ID '{audioName}' not found in audio system — treating as unvoiced");
                    }
                }
                catch
                {
                    // If AudioManager isn't ready, fall back to trusting the audioName
                    LastPrintHadAudio = true;
                }
            }
            else
            {
                LastPrintHadAudio = false;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(BubbleTextKind textKind, string audioName)
        {
            SetMetadata(textKind, audioName);

            if (textKind == BubbleTextKind.Conversation ||
                textKind == BubbleTextKind.DescConversation ||
                textKind == BubbleTextKind.DescPercConversation ||
                textKind == BubbleTextKind.AsciiArtConversation ||
                textKind == BubbleTextKind.AudioConversation)
            {
                ModLog.Debug($"[BubbleTextPrint] textKind={textKind}, hasAudio={LastPrintHadAudio}, audioName={audioName}");
            }
        }
    }

    // ============================================================================
    // PATCH 0b: Refresh the per-line metadata from the actual bubble right before it
    // emits to the conversation window.
    //
    // Print() only CREATES the BubbleTextInfo and queues it; the ConversationHUD.AddText
    // call happens later, from BubbleTextInfo.EmitToTextWindow() when the bubble is shown.
    // When the game Prints several bubbles before any are emitted (e.g. at conversation
    // start it Prints the NPC's description bubble AND the first voiced line back-to-back),
    // the single LastPrint* globals get clobbered by the last Print, so the earlier line's
    // AddText reads the wrong textKind/audio. EmitToTextWindow calls AddText synchronously,
    // so setting the metadata from `this` here guarantees AddText sees the correct line.
    // ============================================================================
    [HarmonyPatch(typeof(BubbleTextManager.BubbleTextInfo), "EmitToTextWindow")]
    public class BubbleTextInfo_EmitToTextWindow_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(BubbleTextManager.BubbleTextInfo __instance)
        {
            if (__instance == null) return;
            BubbleTextManager_Print_Patch.SetMetadata(
                __instance.textKind, __instance.audioName, __instance.hasClickToContinue);
        }
    }

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

        // Cancellation token for the voiceover-wait coroutine, mirroring
        // ConversationState.announcementGeneration. On multi-step dialogue (press Enter
        // to advance), each new line — or an explicit skip — supersedes the previous
        // line's pending read so stale subtitles don't fire late / out of order / doubled.
        private static int speakGeneration = 0;

        // Separate cancellation token for a deferred character/scene description. Kept
        // distinct from speakGeneration so a description deferred behind a voiced line
        // doesn't cancel (or get cancelled by) that line's own subtitle read.
        private static int descriptionGeneration = 0;

        /// <summary>
        /// Cancels any pending voiceover-wait subtitle read. Called when the player
        /// skips/advances the current line so the skipped line isn't spoken afterwards.
        /// </summary>
        public static void CancelPendingSpeak()
        {
            speakGeneration++;
        }

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
                    ModLog.Debug($"[Conversation] Skipping ranger say (already read on hover): {cleanedText}");
                    return;
                }

                // Avoid duplicate announcements within 0.5 seconds
                float currentTime = Time.time;
                if (cleanedText == lastAnnouncedText && (currentTime - lastAnnouncedTime) < 0.5f)
                {
                    return;
                }

                // Check this line's voiceover + bubble kind. BubbleTextManager.Print()
                // fires just before AddText() in the Drama pipeline, so LastPrintHadAudio /
                // LastPrintTextKind describe THIS line.
                bool thisLineHasAudio = BubbleTextManager_Print_Patch.LastPrintHadAudio;
                BubbleTextKind thisLineKind = BubbleTextManager_Print_Patch.LastPrintTextKind;
                bool thisLineHasClickToContinue = BubbleTextManager_Print_Patch.LastPrintHadClickToContinue;
                bool thisLineIsDescription =
                    thisLineKind == BubbleTextKind.DescConversation ||
                    thisLineKind == BubbleTextKind.DescPercConversation;

                if (thisLineIsDescription && thisLineHasClickToContinue)
                {
                    // A description bubble that gates the conversation: the game shows
                    // "click anywhere to continue" and will NOT start the next (often voiced)
                    // line until the player advances. That pending voiceover sits BEHIND this
                    // prompt, so deferring the description "until voiceover finishes" deadlocks —
                    // the audio never starts and the player is never told they can advance.
                    // Read it now and announce the prompt. Own generation bump so a later
                    // line's subtitle read doesn't cancel this.
                    int gen = ++speakGeneration;
                    ScreenReaderManager.SpeakDirect(cleanedText);
                    ScreenReaderManager.SpeakDirect("Press Enter to continue");
                    ModLog.Debug("[Conversation] Description gates on click-to-continue — read immediately + prompt");
                }
                else if (!thisLineHasAudio && thisLineIsDescription &&
                    (VoiceoverHelper.IsVoiceoverPlaying() || VoiceoverHelper.HasPendingOrActiveVoicedAudio()))
                {
                    // An unvoiced character/scene description that arrived while the NPC's
                    // voiced line is still playing. Speaking it now lands it on top of the
                    // dialogue ("lands halfway through"). Defer it until the voiceover
                    // finishes. Uses its own generation, so it neither cancels nor is
                    // cancelled by the concurrent voiced line's subtitle read.
                    ModLog.Debug("[Conversation] Description arrived during voiceover — deferring until VO finishes");
                    int descGen = ++descriptionGeneration;
                    MelonCoroutines.Start(SpeakDescriptionAfterVoiceover(cleanedText, descGen));
                }
                else if (!thisLineHasAudio)
                {
                    // No voiceover for this line and nothing voiced playing — speak immediately.
                    int gen = ++speakGeneration;
                    ScreenReaderManager.SpeakDirect(cleanedText);
                    ModLog.Debug($"[Conversation] No VO — speaking immediately");

                    // If description is showing, prompt to continue
                    bool descriptionShowing = VoiceoverHelper.HasActiveDescriptionBubbles();
                    if (Drama.isConversationOn && !Drama.isCutsceneOn && descriptionShowing)
                    {
                        ScreenReaderManager.SpeakDirect("Press Enter to continue");
                    }
                }
                else
                {
                    // This line has voiceover — wait for it to finish, then read the text.
                    int gen = ++speakGeneration;
                    ModLog.Debug($"[Conversation] Has VO — waiting for audio to finish");
                    MelonCoroutines.Start(SpeakAfterVoiceoverFinishes(cleanedText, gen));
                }

                // Update tracking
                lastAnnouncedText = cleanedText;
                lastAnnouncedTime = currentTime;

                ModLog.Debug($"[Conversation] {(rangerSay ? "Ranger" : "NPC")}: {cleanedText}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.AddText patch: {ex.Message}");
            }
        }

        /// <summary>
        /// Polls until voiced audio finishes, then speaks the text.
        /// Handles the case where audio hasn't started yet (pending behind other bubble texts).
        /// Bails without speaking if a newer line or a skip has superseded this read
        /// (generation mismatch), so stale subtitles don't fire on multi-step dialogue.
        /// </summary>
        private static IEnumerator SpeakAfterVoiceoverFinishes(string text, int generation)
        {
            // Wait until no voiced audio is playing or pending
            float maxWait = 30f; // Safety timeout
            float waited = 0f;
            while (waited < maxWait)
            {
                if (generation != speakGeneration) yield break;
                if (!VoiceoverHelper.IsVoiceoverPlaying() && !VoiceoverHelper.HasPendingOrActiveVoicedAudio())
                {
                    break;
                }
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }

            if (generation != speakGeneration) yield break;

            // Small extra delay for natural pacing after audio stops
            yield return new WaitForSeconds(0.3f);

            if (generation != speakGeneration) yield break;

            ScreenReaderManager.SpeakDirect(text);
        }

        /// <summary>
        /// Waits for the active voiceover to finish, then reads a deferred character/scene
        /// description so it doesn't land on top of the NPC's spoken line. Queued (not
        /// interrupting), and followed by the "Press Enter to continue" prompt if the
        /// description bubble is still the one awaiting advance. Bails on a newer description.
        /// </summary>
        private static IEnumerator SpeakDescriptionAfterVoiceover(string text, int generation)
        {
            float maxWait = 30f; // Safety timeout
            float waited = 0f;
            while (waited < maxWait)
            {
                if (generation != descriptionGeneration) yield break;
                if (!VoiceoverHelper.IsVoiceoverPlaying() && !VoiceoverHelper.HasPendingOrActiveVoicedAudio())
                {
                    break;
                }
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }

            if (generation != descriptionGeneration) yield break;

            // Small extra delay for natural pacing after audio stops
            yield return new WaitForSeconds(0.3f);

            if (generation != descriptionGeneration) yield break;

            // Queue (don't interrupt the voiced line's own subtitle read if it speaks too)
            ScreenReaderManager.SpeakDirect(text, false);

            // Prompt to continue if the description is still the bubble awaiting advance.
            if (Drama.isConversationOn && !Drama.isCutsceneOn && VoiceoverHelper.HasActiveDescriptionBubbles())
            {
                ScreenReaderManager.SpeakDirect("Press Enter to continue", false);
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
                // Skip announcements during conversations - ConversationState handles option navigation
                if (Drama.isConversationOn)
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

                ModLog.Debug($"[Conversation] Button added: {announcement}");
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
                ModLog.Debug($"[Conversation] Button removed: {keywordLabel}");
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
        private static readonly FieldInfo buttonListField =
            typeof(ConversationHUD).GetField("buttonList", BindingFlags.NonPublic | BindingFlags.Instance);

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
                ModLog.Debug($"[Conversation] {announcement}");
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
        private static readonly FieldInfo buttonListField =
            typeof(ConversationHUD).GetField("buttonList", BindingFlags.NonPublic | BindingFlags.Instance);

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
                ModLog.Debug($"[Conversation] Hovering: {announcement}");
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
                ModLog.Debug("[Conversation] Options cleared");
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
                ModLog.Debug($"[Conversation] Started: {npcName}");
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
                ModLog.Debug("[Conversation] Ended");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in ConversationHUD.OnConversationEnd patch: {ex.Message}");
            }
        }
    }
}
