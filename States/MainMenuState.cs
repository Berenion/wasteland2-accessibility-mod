using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Handles keyboard navigation for the main menu.
    /// Priority 60 - above conversation/inventory but below dialogs.
    /// Implements direct keyboard navigation since the game's UIButtonKeys
    /// may not be configured for keyboard-only use.
    /// </summary>
    public class MainMenuState : IAccessibilityState
    {
        public string Name => "MainMenu";
        public int Priority => 60;

        // Track the last selected object to detect external changes
        private GameObject lastSelectedObject = null;

        // Current button index for navigation
        private int currentIndex = -1;

        // Cached list of menu buttons in order
        private List<ButtonEntry> menuButtons = new List<ButtonEntry>();

        private class ButtonEntry
        {
            public GameObject gameObject;
            public UILabel label;
            public UIButton button;
            public string name;
        }

        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

                // Check if main menu exists and is the TOP menu (not covered by submenus)
                MainMenu menu = MonoBehaviourSingleton<GUIManager>.GetInstance().GetMainMenu();
                if (menu == null) return false;

                // Only active if the MainMenu is the top menu
                return menu.isTopMenu;
            }
        }

        public bool HandleInput()
        {
            MainMenu menu = GetMainMenu();
            if (menu == null) return false;

            // Only rebuild if list is empty (first time or after deactivation)
            if (menuButtons.Count == 0)
            {
                RebuildButtonList(menu);
            }

            // Handle Up arrow - move to previous button
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateUp();
                return true;
            }

            // Handle Down arrow - move to next button
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateDown();
                return true;
            }

            // Handle Enter key for activation
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateSelectedButton();
                return true;
            }

            // Check for external selection changes (mouse, etc.)
            CheckForExternalSelectionChange();

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[MainMenuState] Activated");
            lastSelectedObject = null;
            currentIndex = -1;
            menuButtons.Clear();

            // Build button list
            MainMenu menu = GetMainMenu();
            if (menu != null)
            {
                RebuildButtonList(menu);

                // Select first available button if nothing selected
                if (menuButtons.Count > 0)
                {
                    // Check if something is already selected
                    int existingIndex = FindCurrentSelectionIndex();
                    if (existingIndex >= 0)
                    {
                        currentIndex = existingIndex;
                    }
                    else
                    {
                        // Select first enabled button
                        for (int i = 0; i < menuButtons.Count; i++)
                        {
                            if (menuButtons[i].button == null || menuButtons[i].button.isEnabled)
                            {
                                currentIndex = i;
                                SetSelection(currentIndex);
                                break;
                            }
                        }
                    }
                }
            }

            // Announce the menu when it becomes active
            ScreenReaderManager.Speak("Main Menu. Use Up and Down arrows to navigate, Enter to select.", interrupt: true);

            // Announce current selection after a brief delay
            if (currentIndex >= 0 && currentIndex < menuButtons.Count)
            {
                AnnounceButton(currentIndex);
            }
        }

        public void OnDeactivated()
        {
            MelonLogger.Msg("[MainMenuState] Deactivated");
            lastSelectedObject = null;
            currentIndex = -1;
            menuButtons.Clear();
        }

        private MainMenu GetMainMenu()
        {
            if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return null;
            return MonoBehaviourSingleton<GUIManager>.GetInstance().GetMainMenu();
        }

        private void RebuildButtonList(MainMenu menu)
        {
            menuButtons.Clear();

            // Add buttons in visual order (top to bottom)
            // Continue
            if (menu.continueButton != null && menu.continueButton.gameObject.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.continueButton.gameObject,
                    label = menu.continueLabel,
                    button = menu.continueButton,
                    name = GetLabelText(menu.continueLabel) ?? "Continue"
                });
            }

            // Load
            if (menu.loadButton != null && menu.loadButton.gameObject.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.loadButton.gameObject,
                    label = menu.loadLabel,
                    button = menu.loadButton,
                    name = GetLabelText(menu.loadLabel) ?? "Load"
                });
            }

            // New Game
            if (menu.newGameButton != null && menu.newGameButton.gameObject.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.newGameButton.gameObject,
                    label = menu.newGameLabel,
                    button = menu.newGameButton,
                    name = GetLabelText(menu.newGameLabel) ?? "New Game"
                });
            }

            // Options
            if (menu.optionsButton != null && menu.optionsButton.gameObject.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.optionsButton.gameObject,
                    label = menu.optionsLabel,
                    button = menu.optionsButton,
                    name = GetLabelText(menu.optionsLabel) ?? "Options"
                });
            }

            // Credits
            if (menu.creditsButton != null && menu.creditsButton.gameObject.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.creditsButton.gameObject,
                    label = null, // creditsButton is UIButtonKeys, label is separate
                    button = menu.creditsButton.GetComponent<UIButton>(),
                    name = GetLabelText(menu.creditsLabel) ?? "Credits"
                });
            }

            // Exit
            if (menu.exitButton != null && menu.exitButton.activeInHierarchy)
            {
                menuButtons.Add(new ButtonEntry
                {
                    gameObject = menu.exitButton,
                    label = menu.exitGameLabel,
                    button = menu.exitButton.GetComponent<UIButton>(),
                    name = GetLabelText(menu.exitGameLabel) ?? "Exit Game"
                });
            }

        }

        private void NavigateUp()
        {
            if (menuButtons.Count == 0) return;

            // Find previous enabled button
            int startIndex = currentIndex;
            int newIndex = currentIndex;

            do
            {
                newIndex--;
                if (newIndex < 0) newIndex = menuButtons.Count - 1;

                // Check if this button is enabled
                var entry = menuButtons[newIndex];
                if (entry.button == null || entry.button.isEnabled)
                {
                    break;
                }
            }
            while (newIndex != startIndex);

            if (newIndex != currentIndex)
            {
                currentIndex = newIndex;
                SetSelection(currentIndex);
                AnnounceButton(currentIndex);
            }
        }

        private void NavigateDown()
        {
            if (menuButtons.Count == 0) return;

            // Find next enabled button
            int startIndex = currentIndex;
            int newIndex = currentIndex;

            do
            {
                newIndex++;
                if (newIndex >= menuButtons.Count) newIndex = 0;

                // Check if this button is enabled
                var entry = menuButtons[newIndex];
                if (entry.button == null || entry.button.isEnabled)
                {
                    break;
                }
            }
            while (newIndex != startIndex);

            if (newIndex != currentIndex)
            {
                currentIndex = newIndex;
                SetSelection(currentIndex);
                AnnounceButton(currentIndex);
            }
        }

        private void SetSelection(int index)
        {
            if (index < 0 || index >= menuButtons.Count) return;

            var entry = menuButtons[index];
            UICamera.selectedObject = entry.gameObject;
            lastSelectedObject = entry.gameObject;

            // Also trigger hover for visual feedback
            entry.gameObject.SendMessage("OnHover", true, SendMessageOptions.DontRequireReceiver);

            MelonLogger.Msg($"[MainMenuState] Selected button {index}: {entry.name}");
        }

        private void AnnounceButton(int index)
        {
            if (index < 0 || index >= menuButtons.Count) return;

            var entry = menuButtons[index];
            string announcement = entry.name;

            // Check if button is disabled
            if (entry.button != null && !entry.button.isEnabled)
            {
                announcement += ", unavailable";
            }

            // Add position info
            int enabledCount = 0;
            int positionInEnabled = 0;
            for (int i = 0; i < menuButtons.Count; i++)
            {
                var btn = menuButtons[i];
                if (btn.button == null || btn.button.isEnabled)
                {
                    enabledCount++;
                    if (i == index)
                    {
                        positionInEnabled = enabledCount;
                    }
                }
            }

            if (enabledCount > 1)
            {
                announcement += $", {positionInEnabled} of {enabledCount}";
            }

            ScreenReaderManager.Speak(announcement, interrupt: true);
        }

        private int FindCurrentSelectionIndex()
        {
            GameObject current = UICamera.selectedObject;
            if (current == null) return -1;

            for (int i = 0; i < menuButtons.Count; i++)
            {
                if (menuButtons[i].gameObject == current)
                {
                    return i;
                }
            }

            return -1;
        }

        private void CheckForExternalSelectionChange()
        {
            GameObject current = UICamera.selectedObject;
            if (current != lastSelectedObject)
            {
                lastSelectedObject = current;

                // Find the new index
                int newIndex = FindCurrentSelectionIndex();
                if (newIndex >= 0 && newIndex != currentIndex)
                {
                    currentIndex = newIndex;
                    AnnounceButton(currentIndex);
                }
            }
        }

        private string GetLabelText(UILabel label)
        {
            if (label == null) return null;
            return UITextExtractor.CleanText(label.text);
        }

        private void ActivateSelectedButton()
        {
            if (currentIndex < 0 || currentIndex >= menuButtons.Count) return;

            var entry = menuButtons[currentIndex];

            // Check if button is disabled
            if (entry.button != null && !entry.button.isEnabled)
            {
                ScreenReaderManager.Speak("Button unavailable", interrupt: true);
                return;
            }

            // Trigger click via SendMessage
            entry.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            MelonLogger.Msg($"[MainMenuState] Activated button: {entry.name}");
        }
    }
}
