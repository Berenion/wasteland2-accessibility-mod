File: States/GenericMenuState.cs — fallback accessibility state for any GUIScreen-based menu (Options, Pause, SaveLoad, SkillUseMenu, ModItemMenu, etc.); priority 55

namespace Wasteland2AccessibilityMod.States  (line 8)

class GenericMenuState : IAccessibilityState  (line 15)

    // --- Identity ---
    public string Name => "GenericMenu"  (line 17)
    public int Priority => 55  (line 18)

    // --- Fields ---
    private GameObject lastSelectedObject  (line 21)
    private string lastAnnouncedText  (line 22)
    private float activationTime  (line 25)
    private const float ANNOUNCEMENT_DELAY = 0.3f  (line 26)
    private bool initialAnnouncementDone  (line 29)
    private bool selectionEnsured  (line 32)
    private List<GameObject> optionsControls  (line 35)
        // note: shared control list used by all specialized screen types (Options panels, SaveLoad entries, SkillUseMenu buttons, UIGrid buttons).
    private int optionsControlIndex  (line 36)
    private OptionsMenu cachedOptionsMenu  (line 39)
    private GUIScreen cachedTopScreen  (line 42)
    private SaveLoadScreen cachedSaveLoadScreen  (line 45)
    private HUD_SkillUseMenu cachedSkillUseMenu  (line 48)
    private bool isEditingTextField  (line 51)
    private string editingOriginalValue  (line 52)

    // When true, UIInput.ProcessEvent and UIInput.Update are blocked by Harmony patch; prevents accidental text entry while navigating controls.
    internal static bool blockUIInput  (line 58)
        // note: static; read by a Harmony patch outside this class.

    // Tab order for OptionsMenu
    private static readonly string[] tabOrder = { "gameplay", "display", "controls", "audio" }  (line 61)

    // --- IsActive ---
    public bool IsActive { get; }  (line 63)
        // note: returns false when any of the following are true:
        //   GUIManager not present or no menu active;
        //   Drama.isConversationOn (ConversationState handles that);
        //   MainMenu.isTopMenu (MainMenuState handles that);
        //   CharacterScreen active (CharacterState handles that);
        //   CharacterInfoMenu or PopupInventoryMenu visible without an overlay (InventoryState handles those);
        //   top screen is ModalMessageMenu (DialogState handles that);
        //   top screen is KeypadMenu (KeypadState handles that).
        //   ModItemMenu open forces active even over CharacterInfoMenu/PopupInventoryMenu.

    // --- IAccessibilityState interface ---

    // Processes all keyboard input while active; returns true if input was consumed.
    public bool HandleInput()  (line 115)
        // note: sets all three InputSuppressor flags every frame.
        // Big two-phase dispatch:
        //   Phase 1 (lines 128-240): if isEditingTextField, handle Escape/Enter/Backspace/inputString directly
        //     — uses reflection to write UIInput.mValue to avoid triggering ExecuteOnChange -> OnInputEntry -> OnSaveClicked.
        //   Phase 2 (lines 244-335): normal navigation — detects top-screen change and calls ReinitializeForScreen,
        //     then dispatches PageUp/PageDown (Options tabs), arrow keys, Tab, Delete (SaveLoad), Enter, Escape.
        //     Returns false if top screen is ModalMessageMenu (defers to DialogState).

    // Resets all cached state, sets blockUIInput=true, finds top screen, calls ReinitializeForScreen.
    public void OnActivated()  (line 338)

    // Clears all cached references and resets flags; sets blockUIInput=false.
    public void OnDeactivated()  (line 347)

    // --- Private: screen initialization ---

    // Reinitializes for a new top screen; called on activation and on top-screen change.
    private void ReinitializeForScreen(GUIScreen topScreen)  (line 368)
        // note: detects specialized screen type (OptionsMenu, SaveLoadScreen, HUD_SkillUseMenu) and
        //   calls the matching BuildXxxControlList. Falls back to BuildGridButtonList for unknown screens.
        //   Then calls AnnounceMenu and EnsureSelection.

    // --- Private: menu announcement ---

    // Speaks the menu name; customizes wording for SaveLoad, Options, SkillUseMenu, or generic CamelCase screens.
    private void AnnounceMenu(GUIScreen topScreen)  (line 408)
        // note: SaveLoad announces "Load Game" or "Save Game, Delete key to delete a save";
        //   Options announces "Options, <tab> . Page Up and Page Down to switch tabs".
        //   Generic names are passed through FormatCamelCase.

    // Inserts spaces before uppercase transitions in a CamelCase string (e.g. "PauseMenu" -> "Pause Menu").
    private string FormatCamelCase(string text)  (line 444)

    // Returns the name ("Gameplay", "Display", "Controls", "Audio") of the currently visible OptionsMenu panel.
    private string GetActiveTabName(OptionsMenu menu)  (line 458)

    // --- Private: Options tab switching ---

    // Switches to prev/next Options tab with wrapping; skips the Controls tab if its button is hidden.
    private void SwitchOptionsTab(int direction)  (line 469)
        // note: rebuilds optionsControls after switch; resets initialAnnouncementDone so first-item is re-announced.

    // Calls the private OnGameplayClicked/OnDisplayClicked/OnControlsClicked/OnAudioClicked method via reflection.
    private void SwitchToTab(int tabIndex)  (line 511)
        // note: uses reflection (BindingFlags.Instance | NonPublic). Big switch on tabIndex 0-3 maps to method name.

    // --- Private: initial selection ---

    // Speaks the currently tracked element after the ANNOUNCEMENT_DELAY.
    private void ForceAnnounceCurrentSelection()  (line 542)
        // note: prefers optionsControls[optionsControlIndex] over UICamera.selectedObject to avoid game overrides.

    // Ensures at least one interactive element is selected; runs at most once per activation (selectionEnsured guard).
    private void EnsureSelection(GUIScreen topScreen)  (line 567)
        // note: for OptionsMenu calls BuildOptionsControlList first.
        //   If optionsControls populated, selects index 0 and returns.
        //   Otherwise validates UICamera.selectedObject is interactive, then scans UIButtonKeys[] as fallback.

    // --- Private: control list builders ---

    // Builds optionsControls from UIGrid children (buttons only); used for PauseMenu and similar grids.
    private void BuildGridButtonList(GUIScreen screen)  (line 642)

    // Builds optionsControls for SaveLoadScreen: save entries sorted by save time descending, then nameInput, save/load button, close button.
    private void BuildSaveLoadControlList()  (line 675)
        // note: sorts entries by reading the private SaveGameListEntry.saveTime field via reflection.

    // Builds optionsControls for HUD_SkillUseMenu: HUD_HotkeyOptionButton children of buttonGrid, then close button.
    private void BuildSkillUseMenuControlList()  (line 752)

    // Builds optionsControls for OptionsMenu: children of the active panel's UIGrid sorted by name; falls back to scanning for OPT_* components.
    private void BuildOptionsControlList()  (line 783)
        // note: UIGrid children are sorted by name to match UIGrid's sorted=true order.

    // Returns the currently active panel GameObject from OptionsMenu (gameplayPanel / displayPanel / controlsPanel / audioPanel).
    private GameObject GetActivePanel(OptionsMenu menu)  (line 842)

    // --- Private: navigation ---

    // Dispatches to NavigateControlList if optionsControls is populated, otherwise NavigateGeneric.
    private void Navigate(KeyCode direction)  (line 853)

    // Navigates the indexed control list; Left/Right adjusts sliders or cycles dropdowns; Up/Down/Tab move index with wrapping.
    private void NavigateControlList(KeyCode direction)  (line 866)
        // note: for SaveLoadScreen entries, calls cachedSaveLoadScreen.OnEntrySelected on navigation.
        //   Announces directly from optionsControls to avoid UICamera.selectedObject overrides.

    // Cycles an OPT_Dropdown's value by direction (+1 or -1), with wrapping; speaks "unavailable" if disabled.
    private void CycleDropdownValue(OPT_Dropdown dropdown, int direction)  (line 964)

    // Returns the index in optionsControls of UICamera.selectedObject, or the first ancestor control; returns 0 if not found.
    private int FindCurrentControlIndex()  (line 987)

    // Falls back to UIButtonKeys-link navigation for menus without a control list.
    private void NavigateGeneric(KeyCode direction)  (line 1007)
        // note: follows UIButtonKeys.selectOnUp/Down/Left/Right chain.
        //   Tab maps to the first non-null of selectOnRight/Down/Up/Left.
        //   For SaveLoadScreen, manually calls OnEntrySelected because that callback only fires in gamepad mode.

    // --- Private: selection tracking ---

    // Returns the first active non-MainMenu GUIScreen with isTopMenu=true.
    private GUIScreen FindTopScreen()  (line 1088)

    // Compares UICamera.selectedObject to lastSelectedObject; speaks on change after ANNOUNCEMENT_DELAY.
    private void CheckAndAnnounceSelection()  (line 1100)

    // --- Private: text extraction ---

    // Returns a human-readable announcement string for element, covering all control types.
    private string GetElementAnnouncement(GameObject element)  (line 1129)
        // note: large dispatch in priority order:
        //   1. HUD_HotkeyOptionButton — name + count + "skill"/"item"
        //   2. INV_DragDropItem — delegates to BuildDragDropItemAnnouncement
        //   3. SaveGameListEntry — name, location, time
        //   4. OPT_Dropdown / OPT_Checkbox / OPT_Scrollbar (also checks parents)
        //   5. Standard NGUI: UILabel + UIButton/UIToggle/UISlider/UIPopupList/UIInput
        //   Final format: "{label}, {value}, {controlType}"

    // Builds "{item name}, equipped by {PC}" or "{item name}, in {PC}'s backpack" for an INV_DragDropItem.
    private string BuildDragDropItemAnnouncement(INV_DragDropItem dragDrop)  (line 1318)
        // note: equipped state is inferred by comparing item reference against owner PC's WeaponR/WeaponL slots,
        //   because ModItemMenu.PopulateData does not set dragDrop.slot.

    // --- Private: activation ---

    // Activates/clicks the currently tracked control; handles every specialized control type.
    private void ActivateSelected()  (line 1356)
        // note: large dispatch in priority order:
        //   1. HUD_HotkeyOptionButton — invokes HUD_SkillUseMenu.OnHotkeyOptionButtonClicked via reflection.
        //   2. SaveGameListEntry — calls OnEntrySelected then OnSaveClicked/OnLoadClicked (sets SaveLoadScreenSuppressor.AllowNextAction).
        //   3. SaveLoadScreen controls — nameInput enters isEditingTextField mode; save/load/close buttons call their handlers.
        //   4. OPT_Checkbox — toggles value, speaks "checked"/"unchecked".
        //   5. OPT_Dropdown — cycles via CycleDropdownValue.
        //   6. OPT_Scrollbar — announces current value and "use left and right to adjust".
        //   7. UIToggle — toggles value.
        //   8. UIPopupList — SendMessage("OnClick").
        //   9. INV_DragDropItem — invokes controllerACallback via reflection (bypasses isGamepadOn check).
        //  10. Disabled UIButton — speaks "unavailable".
        //  11. Fallback — SendMessage("OnClick").

    // --- Private: close ---

    // Finds the top GUIScreen, sets EventManager.ignoreNextBack=true, calls screen.Close(), speaks "Closed".
    private void CloseMenu()  (line 1572)
        // note: ignoreNextBack prevents the Escape "Back" event bleeding into the next frame and re-opening the pause menu.
