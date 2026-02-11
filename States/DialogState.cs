using System;
using System.Collections.Generic;
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
            RefreshButtons();

            if (buttons.Count == 0) return false;

            // Left or Up - previous button
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                selectedButtonIndex--;
                if (selectedButtonIndex < 0) selectedButtonIndex = buttons.Count - 1;
                AnnounceButton();
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                return true;
            }

            // Right or Down - next button
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                selectedButtonIndex = (selectedButtonIndex + 1) % buttons.Count;
                AnnounceButton();
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                return true;
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

        private void RefreshButtons()
        {
            buttons.Clear();

            // Check for ModalMessageMenu
            ModalMessageMenu modal = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
            if (modal != null && modal.gameObject.activeInHierarchy)
            {
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

                // Detect if dialog changed
                if (dialogId != currentDialogId)
                {
                    currentDialogId = dialogId;
                    selectedButtonIndex = 0;
                }

                return;
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
                }

                return;
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
                    }
                }
            }

            // Clamp index
            if (selectedButtonIndex >= buttons.Count && buttons.Count > 0)
            {
                selectedButtonIndex = buttons.Count - 1;
            }
        }

        private void AnnounceDialog()
        {
            // Announce the full dialog content when it first appears
            ModalMessageMenu modal = UnityEngine.Object.FindObjectOfType<ModalMessageMenu>();
            if (modal != null && modal.gameObject.activeInHierarchy)
            {
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

                string announcement = "";
                if (!string.IsNullOrEmpty(title)) announcement += title + ". ";
                if (!string.IsNullOrEmpty(message)) announcement += message + ". ";

                if (buttons.Count > 0)
                {
                    announcement += $"Button: {buttons[0].Label}";
                    if (buttons.Count > 1)
                    {
                        announcement += $", 1 of {buttons.Count}";
                    }
                }

                ScreenReaderManager.SpeakInterrupt(announcement);
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
    }
}
