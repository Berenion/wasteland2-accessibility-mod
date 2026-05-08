File: States/DialogState.cs — keyboard navigation and announcements for modal dialogs, tutorials, POI panels, difficulty selection, and quantity menus (priority 70).

namespace Wasteland2AccessibilityMod.States  (line 8)

class DialogState : IAccessibilityState  (line 15)

    // --- Interface properties ---
    public string Name => "Dialog"  (line 17)
    public int Priority => 70  (line 18)

    // --- Fields ---
    private int selectedButtonIndex = 0  (line 20)
    private readonly List<DialogButton> buttons = new List<DialogButton>()  (line 21)
    private string currentDialogId = ""  (line 22)
    private DifficultySelectionMenu currentDifficultyMenu  (line 23)
    private int difficultyIndex  (line 24)
    private static readonly string[] DifficultyNames = { "Rookie", "Seasoned", "Ranger", "Legend" }  (line 25)
        // note: hardcoded constant array — four difficulty names indexed by difficultyIndex (0-3).
    private AskQuantityMenu currentQuantityMenu  (line 26)
    private int lastAnnouncedQuantity = -1  (line 27)

    // --- Nested type ---
    private class DialogButton  (line 29)
        public string Label  (line 31)
        public GameObject ButtonObject  (line 32)
        public Action ClickAction  (line 33)

    // --- Interface property ---
    // True when any supported dialog type is active.
    public bool IsActive { get; }  (line 36)
        // note: returns IsModalDialogOpen() || IsTutorialOpen() || IsTutorialPopupMenuOpen() || IsPOIPanelOpen()

    // --- Interface methods ---
    // Reads input and dispatches to dialog-type-specific navigation branches; suppresses all other input while any dialog is open.
    public bool HandleInput()  (line 44)
        // note: three sub-branches — DifficultySelectionMenu (Left/Right for difficulty, Up/Down for buttons),
        //       AskQuantityMenu (Left/Right/PageUp/PageDown/Home/End for quantity, Up/Down for OK/Cancel),
        //       standard dialogs (Left/Up previous, Right/Down next). Enter always activates selected button.
        //       Always sets ShouldSuppressUINavigation = true and ShouldSuppressGameInput = true before returning.

    // Resets index, refreshes buttons, announces dialog content on first activation.
    public void OnActivated()  (line 217)

    // Clears all state (buttons, dialog id, difficulty/quantity menu refs).
    public void OnDeactivated()  (line 227)

    // --- Private query methods ---
    // Returns true when a ModalMessageMenu exists and is active in hierarchy.
    private bool IsModalDialogOpen()  (line 239)
        // note: uses FindObjectOfType, NOT GUIManager.IsAnyMenuActive() — modals created via
        //       CreateMessageMenu() bypass GUIManager.screens.

    // Returns true when TutorialScreen singleton has an active popup.
    private bool IsTutorialOpen()  (line 251)

    // Returns true when a TutorialPopupMenu (GUIScreen-based) is active.
    private bool IsTutorialPopupMenuOpen()  (line 267)

    // Returns true when HUD_POIPanel is active and not closing.
    private bool IsPOIPanelOpen()  (line 273)

    // Rebuilds the buttons list for whichever dialog type is currently open; returns true if the dialog identity changed.
    private bool RefreshButtons()  (line 283)
        // note: priority order — ModalMessageMenu (including DifficultySelectionMenu and AskQuantityMenu subclasses)
        //       → TutorialPopupMenu → TUT_TutorialPopup → HUD_POIPanel. Returns early after first match.
        //       DifficultySelectionMenu.gameDifficultyIndex and AskQuantityMenu.maxValue are read via reflection.
        //       For POI panels, checks encounterButtonContainer, secondaryEntryButtonContainer, then confirm/cancel.
        //       Returns true when currentDialogId changes — used by HandleInput to re-announce stacked dialogs.

    // Speaks a full contextual announcement when a dialog first opens.
    private void AnnounceDialog()  (line 521)
        // note: dispatch order mirrors RefreshButtons. DifficultySelectionMenu speaks difficulty name + index of 4
        //       + description + navigation hint. AskQuantityMenu speaks title/message + quantity/max + unit price
        //       (if unitValue > 0, read via reflection). Standard modal speaks title + message + first button.
        //       POI panel speaks title + description + first button. TutorialPopupMenu/TUT_TutorialPopup prefix "Tutorial.".
        //       All paths call ScreenReaderManager.SpeakInterrupt().

    // Announces the currently focused button with position info ("label, N of total").
    private void AnnounceButton()  (line 690)

    // Invokes the ClickAction of the selected button and speaks "label pressed".
    private void ActivateButton()  (line 703)

    // --- Region: AskQuantityMenu Helpers ---

    // Clamps current quantity by delta within min/max read via reflection, then calls SetQuantity.
    private void AdjustQuantity(int delta)  (line 727)
        // note: reads AskQuantityMenu.minValue and AskQuantityMenu.maxValue via reflection.

    // Sets quantity field, updates quantityInput text and slider position, announces change with optional scrap total.
    private void SetQuantity(int value)  (line 741)
        // note: sets AskQuantityMenu.quantity via reflection; updates slider only when max > min.
        //       Reads AskQuantityMenu.unitValue via reflection — if > 0, appends total scrap to announcement.
        //       Only announces when value != lastAnnouncedQuantity to suppress redundant speech.
