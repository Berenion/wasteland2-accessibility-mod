using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation for conversation dialogue options.
    /// Reads ConversationHUD.buttonList via reflection and provides
    /// Up/Down/Enter navigation independently of the game's UI focus system.
    /// </summary>
    public class ConversationState : AccessibilityStateBase
    {
        public override string Name => "Conversation";
        public override int Priority => 52;

        /// <summary>
        /// When true, passive ConversationPatches should not announce
        /// button additions or hover events (this state handles announcements).
        /// </summary>
        public static bool IsManagingNavigation { get; private set; }

        private int selectedIndex = -1;
        private readonly List<ConversationOption> currentOptions = new List<ConversationOption>();
        private int lastKnownButtonCount = 0;

        // Used to cancel pending queued announcements when user navigates manually
        private static int announcementGeneration = 0;

        // Reflection cache
        private static FieldInfo buttonListField;
        private static bool fieldsCached = false;

        private class ConversationOption
        {
            public string KeywordLabel;
            public string DisplayText;
            public string FullResponseText;
            public string SkillInfo;
            public bool IsGoodbye;
            public bool IsUnavailable;
            public GameObject ButtonObject;
        }

        /// <summary>
        /// True when in ForAdvance state (voiceover/text displaying, Enter to skip)
        /// </summary>
        private bool IsInAdvanceMode
        {
            get
            {
                // Note: Do NOT check Drama.isCutsceneOn here. Many conversations use
                // cutsceneStart() to freeze party movement while the conversation is active
                // (e.g. AZ10_RoadBlock toll shakedown). The WaitState checks are sufficient
                // to determine if the game needs player input.
                if (!Drama.isConversationOn) return false;
                if (!MonoBehaviourSingleton<ConversationHUD>.HasInstance()) return false;
                return DramaGUI.waitState == DramaGUI.WaitState.ForAdvance;
            }
        }

        /// <summary>
        /// True when in ForInput state with options available (full navigation)
        /// </summary>
        private bool IsInInputMode
        {
            get
            {
                if (!Drama.isConversationOn) return false;
                if (!MonoBehaviourSingleton<ConversationHUD>.HasInstance()) return false;
                if (DramaGUI.waitState != DramaGUI.WaitState.ForInput) return false;

                var hud = MonoBehaviourSingleton<ConversationHUD>.GetInstance();
                if (hud == null) return false;

                // Don't activate while "Click to Continue" is showing
                if (hud.clickToContinue != null && hud.clickToContinue.activeSelf) return false;

                // Don't activate while description bubble text is active (1-frame race fix)
                if (VoiceoverHelper.HasActiveDescriptionBubbles()) return false;

                int count = GetButtonCount();
                return count > 0;
            }
        }

        public override bool IsActive
        {
            get
            {
                // Yield to ShopState when the vendor screen is open — the game keeps
                // the conversation panel active behind the shop UI, but ShopState
                // should handle all input while the shop is visible.
                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsVendorScreenOpen())
                    return false;

                return IsInAdvanceMode || IsInInputMode;
            }
        }

        public override bool HandleInput()
        {
            // === Advance mode: voiceover/text playing, Enter to skip ===
            if (IsInAdvanceMode)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SkipCurrentDialogue();
                    InputSuppressor.ShouldSuppressGameInput = true;
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    return true;
                }

                // Suppress UI navigation during voiceover to prevent interference
                if (Input.anyKeyDown)
                {
                    InputSuppressor.ShouldSuppressUINavigation = true;
                }

                return false;
            }

            // === Input mode: full option navigation ===
            RefreshOptions();

            if (currentOptions.Count == 0) return false;

            // Check if button list changed (new options appeared)
            if (currentOptions.Count != lastKnownButtonCount)
            {
                lastKnownButtonCount = currentOptions.Count;
                // If new buttons appeared and we had no selection, select first
                if (selectedIndex < 0)
                {
                    selectedIndex = 0;
                    AnnounceCurrentOptionQueued();
                }
            }

            // Down arrow - next option
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedIndex = (selectedIndex + 1) % currentOptions.Count;
                AnnounceCurrentOption();
                InputSuppressor.ShouldSuppressUINavigation = true;
                return true;
            }

            // Up arrow - previous option
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedIndex--;
                if (selectedIndex < 0) selectedIndex = currentOptions.Count - 1;
                AnnounceCurrentOption();
                InputSuppressor.ShouldSuppressUINavigation = true;
                return true;
            }

            // Enter - select current option
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SelectCurrentOption();
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressUINavigation = true;
                return true;
            }

            // Home - jump to first option
            if (Input.GetKeyDown(KeyCode.Home))
            {
                selectedIndex = 0;
                AnnounceCurrentOption();
                return true;
            }

            // End - jump to last option
            if (Input.GetKeyDown(KeyCode.End))
            {
                selectedIndex = currentOptions.Count - 1;
                AnnounceCurrentOption();
                return true;
            }

            // Suppress game input processing of Enter key during conversations
            // to prevent the text input pane from capturing keystrokes
            if (Input.anyKeyDown)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
            }

            return false;
        }

        public override void OnActivated()
        {
            IsManagingNavigation = true;
            selectedIndex = 0;
            lastKnownButtonCount = 0;

            // Only refresh options if in input mode (buttons exist)
            if (IsInInputMode)
            {
                RefreshOptions();

                if (currentOptions.Count > 0)
                {
                    lastKnownButtonCount = currentOptions.Count;
                    // Queue the first option so it plays after any pending subtitle TTS
                    AnnounceCurrentOptionQueued();
                }

                MelonLogger.Msg($"[ConversationState] Activated with {currentOptions.Count} options");
            }
            else
            {
                MelonLogger.Msg("[ConversationState] Activated in advance mode (Enter to skip)");
            }
        }

        public override void OnDeactivated()
        {
            IsManagingNavigation = false;
            selectedIndex = -1;
            currentOptions.Clear();
            lastKnownButtonCount = 0;

            base.OnDeactivated();
        }

        private void EnsureFieldsCached()
        {
            if (fieldsCached) return;

            buttonListField = typeof(ConversationHUD).GetField("buttonList",
                BindingFlags.NonPublic | BindingFlags.Instance);

            fieldsCached = true;

            if (buttonListField == null)
            {
                MelonLogger.Error("[ConversationState] Failed to cache buttonList field");
            }
        }

        private int GetButtonCount()
        {
            EnsureFieldsCached();
            if (buttonListField == null) return 0;

            var hud = MonoBehaviourSingleton<ConversationHUD>.GetInstance();
            if (hud == null) return 0;

            var list = buttonListField.GetValue(hud) as IList;
            return list != null ? list.Count : 0;
        }

        private void RefreshOptions()
        {
            currentOptions.Clear();

            EnsureFieldsCached();
            if (buttonListField == null) return;

            var hud = MonoBehaviourSingleton<ConversationHUD>.GetInstance();
            if (hud == null) return;

            var buttonList = buttonListField.GetValue(hud) as IList;
            if (buttonList == null) return;

            foreach (var btnInfoObj in buttonList)
            {
                if (btnInfoObj == null) continue;

                try
                {
                    var btnInfo = (ConversationHUD.ButtonInfo)btnInfoObj;

                    var option = new ConversationOption
                    {
                        KeywordLabel = btnInfo.keywordLabel,
                        ButtonObject = btnInfo.gobButton,
                        IsGoodbye = (btnInfo.keywordLabel == "Goodbye"),
                    };

                    // Get display text from button label
                    if (btnInfo.gobButton != null)
                    {
                        UILabel label = btnInfo.gobButton.GetComponentInChildren<UILabel>();
                        if (label != null)
                        {
                            option.DisplayText = UITextExtractor.CleanText(label.text);
                        }
                    }

                    if (string.IsNullOrEmpty(option.DisplayText))
                    {
                        option.DisplayText = UITextExtractor.CleanText(btnInfo.keywordLabel);
                    }

                    // Get full response text (what the ranger will say)
                    if (!string.IsNullOrEmpty(btnInfo.sayRangerText))
                    {
                        option.FullResponseText = UITextExtractor.CleanText(btnInfo.sayRangerText);
                    }

                    // Get skill info
                    if (btnInfo.keywordInfo != null && btnInfo.keywordInfo.isSkill)
                    {
                        string skillName = UITextExtractor.CleanText(btnInfo.keywordInfo.skillDisplayName);
                        int required = btnInfo.keywordInfo.skillRequired;
                        int player = btnInfo.keywordInfo.skillPlayer;

                        if (!string.IsNullOrEmpty(skillName))
                        {
                            if (player >= required)
                            {
                                option.SkillInfo = $"{skillName} level {required}";
                            }
                            else
                            {
                                option.SkillInfo = $"{skillName} level {required} required, unavailable";
                                option.IsUnavailable = true;
                            }
                        }
                    }

                    currentOptions.Add(option);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ConversationState] Error reading button info: {ex.Message}");
                }
            }

            // Clamp selected index to valid range
            if (selectedIndex >= currentOptions.Count)
            {
                selectedIndex = currentOptions.Count - 1;
            }
            if (selectedIndex < 0 && currentOptions.Count > 0)
            {
                selectedIndex = 0;
            }
        }

        /// <summary>
        /// Skips current voiceover/text by flushing the current bubble text.
        /// Stops audio and advances the conversation.
        /// </summary>
        private void SkipCurrentDialogue()
        {
            if (MonoBehaviourSingleton<BubbleTextManager>.HasInstance())
            {
                MonoBehaviourSingleton<BubbleTextManager>.GetInstance().FlushCurrentBark();
                ScreenReaderManager.SpeakInterrupt("Skipped");
                MelonLogger.Msg("[ConversationState] Skipped current dialogue");
            }
        }

        /// <summary>
        /// Announces the current option with queued speech (doesn't interrupt).
        /// If voiceover is still playing, polls until it finishes before speaking.
        /// Used for auto-focus when options first appear.
        /// </summary>
        private void AnnounceCurrentOptionQueued()
        {
            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count) return;
            string announcement = BuildOptionAnnouncement(currentOptions[selectedIndex]);

            if (VoiceoverHelper.IsVoiceoverPlaying() || VoiceoverHelper.HasPendingOrActiveVoicedAudio())
            {
                int gen = ++announcementGeneration;
                MelonCoroutines.Start(SpeakOptionAfterVoiceover(announcement, gen));
            }
            else
            {
                // No voiceover — queue with Tolk (plays after any current TTS)
                ScreenReaderManager.SpeakDirect(announcement, false);
            }
        }

        /// <summary>
        /// Announces the current option with interrupt (cuts off current speech).
        /// Used for manual navigation (Up/Down/Home/End).
        /// </summary>
        private void AnnounceCurrentOption()
        {
            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count) return;
            announcementGeneration++; // Cancel any pending queued announcement
            string announcement = BuildOptionAnnouncement(currentOptions[selectedIndex]);
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        /// <summary>
        /// Polls until voiceover finishes, then speaks the option announcement.
        /// Cancels itself if the user navigates manually (generation mismatch).
        /// </summary>
        private static IEnumerator SpeakOptionAfterVoiceover(string text, int generation)
        {
            float maxWait = 30f;
            float waited = 0f;
            while (waited < maxWait)
            {
                if (announcementGeneration != generation) yield break;
                if (!VoiceoverHelper.IsVoiceoverPlaying() && !VoiceoverHelper.HasPendingOrActiveVoicedAudio())
                    break;
                yield return new WaitForSeconds(0.2f);
                waited += 0.2f;
            }
            if (announcementGeneration != generation) yield break;
            yield return new WaitForSeconds(0.3f);
            if (announcementGeneration != generation) yield break;
            // Queue with Tolk (don't interrupt any subtitle TTS that may be speaking)
            ScreenReaderManager.SpeakDirect(text, false);
        }

        private string BuildOptionAnnouncement(ConversationOption opt)
        {
            // Build announcement: "response text, skill info, 1 of 3"
            string announcement;

            // Prefer full response text over keyword label
            if (!string.IsNullOrEmpty(opt.FullResponseText))
                announcement = opt.FullResponseText;
            else
                announcement = opt.DisplayText;

            // Add skill info
            if (!string.IsNullOrEmpty(opt.SkillInfo))
            {
                announcement += $", {opt.SkillInfo}";
            }

            // Add goodbye context
            if (opt.IsGoodbye)
            {
                announcement += ", ends conversation";
            }

            announcement += $", {selectedIndex + 1} of {currentOptions.Count}";
            return announcement;
        }

        private void SelectCurrentOption()
        {
            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count) return;

            var opt = currentOptions[selectedIndex];

            if (opt.IsUnavailable)
            {
                ScreenReaderManager.SpeakInterrupt("This option is unavailable");
                return;
            }

            if (opt.ButtonObject == null)
            {
                MelonLogger.Warning("[ConversationState] Selected option has null button object");
                return;
            }

            // Call OnTopicPressed on the ConversationHUD with the button's GameObject
            var hud = MonoBehaviourSingleton<ConversationHUD>.GetInstance();
            if (hud != null)
            {
                hud.OnTopicPressed(opt.ButtonObject);
            }

            // Reset state for next set of options
            selectedIndex = 0;
            lastKnownButtonCount = 0;

            MelonLogger.Msg($"[ConversationState] Selected: {opt.KeywordLabel}");
        }
    }
}
