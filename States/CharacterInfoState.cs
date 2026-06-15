using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation and screen reader support for the in-game CharacterInfoMenu
    /// on non-Inventory tabs: Attributes, Skills, Traits, Dossier, Logbook.
    /// Priority 50 - same level as InventoryState/CharacterState.
    /// </summary>
    public class CharacterInfoState : AccessibilityStateBase
    {
        public override string Name => "CharacterInfo";
        public override int Priority => 54;

        public override string GetHelpText()
        {
            return "Character info. Tab announces the panel and position, D reads the character summary, " +
                   "Page Up and Page Down switch tabs, E reads current XP, S opens the stats browser. " +
                   "On Attributes and Skills: Up and Down move, plus and minus or Enter raise with available points, " +
                   "I describes, P points remaining; on Skills, F cycles skill categories. " +
                   "On Traits: Enter or Space toggles if perk points are available, I gives the perk description. " +
                   "On Dossier: I reads the quirk description or the biography. " +
                   "On Logbook: Enter or Right opens an entry, F next sort category, X toggles flagged, I reads the source.";
        }

        /// <summary>
        /// When true, the next CharacterInfoMenu.OnEnable should switch to Attributes tab.
        /// Set by CharacterInfoPatches when C key opens the menu.
        /// </summary>
        // Kept for potential future use but no longer set by CharacterInfoPatches

        /// <summary>
        /// When true, UIInput.ProcessEvent is blocked.
        /// </summary>
        internal static bool blockUIInput = false;

        // Navigation state
        private List<GameObject> controlList = new List<GameObject>();
        private int controlIndex = -1;
        private CharacterInfoMenu.InfoPanel lastPanel = CharacterInfoMenu.InfoPanel.None;

        // Skills section tracking
        private enum SkillSection { Learned, Combat, Knowledge, General }
        private SkillSection currentSkillSection = SkillSection.Combat;

        // Info browser — generic line-list mode used by S (stats browser with
        // Header/Combat/Derived sections) and I (stat / trait descriptions).
        // Up/Down to step, Esc to close.
        private enum InfoMode { None, Stats, StatDescription, TraitDescription }
        private enum StatsSection { Header, Combat, Derived }
        private InfoMode infoMode = InfoMode.None;
        private StatsSection currentStatsSection = StatsSection.Header;
        private List<string> infoLines = new List<string>();
        private int infoIndex = -1;

        // Level-up editing mode
        private bool isEditingValue = false;

        // Suspension support
        private int suspendedIndex = -1;
        private bool hasSuspendedState = false;

        // Announcement tracking
        private string lastAnnouncedText = null;
        private int lastAnnouncedIndex = -1;
        private float activationTime = 0f;
        private bool initialAnnouncementDone = false;
        private const float ANNOUNCEMENT_DELAY = 0.3f;

        // Dossier info browsing
        private List<string> dossierLines = new List<string>();

        // Logbook browsing
        private bool logbookDetailMode = false;
        private List<string> logbookDetailLines = new List<string>();
        private int logbookDetailIndex = -1;
        private string currentLogbookEntryName = null;
        private JournalManager.EntrySortType currentLogbookSort = JournalManager.EntrySortType.Ongoing;

        // Reflection caches
        private static bool reflectionCached = false;
        private static FieldInfo charInfoCurrentPanelField;
        private static FieldInfo charInfoCurrentPCField;

        public override bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;
                var guiManager = MonoBehaviourSingleton<GUIManager>.GetInstance();

                // Yield to GenericMenuState when an overlay screen is open
                if (guiManager.IsItemInfoScreenOpen()) return false;

                var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
                if (charInfoMenu == null || !charInfoMenu.gameObject.activeInHierarchy)
                    return false;

                var currentPanel = GetCurrentPanel(charInfoMenu);

                // Only active for non-Inventory, non-None panels
                if (currentPanel == CharacterInfoMenu.InfoPanel.None ||
                    currentPanel == CharacterInfoMenu.InfoPanel.Inventory)
                    return false;

                return true;
            }
        }

        public override bool HandleInput()
        {
            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu == null) return false;

            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;
            blockUIInput = true;

            if (!reflectionCached) CacheReflection();

            // Detect panel changes
            var currentPanel = GetCurrentPanel(charInfoMenu);
            if (currentPanel != lastPanel)
            {
                OnPanelChanged(charInfoMenu, currentPanel);
            }

            // Delayed initial announcement
            if (!initialAnnouncementDone && Time.time - activationTime >= ANNOUNCEMENT_DELAY)
            {
                initialAnnouncementDone = true;
                AnnounceCurrentControl(interrupt: false);
            }

            // Info browser (Stats / descriptions) intercepts all input
            if (infoMode != InfoMode.None)
                return HandleInfoInput(charInfoMenu);

            // Logbook detail mode intercepts all input
            if (logbookDetailMode)
                return HandleLogbookDetailInput(charInfoMenu);

            // Route by panel
            switch (lastPanel)
            {
                case CharacterInfoMenu.InfoPanel.Attributes:
                    return HandleAttributesInput(charInfoMenu);
                case CharacterInfoMenu.InfoPanel.Skills:
                    return HandleSkillsInput(charInfoMenu);
                case CharacterInfoMenu.InfoPanel.Traits:
                    return HandleTraitsInput(charInfoMenu);
                case CharacterInfoMenu.InfoPanel.Dossier:
                    return HandleDossierInput(charInfoMenu);
                case CharacterInfoMenu.InfoPanel.Logbook:
                    return HandleLogbookInput(charInfoMenu);
                default:
                    return HandleCommonInput(charInfoMenu);
            }
        }

        public override void OnActivated()
        {
            blockUIInput = true;
            lastAnnouncedText = null;
            lastAnnouncedIndex = -1;
            lastPanel = CharacterInfoMenu.InfoPanel.None;
            controlList.Clear();
            controlIndex = -1;
            isEditingValue = false;
            infoMode = InfoMode.None;
            infoIndex = -1;
            infoLines.Clear();
            currentStatsSection = StatsSection.Header;
            activationTime = Time.time;
            initialAnnouncementDone = false;
            logbookDetailMode = false;
            logbookDetailIndex = -1;
            logbookDetailLines.Clear();
            currentLogbookEntryName = null;

            if (!reflectionCached) CacheReflection();
            CharacterAnnouncementHelper.EnsureReflectionCached();

            var charInfoMenu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (charInfoMenu != null)
            {
                // Restore from suspension
                if (hasSuspendedState)
                {
                    hasSuspendedState = false;
                    var currentPanel = GetCurrentPanel(charInfoMenu);
                    OnPanelChanged(charInfoMenu, currentPanel);

                    if (suspendedIndex >= 0 && suspendedIndex < controlList.Count)
                        controlIndex = suspendedIndex;
                    else if (controlList.Count > 0)
                        controlIndex = Math.Min(suspendedIndex, controlList.Count - 1);

                    AnnounceCurrentControl(interrupt: true);
                    ModLog.Debug($"[CharacterInfoState] Restored from suspend, panel={currentPanel}, index={controlIndex}");
                    return;
                }

                var panel = GetCurrentPanel(charInfoMenu);
                OnPanelChanged(charInfoMenu, panel);
            }

            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            suspendedIndex = controlIndex;
            hasSuspendedState = true;

            blockUIInput = false;
            lastAnnouncedText = null;
            lastAnnouncedIndex = -1;
            controlList.Clear();
            isEditingValue = false;
            infoMode = InfoMode.None;
            infoLines.Clear();
            logbookDetailMode = false;
            logbookDetailLines.Clear();
            ModLog.Debug($"[CharacterInfoState] Suspended at index={suspendedIndex}");
            base.OnDeactivated();
        }

        #region Panel Detection & Change

        private CharacterInfoMenu.InfoPanel GetCurrentPanel(CharacterInfoMenu menu)
        {
            if (charInfoCurrentPanelField != null)
            {
                return (CharacterInfoMenu.InfoPanel)charInfoCurrentPanelField.GetValue(menu);
            }

            // Fallback: detect from active panels
            if (menu.skillPanel != null && menu.skillPanel.gameObject.activeInHierarchy)
            {
                // Skills and Attributes/Dossier panels can overlap, so check skillPanel first
                // since skillPanel is only active for Skills tab
                return CharacterInfoMenu.InfoPanel.Skills;
            }
            if (menu.traitPanel != null && menu.traitPanel.gameObject.activeInHierarchy)
                return CharacterInfoMenu.InfoPanel.Traits;
            if (menu.dossierPanel != null && menu.dossierPanel.gameObject.activeInHierarchy)
            {
                // Dossier and Attributes both show attributePanel + dossierPanel
                // Distinguish by whether skillPanel is also active
                if (menu.attributePanel != null && menu.attributePanel.gameObject.activeInHierarchy)
                {
                    // Both active — could be Attributes or Dossier
                    // Check inventory panel to rule that out
                    if (menu.inventoryPanel != null && menu.inventoryPanel.gameObject.activeInHierarchy)
                        return CharacterInfoMenu.InfoPanel.Inventory;
                    // Default to Attributes — if we're wrong, the reflection field will correct it
                    return CharacterInfoMenu.InfoPanel.Attributes;
                }
                return CharacterInfoMenu.InfoPanel.Dossier;
            }
            if (menu.attributePanel != null && menu.attributePanel.gameObject.activeInHierarchy)
                return CharacterInfoMenu.InfoPanel.Attributes;
            if (menu.inventoryPanel != null && menu.inventoryPanel.gameObject.activeInHierarchy)
                return CharacterInfoMenu.InfoPanel.Inventory;
            if (menu.journalPanel != null && menu.journalPanel.gameObject.activeInHierarchy)
                return CharacterInfoMenu.InfoPanel.Logbook;

            return CharacterInfoMenu.InfoPanel.None;
        }

        private PC GetCurrentPC(CharacterInfoMenu menu)
        {
            if (charInfoCurrentPCField != null)
                return charInfoCurrentPCField.GetValue(menu) as PC;
            return null;
        }

        private void OnPanelChanged(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel newPanel)
        {
            lastPanel = newPanel;
            isEditingValue = false;
            logbookDetailMode = false;
            logbookDetailIndex = -1;
            logbookDetailLines.Clear();
            currentLogbookEntryName = null;
            controlList.Clear();
            controlIndex = -1;
            lastAnnouncedText = null;
            lastAnnouncedIndex = -1;

            BuildControlList(menu, newPanel);

            if (controlList.Count > 0)
                controlIndex = 0;

            string panelName = GetPanelDisplayName(newPanel);

            // Build context hint
            string hint = "";
            switch (newPanel)
            {
                case CharacterInfoMenu.InfoPanel.Attributes:
                    hint = ". Up and Down to navigate, I for description, S for stats browser";
                    break;
                case CharacterInfoMenu.InfoPanel.Skills:
                    hint = ". Up and Down to navigate, F to switch section, Left and Right for category";
                    break;
                case CharacterInfoMenu.InfoPanel.Traits:
                    hint = ". Up and Down to navigate, I for description, P for perk points";
                    break;
                case CharacterInfoMenu.InfoPanel.Dossier:
                    hint = ". Up and Down to browse";
                    break;
                case CharacterInfoMenu.InfoPanel.Logbook:
                    hint = ". Up and Down to navigate entries, Enter for details, Left and Right to switch category";
                    break;
            }

            // Points-available auto-mention (mirrors the blinker sprite the sighted user sees)
            string pointsHint = CharacterAnnouncementHelper.BuildPointsAvailableHint(GetCurrentPC(menu), newPanel);
            if (!string.IsNullOrEmpty(pointsHint))
                hint = ". " + pointsHint + hint;

            ScreenReaderManager.SpeakInterrupt($"{panelName}{hint}");
            ModLog.Debug($"[CharacterInfoState] Panel changed to {newPanel}, controls={controlList.Count}");
        }

        private string GetPanelDisplayName(CharacterInfoMenu.InfoPanel panel)
        {
            switch (panel)
            {
                case CharacterInfoMenu.InfoPanel.Attributes: return "Attributes";
                case CharacterInfoMenu.InfoPanel.Skills: return "Skills";
                case CharacterInfoMenu.InfoPanel.Traits: return "Perks";
                case CharacterInfoMenu.InfoPanel.Dossier: return "Dossier";
                case CharacterInfoMenu.InfoPanel.Logbook: return "Logbook";
                case CharacterInfoMenu.InfoPanel.Inventory: return "Inventory";
                default: return "Character Info";
            }
        }

        #endregion

        #region Build Control Lists

        private void BuildControlList(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel panel)
        {
            controlList.Clear();

            switch (panel)
            {
                case CharacterInfoMenu.InfoPanel.Attributes:
                    BuildAttributeControls(menu);
                    break;
                case CharacterInfoMenu.InfoPanel.Skills:
                    BuildSkillControls(menu);
                    break;
                case CharacterInfoMenu.InfoPanel.Traits:
                    BuildTraitControls(menu);
                    break;
                case CharacterInfoMenu.InfoPanel.Dossier:
                    BuildDossierControls(menu);
                    break;
                case CharacterInfoMenu.InfoPanel.Logbook:
                    BuildLogbookControls(menu);
                    break;
            }
        }

        private void BuildAttributeControls(CharacterInfoMenu menu)
        {
            if (menu.attributePanel == null || menu.attributePanel.attributeGrid == null) return;

            var grid = menu.attributePanel.attributeGrid;
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < grid.transform.childCount; i++)
            {
                var child = grid.transform.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                    children.Add(child);
            }

            children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            foreach (var child in children)
                controlList.Add(child.gameObject);

            ModLog.Debug($"[CharacterInfoState] Attribute controls: {controlList.Count}");
        }

        private void BuildSkillControls(CharacterInfoMenu menu)
        {
            if (menu.skillPanel == null) return;

            var skillInfoPanel = menu.skillPanel;

            if (currentSkillSection == SkillSection.Learned)
            {
                // Build from learned skills grid
                if (skillInfoPanel.learnedGrid != null)
                {
                    AddGridChildren(skillInfoPanel.learnedGrid);
                }
            }
            else
            {
                // Build from active unlearned category grid
                UIGrid activeGrid = GetActiveUnlearnedGrid(skillInfoPanel);
                if (activeGrid != null)
                {
                    AddGridChildren(activeGrid);
                }
            }

            ModLog.Debug($"[CharacterInfoState] Skill controls ({currentSkillSection}): {controlList.Count}");
        }

        private UIGrid GetActiveUnlearnedGrid(CHA_SkillInfoPanel panel)
        {
            switch (currentSkillSection)
            {
                case SkillSection.Combat:
                    return panel.combatGrid;
                case SkillSection.Knowledge:
                    return panel.knowledgeGrid;
                case SkillSection.General:
                    return panel.generalGrid;
                default:
                    return panel.combatGrid;
            }
        }

        private string GetSkillSectionName()
        {
            switch (currentSkillSection)
            {
                case SkillSection.Learned: return "Learned Skills";
                case SkillSection.Combat: return "Combat Skills";
                case SkillSection.Knowledge: return "Knowledge Skills";
                case SkillSection.General: return "General Skills";
                default: return "Skills";
            }
        }

        private void BuildTraitControls(CharacterInfoMenu menu)
        {
            if (menu.traitPanel == null) return;

            // Add learned traits first
            if (menu.traitPanel.learnedTraitGrid != null)
            {
                AddGridChildren(menu.traitPanel.learnedTraitGrid);
            }

            // Then unlearned traits
            if (menu.traitPanel.traitGrid != null)
            {
                AddGridChildren(menu.traitPanel.traitGrid);
            }

            ModLog.Debug($"[CharacterInfoState] Trait controls: {controlList.Count}");
        }

        private void BuildDossierControls(CharacterInfoMenu menu)
        {
            // Dossier is read-only text — collect info lines for browsing
            dossierLines.Clear();

            if (menu.dossierPanel == null) return;

            var dossier = menu.dossierPanel;

            AddDossierLabel("Name", dossier.nameLabel);
            AddDossierLabel("Age", dossier.ageLabel);
            AddDossierLabel("Level", dossier.levelLabel);
            AddDossierXP(GetCurrentPC(menu));
            AddDossierLabel("Religion", dossier.religionLabel);
            AddDossierLabel("Ethnicity", dossier.ethnicityLabel);
            AddDossierLabel("Kills", dossier.killLabel);
            AddDossierLabel("Damage Dealt", dossier.damageLabel);
            AddDossierLabel("Cigarettes Smoked", dossier.smokesLabel);
            AddDossierLabel("Radiation Protection", dossier.radSuitLabel);
            AddDossierLabel("Water", dossier.canteenLabel);
            AddDossierLabel("Constitution Per Level", dossier.conPerLevelLabel);
            AddDossierSkillPointsPerLevel(dossier);

            // Quirk/trait info
            if (dossier.traitNameLabel != null && !string.IsNullOrEmpty(dossier.traitNameLabel.text))
            {
                string traitName = UITextExtractor.CleanText(dossier.traitNameLabel.text);
                if (!string.IsNullOrEmpty(traitName))
                    dossierLines.Add($"Quirk: {traitName}");
            }

            // Biography
            if (dossier.biographyLabel != null && !string.IsNullOrEmpty(dossier.biographyLabel.text))
            {
                string bio = UITextExtractor.CleanText(dossier.biographyLabel.text);
                if (!string.IsNullOrEmpty(bio))
                    dossierLines.Add($"Biography: {bio}");
            }

            // Use dossierLines as the "control list" equivalent
            // We'll use controlIndex to track position in dossierLines
            controlList.Clear(); // No GameObjects for dossier
            controlIndex = dossierLines.Count > 0 ? 0 : -1;

            ModLog.Debug($"[CharacterInfoState] Dossier lines: {dossierLines.Count}");
        }

        private void BuildLogbookControls(CharacterInfoMenu menu)
        {
            if (menu.journalPanel == null || menu.journalPanel.mainPanel == null) return;

            var mainPanel = menu.journalPanel.mainPanel;
            var entries = mainPanel.GetComponentsInChildren<JNL_JournalEntry>(false);
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry != null && entry.gameObject.activeInHierarchy)
                    controlList.Add(entry.gameObject);
            }

            ModLog.Debug($"[CharacterInfoState] Logbook controls ({GetLogbookSortName()}): {controlList.Count}");
        }

        private void AddDossierLabel(string prefix, UILabel label)
        {
            if (label == null) return;
            string text = UITextExtractor.CleanText(label.text);
            if (string.IsNullOrEmpty(text)) return;
            dossierLines.Add($"{prefix}: {text}");
        }

        /// <summary>
        /// Skill points per level can be buffed/debuffed by traits — the game color-codes
        /// the label. We approximate by comparing label color to GUIManager's buffed/debuffed colors.
        /// </summary>
        private void AddDossierSkillPointsPerLevel(CHA_FlavorDisplayPanel dossier)
        {
            if (dossier == null || dossier.skillPointsPerLevelLabel == null) return;
            var label = dossier.skillPointsPerLevelLabel;
            string text = UITextExtractor.CleanText(label.text);
            if (string.IsNullOrEmpty(text)) return;

            string state = "";
            try
            {
                if (label.color == GUIManager.buffedTextColor) state = ", buffed";
                else if (label.color == GUIManager.debuffedTextColor) state = ", debuffed";
            }
            catch (Exception ex) { MelonLogger.Warning($"[CharacterInfoState] AddDossierSkillPointsPerLevel buff color compare failed: {ex.Message}"); }

            dossierLines.Add($"Skill Points Per Level: {text}{state}");
        }

        private void AddDossierXP(PC pc)
        {
            if (pc == null || pc.pcTemplate == null) return;
            var tmpl = pc.pcTemplate;

            if (tmpl.IsAtMaxLevel())
            {
                dossierLines.Add($"Experience: {tmpl.GetXP()}, max level");
                return;
            }

            int level = tmpl.GetCurrentLevel();
            int xpCur = tmpl.GetXP();
            int xpNext = tmpl.GetXPForLevel(level + 1);
            string line = $"Experience: {xpCur} of {xpNext}";
            if (pc.CanLevelUp(ignoreHealthState: true))
                line += ", level up available";
            dossierLines.Add(line);
        }

        private void AddGridChildren(UIGrid grid)
        {
            if (grid == null) return;

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < grid.transform.childCount; i++)
            {
                var child = grid.transform.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Skip disabled skill editors (e.g. Combat Shooting, Southwestern Folklore)
                // These are DLC skills that can't be learned through normal level-up
                var skillEditor = child.GetComponent<CHA_SkillEditor>();
                if (skillEditor != null && !skillEditor.enabled)
                    continue;

                children.Add(child);
            }

            children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            foreach (var child in children)
                controlList.Add(child.gameObject);
        }

        #endregion

        #region Navigation

        private void JumpToEdge(bool toFirst)
        {
            if (lastPanel == CharacterInfoMenu.InfoPanel.Dossier)
            {
                if (dossierLines.Count == 0) return;
                controlIndex = toFirst ? 0 : dossierLines.Count - 1;
                AnnounceCurrentControl();
                return;
            }
            if (controlList.Count == 0) return;
            controlIndex = toFirst ? 0 : controlList.Count - 1;
            SelectControl(controlList[controlIndex]);
            AnnounceCurrentControl();
        }

        private void NavigateList(int direction)
        {
            if (lastPanel == CharacterInfoMenu.InfoPanel.Dossier)
            {
                NavigateDossier(direction);
                return;
            }

            // Logbook uses controlList but has custom announcement
            if (controlList.Count == 0) return;

            int newIndex = controlIndex + direction;
            bool wrapped = false;
            if (newIndex < 0) { newIndex = controlList.Count - 1; wrapped = true; }
            if (newIndex >= controlList.Count) { newIndex = 0; wrapped = true; }

            if (newIndex != controlIndex)
            {
                controlIndex = newIndex;
                if (wrapped) MenuCue.PlayWrap();
                SelectControl(controlList[controlIndex]);
                AnnounceCurrentControl();
            }
        }

        private void NavigateDossier(int direction)
        {
            if (dossierLines.Count == 0) return;

            int newIndex = controlIndex + direction;
            bool wrapped = false;
            if (newIndex < 0) { newIndex = dossierLines.Count - 1; wrapped = true; }
            if (newIndex >= dossierLines.Count) { newIndex = 0; wrapped = true; }

            if (newIndex != controlIndex || lastAnnouncedText == null)
            {
                if (wrapped && newIndex != controlIndex) MenuCue.PlayWrap();
                controlIndex = newIndex;
                string line = dossierLines[controlIndex];
                lastAnnouncedText = line;
                lastAnnouncedIndex = controlIndex;
                ScreenReaderManager.SpeakInterrupt($"{line}, {controlIndex + 1} of {dossierLines.Count}");
            }
        }

        private void SelectControl(GameObject obj)
        {
            UICamera.selectedObject = obj;
        }

        private void AnnounceCurrentControl(bool interrupt = true)
        {
            if (lastPanel == CharacterInfoMenu.InfoPanel.Dossier)
            {
                if (controlIndex >= 0 && controlIndex < dossierLines.Count)
                {
                    string line = dossierLines[controlIndex];
                    if (line != lastAnnouncedText || controlIndex != lastAnnouncedIndex)
                    {
                        lastAnnouncedText = line;
                        lastAnnouncedIndex = controlIndex;
                        if (interrupt) ScreenReaderManager.SpeakInterrupt(line);
                        else ScreenReaderManager.Speak(line);
                    }
                }
                return;
            }

            if (lastPanel == CharacterInfoMenu.InfoPanel.Logbook)
            {
                AnnounceLogbookEntry(interrupt);
                return;
            }

            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var obj = controlList[controlIndex];
            if (obj == null) return;

            string announcement = CharacterAnnouncementHelper.GetControlAnnouncement(obj);
            if (!string.IsNullOrEmpty(announcement) && (announcement != lastAnnouncedText || controlIndex != lastAnnouncedIndex))
            {
                lastAnnouncedText = announcement;
                lastAnnouncedIndex = controlIndex;
                if (interrupt) ScreenReaderManager.SpeakInterrupt(announcement);
                else ScreenReaderManager.Speak(announcement);
                ModLog.Debug($"[CharacterInfoState] Announce [{controlIndex}]: {announcement}");
            }
        }

        #endregion

        #region Common Input

        private bool HandleCommonInput(CharacterInfoMenu menu)
        {
            // Tab to announce current context
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                string panelName = GetPanelDisplayName(lastPanel);
                int total = lastPanel == CharacterInfoMenu.InfoPanel.Dossier ? dossierLines.Count : controlList.Count;
                string extra = "";
                if (lastPanel == CharacterInfoMenu.InfoPanel.Logbook)
                    extra = $", {GetLogbookSortName()}";
                ScreenReaderManager.SpeakInterrupt($"{panelName}{extra}, {controlIndex + 1} of {total}");
                return true;
            }

            // Home / End jump to first / last entry in the current panel. Skipped while
            // editing a value so those keys still reach the edit interaction.
            if (!isEditingValue)
            {
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    JumpToEdge(toFirst: true);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    JumpToEdge(toFirst: false);
                    return true;
                }
            }

            // D for character summary
            if (Input.GetKeyDown(KeyCode.D))
            {
                PC pc = GetCurrentPC(menu);
                if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                    pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                CharacterAnnouncementHelper.AnnounceCharacterSummary(pc);
                return true;
            }

            // PageUp/PageDown to switch tabs
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                SwitchTab(menu, -1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                SwitchTab(menu, 1);
                return true;
            }

            // F1-F7 switch party member
            if (HandlePartySwitch(menu))
                return true;

            // E: announce current XP / next level XP
            if (Input.GetKeyDown(KeyCode.E))
            {
                PC pc = GetCurrentPC(menu);
                if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                    pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                CharacterAnnouncementHelper.AnnounceXP(pc);
                return true;
            }

            // S: open unified stats browser (Header / Combat / Derived sections)
            if (Input.GetKeyDown(KeyCode.S))
            {
                OpenStatsBrowser(menu, StatsSection.Header);
                return true;
            }

            // Escape: close the character info menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                hasSuspendedState = false;
                // Prevent the "Back" event from bleeding into the next frame and opening the pause menu
                EventManager.ignoreNextBack = true;
                menu.Close();
                ModLog.Debug("[CharacterInfoState] Closed character info menu");
                return true;
            }

            return false;
        }

        private void SwitchTab(CharacterInfoMenu menu, int direction)
        {
            hasSuspendedState = false; // Don't restore when switching tabs
            CyclePanel(menu, lastPanel, direction);
            ModLog.Debug($"[CharacterInfoState] Switched tab, direction={direction}");
        }

        // Cycle order including Dossier. The game's own GoToNextPanel()/GoToPreviousPanel()
        // skip Dossier entirely (INV_TabBar has no dossierLED — DC's tab bar only exposes
        // Inventory, Log, Character, Attributes, Skills, Traits), so we cycle ourselves and
        // call OnDossierClicked() directly when we land on it.
        private static readonly CharacterInfoMenu.InfoPanel[] TabCycle =
        {
            CharacterInfoMenu.InfoPanel.Inventory,
            CharacterInfoMenu.InfoPanel.Attributes,
            CharacterInfoMenu.InfoPanel.Skills,
            CharacterInfoMenu.InfoPanel.Traits,
            CharacterInfoMenu.InfoPanel.Dossier,
            CharacterInfoMenu.InfoPanel.Logbook,
        };

        internal static void CyclePanel(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel current, int direction)
        {
            int idx = Array.IndexOf(TabCycle, current);
            if (idx < 0) idx = 0;
            int next = (idx + direction + TabCycle.Length) % TabCycle.Length;
            InvokePanelClick(menu, TabCycle[next]);
        }

        private static void InvokePanelClick(CharacterInfoMenu menu, CharacterInfoMenu.InfoPanel panel)
        {
            switch (panel)
            {
                case CharacterInfoMenu.InfoPanel.Inventory: menu.OnInventoryClicked(); break;
                case CharacterInfoMenu.InfoPanel.Attributes: menu.OnAttributesClicked(); break;
                case CharacterInfoMenu.InfoPanel.Skills: menu.OnSkillsClicked(); break;
                case CharacterInfoMenu.InfoPanel.Traits: menu.OnTraitsClicked(); break;
                case CharacterInfoMenu.InfoPanel.Dossier: menu.OnDossierClicked(); break;
                case CharacterInfoMenu.InfoPanel.Logbook: menu.OnLogbookClicked(); break;
            }
        }

        private bool HandlePartySwitch(CharacterInfoMenu menu)
        {
            for (int i = 0; i < 7; i++)
            {
                KeyCode key = KeyCode.F1 + i;
                if (Input.GetKeyDown(key))
                {
                    SwitchToPartyMember(menu, i);
                    return true;
                }
            }
            return false;
        }

        private void SwitchToPartyMember(CharacterInfoMenu menu, int index)
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt("No ranger in that slot");
                return;
            }

            var pc = party[index];
            if (pc == null) return;

            // Use the menu's PopulateData method to switch character
            menu.PopulateData(pc, force: true);

            // Rebuild controls for the new character
            var panel = GetCurrentPanel(menu);
            controlList.Clear();
            controlIndex = -1;
            BuildControlList(menu, panel);
            if (controlList.Count > 0)
                controlIndex = 0;

            string name = pc.pcTemplate != null
                ? UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty))
                : "Unknown";
            ScreenReaderManager.SpeakInterrupt(name);
            AnnounceCurrentControl(interrupt: false);

            ModLog.Debug($"[CharacterInfoState] Switched to party member {index}: {name}");
        }

        #endregion

        #region Attributes Input

        private bool HandleAttributesInput(CharacterInfoMenu menu)
        {
            // Edit mode for level-up
            if (isEditingValue)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
                {
                    isEditingValue = false;
                    ScreenReaderManager.SpeakInterrupt("Done editing");
                    AnnounceCurrentControl(interrupt: false);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
                {
                    AdjustCurrentAttribute(-1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
                {
                    AdjustCurrentAttribute(1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.I))
                {
                    AnnounceCurrentStatDescription();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    return true;
                return false;
            }

            if (HandleCommonInput(menu)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // +/- to adjust attribute directly (level-up)
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
            {
                AdjustCurrentAttribute(1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
            {
                AdjustCurrentAttribute(-1);
                return true;
            }

            // Enter to start editing
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_AttributeEditor>();
                    if (editor != null)
                    {
                        isEditingValue = true;
                        string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.attribute;
                        ScreenReaderManager.SpeakInterrupt($"Editing {name}. Left and Right to adjust, Enter to confirm");
                    }
                }
                return true;
            }

            // Block Left/Right in normal mode
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                return true;

            // I for description
            if (Input.GetKeyDown(KeyCode.I))
            {
                AnnounceCurrentStatDescription();
                return true;
            }

            // P for points remaining
            if (Input.GetKeyDown(KeyCode.P))
            {
                AnnounceAttributePointsRemaining(menu);
                return true;
            }

            return false;
        }

        private void AdjustCurrentAttribute(int direction)
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;
            var editor = controlList[controlIndex].GetComponent<CHA_AttributeEditor>();
            if (editor == null) return;
            CharacterAnnouncementHelper.AdjustAttribute(editor, direction, () => AnnounceCurrentControl());
        }

        private void AnnounceAttributePointsRemaining(CharacterInfoMenu menu)
        {
            PC pc = GetCurrentPC(menu);
            if (pc != null && pc.pcTemplate != null)
            {
                int points = pc.pcTemplate.availableAttributePoints;
                ScreenReaderManager.SpeakInterrupt($"{points} attribute point{(points == 1 ? "" : "s")} remaining");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("No points information available");
            }
        }

        #endregion

        #region Skills Input

        private bool HandleSkillsInput(CharacterInfoMenu menu)
        {
            // Edit mode for level-up
            if (isEditingValue)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
                {
                    isEditingValue = false;
                    ScreenReaderManager.SpeakInterrupt("Done editing");
                    AnnounceCurrentControl(interrupt: false);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
                {
                    AdjustCurrentSkill(-1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
                {
                    AdjustCurrentSkill(1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.I))
                {
                    AnnounceCurrentStatDescription();
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                    return true;
                return false;
            }

            if (HandleCommonInput(menu)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // +/- to adjust skill directly (level-up)
            if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals))
            {
                AdjustCurrentSkill(1);
                return true;
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus))
            {
                AdjustCurrentSkill(-1);
                return true;
            }

            // Enter to start editing
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_SkillEditor>();
                    if (editor != null)
                    {
                        isEditingValue = true;
                        string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.skillName;
                        ScreenReaderManager.SpeakInterrupt($"Editing {name}. Left and Right to adjust, Enter to confirm");
                    }
                }
                return true;
            }

            // F to cycle skill sections (Learned → Combat → Knowledge → General)
            if (Input.GetKeyDown(KeyCode.F))
            {
                CycleSkillSection(menu);
                return true;
            }

            // Left/Right to switch unlearned skill categories
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (currentSkillSection != SkillSection.Learned)
                {
                    SwitchUnlearnedCategory(menu, Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1);
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Press F to switch to unlearned skills first");
                }
                return true;
            }

            // I for description
            if (Input.GetKeyDown(KeyCode.I))
            {
                AnnounceCurrentStatDescription();
                return true;
            }

            // P for points remaining
            if (Input.GetKeyDown(KeyCode.P))
            {
                AnnounceSkillPointsRemaining(menu);
                return true;
            }

            return false;
        }

        private void CycleSkillSection(CharacterInfoMenu menu)
        {
            // Cycle: Learned → Combat → Knowledge → General → Learned
            switch (currentSkillSection)
            {
                case SkillSection.Learned:
                    currentSkillSection = SkillSection.Combat;
                    if (menu.skillPanel != null)
                        menu.skillPanel.OnCombatSkillsClicked();
                    break;
                case SkillSection.Combat:
                    currentSkillSection = SkillSection.Knowledge;
                    if (menu.skillPanel != null)
                        menu.skillPanel.OnKnowledgeSkillsClicked();
                    break;
                case SkillSection.Knowledge:
                    currentSkillSection = SkillSection.General;
                    if (menu.skillPanel != null)
                        menu.skillPanel.OnGeneralSkillsClicked();
                    break;
                case SkillSection.General:
                    currentSkillSection = SkillSection.Learned;
                    break;
            }

            // Rebuild controls for new section
            controlList.Clear();
            controlIndex = -1;
            BuildSkillControls(menu);

            if (controlList.Count > 0)
                controlIndex = 0;

            string sectionName = GetSkillSectionName();
            ScreenReaderManager.SpeakInterrupt($"{sectionName}, {controlList.Count} skills");
            AnnounceCurrentControl(interrupt: false);

            ModLog.Debug($"[CharacterInfoState] Skill section: {currentSkillSection}, controls={controlList.Count}");
        }

        private void SwitchUnlearnedCategory(CharacterInfoMenu menu, int direction)
        {
            // Cycle between Combat, Knowledge, General
            SkillSection[] categories = { SkillSection.Combat, SkillSection.Knowledge, SkillSection.General };
            int currentIdx = Array.IndexOf(categories, currentSkillSection);
            if (currentIdx < 0) currentIdx = 0;

            int newIdx = (currentIdx + direction + categories.Length) % categories.Length;
            currentSkillSection = categories[newIdx];

            // Tell the game to switch category
            if (menu.skillPanel != null)
            {
                switch (currentSkillSection)
                {
                    case SkillSection.Combat:
                        menu.skillPanel.OnCombatSkillsClicked();
                        break;
                    case SkillSection.Knowledge:
                        menu.skillPanel.OnKnowledgeSkillsClicked();
                        break;
                    case SkillSection.General:
                        menu.skillPanel.OnGeneralSkillsClicked();
                        break;
                }
            }

            // Rebuild controls
            controlList.Clear();
            controlIndex = -1;
            BuildSkillControls(menu);

            if (controlList.Count > 0)
                controlIndex = 0;

            string sectionName = GetSkillSectionName();
            ScreenReaderManager.SpeakInterrupt($"{sectionName}");
            AnnounceCurrentControl(interrupt: false);

            ModLog.Debug($"[CharacterInfoState] Skill category switched to {currentSkillSection}");
        }

        private void AdjustCurrentSkill(int direction)
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;
            var editor = controlList[controlIndex].GetComponent<CHA_SkillEditor>();
            if (editor == null) return;
            CharacterAnnouncementHelper.AdjustSkill(editor, direction, () => AnnounceCurrentControl());
        }

        private void AnnounceSkillPointsRemaining(CharacterInfoMenu menu)
        {
            PC pc = GetCurrentPC(menu);
            if (pc != null && pc.pcTemplate != null)
            {
                int points = pc.pcTemplate.availableSkillPoints;
                ScreenReaderManager.SpeakInterrupt($"{points} skill point{(points == 1 ? "" : "s")} remaining");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("No points information available");
            }
        }

        #endregion

        #region Traits Input

        private bool HandleTraitsInput(CharacterInfoMenu menu)
        {
            if (HandleCommonInput(menu)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Enter or Space to toggle trait (if trait points available)
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_TraitEditor>();
                    if (editor != null && editor.checkbox != null)
                        CharacterAnnouncementHelper.ToggleTrait(editor);
                }
                return true;
            }

            // I: open browsable perk description
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_TraitEditor>();
                    if (editor != null)
                    {
                        Trait trait = CharacterAnnouncementHelper.GetTraitFromEditor(editor);
                        if (trait != null)
                        {
                            OpenInfoBrowser(menu, InfoMode.TraitDescription, trait: trait);
                        }
                        else
                        {
                            ScreenReaderManager.SpeakInterrupt("No description available");
                        }
                    }
                }
                return true;
            }

            // P for perk points remaining
            if (Input.GetKeyDown(KeyCode.P))
            {
                AnnouncePerkPointsRemaining(menu);
                return true;
            }

            return false;
        }

        private void AnnouncePerkPointsRemaining(CharacterInfoMenu menu)
        {
            PC pc = GetCurrentPC(menu);
            if (pc != null && pc.pcTemplate != null)
            {
                int points = pc.pcTemplate.availableTraitPoints;
                ScreenReaderManager.SpeakInterrupt($"{points} perk point{(points == 1 ? "" : "s")} remaining");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("No points information available");
            }
        }

        #endregion

        #region Dossier Input

        private bool HandleDossierInput(CharacterInfoMenu menu)
        {
            if (HandleCommonInput(menu)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateDossier(dir);
                return true;
            }

            // I: open browsable quirk description when on the quirk line, otherwise read biography
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (controlIndex >= 0 && controlIndex < dossierLines.Count
                    && dossierLines[controlIndex].StartsWith("Quirk:"))
                {
                    PC pc = GetCurrentPC(menu);
                    if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                        pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                    Trait quirk = pc != null && pc.pcTemplate != null ? pc.pcTemplate.GetQuirk() : null;
                    if (quirk != null)
                        OpenInfoBrowser(menu, InfoMode.TraitDescription, trait: quirk);
                    else
                        ScreenReaderManager.SpeakInterrupt("No quirk description available");
                    return true;
                }

                for (int i = 0; i < dossierLines.Count; i++)
                {
                    if (dossierLines[i].StartsWith("Biography:"))
                    {
                        ScreenReaderManager.SpeakInterrupt(dossierLines[i]);
                        return true;
                    }
                }
                ScreenReaderManager.SpeakInterrupt("No biography available");
                return true;
            }

            return false;
        }

        #endregion

        #region Logbook Input

        private bool HandleLogbookInput(CharacterInfoMenu menu)
        {
            if (HandleCommonInput(menu)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Enter or Right to view details of selected entry
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                OpenLogbookDetails(menu);
                return true;
            }

            // Left/Right to switch sort category
            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                SwitchLogbookCategory(menu, -1);
                return true;
            }

            // F to cycle sort category (alternative to Left/Right)
            if (Input.GetKeyDown(KeyCode.F))
            {
                SwitchLogbookCategory(menu, 1);
                return true;
            }

            // X to toggle flagged on current entry
            if (Input.GetKeyDown(KeyCode.X))
            {
                ToggleLogbookFlag();
                return true;
            }

            // I to announce entry location/originator
            if (Input.GetKeyDown(KeyCode.I))
            {
                AnnounceLogbookEntryInfo();
                return true;
            }

            return false;
        }

        private bool HandleLogbookDetailInput(CharacterInfoMenu menu)
        {
            // Escape or Left to return to entry list
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                CloseLogbookDetails();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                logbookDetailIndex--;
                if (logbookDetailIndex < 0) { logbookDetailIndex = logbookDetailLines.Count - 1; if (logbookDetailLines.Count > 1) MenuCue.PlayWrap(); }
                AnnounceLogbookDetail();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                logbookDetailIndex++;
                if (logbookDetailIndex >= logbookDetailLines.Count) { logbookDetailIndex = 0; if (logbookDetailLines.Count > 1) MenuCue.PlayWrap(); }
                AnnounceLogbookDetail();
                return true;
            }

            // Tab to announce position
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (logbookDetailIndex >= 0 && logbookDetailIndex < logbookDetailLines.Count)
                    ScreenReaderManager.SpeakInterrupt($"Detail {logbookDetailIndex + 1} of {logbookDetailLines.Count}");
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                logbookDetailIndex = 0;
                AnnounceLogbookDetail();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                logbookDetailIndex = logbookDetailLines.Count - 1;
                AnnounceLogbookDetail();
                return true;
            }

            return true; // Block all other keys in detail mode
        }

        private void OpenLogbookDetails(CharacterInfoMenu menu)
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var entryObj = controlList[controlIndex];
            var journalEntry = entryObj.GetComponent<JNL_JournalEntry>();
            if (journalEntry == null) return;

            currentLogbookEntryName = journalEntry.entryName;
            if (string.IsNullOrEmpty(currentLogbookEntryName)) return;

            if (!MonoBehaviourSingleton<JournalManager>.HasInstance()) return;
            var entryInstance = MonoBehaviourSingleton<JournalManager>.GetInstance().GetEntry(currentLogbookEntryName);
            if (entryInstance == null) return;

            // Build detail lines
            logbookDetailLines.Clear();
            int numDetails = entryInstance.GetNumVisibleDetails();
            for (int i = numDetails - 1; i >= 0; i--)
            {
                string detailText = entryInstance.GetDetail(i);
                if (string.IsNullOrEmpty(detailText)) continue;

                string cleanDetail = UITextExtractor.CleanText(detailText);
                if (string.IsNullOrEmpty(cleanDetail)) continue;

                // Check if this is a completed objective (not the last/current one)
                bool isComplete = i < numDetails - 1;
                string prefix = isComplete ? "Complete: " : "";

                string note = entryInstance.GetDetailNote(i);
                string noteSuffix = "";
                if (!string.IsNullOrEmpty(note))
                    noteSuffix = $". Note: {note}";

                logbookDetailLines.Add($"{prefix}{cleanDetail}{noteSuffix}");
            }

            if (logbookDetailLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No details available");
                return;
            }

            logbookDetailMode = true;
            logbookDetailIndex = 0;

            string entryName = UITextExtractor.CleanText(
                Language.Localize(entryInstance.GetDescription(), false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt(
                $"{entryName}, {logbookDetailLines.Count} detail{(logbookDetailLines.Count == 1 ? "" : "s")}. Up and Down to navigate, Escape to go back");
            AnnounceLogbookDetail(interrupt: false);
            ModLog.Debug($"[CharacterInfoState] Logbook details opened for {currentLogbookEntryName}, {logbookDetailLines.Count} details");
        }

        private void CloseLogbookDetails()
        {
            logbookDetailMode = false;
            logbookDetailIndex = -1;
            logbookDetailLines.Clear();
            currentLogbookEntryName = null;
            EventManager.ignoreNextBack = true;
            lastAnnouncedText = null;
            ScreenReaderManager.SpeakInterrupt("Back to entries");
            AnnounceCurrentControl(interrupt: false);
            ModLog.Debug("[CharacterInfoState] Logbook details closed");
        }

        private void AnnounceLogbookDetail(bool interrupt = true)
        {
            if (logbookDetailIndex < 0 || logbookDetailIndex >= logbookDetailLines.Count) return;
            string line = logbookDetailLines[logbookDetailIndex];
            if (interrupt)
                ScreenReaderManager.SpeakInterrupt(line);
            else
                ScreenReaderManager.Speak(line);
        }

        private void AnnounceLogbookEntry(bool interrupt = true)
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var entryObj = controlList[controlIndex];
            var journalEntry = entryObj.GetComponent<JNL_JournalEntry>();
            if (journalEntry == null) return;

            // Get the description text from the label
            string text = "";
            if (journalEntry.descriptionLabel != null)
                text = UITextExtractor.CleanText(journalEntry.descriptionLabel.text);

            if (string.IsNullOrEmpty(text))
                text = journalEntry.entryName;

            // Build status indicators
            string status = "";
            if (!string.IsNullOrEmpty(journalEntry.entryName) && MonoBehaviourSingleton<JournalManager>.HasInstance())
            {
                var entryInstance = MonoBehaviourSingleton<JournalManager>.GetInstance().GetEntry(journalEntry.entryName);
                if (entryInstance != null)
                {
                    if (entryInstance.updated)
                        status = ", new";
                    if (entryInstance.edited)
                        status += ", flagged";
                    if (entryInstance.resolved)
                        status += ", resolved";
                }
            }

            string announcement = $"{text}{status}";
            if (announcement != lastAnnouncedText || controlIndex != lastAnnouncedIndex)
            {
                lastAnnouncedText = announcement;
                lastAnnouncedIndex = controlIndex;
                if (interrupt) ScreenReaderManager.SpeakInterrupt(announcement);
                else ScreenReaderManager.Speak(announcement);
                ModLog.Debug($"[CharacterInfoState] Logbook entry [{controlIndex}]: {announcement}");
            }
        }

        private void AnnounceLogbookEntryInfo()
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var journalEntry = controlList[controlIndex].GetComponent<JNL_JournalEntry>();
            if (journalEntry == null || string.IsNullOrEmpty(journalEntry.entryName)) return;

            if (!MonoBehaviourSingleton<JournalManager>.HasInstance()) return;
            var entryInstance = MonoBehaviourSingleton<JournalManager>.GetInstance().GetEntry(journalEntry.entryName);
            if (entryInstance == null) return;

            string location = Language.Localize(entryInstance.GetLocation(), false, false, string.Empty);
            string originator = Language.Localize(entryInstance.GetOriginator(), false, false, string.Empty);

            string info = "";
            if (!string.IsNullOrEmpty(location))
                info += $"Location: {UITextExtractor.CleanText(location)}";
            if (!string.IsNullOrEmpty(originator))
            {
                if (!string.IsNullOrEmpty(info)) info += ". ";
                info += $"From: {UITextExtractor.CleanText(originator)}";
            }

            int numDetails = entryInstance.GetNumVisibleDetails();
            if (!string.IsNullOrEmpty(info)) info += ". ";
            info += $"{numDetails} objective{(numDetails == 1 ? "" : "s")}";

            if (string.IsNullOrEmpty(info))
                info = "No additional information";

            ScreenReaderManager.SpeakInterrupt(info);
        }

        private void SwitchLogbookCategory(CharacterInfoMenu menu, int direction)
        {
            if (menu.journalPanel == null) return;

            JournalManager.EntrySortType[] categories = {
                JournalManager.EntrySortType.Flagged,
                JournalManager.EntrySortType.Ongoing,
                JournalManager.EntrySortType.Resolved
            };

            int currentIdx = Array.IndexOf(categories, currentLogbookSort);
            if (currentIdx < 0) currentIdx = 1; // Default to Ongoing
            int newIdx = (currentIdx + direction + categories.Length) % categories.Length;
            currentLogbookSort = categories[newIdx];

            // Call the appropriate method on JournalScreen
            switch (currentLogbookSort)
            {
                case JournalManager.EntrySortType.Flagged:
                    menu.journalPanel.OnFlaggedClicked(null);
                    break;
                case JournalManager.EntrySortType.Ongoing:
                    menu.journalPanel.OnOngoingClicked(null);
                    break;
                case JournalManager.EntrySortType.Resolved:
                    menu.journalPanel.OnResolvedClicked(null);
                    break;
            }

            // Rebuild controls
            controlList.Clear();
            controlIndex = -1;
            lastAnnouncedText = null;
            lastAnnouncedIndex = -1;
            BuildLogbookControls(menu);

            if (controlList.Count > 0)
                controlIndex = 0;

            string sortName = GetLogbookSortName();
            ScreenReaderManager.SpeakInterrupt($"{sortName}, {controlList.Count} entr{(controlList.Count == 1 ? "y" : "ies")}");
            AnnounceCurrentControl(interrupt: false);

            ModLog.Debug($"[CharacterInfoState] Logbook category: {currentLogbookSort}, entries={controlList.Count}");
        }

        private void ToggleLogbookFlag()
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var journalEntry = controlList[controlIndex].GetComponent<JNL_JournalEntry>();
            if (journalEntry == null) return;

            journalEntry.ToggleFlagged();

            // Announce the new state
            if (!string.IsNullOrEmpty(journalEntry.entryName) && MonoBehaviourSingleton<JournalManager>.HasInstance())
            {
                var entryInstance = MonoBehaviourSingleton<JournalManager>.GetInstance().GetEntry(journalEntry.entryName);
                if (entryInstance != null)
                {
                    string state = entryInstance.edited ? "Flagged" : "Unflagged";
                    ScreenReaderManager.SpeakInterrupt(state);
                }
            }
        }

        private string GetLogbookSortName()
        {
            switch (currentLogbookSort)
            {
                case JournalManager.EntrySortType.Flagged: return "Flagged";
                case JournalManager.EntrySortType.Ongoing: return "Ongoing";
                case JournalManager.EntrySortType.Resolved: return "Resolved";
                default: return "Logbook";
            }
        }

        #endregion

        #region Info Browser (Stats / Stat / Trait)

        private void OpenStatsBrowser(CharacterInfoMenu menu, StatsSection section)
        {
            PC pc = GetCurrentPC(menu);
            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();

            currentStatsSection = section;
            infoLines = BuildStatsLinesFor(section, pc);

            if (infoLines == null || infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No information available");
                return;
            }

            infoMode = InfoMode.Stats;
            infoIndex = 0;

            string title = GetStatsSectionTitle(section);
            ScreenReaderManager.SpeakInterrupt(
                $"{title}, {infoLines.Count} items. Up and Down to navigate, Left and Right for sections, I for description, Escape to close");
            AnnounceInfoLine(interrupt: false);
            ModLog.Debug($"[CharacterInfoState] Stats browser opened: {section}, {infoLines.Count} lines");
        }

        private void SwitchStatsSection(CharacterInfoMenu menu, int direction)
        {
            int count = 3;
            int next = (((int)currentStatsSection + direction) % count + count) % count;
            currentStatsSection = (StatsSection)next;

            PC pc = GetCurrentPC(menu);
            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();

            infoLines = BuildStatsLinesFor(currentStatsSection, pc);
            infoIndex = infoLines.Count > 0 ? 0 : -1;

            string title = GetStatsSectionTitle(currentStatsSection);
            if (infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt($"{title}, no information");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt($"{title}, {infoLines.Count} items");
                AnnounceInfoLine(interrupt: false);
            }
            ModLog.Debug($"[CharacterInfoState] Stats section switched: {currentStatsSection}, {infoLines.Count} lines");
        }

        private static List<string> BuildStatsLinesFor(StatsSection section, PC pc)
        {
            switch (section)
            {
                case StatsSection.Header:  return CharacterAnnouncementHelper.BuildHeaderSnapshotLines(pc);
                case StatsSection.Combat:  return CharacterAnnouncementHelper.BuildCombatSnapshotLines(pc);
                case StatsSection.Derived: return CharacterAnnouncementHelper.BuildDerivedStatLines(pc);
                default: return new List<string>();
            }
        }

        private static string GetStatsSectionTitle(StatsSection section)
        {
            switch (section)
            {
                case StatsSection.Header:  return "Character info";
                case StatsSection.Combat:  return "Combat stats";
                case StatsSection.Derived: return "Derived stats";
                default: return "Stats";
            }
        }

        private void OpenInfoBrowser(CharacterInfoMenu menu, InfoMode mode, GameObject statObj = null, Trait trait = null)
        {
            PC pc = GetCurrentPC(menu);
            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();

            infoLines = BuildInfoLinesFor(mode, pc, statObj, trait);

            if (infoLines == null || infoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No information available");
                return;
            }

            infoMode = mode;
            infoIndex = 0;

            string title = GetInfoBrowserTitle(mode);
            ScreenReaderManager.SpeakInterrupt(
                $"{title}, {infoLines.Count} items. Up and Down to navigate, Escape to close");
            AnnounceInfoLine(interrupt: false);
            ModLog.Debug($"[CharacterInfoState] Info browser opened: {mode}, {infoLines.Count} lines");
        }

        private static List<string> BuildInfoLinesFor(InfoMode mode, PC pc, GameObject statObj, Trait trait)
        {
            switch (mode)
            {
                case InfoMode.StatDescription:
                    return CharacterAnnouncementHelper.BuildStatDescriptionLines(statObj);
                case InfoMode.TraitDescription:
                    return CharacterAnnouncementHelper.BuildTraitDescriptionLines(trait);
                default:
                    return new List<string>();
            }
        }

        private static string GetInfoBrowserTitle(InfoMode mode)
        {
            switch (mode)
            {
                case InfoMode.StatDescription: return "Details";
                case InfoMode.TraitDescription: return "Perk details";
                default: return "Info";
            }
        }

        private void CloseInfoBrowser()
        {
            var prev = infoMode;
            string title = prev == InfoMode.Stats ? "Stats" : GetInfoBrowserTitle(prev);
            infoMode = InfoMode.None;
            infoIndex = -1;
            infoLines.Clear();
            EventManager.ignoreNextBack = true;
            ScreenReaderManager.SpeakInterrupt(title + " closed");
            lastAnnouncedText = null;
            AnnounceCurrentControl(interrupt: false);
            ModLog.Debug($"[CharacterInfoState] Info browser closed: {prev}");
        }

        private void AnnounceInfoLine(bool interrupt = true)
        {
            if (infoIndex < 0 || infoIndex >= infoLines.Count) return;
            string line = infoLines[infoIndex];
            string msg = $"{line}, {infoIndex + 1} of {infoLines.Count}";
            if (interrupt) ScreenReaderManager.SpeakInterrupt(msg);
            else ScreenReaderManager.Speak(msg);
        }

        private bool HandleInfoInput(CharacterInfoMenu menu)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInfoBrowser();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                infoIndex--;
                if (infoIndex < 0) { infoIndex = infoLines.Count - 1; if (infoLines.Count > 1) MenuCue.PlayWrap(); }
                AnnounceInfoLine();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                infoIndex++;
                if (infoIndex >= infoLines.Count) { infoIndex = 0; if (infoLines.Count > 1) MenuCue.PlayWrap(); }
                AnnounceInfoLine();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                infoIndex = 0;
                AnnounceInfoLine();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                infoIndex = infoLines.Count - 1;
                AnnounceInfoLine();
                return true;
            }

            // Stats-mode-only: Left/Right switches sections, I announces description (Derived only).
            if (infoMode == InfoMode.Stats)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    SwitchStatsSection(menu, -1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    SwitchStatsSection(menu, 1);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.I))
                {
                    if (currentStatsSection == StatsSection.Derived)
                    {
                        PC pc = GetCurrentPC(menu);
                        if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                            pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                        CharacterAnnouncementHelper.AnnounceDerivedStatDescription(pc, infoIndex);
                    }
                    else
                    {
                        ScreenReaderManager.SpeakInterrupt("No description available");
                    }
                    return true;
                }
            }

            // F1-F7 switches character. For Stats (character-snapshot mode) we
            // refresh the lines in-place; for Stat/Trait descriptions the content is tied
            // to a specific control that no longer applies to the new PC, so close first.
            for (int i = 0; i < 7; i++)
            {
                if (Input.GetKeyDown(KeyCode.F1 + i))
                {
                    if (infoMode == InfoMode.Stats)
                    {
                        SwitchToPartyMember(menu, i);
                        PC pc = GetCurrentPC(menu);
                        if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
                            pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                        infoLines = BuildStatsLinesFor(currentStatsSection, pc);
                        if (infoIndex >= infoLines.Count) infoIndex = infoLines.Count - 1;
                        if (infoIndex < 0 && infoLines.Count > 0) infoIndex = 0;
                        AnnounceInfoLine();
                    }
                    else
                    {
                        CloseInfoBrowser();
                        SwitchToPartyMember(menu, i);
                    }
                    return true;
                }
            }

            return true; // Block all other keys
        }

        #endregion

        #region Stat Description Helper

        private void AnnounceCurrentStatDescription()
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;
            var menu = UnityEngine.Object.FindObjectOfType<CharacterInfoMenu>();
            if (menu == null) return;
            OpenInfoBrowser(menu, InfoMode.StatDescription, statObj: controlList[controlIndex]);
        }

        #endregion

        #region Reflection

        private void CacheReflection()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

            charInfoCurrentPanelField = typeof(CharacterInfoMenu).GetField("currentPanel", flags);
            if (charInfoCurrentPanelField == null)
                MelonLogger.Warning("[CharacterInfoState] Could not find CharacterInfoMenu.currentPanel");

            charInfoCurrentPCField = typeof(CharacterInfoMenu).GetField("currentPC", flags);
            if (charInfoCurrentPCField == null)
                MelonLogger.Warning("[CharacterInfoState] Could not find CharacterInfoMenu.currentPC");

            reflectionCached = true;
            ModLog.Debug("[CharacterInfoState] Reflection cached");
        }

        #endregion
    }
}
