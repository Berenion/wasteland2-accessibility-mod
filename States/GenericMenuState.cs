using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Handles keyboard navigation for generic popup menus (Options, Load/Save, etc.).
    /// Priority 55 - below MainMenu but above conversation states.
    /// Builds custom navigation for the Options menu since it has no native keyboard support.
    /// </summary>
    public class GenericMenuState : IAccessibilityState
    {
        public string Name => "GenericMenu";
        public int Priority => 55;

        // Track the last selected object to detect changes
        private GameObject lastSelectedObject = null;
        private string lastAnnouncedText = null;

        // Delay announcements after activation to avoid spamming during menu init
        private float activationTime = 0f;
        private const float ANNOUNCEMENT_DELAY = 0.3f;

        // Track if we've done the initial announcement after activation delay
        private bool initialAnnouncementDone = false;

        // Flag to prevent repeated EnsureSelection calls
        private bool selectionEnsured = false;

        // Cached list of controls in the active options panel
        private List<GameObject> optionsControls = null;
        private int optionsControlIndex = -1;

        // Cached OptionsMenu reference
        private OptionsMenu cachedOptionsMenu = null;

        // Tab order for OptionsMenu
        private static readonly string[] tabOrder = { "gameplay", "display", "controls", "audio" };

        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;

                var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();

                // Not active if no menus open
                if (!guiManager.IsAnyMenuActive()) return false;

                // Not active during conversations (ConversationState handles that)
                if (Drama.isConversationOn) return false;

                // Check if MainMenu exists and is the top menu - if so, MainMenuState handles it
                MainMenu mainMenu = guiManager.GetMainMenu();
                if (mainMenu != null && mainMenu.isTopMenu) return false;

                // Not active when CharacterScreen is showing - CharacterState handles that
                if (CharacterScreen.instance != null && CharacterScreen.instance.gameObject.activeInHierarchy)
                    return false;

                // Active when any other menu is on top (including submenus over MainMenu)
                return true;
            }
        }

        public bool HandleInput()
        {
            // Check for selection changes and announce
            CheckAndAnnounceSelection();

            // Force an initial announcement after activation delay
            if (!initialAnnouncementDone && Time.time - activationTime >= ANNOUNCEMENT_DELAY)
            {
                initialAnnouncementDone = true;
                ForceAnnounceCurrentSelection();
            }

            // Handle OptionsMenu tab switching with Page Up/Page Down
            if (cachedOptionsMenu != null)
            {
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    SwitchOptionsTab(-1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    SwitchOptionsTab(1);
                    return true;
                }
            }

            // Handle navigation keys
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Navigate(KeyCode.UpArrow);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Navigate(KeyCode.DownArrow);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Navigate(KeyCode.LeftArrow);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Navigate(KeyCode.RightArrow);
                return true;
            }

            // Handle Tab for next element
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Navigate(KeyCode.Tab);
                return true;
            }

            // Handle Enter for activation
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateSelected();
                return true;
            }

            // Handle Escape to close menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseMenu();
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[GenericMenuState] Activated");
            lastSelectedObject = null;
            lastAnnouncedText = null;
            activationTime = Time.time;
            selectionEnsured = false;
            initialAnnouncementDone = false;
            optionsControls = null;
            optionsControlIndex = -1;
            cachedOptionsMenu = null;

            // Detect OptionsMenu
            GUIScreen topScreen = FindTopScreen();
            cachedOptionsMenu = topScreen as OptionsMenu;

            // Announce the menu type
            AnnounceMenu(topScreen);

            // Ensure something is selected
            EnsureSelection(topScreen);
        }

        public void OnDeactivated()
        {
            MelonLogger.Msg("[GenericMenuState] Deactivated");
            lastSelectedObject = null;
            lastAnnouncedText = null;
            selectionEnsured = false;
            initialAnnouncementDone = false;
            optionsControls = null;
            optionsControlIndex = -1;
            cachedOptionsMenu = null;
        }

        // ========== Menu Announcement ==========

        private void AnnounceMenu(GUIScreen topScreen)
        {
            if (topScreen == null) return;

            string menuName = topScreen.name;

            if (cachedOptionsMenu != null)
            {
                menuName = "Options, " + GetActiveTabName(cachedOptionsMenu);
                menuName += ". Page Up and Page Down to switch tabs";
            }

            MelonLogger.Msg($"[GenericMenuState] Menu: {menuName}");
            ScreenReaderManager.SpeakInterrupt(menuName);
        }

        private string GetActiveTabName(OptionsMenu menu)
        {
            if (menu.gameplayPanel != null && menu.gameplayPanel.activeSelf) return "Gameplay";
            if (menu.displayPanel != null && menu.displayPanel.activeSelf) return "Display";
            if (menu.controlsPanel != null && menu.controlsPanel.activeSelf) return "Controls";
            if (menu.audioPanel != null && menu.audioPanel.activeSelf) return "Audio";
            return "Unknown";
        }

        // ========== Options Tab Switching ==========

        private void SwitchOptionsTab(int direction)
        {
            if (cachedOptionsMenu == null) return;

            // Find current tab index
            int currentTab = 0;
            if (cachedOptionsMenu.gameplayPanel.activeSelf) currentTab = 0;
            else if (cachedOptionsMenu.displayPanel.activeSelf) currentTab = 1;
            else if (cachedOptionsMenu.controlsPanel.activeSelf) currentTab = 2;
            else if (cachedOptionsMenu.audioPanel.activeSelf) currentTab = 3;

            // Calculate new tab (with wrapping)
            int newTab = (currentTab + direction + 4) % 4;

            // Skip controls tab if it's hidden
            if (newTab == 2 && cachedOptionsMenu.controlsButton != null &&
                !cachedOptionsMenu.controlsButton.gameObject.activeSelf)
            {
                newTab = (newTab + direction + 4) % 4;
            }

            // Switch to new tab using reflection to call private methods
            SwitchToTab(newTab);

            // Clear cached controls for the new panel
            optionsControls = null;
            optionsControlIndex = -1;

            // Announce the new tab
            string tabName = GetActiveTabName(cachedOptionsMenu);
            MelonLogger.Msg($"[GenericMenuState] Switched to tab: {tabName}");
            ScreenReaderManager.SpeakInterrupt($"Options, {tabName}");

            // Select first control in new tab after a short delay
            selectionEnsured = false;
            EnsureSelection(cachedOptionsMenu);

            // Force re-announce after switching
            initialAnnouncementDone = false;
            activationTime = Time.time;
        }

        private void SwitchToTab(int tabIndex)
        {
            if (cachedOptionsMenu == null) return;

            // Use reflection to call private On*Clicked methods
            string methodName;
            switch (tabIndex)
            {
                case 0: methodName = "OnGameplayClicked"; break;
                case 1: methodName = "OnDisplayClicked"; break;
                case 2: methodName = "OnControlsClicked"; break;
                case 3: methodName = "OnAudioClicked"; break;
                default: return;
            }

            var method = typeof(OptionsMenu).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (method != null)
            {
                method.Invoke(cachedOptionsMenu, new object[] { null });
                MelonLogger.Msg($"[GenericMenuState] Called {methodName}");
            }
            else
            {
                MelonLogger.Warning($"[GenericMenuState] Could not find method: {methodName}");
            }
        }

        // ========== Initial Selection ==========

        private void ForceAnnounceCurrentSelection()
        {
            GameObject current = UICamera.selectedObject;
            if (current == null)
            {
                MelonLogger.Msg("[GenericMenuState] ForceAnnounce: No selection");
                return;
            }

            string announcement = GetElementAnnouncement(current);
            if (!string.IsNullOrEmpty(announcement))
            {
                lastSelectedObject = current;
                lastAnnouncedText = announcement;
                ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[GenericMenuState] Initial announcement: {announcement}");
            }
        }

        private void EnsureSelection(GUIScreen topScreen)
        {
            // Only try once per activation
            if (selectionEnsured) return;
            selectionEnsured = true;

            if (topScreen == null) return;

            // For OptionsMenu, build our control list and select first item
            if (cachedOptionsMenu != null)
            {
                BuildOptionsControlList();
                if (optionsControls != null && optionsControls.Count > 0)
                {
                    optionsControlIndex = 0;
                    UICamera.selectedObject = optionsControls[0];
                    lastSelectedObject = optionsControls[0];
                    MelonLogger.Msg($"[GenericMenuState] Options selected first: {optionsControls[0].name} (of {optionsControls.Count} controls)");
                    return;
                }
            }

            // If nothing is selected, try to find and select something
            if (UICamera.selectedObject == null)
            {
                // Find UIButtonKeys within the top screen (fallback)
                UIButtonKeys[] buttonKeys = topScreen.GetComponentsInChildren<UIButtonKeys>(true);

                foreach (var bk in buttonKeys)
                {
                    if (bk == null || bk.gameObject == null) continue;
                    if (!bk.gameObject.activeInHierarchy) continue;

                    UIButton button = bk.GetComponent<UIButton>();
                    if (button != null && !button.isEnabled) continue;

                    UICamera.selectedObject = bk.gameObject;
                    lastSelectedObject = bk.gameObject;
                    MelonLogger.Msg($"[GenericMenuState] Auto-selected: {bk.gameObject.name}");
                    break;
                }
            }
            else
            {
                lastSelectedObject = UICamera.selectedObject;
                MelonLogger.Msg($"[GenericMenuState] Already selected: {UICamera.selectedObject.name}");
            }
        }

        // ========== Options Menu Control List ==========

        private void BuildOptionsControlList()
        {
            if (cachedOptionsMenu == null) return;

            optionsControls = new List<GameObject>();

            // Find the active panel
            GameObject activePanel = GetActivePanel(cachedOptionsMenu);
            if (activePanel == null)
            {
                MelonLogger.Warning("[GenericMenuState] No active panel found in OptionsMenu");
                return;
            }

            // Find the UIGrid in the active panel - controls are its children
            UIGrid grid = activePanel.GetComponentInChildren<UIGrid>();
            if (grid != null)
            {
                // Get children sorted by name (same order UIGrid uses with sorted=true)
                List<Transform> children = new List<Transform>();
                for (int i = 0; i < grid.transform.childCount; i++)
                {
                    Transform child = grid.transform.GetChild(i);
                    if (child != null && child.gameObject.activeInHierarchy)
                    {
                        children.Add(child);
                    }
                }

                // Sort by name to match UIGrid's sorted order
                children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

                foreach (var child in children)
                {
                    optionsControls.Add(child.gameObject);
                }

                MelonLogger.Msg($"[GenericMenuState] Built control list from UIGrid: {optionsControls.Count} controls");
            }
            else
            {
                // Fallback: find all OPT_* controls in the panel
                foreach (var c in activePanel.GetComponentsInChildren<MonoBehaviour>(false))
                {
                    if (c is OPT_Dropdown || c is OPT_Checkbox || c is OPT_Scrollbar)
                    {
                        optionsControls.Add(c.gameObject);
                    }
                }
                MelonLogger.Msg($"[GenericMenuState] Built control list from OPT_* scan: {optionsControls.Count} controls");
            }

            // Log the control list
            for (int i = 0; i < optionsControls.Count; i++)
            {
                MelonLogger.Msg($"  [{i}] {optionsControls[i].name}");
            }
        }

        private GameObject GetActivePanel(OptionsMenu menu)
        {
            if (menu.gameplayPanel != null && menu.gameplayPanel.activeSelf) return menu.gameplayPanel;
            if (menu.displayPanel != null && menu.displayPanel.activeSelf) return menu.displayPanel;
            if (menu.controlsPanel != null && menu.controlsPanel.activeSelf) return menu.controlsPanel;
            if (menu.audioPanel != null && menu.audioPanel.activeSelf) return menu.audioPanel;
            return null;
        }

        // ========== Navigation ==========

        private void Navigate(KeyCode direction)
        {
            // Use custom Options navigation if we have a control list
            if (cachedOptionsMenu != null && optionsControls != null && optionsControls.Count > 0)
            {
                NavigateOptionsMenu(direction);
                return;
            }

            // Fallback: UIButtonKeys-based navigation for non-Options menus
            NavigateGeneric(direction);
        }

        private void NavigateOptionsMenu(KeyCode direction)
        {
            // Find current index if we lost track
            if (optionsControlIndex < 0 || optionsControlIndex >= optionsControls.Count)
            {
                optionsControlIndex = FindCurrentControlIndex();
            }

            // Handle slider left/right
            if (direction == KeyCode.LeftArrow || direction == KeyCode.RightArrow)
            {
                GameObject current = (optionsControlIndex >= 0 && optionsControlIndex < optionsControls.Count)
                    ? optionsControls[optionsControlIndex] : UICamera.selectedObject;

                if (current != null)
                {
                    OPT_Scrollbar optScrollbar = current.GetComponent<OPT_Scrollbar>();
                    if (optScrollbar != null && optScrollbar.slider != null)
                    {
                        float step = (optScrollbar.slider.numberOfSteps > 0)
                            ? 1f / optScrollbar.slider.numberOfSteps
                            : 0.1f;

                        if (direction == KeyCode.LeftArrow)
                            optScrollbar.slider.value = Mathf.Max(0f, optScrollbar.slider.value - step);
                        else
                            optScrollbar.slider.value = Mathf.Min(1f, optScrollbar.slider.value + step);

                        // Announce using valueLabel if available, otherwise percentage
                        string valueText;
                        if (optScrollbar.valueLabel != null && optScrollbar.valueLabel.enabled &&
                            !string.IsNullOrEmpty(optScrollbar.valueLabel.text))
                        {
                            valueText = UITextExtractor.CleanText(optScrollbar.valueLabel.text);
                        }
                        else
                        {
                            int percent = Mathf.RoundToInt(optScrollbar.slider.value * 100);
                            valueText = $"{percent} percent";
                        }
                        ScreenReaderManager.SpeakInterrupt(valueText);
                        MelonLogger.Msg($"[GenericMenuState] Slider adjusted: {optScrollbar.slider.value}");
                        return;
                    }

                    // For dropdowns, left/right could cycle through values
                    OPT_Dropdown optDropdown = current.GetComponent<OPT_Dropdown>();
                    if (optDropdown != null && optDropdown.popupList != null)
                    {
                        CycleDropdownValue(optDropdown, direction == KeyCode.RightArrow ? 1 : -1);
                        return;
                    }
                }

                // Left/right does nothing else in options menu
                return;
            }

            // Up/Down navigation
            int newIndex = optionsControlIndex;
            if (direction == KeyCode.UpArrow)
            {
                newIndex = optionsControlIndex - 1;
                if (newIndex < 0) newIndex = optionsControls.Count - 1; // Wrap
            }
            else if (direction == KeyCode.DownArrow || direction == KeyCode.Tab)
            {
                newIndex = optionsControlIndex + 1;
                if (newIndex >= optionsControls.Count) newIndex = 0; // Wrap
            }

            if (newIndex != optionsControlIndex && newIndex >= 0 && newIndex < optionsControls.Count)
            {
                optionsControlIndex = newIndex;
                UICamera.selectedObject = optionsControls[newIndex];
                MelonLogger.Msg($"[GenericMenuState] Options nav to [{newIndex}]: {optionsControls[newIndex].name}");
            }

            CheckAndAnnounceSelection();
        }

        private void CycleDropdownValue(OPT_Dropdown dropdown, int direction)
        {
            if (dropdown.popupList == null || dropdown.popupList.items == null) return;
            if (dropdown.popupList.items.Count == 0) return;

            // Check if the popup list is enabled
            if (!dropdown.popupList.enabled)
            {
                ScreenReaderManager.SpeakInterrupt("unavailable");
                return;
            }

            int currentIndex = dropdown.popupList.items.IndexOf(dropdown.popupList.value);
            int newIndex = currentIndex + direction;
            if (newIndex < 0) newIndex = dropdown.popupList.items.Count - 1;
            if (newIndex >= dropdown.popupList.items.Count) newIndex = 0;

            dropdown.popupList.value = dropdown.popupList.items[newIndex];
            string val = UITextExtractor.CleanText(dropdown.popupList.value);
            ScreenReaderManager.SpeakInterrupt(val);
            MelonLogger.Msg($"[GenericMenuState] Dropdown cycled to: {val}");
        }

        private int FindCurrentControlIndex()
        {
            if (optionsControls == null) return -1;
            GameObject current = UICamera.selectedObject;
            if (current == null) return 0;

            for (int i = 0; i < optionsControls.Count; i++)
            {
                if (optionsControls[i] == current) return i;
            }

            // Check if selected is a child of one of our controls
            for (int i = 0; i < optionsControls.Count; i++)
            {
                if (current.transform.IsChildOf(optionsControls[i].transform)) return i;
            }

            return 0;
        }

        private void NavigateGeneric(KeyCode direction)
        {
            GameObject current = UICamera.selectedObject;
            if (current == null)
            {
                MelonLogger.Msg("[GenericMenuState] Navigate: No selection");
                return;
            }

            UIButtonKeys buttonKeys = current.GetComponent<UIButtonKeys>();
            if (buttonKeys == null)
            {
                buttonKeys = current.GetComponentInParent<UIButtonKeys>();
            }

            if (buttonKeys == null)
            {
                MelonLogger.Msg($"[GenericMenuState] Navigate: No UIButtonKeys on {current.name}");
                current.SendMessage("OnKey", direction, SendMessageOptions.DontRequireReceiver);
                CheckAndAnnounceSelection();
                return;
            }

            UIButtonKeys target = null;

            switch (direction)
            {
                case KeyCode.UpArrow:
                    target = buttonKeys.selectOnUp;
                    break;
                case KeyCode.DownArrow:
                    target = buttonKeys.selectOnDown;
                    break;
                case KeyCode.LeftArrow:
                    target = buttonKeys.selectOnLeft;
                    break;
                case KeyCode.RightArrow:
                    target = buttonKeys.selectOnRight;
                    break;
                case KeyCode.Tab:
                    target = buttonKeys.selectOnRight ?? buttonKeys.selectOnDown ?? buttonKeys.selectOnUp ?? buttonKeys.selectOnLeft;
                    break;
            }

            if (target == null)
            {
                MelonLogger.Msg($"[GenericMenuState] Navigate: No target for {direction} from {current.name}");
                return;
            }

            if (!target.gameObject.activeInHierarchy)
            {
                MelonLogger.Msg($"[GenericMenuState] Navigate: Target {target.gameObject.name} not active");
                return;
            }

            UIButton targetButton = target.GetComponent<UIButton>();
            if (targetButton == null || targetButton.isEnabled)
            {
                UICamera.selectedObject = target.gameObject;
                target.gameObject.SendMessage("OnHover", true, SendMessageOptions.DontRequireReceiver);
                MelonLogger.Msg($"[GenericMenuState] Navigated to: {target.gameObject.name}");
            }

            CheckAndAnnounceSelection();
        }

        // ========== Selection Tracking ==========

        private GUIScreen FindTopScreen()
        {
            GUIScreen[] screens = UnityEngine.Object.FindObjectsOfType<GUIScreen>();
            foreach (var screen in screens)
            {
                if (screen == null || !screen.gameObject.activeInHierarchy) continue;
                if (screen is MainMenu) continue; // Skip main menu
                if (screen.isTopMenu) return screen;
            }
            return null;
        }

        private void CheckAndAnnounceSelection()
        {
            // Don't announce during the initial delay after activation
            if (Time.time - activationTime < ANNOUNCEMENT_DELAY)
            {
                lastSelectedObject = UICamera.selectedObject;
                return;
            }

            GameObject current = UICamera.selectedObject;

            if (current != lastSelectedObject)
            {
                lastSelectedObject = current;

                if (current != null)
                {
                    string announcement = GetElementAnnouncement(current);
                    if (!string.IsNullOrEmpty(announcement) && announcement != lastAnnouncedText)
                    {
                        lastAnnouncedText = announcement;
                        ScreenReaderManager.SpeakInterrupt(announcement);
                    }
                }
            }
        }

        // ========== Text Extraction ==========

        private string GetElementAnnouncement(GameObject element)
        {
            if (element == null) return null;

            string announcement = "";
            string controlType = "";
            string controlValue = "";

            // First check for OPT_* controls (Options menu)
            OPT_Dropdown optDropdown = element.GetComponent<OPT_Dropdown>();
            OPT_Checkbox optCheckbox = element.GetComponent<OPT_Checkbox>();
            OPT_Scrollbar optScrollbar = element.GetComponent<OPT_Scrollbar>();

            // Also check parents in case selected object is a child
            if (optDropdown == null) optDropdown = element.GetComponentInParent<OPT_Dropdown>();
            if (optCheckbox == null) optCheckbox = element.GetComponentInParent<OPT_Checkbox>();
            if (optScrollbar == null) optScrollbar = element.GetComponentInParent<OPT_Scrollbar>();

            if (optDropdown != null)
            {
                if (optDropdown.label != null)
                    announcement = UITextExtractor.CleanText(optDropdown.label.text);
                if (optDropdown.popupList != null)
                {
                    controlValue = UITextExtractor.CleanText(optDropdown.popupList.value);
                    controlType = "dropdown";
                    if (!optDropdown.popupList.enabled)
                        controlType = "unavailable";
                }
            }
            else if (optCheckbox != null)
            {
                if (optCheckbox.label != null)
                    announcement = UITextExtractor.CleanText(optCheckbox.label.text);
                if (optCheckbox.checkbox != null)
                {
                    controlValue = optCheckbox.checkbox.value ? "checked" : "unchecked";
                    controlType = "checkbox";
                    if (!optCheckbox.checkbox.enabled)
                        controlType = "unavailable";
                }
            }
            else if (optScrollbar != null)
            {
                if (optScrollbar.label != null)
                    announcement = UITextExtractor.CleanText(optScrollbar.label.text);
                if (optScrollbar.slider != null)
                {
                    // Use valueLabel if it exists and is enabled
                    if (optScrollbar.valueLabel != null && optScrollbar.valueLabel.enabled &&
                        !string.IsNullOrEmpty(optScrollbar.valueLabel.text))
                    {
                        controlValue = UITextExtractor.CleanText(optScrollbar.valueLabel.text);
                    }
                    else
                    {
                        int percent = Mathf.RoundToInt(optScrollbar.slider.value * 100);
                        controlValue = $"{percent} percent";
                    }
                    controlType = "slider";
                    if (!optScrollbar.slider.enabled)
                        controlType = "unavailable";
                }
            }
            else
            {
                // Standard NGUI controls
                UILabel label = element.GetComponent<UILabel>();
                if (label == null)
                    label = element.GetComponentInChildren<UILabel>();

                if (label != null)
                    announcement = UITextExtractor.CleanText(label.text);

                UIButton button = element.GetComponent<UIButton>();
                UIToggle toggle = element.GetComponent<UIToggle>();
                UISlider slider = element.GetComponent<UISlider>();
                UIPopupList popupList = element.GetComponent<UIPopupList>();
                UIInput input = element.GetComponent<UIInput>();

                if (toggle != null)
                {
                    controlValue = toggle.value ? "checked" : "unchecked";
                    controlType = "checkbox";
                }
                else if (slider != null)
                {
                    int percent = Mathf.RoundToInt(slider.value * 100);
                    controlValue = $"{percent} percent";
                    controlType = "slider";
                }
                else if (popupList != null)
                {
                    controlValue = popupList.value;
                    controlType = "dropdown";
                }
                else if (input != null)
                {
                    if (!string.IsNullOrEmpty(input.value))
                        controlValue = input.value;
                    controlType = "edit";
                }
                else if (button != null)
                {
                    controlType = button.isEnabled ? "button" : "unavailable";
                }
            }

            // Build the final announcement
            if (string.IsNullOrEmpty(announcement))
                announcement = element.name;

            if (!string.IsNullOrEmpty(controlValue))
                announcement += $", {controlValue}";

            if (!string.IsNullOrEmpty(controlType))
                announcement += $", {controlType}";

            return announcement;
        }

        // ========== Activation ==========

        private void ActivateSelected()
        {
            GameObject current = UICamera.selectedObject;
            if (current == null) return;

            // Check for OPT_Checkbox
            OPT_Checkbox optCheckbox = current.GetComponent<OPT_Checkbox>();
            if (optCheckbox == null) optCheckbox = current.GetComponentInParent<OPT_Checkbox>();
            if (optCheckbox != null && optCheckbox.checkbox != null)
            {
                if (!optCheckbox.checkbox.enabled)
                {
                    ScreenReaderManager.SpeakInterrupt("unavailable");
                    return;
                }
                optCheckbox.checkbox.value = !optCheckbox.checkbox.value;
                string state = optCheckbox.checkbox.value ? "checked" : "unchecked";
                ScreenReaderManager.SpeakInterrupt(state);
                MelonLogger.Msg($"[GenericMenuState] OPT_Checkbox toggled: {current.name} = {optCheckbox.checkbox.value}");
                return;
            }

            // Check for OPT_Dropdown
            OPT_Dropdown optDropdown = current.GetComponent<OPT_Dropdown>();
            if (optDropdown == null) optDropdown = current.GetComponentInParent<OPT_Dropdown>();
            if (optDropdown != null && optDropdown.popupList != null)
            {
                if (!optDropdown.popupList.enabled)
                {
                    ScreenReaderManager.SpeakInterrupt("unavailable");
                    return;
                }
                // Cycle to next value on Enter
                CycleDropdownValue(optDropdown, 1);
                MelonLogger.Msg($"[GenericMenuState] OPT_Dropdown cycled: {current.name}");
                return;
            }

            // Check for OPT_Scrollbar
            OPT_Scrollbar optScrollbar = current.GetComponent<OPT_Scrollbar>();
            if (optScrollbar == null) optScrollbar = current.GetComponentInParent<OPT_Scrollbar>();
            if (optScrollbar != null && optScrollbar.slider != null)
            {
                if (!optScrollbar.slider.enabled)
                {
                    ScreenReaderManager.SpeakInterrupt("unavailable");
                    return;
                }
                string valueText;
                if (optScrollbar.valueLabel != null && optScrollbar.valueLabel.enabled)
                    valueText = UITextExtractor.CleanText(optScrollbar.valueLabel.text);
                else
                {
                    int percent = Mathf.RoundToInt(optScrollbar.slider.value * 100);
                    valueText = $"{percent} percent";
                }
                ScreenReaderManager.SpeakInterrupt($"{valueText}, use left and right to adjust");
                return;
            }

            // Check for standard toggle
            UIToggle toggle = current.GetComponent<UIToggle>();
            if (toggle != null)
            {
                toggle.value = !toggle.value;
                string state = toggle.value ? "checked" : "unchecked";
                ScreenReaderManager.SpeakInterrupt(state);
                MelonLogger.Msg($"[GenericMenuState] Toggled: {current.name} = {toggle.value}");
                return;
            }

            // Check for popup list
            UIPopupList popupList = current.GetComponent<UIPopupList>();
            if (popupList != null)
            {
                current.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                MelonLogger.Msg($"[GenericMenuState] Opened popup: {current.name}");
                return;
            }

            // Check if button is disabled
            UIButton button = current.GetComponent<UIButton>();
            if (button != null && !button.isEnabled)
            {
                ScreenReaderManager.SpeakInterrupt("unavailable");
                return;
            }

            // Trigger click
            current.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            MelonLogger.Msg($"[GenericMenuState] Clicked: {current.name}");
        }

        // ========== Close Menu ==========

        private void CloseMenu()
        {
            GUIScreen[] screens = UnityEngine.Object.FindObjectsOfType<GUIScreen>();
            GUIScreen topScreen = null;

            foreach (var screen in screens)
            {
                if (screen == null || !screen.gameObject.activeInHierarchy) continue;
                if (screen.isTopMenu)
                {
                    topScreen = screen;
                    break;
                }
            }

            if (topScreen != null)
            {
                topScreen.Close();
                ScreenReaderManager.SpeakInterrupt("Closed");
                MelonLogger.Msg($"[GenericMenuState] Closed: {topScreen.name}");
            }
        }
    }
}
