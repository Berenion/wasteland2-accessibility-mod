File: States/CharacterState.cs — accessibility state for character creation / squad creation screen, covering all 8 panel types with keyboard navigation, value editing, derived-stats browser, and info browser.

namespace Wasteland2AccessibilityMod.States  (line 8)

class CharacterState : IAccessibilityState  (line 15)
    // Priority 50 — same as Inventory/Conversation.
    // Handles panels: UseDefaultParty, Party, AddCharacter, Attributes, Skills, Traits, Dossier, Flavor.

    public string Name => "Character"  (line 17)
    public int Priority => 50  (line 18)

    // Navigation state
    private List<GameObject> controlList  (line 21)
    private int controlIndex  (line 22)
    private CharacterScreen.EditorPanel lastPanelType  (line 23)
    private bool skillsFocused  (line 24)   // true when Attributes panel has switched to skills sub-area
    private bool isEditingTextField  (line 25)   // true when user is typing in a UIInput
    private bool isEditingValue  (line 26)   // true when in Left/Right attribute/skill adjustment mode

    // Derived stats browsing mode
    private int derivedStatsIndex  (line 29)

    // When true, the derived stats browser is open.
    internal static bool derivedStatsBrowsing  (line 34)

    private enum InfoMode  (line 37)
        None
        StatDescription
        TraitDescription

    private InfoMode infoMode  (line 39)
    private List<string> infoLines  (line 40)
    private int infoIndex  (line 41)

    // When true, UIInput.ProcessEvent is blocked by Harmony patch (blocks text-field key events globally).
    internal static bool blockUIInput  (line 46)

    // Announcement dedup tracking
    private string lastAnnouncedText  (line 49)
    private int lastAnnouncedIndex  (line 50)
    private float activationTime  (line 51)
    private bool initialAnnouncementDone  (line 52)
    private const float ANNOUNCEMENT_DELAY = 0.3f  (line 53)

    // Reflection caches (all static, CharacterScreen-specific)
    private static FieldInfo panelTypeField  (line 56)       // CharacterScreen.panelType
    private static FieldInfo currentPCField  (line 57)       // CharacterScreen.currentPC
    private static FieldInfo skillEditorsField  (line 58)    // CHA_SkillPanel.skillEditors
    private static FieldInfo skillCurrentCategoryField  (line 59)
    private static FieldInfo traitEditorsField  (line 60)    // CHA_TraitsPanel.traitEditors
    private static FieldInfo attrEditorsField  (line 61)     // CHA_AttributePanel.attributeEditors
    private static FieldInfo addCharEntryListField  (line 62)
    private static FieldInfo addCharCurrentEntryField  (line 63)
    private static FieldInfo traitCurrentEditorField  (line 64)
    private static FieldInfo statDisplayListField  (line 65)
    private static MethodInfo onDoneClickedMethod  (line 66)
    private static bool reflectionCached  (line 67)

    public bool IsActive { get; }  (line 69)
    // note: returns false when CHA_InventoryPanel is active (InventoryState handles that).

    public bool HandleInput()  (line 86)
    // note: if isEditingTextField, only Escape (cancel) and Enter (confirm) are intercepted;
    //       all other keys pass through to UIInput. After that, detects panel changes and fires
    //       OnPanelChanged(). Dispatches to HandleDerivedStatsInput / HandleInfoInput when their
    //       modes are active, otherwise routes via switch on lastPanelType.
    //       note: big switch table — one arm per EditorPanel value (lines ~150–170).

    public void OnActivated()  (line 173)
    // Resets all state, caches reflection, calls OnPanelChanged for the current panel.

    public void OnDeactivated()  (line 203)
    // Clears all mode flags, resets blockUIInput to false.

    // ---- Reflection ----  (section, line 221)

    private void CacheReflection()  (line 223)
    // Populates all static FieldInfo/MethodInfo caches; also calls CharacterAnnouncementHelper.EnsureReflectionCached().

    private CharacterScreen.EditorPanel GetCurrentPanelType(CharacterScreen screen)  (line 250)
    // Reads panelTypeField via reflection; falls back to checking which panel GameObject is active.

    private PC GetCurrentPC(CharacterScreen screen)  (line 278)
    // Reads currentPCField via reflection from the screen instance.

    // ---- Panel Change Detection ----  (section, line 285)

    private void OnPanelChanged(CharacterScreen screen, CharacterScreen.EditorPanel newPanel)  (line 287)
    // Resets all sub-mode state, calls BuildControlList, announces panel name with context hints.
    // note: if a TutorialPopupMenu is active it queues the announcement (Speak) instead of interrupting,
    //       so DialogState's tutorial announcement is heard first.

    private string GetPanelDisplayName(CharacterScreen.EditorPanel panel)  (line 347)
    // Switch over EditorPanel → human-readable string (e.g. Flavor → "Customization").

    private int GetPartyCount(CharacterScreen screen)  (line 363)
    // Counts party entries where infoContainer is active (i.e. slot is occupied).

    // ---- Control List Building ----  (section, line 380)

    private void BuildControlList(CharacterScreen screen, CharacterScreen.EditorPanel panel)  (line 382)
    // Dispatch switch — calls the appropriate Build*Controls method for the panel.

    private void BuildUseDefaultPartyControls(CharacterScreen screen)  (line 418)
    // Finds UIButtons with meaningful labels on usePremadePartyPanel; deduplicates by name.

    private void BuildPartyControls(CharacterScreen screen)  (line 446)
    // Adds active partyEntries + the Start Playing button if visible.

    private void BuildAddCharacterControls(CharacterScreen screen)  (line 467)
    // Reads CHA_PremadeCharacterEntry list via addCharEntryListField reflection;
    // falls back to scanning entryContainer UIGrid children.

    private void BuildAttributeControls(CharacterScreen screen)  (line 510)
    // Reads attributeGrid children, sorts by name to match UIGrid order.

    private void BuildSkillControls(CharacterScreen screen)  (line 536)
    // Gets active skill grid via GetActiveSkillGrid(); skips disabled CHA_SkillEditor components (DLC skills).

    private UIGrid GetActiveSkillGrid(CharacterScreen screen)  (line 570)
    // Returns the first active grid among combatGrid / knowledgeGrid / generalGrid; defaults to combatGrid.

    private string GetActiveSkillCategory(CharacterScreen screen)  (line 584)
    // Returns "Combat", "Knowledge", or "General" based on which grid is active.

    private void BuildTraitControls(CharacterScreen screen)  (line 595)
    // Reads traitEditors via reflection; falls back to scanning traitsPanel.traitGrid children.

    private void BuildDossierControls(CharacterScreen screen)  (line 633)
    // Builds: gender buttons (Male/Female found recursively) + flavor panel inputs (name, age,
    // ethnicity, religion, smokes, biography) + portrait/appearance buttons (deduped by name).

    private Transform FindChildRecursive(Transform parent, string name)  (line 701)
    // Recursive depth-first child search by exact name; only returns active GameObjects.

    private void BuildFlavorControls(CharacterScreen screen)  (line 714)
    // Delegates to BuildDossierControls (same layout).

    // ---- Navigation ----  (section, line 720)

    private void NavigateList(int direction)  (line 722)
    // Wraps around; calls SelectControl + AnnounceCurrentControl on index change.

    // Sets UICamera.selectedObject; guards against UIInput auto-entering edit mode.
    private void SelectControl(GameObject obj)  (line 744)

    private void AnnounceCurrentControl(bool interrupt = true)  (line 749)
    // Announces via SpeakInterrupt or Speak; deduplicates against lastAnnouncedText + lastAnnouncedIndex.

    // ---- Control Announcements ----  (section, line 766)

    private string GetControlAnnouncement(GameObject obj)  (line 768)
    // Component-type dispatch: CHA_AttributeEditor → GetAttributeEditorAnnouncement,
    //   CHA_SkillEditor → GetSkillEditorAnnouncement, CHA_TraitEditor → GetTraitEditorAnnouncement,
    //   CHA_PartyEntry → GetPartyEntryAnnouncement, CHA_PremadeCharacterEntry → GetPremadeEntryAnnouncement,
    //   UIInput → "<label>, <value>, text field", UIPopupList → "<label>, <value>, dropdown",
    //   UIButton → "<text>, button" (with special handling for Male/Female buttons showing selected state).

    private string GetAttributeEditorAnnouncement(CHA_AttributeEditor editor)  (line 852)
    // Delegates to CharacterAnnouncementHelper.GetAttributeEditorAnnouncement.

    private string GetSkillEditorAnnouncement(CHA_SkillEditor editor)  (line 857)
    // Delegates to CharacterAnnouncementHelper.GetSkillEditorAnnouncement.

    private string GetTraitEditorAnnouncement(CHA_TraitEditor editor)  (line 862)
    // Delegates to CharacterAnnouncementHelper.GetTraitEditorAnnouncement.

    private string GetPartyEntryAnnouncement(CHA_PartyEntry entry)  (line 867)
    // Builds: name + role, attribute pairs, skill pairs (non-zero only), trait label, edit/delete hint.
    // note: uses BuildPairedLabelText to zip newline-separated name/value UILabel pairs.

    private string BuildPairedLabelText(UILabel nameLabel, UILabel valueLabel)  (line 921)
    // Zips parallel newline-split label texts into "Name Value" pairs; skips pairs with value "0".

    private string GetPremadeEntryAnnouncement(CHA_PremadeCharacterEntry entry)  (line 946)
    // Returns "name, specialization" from entry labels.

    // Gets a descriptive label for a UIInput field by checking known flavor panel references.
    private string GetInputFieldLabel(GameObject obj, UIInput input)  (line 970)
    // note: matches against flavor.nameInput / ageInput / biographyInput by reference; falls back to FindLabelText.

    private string FindLabelText(GameObject obj)  (line 986)
    // Searches for UILabel in children, then parent's children; returns obj.name as last resort.

    // ---- Stat Description Helper ----  (section, line 1004)

    private void AnnounceCurrentStatDescription()  (line 1006)
    // Opens info browser in StatDescription mode for the currently focused control.

    // ---- Value Adjustment Helpers ----  (section, line 1012)

    private void AdjustCurrentAttribute(int direction)  (line 1014)
    // Delegates to CharacterAnnouncementHelper.AdjustAttribute with a re-announce callback.

    private void AdjustCurrentSkill(int direction)  (line 1022)
    // Delegates to CharacterAnnouncementHelper.AdjustSkill with a re-announce callback.

    // ---- Panel-Specific Input Handlers ----  (section, line 1030)

    private bool HandleCommonInput(CharacterScreen screen)  (line 1032)
    // Common keys across all panels: Tab=re-announce panel+position, D=derived stats or summary,
    //   N=OnDoneClicked. Escape is intentionally NOT handled here (let game dispatch Back event).

    private bool HandleUseDefaultPartyInput(CharacterScreen screen)  (line 1081)
    // Up/Down navigate, Enter activates focused button via SendMessage("OnClick").

    private bool HandlePartyInput(CharacterScreen screen)  (line 1105)
    // Up/Down navigate; Enter clicks party entry via CHA_PartyPanel.OnPartyEntryClicked (reflection)
    //   or generic SendMessage fallback; I re-announces details; Delete calls OnDeleteCharacterClicked;
    //   S shortcut triggers Start Playing button.

    private bool HandleAddCharacterInput(CharacterScreen screen)  (line 1201)
    // Up/Down navigate + fires selectCallback for preview; Enter calls entry.OnAddClicked();
    //   R reads biography label.

    private bool HandleAttributesInput(CharacterScreen screen)  (line 1253)
    // Two modes: normal (Up/Down navigate, +/- adjust, Enter enters edit mode, F toggle to skills,
    //   I description, P points remaining) and isEditingValue (Left/Right or +/- adjust, Enter/Esc exit).

    private bool HandleSkillsInput(CharacterScreen screen)  (line 1363)
    // Mirrors HandleAttributesInput but with skill adjusters; Left/Right in normal mode cycles
    //   skill category via SwitchSkillCategory; F toggles back to attributes when on Attributes panel.
    // note: DLC skill editors (disabled CHA_SkillEditor) are excluded from controlList at build time.

    private bool HandleTraitsInput(CharacterScreen screen)  (line 1475)
    // Up/Down navigate; Enter/Space toggles trait via CharacterAnnouncementHelper.ToggleTrait;
    //   I opens info browser in TraitDescription mode.

    private bool HandleDossierInput(CharacterScreen screen)  (line 1519)
    // Up/Down navigate; Left/Right cycles UIPopupList values or clicks gender buttons;
    //   Enter: enters UIInput edit mode (sets isEditingTextField=true, blockUIInput=false) or SendMessage click.

    private bool HandleFlavorInput(CharacterScreen screen)  (line 1590)
    // Delegates to HandleDossierInput.

    private bool HandleGenericInput(CharacterScreen screen)  (line 1595)
    // Fallback: Up/Down navigate, Enter SendMessage click.

    // ---- Skill Category Switching ----  (section, line 1618)

    private void SwitchSkillCategory(CharacterScreen screen, int direction)  (line 1620)
    // Determines current category (0=Combat,1=Knowledge,2=General), wraps with modulo 3,
    //   calls the appropriate CHA_SkillPanel.On*Clicked() public method (NOT SendMessage).
    //   Rebuilds control list and restarts initial-announcement delay timer.
    // note: direct method calls required — SendMessage("OnClick") does not work for category buttons.

    // ---- Points Remaining ----  (section, line 1665)

    private void AnnounceAttributePointsRemaining(CharacterScreen screen)  (line 1667)
    // Reads attributePanel.GetPointsRemaining() and pointsRemainingTitleLabel.

    private void AnnounceSkillPointsRemaining(CharacterScreen screen)  (line 1685)
    // Reads skillPanel.GetPointsRemaining() and pointsRemainingTitleLabel.

    // ---- Trait Description ----  (section, line 1703)

    private string GetTraitDescription(CHA_TraitEditor editor)  (line 1705)
    // Delegates to CharacterAnnouncementHelper.GetTraitDescription.

    // ---- Derived Stats Browser ----  (section, line 1710)

    private void OpenDerivedStatsBrowser(CharacterScreen screen)  (line 1712)
    // Sets derivedStatsBrowsing=true, announces header, announces first stat.

    private void CloseDerivedStatsBrowser()  (line 1721)
    // Sets derivedStatsBrowsing=false; sets EventManager.ignoreNextBack to suppress Back event;
    //   re-announces current control.

    private bool HandleDerivedStatsInput(CharacterScreen screen)  (line 1733)
    // Esc=close, Up/Down=navigate derived stats (wrapping), I=description, Home/End=jump.
    //   Blocks all other keys (returns true).

    private void AnnounceDerivedStat(CharacterScreen screen, bool interrupt = true)  (line 1781)
    // Delegates to CharacterAnnouncementHelper.AnnounceDerivedStat(pc, derivedStatsIndex, interrupt).

    private void AnnounceDerivedStatDescription(CharacterScreen screen)  (line 1787)
    // Delegates to CharacterAnnouncementHelper.AnnounceDerivedStatDescription(pc, derivedStatsIndex).

    // ---- Character Summary ----  (section, line 1793)

    private void AnnounceCharacterSummary(CharacterScreen screen)  (line 1795)
    // Gets current PC (falls back to Game.GetFirstSelectedPC) and delegates to
    //   CharacterAnnouncementHelper.AnnounceCharacterSummary(pc).

    // ---- Info Browser (I-key descriptions) ----  (section, line 1803)

    private void OpenInfoBrowser(InfoMode mode, GameObject statObj = null, Trait trait = null)  (line 1805)
    // Builds infoLines from CharacterAnnouncementHelper (StatDescription or TraitDescription path);
    //   sets infoMode, announces header with count and navigation hint, then first line.

    private void CloseInfoBrowser()  (line 1835)
    // Sets EventManager.ignoreNextBack; re-announces current control after closing.

    private void AnnounceInfoLine(bool interrupt = true)  (line 1849)
    // Speaks current infoLines[infoIndex] with "N of M" position suffix.

    private bool HandleInfoInput(CharacterScreen screen)  (line 1858)
    // Esc=close, Up/Down/Home/End navigate infoLines; blocks all other keys (returns true).
