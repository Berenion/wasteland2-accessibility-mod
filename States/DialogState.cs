using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation for modal dialogs (Yes/No confirmations, tutorials).
    /// Higher priority (70) than conversation/inventory so it captures input
    /// when a dialog appears over other screens.
    /// </summary>
    public class DialogState : IAccessibilityState
    {
        public string Name => "Dialog";
        public int Priority => 70;

        private int selectedButtonIndex = 0;
        private readonly List<DialogButton> buttons = new List<DialogButton>();
        private string currentDialogId = "";
        private DifficultySelectionMenu currentDifficultyMenu;
        private int difficultyIndex;
        private static readonly string[] DifficultyNames = { "Rookie", "Seasoned", "Ranger", "Legend" };
        private AskQuantityMenu currentQuantityMenu;
        private int lastAnnouncedQuantity = -1;

        private class DialogButton
        {
            public string Label;
            public GameObject ButtonObject;
            public Action ClickAction;
        }

        public bool IsActive
        {
            get
            {
                return IsModalDialogOpen() || IsTutorialOpen() || IsTutorialPopupMenuOpen();
            }
        }

        public bool HandleInput()
        {
            bool dialogChanged = RefreshButtons();

            // When the dialog changes while DialogState stays active (e.g. one tutorial
            // closes revealing another), re-announce the new content
            if (dialogChanged)
            {
                AnnounceDialog();
            }

            if (buttons.Count == 0) return false;

            // Difficulty selection: Left/Right changes difficulty, Up/Down navigates Play/Back
            if (currentDifficultyMenu != null)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (difficultyIndex > 0)
                    {
                        difficultyIndex--;
                        currentDifficultyMenu.SelectDifficulty(difficultyIndex);
                        // Harmony patch announces the result
                    }
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (difficultyIndex < 3)
                    {
                        difficultyIndex++;
                        currentDifficultyMenu.SelectDifficulty(difficultyIndex);
                    }
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                // Up - previous button (Play/Back)
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    selectedButtonIndex--;
                    if (selectedButtonIndex < 0) selectedButtonIndex = buttons.Count - 1;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                // Down - next button (Play/Back)
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selectedButtonIndex = (selectedButtonIndex + 1) % buttons.Count;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }
            }
            else if (currentQuantityMenu != null)
            {
                // AskQuantityMenu: Left/Right adjusts quantity, number keys type, Up/Down for OK/Cancel
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    AdjustQuantity(-1);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    AdjustQuantity(1);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    AdjustQuantity(-10);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    AdjustQuantity(10);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Home))
                {
                    SetQuantity(1);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.End))
                {
                    var maxField = typeof(AskQuantityMenu).GetField("maxValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    int max = maxField != null ? (int)maxField.GetValue(currentQuantityMenu) : 999;
                    SetQuantity(max);
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                // Up/Down navigate OK/Cancel buttons
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    selectedButtonIndex--;
                    if (selectedButtonIndex < 0) selectedButtonIndex = buttons.Count - 1;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selectedButtonIndex = (selectedButtonIndex + 1) % buttons.Count;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }
            }
            else
            {
                // Standard dialog: Left/Up previous, Right/Down next
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    selectedButtonIndex--;
                    if (selectedButtonIndex < 0) selectedButtonIndex = buttons.Count - 1;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    selectedButtonIndex = (selectedButtonIndex + 1) % buttons.Count;
                    AnnounceButton();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    InputSuppressor.ShouldSuppressGameInput = true;
                    return true;
                }
            }

            // Enter - activate selected button
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateButton();
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                return true;
            }

            // Suppress other input while dialog is open
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressGameInput = true;
            return false;
        }

        public void OnActivated()
        {
            selectedButtonIndex = 0;
            currentDialogId = "";
            RefreshButtons();
            AnnounceDialog();

            MelonLogger.Msg("[DialogState] Activated");
        }

        public void OnDeactivated()
        {
            selectedButtonIndex = 0;
            buttons.Clear();
            currentDialogId = "";
            currentDifficultyMenu = null;
            currentQuantityMenu = null;
            lastAnnouncedQuantity = -1;

            MelonLogger.Msg("[DialogState] Deactivated");
        }

        private bool IsModalDialogOpen()
        {
            if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

            // Check if any modal message menu is active in the screen stack
            var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();
            if (!guiManager.IsAnyMenuActive()) return false;

            // ModalMessageMenu is created as a child of menuRoot, not GUIManager,
            // so GetComponentInChildren on GUIManager won't find it. Use FindObjectOfType instead.
            var modal = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
            return modal != null && modal.gameObject.activeInHierarchy;
        }

        private bool IsTutorialOpen()
        {
            if (!MonoBehaviourSingleton<TutorialScreen>.HasInstance()) return false;

            var tutScreen = MonoBehaviourSingleton<TutorialScreen>.GetInstance();
            if (tutScreen == null) return false;

            // Check if the popup is active
            if (tutScreen.popup != null && tutScreen.popup.gameObject.activeInHierarchy)
            {
                return true;
            }

            return false;
        }

        private bool IsTutorialPopupMenuOpen()
        {
            var popup = UnityEngine.Object.FindObjectOfType<TutorialPopupMenu>();
            return popup != null && popup.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Refreshes the button list for the current dialog.
        /// Returns true if the dialog changed (new dialog appeared).
        /// </summary>
        private bool RefreshButtons()
        {
            buttons.Clear();
            bool changed = false;

            // Check for ModalMessageMenu (includes DifficultySelectionMenu which inherits from it)
            ModalMessageMenu modal = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
            if (modal != null && modal.gameObject.activeInHierarchy)
            {
                // Check if this is the difficulty selection screen
                var diffMenu = modal as DifficultySelectionMenu;
                if (diffMenu != null)
                {
                    currentDifficultyMenu = diffMenu;
                    // Sync difficulty index via reflection (gameDifficultyIndex is private)
                    var field = typeof(DifficultySelectionMenu).GetField("gameDifficultyIndex", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        difficultyIndex = (int)field.GetValue(diffMenu);
                    }
                }
                else
                {
                    currentDifficultyMenu = null;
                }

                // Check if this is a quantity selection menu
                currentQuantityMenu = modal as AskQuantityMenu;
                if (currentQuantityMenu != null)
                {
                    lastAnnouncedQuantity = -1;
                }

                string dialogId = "modal_" + (modal.titleLabel != null ? modal.titleLabel.text : "unknown");

                // Yes button
                if (modal.yesButton != null && modal.yesButton.gameObject.activeSelf)
                {
                    string yesText = "OK";
                    if (modal.yesLabel != null && !string.IsNullOrEmpty(modal.yesLabel.text))
                    {
                        yesText = UITextExtractor.CleanText(modal.yesLabel.text);
                    }

                    buttons.Add(new DialogButton
                    {
                        Label = yesText,
                        ButtonObject = modal.yesButton.gameObject,
                        ClickAction = () => modal.yesButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver)
                    });
                }

                // No button
                if (modal.noButton != null && modal.noButton.gameObject.activeSelf)
                {
                    string noText = "Cancel";
                    if (modal.noLabel != null && !string.IsNullOrEmpty(modal.noLabel.text))
                    {
                        noText = UITextExtractor.CleanText(modal.noLabel.text);
                    }

                    buttons.Add(new DialogButton
                    {
                        Label = noText,
                        ButtonObject = modal.noButton.gameObject,
                        ClickAction = () => modal.noButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver)
                    });
                }

                if (dialogId != currentDialogId)
                {
                    currentDialogId = dialogId;
                    selectedButtonIndex = 0;
                    changed = true;
                }

                return changed;
            }

            // Check for TutorialPopupMenu (GUIScreen-based tutorials, e.g. character creation)
            var tutorialPopupMenu = UnityEngine.Object.FindObjectOfType<TutorialPopupMenu>();
            if (tutorialPopupMenu != null && tutorialPopupMenu.gameObject.activeInHierarchy)
            {
                string dialogId = "tutmenu_" + (tutorialPopupMenu.titleLabel != null ? tutorialPopupMenu.titleLabel.text : "unknown");

                string continueText = "Continue";
                if (tutorialPopupMenu.clickToContinueLabel != null && !string.IsNullOrEmpty(tutorialPopupMenu.clickToContinueLabel.text))
                {
                    continueText = UITextExtractor.CleanText(tutorialPopupMenu.clickToContinueLabel.text);
                }

                buttons.Add(new DialogButton
                {
                    Label = continueText,
                    ButtonObject = tutorialPopupMenu.gameObject,
                    ClickAction = () => tutorialPopupMenu.Close()
                });

                if (dialogId != currentDialogId)
                {
                    currentDialogId = dialogId;
                    selectedButtonIndex = 0;
                    changed = true;
                }

                return changed;
            }

            // Check for TutorialPopup (TUT_TutorialPopup)
            if (IsTutorialOpen())
            {
                var tutScreen = MonoBehaviourSingleton<TutorialScreen>.GetInstance();
                if (tutScreen.popup != null)
                {
                    string dialogId = "tutorial_" + (tutScreen.popup.titleLabel != null ? tutScreen.popup.titleLabel.text : "unknown");

                    // OK/Next button
                    if (tutScreen.popup.okayButtonLabel != null)
                    {
                        string okText = UITextExtractor.CleanText(tutScreen.popup.okayButtonLabel.text);
                        if (string.IsNullOrEmpty(okText)) okText = "OK";

                        buttons.Add(new DialogButton
                        {
                            Label = okText,
                            ButtonObject = tutScreen.popup.okayButtonLabel.gameObject,
                            ClickAction = () => tutScreen.popup.OnOkayClicked()
                        });
                    }

                    if (dialogId != currentDialogId)
                    {
                        currentDialogId = dialogId;
                        selectedButtonIndex = 0;
                        changed = true;
                    }
                }
            }

            // Clamp index
            if (selectedButtonIndex >= buttons.Count && buttons.Count > 0)
            {
                selectedButtonIndex = buttons.Count - 1;
            }

            return changed;
        }

        private void AnnounceDialog()
        {
            // Announce the full dialog content when it first appears
            ModalMessageMenu modal = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
            if (modal != null && modal.gameObject.activeInHierarchy)
            {
                // DifficultySelectionMenu gets a specialized announcement
                if (currentDifficultyMenu != null)
                {
                    string diffName = DifficultyNames[difficultyIndex];
                    string description = "";
                    if (currentDifficultyMenu.descriptionLabel != null)
                    {
                        description = UITextExtractor.CleanText(currentDifficultyMenu.descriptionLabel.text);
                    }

                    string announcement = $"Difficulty Selection. {diffName}, {difficultyIndex + 1} of 4";
                    if (!string.IsNullOrEmpty(description))
                    {
                        announcement += ". " + description;
                    }
                    announcement += ". Left and Right to change difficulty, Up and Down for Play or Back";

                    ScreenReaderManager.SpeakInterrupt(announcement);
                    return;
                }

                // AskQuantityMenu gets a specialized announcement
                if (currentQuantityMenu != null)
                {
                    string qtyTitle = "";
                    string qtyMessage = "";
                    if (modal.titleLabel != null && !string.IsNullOrEmpty(modal.titleLabel.text))
                        qtyTitle = UITextExtractor.CleanText(modal.titleLabel.text);
                    if (modal.messageLabel != null && !string.IsNullOrEmpty(modal.messageLabel.text))
                        qtyMessage = UITextExtractor.CleanText(modal.messageLabel.text);

                    var maxField = typeof(AskQuantityMenu).GetField("maxValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    int max = maxField != null ? (int)maxField.GetValue(currentQuantityMenu) : 0;
                    int current = currentQuantityMenu.GetQuantity();
                    lastAnnouncedQuantity = current;

                    string qtyAnnouncement = "";
                    if (!string.IsNullOrEmpty(qtyTitle)) qtyAnnouncement += qtyTitle + ". ";
                    if (!string.IsNullOrEmpty(qtyMessage)) qtyAnnouncement += qtyMessage + ". ";
                    qtyAnnouncement += $"{current} of {max}. Left and Right to adjust, Page Up and Page Down by 10, Home for minimum, End for maximum. Up and Down for OK or Cancel";

                    ScreenReaderManager.SpeakInterrupt(qtyAnnouncement);
                    return;
                }

                string title = "";
                string message = "";

                if (modal.titleLabel != null && !string.IsNullOrEmpty(modal.titleLabel.text))
                {
                    title = UITextExtractor.CleanText(modal.titleLabel.text);
                }

                if (modal.messageLabel != null && !string.IsNullOrEmpty(modal.messageLabel.text))
                {
                    message = UITextExtractor.CleanText(modal.messageLabel.text);
                }

                string announcement2 = "";
                if (!string.IsNullOrEmpty(title)) announcement2 += title + ". ";
                if (!string.IsNullOrEmpty(message)) announcement2 += message + ". ";

                if (buttons.Count > 0)
                {
                    announcement2 += $"Button: {buttons[0].Label}";
                    if (buttons.Count > 1)
                    {
                        announcement2 += $", 1 of {buttons.Count}";
                    }
                }

                ScreenReaderManager.SpeakInterrupt(announcement2);
                return;
            }

            // TutorialPopupMenu (GUIScreen-based)
            var tutorialPopupMenu = UnityEngine.Object.FindObjectOfType<TutorialPopupMenu>();
            if (tutorialPopupMenu != null && tutorialPopupMenu.gameObject.activeInHierarchy)
            {
                string title = "";
                string message = "";

                if (tutorialPopupMenu.titleLabel != null)
                {
                    title = UITextExtractor.CleanText(tutorialPopupMenu.titleLabel.text);
                }

                if (tutorialPopupMenu.tutorialLabel != null)
                {
                    message = UITextExtractor.CleanText(tutorialPopupMenu.tutorialLabel.text);
                }

                string announcement = "Tutorial. ";
                if (!string.IsNullOrEmpty(title)) announcement += title + ". ";
                if (!string.IsNullOrEmpty(message)) announcement += message + ". ";
                announcement += "Press Enter to continue";

                ScreenReaderManager.SpeakInterrupt(announcement);
                return;
            }

            // TUT_TutorialPopup
            if (IsTutorialOpen())
            {
                var tutScreen = MonoBehaviourSingleton<TutorialScreen>.GetInstance();
                if (tutScreen.popup != null)
                {
                    string title = "";
                    string message = "";

                    if (tutScreen.popup.titleLabel != null)
                    {
                        title = UITextExtractor.CleanText(tutScreen.popup.titleLabel.text);
                    }

                    if (tutScreen.popup.messageLabel != null)
                    {
                        message = UITextExtractor.CleanText(tutScreen.popup.messageLabel.text);
                    }

                    string announcement = "Tutorial. ";
                    if (!string.IsNullOrEmpty(title)) announcement += title + ". ";
                    if (!string.IsNullOrEmpty(message)) announcement += message;

                    ScreenReaderManager.SpeakInterrupt(announcement);
                }
            }
        }

        private void AnnounceButton()
        {
            if (selectedButtonIndex < 0 || selectedButtonIndex >= buttons.Count) return;

            string announcement = buttons[selectedButtonIndex].Label;
            if (buttons.Count > 1)
            {
                announcement += $", {selectedButtonIndex + 1} of {buttons.Count}";
            }

            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private void ActivateButton()
        {
            if (selectedButtonIndex < 0 || selectedButtonIndex >= buttons.Count) return;

            var btn = buttons[selectedButtonIndex];

            ScreenReaderManager.SpeakInterrupt($"{btn.Label} pressed");

            MelonLogger.Msg($"[DialogState] Pressed: {btn.Label}");

            if (btn.ClickAction != null)
            {
                try
                {
                    btn.ClickAction();
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[DialogState] Error executing button action: {ex.Message}");
                }
            }
        }
        #region AskQuantityMenu Helpers

        private void AdjustQuantity(int delta)
        {
            if (currentQuantityMenu == null) return;

            int current = currentQuantityMenu.GetQuantity();
            var minField = typeof(AskQuantityMenu).GetField("minValue", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxField = typeof(AskQuantityMenu).GetField("maxValue", BindingFlags.NonPublic | BindingFlags.Instance);
            int min = minField != null ? (int)minField.GetValue(currentQuantityMenu) : 1;
            int max = maxField != null ? (int)maxField.GetValue(currentQuantityMenu) : 999;

            int newValue = Mathf.Clamp(current + delta, min, max);
            SetQuantity(newValue);
        }

        private void SetQuantity(int value)
        {
            if (currentQuantityMenu == null) return;

            var minField = typeof(AskQuantityMenu).GetField("minValue", BindingFlags.NonPublic | BindingFlags.Instance);
            var maxField = typeof(AskQuantityMenu).GetField("maxValue", BindingFlags.NonPublic | BindingFlags.Instance);
            int min = minField != null ? (int)minField.GetValue(currentQuantityMenu) : 1;
            int max = maxField != null ? (int)maxField.GetValue(currentQuantityMenu) : 999;

            value = Mathf.Clamp(value, min, max);

            // Update the quantity field
            var qtyField = typeof(AskQuantityMenu).GetField("quantity", BindingFlags.NonPublic | BindingFlags.Instance);
            if (qtyField != null)
                qtyField.SetValue(currentQuantityMenu, value);

            // Update the text input to reflect the new value
            if (currentQuantityMenu.quantityInput != null)
            {
                currentQuantityMenu.quantityInput.value = value.ToString();
            }

            // Update the slider position
            if (currentQuantityMenu.slider != null && max > min)
            {
                currentQuantityMenu.slider.value = (float)(value - min) / (float)(max - min);
            }

            // Announce if changed
            if (value != lastAnnouncedQuantity)
            {
                lastAnnouncedQuantity = value;
                ScreenReaderManager.SpeakInterrupt($"{value} of {max}");
            }
        }

        #endregion
    }
}
