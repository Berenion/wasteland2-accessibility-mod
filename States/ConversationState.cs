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
    public class ConversationState : IAccessibilityState
    {
        public string Name => "Conversation";
        public int Priority => 50;

        /// <summary>
        /// When true, passive ConversationPatches should not announce
        /// button additions or hover events (this state handles announcements).
        /// </summary>
        public static bool IsManagingNavigation { get; private set; }

        private int selectedIndex = -1;
        private readonly List<ConversationOption> currentOptions = new List<ConversationOption>();
        private int lastKnownButtonCount = 0;

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

        public bool IsActive
        {
            get
            {
                if (!Drama.isConversationOn || Drama.isCutsceneOn) return false;
                if (!MonoBehaviourSingleton<ConversationHUD>.HasInstance()) return false;

                // Only active when waiting for player input (buttons are enabled)
                if (DramaGUI.waitState != DramaGUI.WaitState.ForInput) return false;

                var hud = MonoBehaviourSingleton<ConversationHUD>.GetInstance();
                if (hud == null) return false;

                // Don't activate while "Click to Continue" is showing - NPC text is still displaying
                if (hud.clickToContinue != null && hud.clickToContinue.activeSelf) return false;

                // Don't activate while any conversation bubble text is active in BubbleTextManager.
                // This covers description text, voiced audio, and all conversation text types.
                // Prevents the 1-frame race where buttons are added before clickToContinue
                // is set active by BubbleTextManager.Update().
                if (VoiceoverHelper.HasActiveConversationBubbles()) return false;

                int count = GetButtonCount();
                return count > 0;
            }
        }

        public bool HandleInput()
        {
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
                    AnnounceCurrentOption();
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

        public void OnActivated()
        {
            IsManagingNavigation = true;
            selectedIndex = 0;
            lastKnownButtonCount = 0;
            RefreshOptions();

            if (currentOptions.Count > 0)
            {
                lastKnownButtonCount = currentOptions.Count;
                AnnounceCurrentOption();
            }

            MelonLogger.Msg($"[ConversationState] Activated with {currentOptions.Count} options");
        }

        public void OnDeactivated()
        {
            IsManagingNavigation = false;
            selectedIndex = -1;
            currentOptions.Clear();
            lastKnownButtonCount = 0;

            MelonLogger.Msg("[ConversationState] Deactivated");
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

        private void AnnounceCurrentOption()
        {
            if (selectedIndex < 0 || selectedIndex >= currentOptions.Count) return;

            var opt = currentOptions[selectedIndex];

            // Build announcement: "1 of 3: response text, skill info"
            string announcement = $"{selectedIndex + 1} of {currentOptions.Count}: ";

            // Prefer full response text over keyword label
            if (!string.IsNullOrEmpty(opt.FullResponseText))
            {
                announcement += opt.FullResponseText;
            }
            else
            {
                announcement += opt.DisplayText;
            }

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

            ScreenReaderManager.SpeakInterrupt(announcement);
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

            // Announce selection
            string selText = !string.IsNullOrEmpty(opt.FullResponseText)
                ? opt.FullResponseText
                : opt.DisplayText;
            ScreenReaderManager.Speak($"Selected: {selText}");

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
