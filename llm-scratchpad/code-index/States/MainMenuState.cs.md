File: States/MainMenuState.cs — keyboard navigation for the main menu (Continue/Load/New Game/Options/Credits/Exit), active only when MainMenu is the topmost screen (priority 60).

namespace Wasteland2AccessibilityMod.States  (line 7)

class MainMenuState : IAccessibilityState  (line 15)

    // --- Interface properties ---
    public string Name => "MainMenu"  (line 17)
    public int Priority => 60  (line 18)

    // --- Fields ---
    private GameObject lastSelectedObject = null  (line 21)
        // note: tracks UICamera.selectedObject to detect external selection changes (mouse hover, controller).
    private int currentIndex = -1  (line 24)
    private List<ButtonEntry> menuButtons = new List<ButtonEntry>()  (line 27)

    // --- Nested type ---
    private class ButtonEntry  (line 29)
        public GameObject gameObject  (line 31)
        public UILabel label  (line 32)
        public UIButton button  (line 33)
        public string name  (line 34)

    // --- Interface property ---
    // Active only when GUIManager has a MainMenu and menu.isTopMenu is true (not covered by a submenu).
    public bool IsActive { get; }  (line 38)

    // --- Interface methods ---
    // Rebuilds button list if empty; handles Up/Down navigation, Enter activation, and external-change detection.
    public bool HandleInput()  (line 52)
        // note: RebuildButtonList is called lazily (only when menuButtons is empty).
        //       CheckForExternalSelectionChange always runs, even when no key was pressed, so mouse navigation is tracked.

    // Clears state, rebuilds button list, restores or auto-selects first enabled button, announces "Main Menu" with nav hint.
    public void OnActivated()  (line 90)
        // note: hardcoded announcement string "Main Menu. Use Up and Down arrows to navigate, Enter to select." (line 129).
        //       Uses FindCurrentSelectionIndex() to preserve any pre-existing UICamera selection.

    // Clears all state.
    public void OnDeactivated()  (line 138)

    // --- Private helpers ---
    private MainMenu GetMainMenu()  (line 146)

    // Builds menuButtons in visual top-to-bottom order from MainMenu fields; skips buttons whose GameObject is inactive.
    private void RebuildButtonList(MainMenu menu)  (line 152)
        // note: hardcoded button order — Continue, Load, New Game, Options, Credits, Exit.
        //       Credits entry uses menu.creditsLabel for name but null for label field (creditsButton is UIButtonKeys).
        //       Exit uses menu.exitButton (raw GameObject, not UIButton) — GetComponent<UIButton>() called inline.

    // Finds previous enabled button (wraps), calls SetSelection and AnnounceButton.
    private void NavigateUp()  (line 231)
        // note: skips disabled buttons; wraps around; does nothing if only one or no enabled buttons.

    // Finds next enabled button (wraps), calls SetSelection and AnnounceButton.
    private void NavigateDown()  (line 262)

    // Sets UICamera.selectedObject and sends OnHover(true) for visual feedback.
    private void SetSelection(int index)  (line 291)

    // Speaks button name, appends ", unavailable" if disabled, appends position among enabled buttons ("N of total").
    private void AnnounceButton(int index)  (line 305)
        // note: position is computed only among enabled buttons, not total button count.

    // Searches menuButtons for the one matching UICamera.selectedObject; returns -1 if not found.
    private int FindCurrentSelectionIndex()  (line 342)

    // Compares UICamera.selectedObject to lastSelectedObject; if changed and the new object is a known button, updates currentIndex and announces it.
    private void CheckForExternalSelectionChange()  (line 358)

    // Returns UITextExtractor.CleanText(label.text) or null if label is null.
    private string GetLabelText(UILabel label)  (line 375)

    // Checks enabled state, then fires SendMessage("OnClick") on the selected button's GameObject.
    private void ActivateSelectedButton()  (line 381)
