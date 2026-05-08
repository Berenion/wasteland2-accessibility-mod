File: States/CharacterInfoState.cs — keyboard nav and screen reader support for CharacterInfoMenu tabs: Attributes, Skills, Traits, Dossier, Logbook

namespace Wasteland2AccessibilityMod.States  (line 8)

class CharacterInfoState : IAccessibilityState  (line 15)

    // IAccessibilityState identity
    public string Name => "CharacterInfo"  (line 17)
    public int Priority => 50  (line 18)

    // Static flags
    // Kept for potential future use; no longer set by CharacterInfoPatches
    public static bool openToAttributes = false  (line 25)
    // When true, UIInput.ProcessEvent is blocked
    internal static bool blockUIInput = false  (line 30)

    // Navigation state
    private List<GameObject> controlList  (line 33)
    private int controlIndex  (line 34)
    private CharacterInfoMenu.InfoPanel lastPanel  (line 35)

    // Private enum SkillSection  (line 38)
    //   Learned, Combat, Knowledge, General
    private SkillSection currentSkillSection  (line 40)

    // Private enum InfoMode  (line 44)
    //   None, Stats, StatDescription, TraitDescription
    // Private enum StatsSection  (line 45)
    //   Header, Combat, Derived
    private InfoMode infoMode  (line 46)
    private StatsSection currentStatsSection  (line 47)
    private List<string> infoLines  (line 48)
    private int infoIndex  (line 49)

    private bool isEditingValue  (line 52)

    // Suspension support (save/restore index when an overlay takes focus)
    private CharacterInfoMenu.InfoPanel suspendedPanel  (line 55)
    private int suspendedIndex  (line 56)
    private bool hasSuspendedState  (line 57)

    // Announcement dedup tracking
    private string lastAnnouncedText  (line 60)
    private int lastAnnouncedIndex  (line 61)
    private float activationTime  (line 62)
    private bool initialAnnouncementDone  (line 63)
    private const float ANNOUNCEMENT_DELAY = 0.3f  (line 64)

    // Dossier / logbook browsing state
    private List<string> dossierLines  (line 67)
    private bool logbookDetailMode  (line 70)
    private List<string> logbookDetailLines  (line 71)
    private int logbookDetailIndex  (line 72)
    private string currentLogbookEntryName  (line 73)
    private JournalManager.EntrySortType currentLogbookSort  (line 74)

    // Reflection caches (one-time, static)
    private static bool reflectionCached  (line 77)
    private static FieldInfo charInfoCurrentPanelField  (line 78)
    private static FieldInfo charInfoCurrentPCField  (line 79)
    private static FieldInfo skillInfoEditorsField  (line 80)
    private static FieldInfo skillInfoCurrentCategoryField  (line 81)

    // --- IAccessibilityState members ---

    // Active when CharacterInfoMenu is open on a non-Inventory, non-None panel; yields to GenericMenuState when ItemInfoScreen is open
    public bool IsActive { get; }  (line 83)

    // Dispatches to per-panel handle methods; also manages info browser and logbook detail modes
    // note: big switch on lastPanel; info browser and logbook detail modes take priority over panel dispatch
    public bool HandleInput()  (line 108)

    // Resets all transient state; restores from suspension if hasSuspendedState; calls OnPanelChanged to build initial control list
    public void OnActivated()  (line 160)

    // Saves current position to suspension fields; clears transient state
    public void OnDeactivated()  (line 210)

    // --- Panel Detection & Change (#region line 228) ---

    // Reads currentPanel via reflection; falls back to inspecting which sub-panels are active in the hierarchy
    // note: fallback heuristic can misidentify Dossier vs Attributes when both panels are active
    private CharacterInfoMenu.InfoPanel GetCurrentPanel(CharacterInfoMenu menu)  (line 230)

    // Returns currentPC via reflection; returns null on failure
    private PC GetCurrentPC(CharacterInfoMenu menu)  (line 271)

    // Resets per-panel state, calls BuildControlList, announces panel name with context hint and any available points hint
    private void OnPanelChanged(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel newPanel)  (line 278)

    // Maps InfoPanel enum value to display string
    private string GetPanelDisplayName(CharacterInfoMenu.InfoPanel panel)  (line 328)

    // --- Build Control Lists (#region line 344) ---

    // Dispatches to per-panel build method
    private void BuildControlList(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel panel)  (line 346)

    // Collects active children of attributeGrid, sorts by name, adds to controlList
    private void BuildAttributeControls(CharacterInfoMenu menu)  (line 370)

    // Builds from learnedGrid (Learned section) or the active unlearned category grid
    private void BuildSkillControls(CharacterInfoMenu menu)  (line 391)

    // Returns the UIGrid for the current unlearned SkillSection (Combat/Knowledge/General)
    private UIGrid GetActiveUnlearnedGrid(CHA_SkillInfoPanel panel)  (line 418)

    // Maps SkillSection to display string
    private string GetSkillSectionName()  (line 433)

    // Adds learnedTraitGrid then traitGrid children to controlList
    private void BuildTraitControls(CharacterInfoMenu menu)  (line 445)

    // Populates dossierLines with labeled fields; sets controlIndex but leaves controlList empty (dossier is read-only)
    private void BuildDossierControls(CharacterInfoMenu menu)  (line 464)

    // Collects active JNL_JournalEntry components from journalPanel.mainPanel
    private void BuildLogbookControls(CharacterInfoMenu menu)  (line 512)

    // Adds "prefix: text" to dossierLines if label is non-empty
    private void AddDossierLabel(string prefix, UILabel label)  (line 529)

    // Reads skillPointsPerLevelLabel; appends ", buffed" or ", debuffed" by comparing label.color to GUIManager color constants
    private void AddDossierSkillPointsPerLevel(CHA_FlavorDisplayPanel dossier)  (line 541)

    // Appends XP line to dossierLines; includes "level up available" flag when applicable
    private void AddDossierXP(PC pc)  (line 559)

    // Adds active, enabled child GameObjects of a UIGrid to controlList, sorted by name; skips disabled CHA_SkillEditors
    private void AddGridChildren(UIGrid grid)  (line 579)

    // --- Navigation (#region line 607) ---

    // Wraps index within controlList (or dossierLines for Dossier), calls AnnounceCurrentControl
    private void NavigateList(int direction)  (line 609)

    // Wraps controlIndex within dossierLines, announces line with position info
    private void NavigateDossier(int direction)  (line 632)

    // Sets UICamera.selectedObject to the given GameObject
    private void SelectControl(GameObject obj)  (line 650)

    // Announces the current control via CharacterAnnouncementHelper; deduplicates against lastAnnouncedText/lastAnnouncedIndex
    private void AnnounceCurrentControl(bool interrupt = true)  (line 655)

    // --- Common Input (#region line 697) ---

    // Tab=context; D=character summary; PageUp/PageDown=tab switch; F1-F7=party switch; E=XP; S=stats browser; Escape=close
    private bool HandleCommonInput(CharacterInfoMenu menu)  (line 699)

    // Clears hasSuspendedState before calling GoToNextPanel / GoToPreviousPanel
    private void SwitchTab(CharacterInfoMenu menu, int direction)  (line 770)

    // Loops F1-F7, calls SwitchToPartyMember on match
    private bool HandlePartySwitch(CharacterInfoMenu menu)  (line 781)

    // Calls menu.PopulateData(pc, force:true) then rebuilds control list for current panel
    private void SwitchToPartyMember(CharacterInfoMenu menu, int index)  (line 795)

    // --- Attributes Input (#region line 831) ---

    // Handles edit mode (Enter/Esc confirm, Left/Right adjust, I description); normal mode (Up/Down nav, +/- adjust, Enter enter-edit, I description, P points)
    // note: state machine — isEditingValue gates a secondary key map
    private bool HandleAttributesInput(CharacterInfoMenu menu)  (line 833)

    // Delegates to CharacterAnnouncementHelper.AdjustAttribute with a re-announce callback
    private void AdjustCurrentAttribute(int direction)  (line 923)

    // Reads pc.pcTemplate.availableAttributePoints and speaks it
    private void AnnounceAttributePointsRemaining(CharacterInfoMenu menu)  (line 931)

    // --- Skills Input (#region line 947) ---

    // Same edit-mode / normal-mode state machine as Attributes; adds F=cycle section, Left/Right=switch unlearned category, P=points
    // note: state machine — isEditingValue gates a secondary key map
    private bool HandleSkillsInput(CharacterInfoMenu menu)  (line 949)

    // Cycles Learned→Combat→Knowledge→General→Learned; calls the appropriate On*SkillsClicked() on the game's skillPanel
    private void CycleSkillSection(CharacterInfoMenu menu)  (line 1056)

    // Cycles Combat/Knowledge/General; calls On*SkillsClicked on the game's skillPanel
    private void SwitchUnlearnedCategory(CharacterInfoMenu menu, int direction)  (line 1096)

    // Delegates to CharacterAnnouncementHelper.AdjustSkill with a re-announce callback
    private void AdjustCurrentSkill(int direction)  (line 1138)

    // Reads pc.pcTemplate.availableSkillPoints and speaks it
    private void AnnounceSkillPointsRemaining(CharacterInfoMenu menu)  (line 1146)

    // --- Traits Input (#region line 1162) ---

    // Up/Down nav; Enter/Space=toggle trait; I=open trait description browser; P=perk points
    private bool HandleTraitsInput(CharacterInfoMenu menu)  (line 1164)

    // Reads pc.pcTemplate.availableTraitPoints and speaks it
    private void AnnouncePerkPointsRemaining(CharacterInfoMenu menu)  (line 1219)

    // --- Dossier Input (#region line 1235) ---

    // Up/Down=navigate dossierLines; I=quirk description (if on Quirk line) or reads biography line
    private bool HandleDossierInput(CharacterInfoMenu menu)  (line 1237)

    // --- Logbook Input (#region line 1282) ---

    // Up/Down nav; Enter/Right=open details; Left/F=switch sort category; X=toggle flag; I=entry info
    private bool HandleLogbookInput(CharacterInfoMenu menu)  (line 1284)

    // Handles detail-mode keys: Escape/Left=close, Up/Down=step, Tab=position, Home/End; blocks all other keys
    private bool HandleLogbookDetailInput(CharacterInfoMenu menu)  (line 1333)

    // Gets JNL_JournalEntry at controlIndex, reads visible details in reverse order, populates logbookDetailLines and enters detail mode
    // note: iterates details newest-first (i = numDetails-1 downto 0); marks older details as "Complete:"
    private void OpenLogbookDetails(CharacterInfoMenu menu)  (line 1383)

    // Clears logbook detail state, sets ignoreNextBack, re-announces current list entry
    private void CloseLogbookDetails()  (line 1438)

    // Speaks logbookDetailLines[logbookDetailIndex]
    private void AnnounceLogbookDetail(bool interrupt = true)  (line 1451)

    // Builds announcement for current logbook entry including status flags (new/flagged/resolved)
    private void AnnounceLogbookEntry(bool interrupt = true)  (line 1461)

    // Speaks location, originator, and detail count for current logbook entry
    private void AnnounceLogbookEntryInfo()  (line 1504)

    // Cycles Flagged/Ongoing/Resolved; calls On*Clicked on the journal panel; rebuilds controls
    private void SwitchLogbookCategory(CharacterInfoMenu menu, int direction)  (line 1537)

    // Calls JNL_JournalEntry.ToggleFlagged() and speaks new flagged/unflagged state
    private void ToggleLogbookFlag()  (line 1583)

    // Maps EntrySortType to display string
    private string GetLogbookSortName()  (line 1604)

    // --- Info Browser (#region line 1617) ---

    // Builds stats lines for the given section, enters InfoMode.Stats, announces section title and count
    private void OpenStatsBrowser(CharacterInfoMenu menu, StatsSection section)  (line 1619)

    // Wraps section index modulo 3, rebuilds lines, announces new section title
    private void SwitchStatsSection(CharacterInfoMenu menu, int direction)  (line 1644)

    // Dispatch table: delegates to CharacterAnnouncementHelper.Build*Lines per StatsSection
    private static List<string> BuildStatsLinesFor(StatsSection section, PC pc)  (line 1670)

    // Maps StatsSection to display string
    private static string GetStatsSectionTitle(StatsSection section)  (line 1681)

    // Builds info lines for StatDescription or TraitDescription mode and enters that mode
    private void OpenInfoBrowser(CharacterInfoMenu menu, InfoMode mode, GameObject statObj = null, Trait trait = null)  (line 1692)

    // Dispatch table: delegates to CharacterAnnouncementHelper.Build*Lines per InfoMode
    private static List<string> BuildInfoLinesFor(InfoMode mode, PC pc, GameObject statObj, Trait trait)  (line 1716)

    // Maps InfoMode to display string for the browser title
    private static string GetInfoBrowserTitle(InfoMode mode)  (line 1729)

    // Sets infoMode=None, sets ignoreNextBack, re-announces current control
    private void CloseInfoBrowser()  (line 1739)

    // Speaks infoLines[infoIndex] with "N of M" position
    private void AnnounceInfoLine(bool interrupt = true)  (line 1753)

    // Handles info browser keys: Escape=close, Up/Down=step, Home/End; Stats-mode adds Left/Right=switch section, I=derived description; F1-F7=party switch (refreshes lines for Stats mode, closes for others); blocks all other keys
    // note: state machine; F1-F7 branch behaves differently for Stats vs Stat/Trait description modes
    private bool HandleInfoInput(CharacterInfoMenu menu)  (line 1762)

    // --- Stat Description Helper (#region line 1862) ---

    // Opens info browser in StatDescription mode for the GameObject at controlIndex
    private void AnnounceCurrentStatDescription()  (line 1864)

    // --- Reflection (#region line 1874) ---

    // Caches CharacterInfoMenu.currentPanel, currentPC, CHA_SkillInfoPanel.skillEditors and currentCategory via reflection
    // note: uses reflection because fields are private; logs warnings for any that can't be found
    private void CacheReflection()  (line 1876)
