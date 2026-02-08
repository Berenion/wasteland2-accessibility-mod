using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Full keyboard navigation and screen reader support for the character creation / squad creation screen.
    /// Handles all 8 panel types: UseDefaultParty, Party, AddCharacter, Attributes, Skills, Traits, Dossier, Flavor.
    /// Priority 50 - same as Inventory/Conversation.
    /// </summary>
    public class CharacterState : IAccessibilityState
    {
        public string Name => "Character";
        public int Priority => 50;

        // Navigation state
        private List<GameObject> controlList = new List<GameObject>();
        private int controlIndex = -1;
        private CharacterScreen.EditorPanel lastPanelType = (CharacterScreen.EditorPanel)(-1);
        private bool skillsFocused = false;

        // Announcement tracking
        private string lastAnnouncedText = null;
        private float activationTime = 0f;
        private bool initialAnnouncementDone = false;
        private const float ANNOUNCEMENT_DELAY = 0.3f;

        // Reflection caches
        private static FieldInfo panelTypeField;
        private static FieldInfo currentPCField;
        private static FieldInfo skillEditorsField;
        private static FieldInfo skillCurrentCategoryField;
        private static FieldInfo traitEditorsField;
        private static FieldInfo attrEditorsField;
        private static FieldInfo addCharEntryListField;
        private static FieldInfo addCharCurrentEntryField;
        private static FieldInfo traitCurrentEditorField;
        private static FieldInfo skillEditorCurrentValueField;
        private static FieldInfo attrEditorCurrentValueField;
        private static FieldInfo traitEditorTraitField;
        private static MethodInfo onDoneClickedMethod;
        private static bool reflectionCached = false;

        public bool IsActive
        {
            get
            {
                var charScreen = CharacterScreen.instance;
                if (charScreen == null || !charScreen.gameObject.activeInHierarchy)
                    return false;

                // Not active if inventory panel is showing (InventoryState handles that)
                var chaInvPanel = UnityEngine.Object.FindObjectOfType<CHA_InventoryPanel>();
                if (chaInvPanel != null && chaInvPanel.gameObject.activeInHierarchy)
                    return false;

                return true;
            }
        }

        public bool HandleInput()
        {
            var charScreen = CharacterScreen.instance;
            if (charScreen == null) return false;

            // Cache reflection if needed
            if (!reflectionCached) CacheReflection();

            // Detect panel changes
            var currentPanelType = GetCurrentPanelType(charScreen);
            if (currentPanelType != lastPanelType)
            {
                OnPanelChanged(charScreen, currentPanelType);
            }

            // Force initial announcement after delay (queued, don't interrupt panel announcement)
            if (!initialAnnouncementDone && Time.time - activationTime >= ANNOUNCEMENT_DELAY)
            {
                initialAnnouncementDone = true;
                AnnounceCurrentControl(interrupt: false);
            }

            // Route input based on panel type
            switch (lastPanelType)
            {
                case CharacterScreen.EditorPanel.UseDefaultParty:
                    return HandleUseDefaultPartyInput(charScreen);
                case CharacterScreen.EditorPanel.Party:
                    return HandlePartyInput(charScreen);
                case CharacterScreen.EditorPanel.AddCharacter:
                    return HandleAddCharacterInput(charScreen);
                case CharacterScreen.EditorPanel.Attributes:
                    return skillsFocused ? HandleSkillsInput(charScreen) : HandleAttributesInput(charScreen);
                case CharacterScreen.EditorPanel.Skills:
                    return HandleSkillsInput(charScreen);
                case CharacterScreen.EditorPanel.Traits:
                    return HandleTraitsInput(charScreen);
                case CharacterScreen.EditorPanel.Dossier:
                    return HandleDossierInput(charScreen);
                case CharacterScreen.EditorPanel.Flavor:
                    return HandleFlavorInput(charScreen);
                default:
                    return HandleGenericInput(charScreen);
            }
        }

        public void OnActivated()
        {

            lastAnnouncedText = null;
            lastPanelType = (CharacterScreen.EditorPanel)(-1);
            controlList.Clear();
            controlIndex = -1;
            skillsFocused = false;
            activationTime = Time.time;
            initialAnnouncementDone = false;

            if (!reflectionCached) CacheReflection();

            var charScreen = CharacterScreen.instance;
            if (charScreen != null)
            {
                var panelType = GetCurrentPanelType(charScreen);
                OnPanelChanged(charScreen, panelType);
            }

            MelonLogger.Msg("[CharacterState] Activated");
        }

        public void OnDeactivated()
        {

            lastAnnouncedText = null;
            controlList.Clear();
            controlIndex = -1;
            skillsFocused = false;
            MelonLogger.Msg("[CharacterState] Deactivated");
        }

        // ========== Reflection ==========

        private void CacheReflection()
        {
            var flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

            panelTypeField = typeof(CharacterScreen).GetField("panelType", flags);
            currentPCField = typeof(CharacterScreen).GetField("currentPC", flags);
            onDoneClickedMethod = typeof(CharacterScreen).GetMethod("OnDoneClicked", flags);

            skillEditorsField = typeof(CHA_SkillPanel).GetField("skillEditors", flags);
            skillCurrentCategoryField = typeof(CHA_SkillPanel).GetField("currentCategory", flags);

            traitEditorsField = typeof(CHA_TraitsPanel).GetField("traitEditors", flags);
            traitCurrentEditorField = typeof(CHA_TraitsPanel).GetField("currentEditor", flags);

            attrEditorsField = typeof(CHA_AttributePanel).GetField("attributeEditors", flags);

            addCharEntryListField = typeof(CHA_AddCharacterPanel).GetField("entryList", flags);
            addCharCurrentEntryField = typeof(CHA_AddCharacterPanel).GetField("currentEntry", flags);

            skillEditorCurrentValueField = typeof(CHA_SkillEditor).GetField("currentValue", flags);
            attrEditorCurrentValueField = typeof(CHA_AttributeEditor).GetField("currentValue", flags);
            traitEditorTraitField = typeof(CHA_TraitEditor).GetField("trait", flags);

            reflectionCached = true;
            MelonLogger.Msg("[CharacterState] Reflection cached");
        }

        private CharacterScreen.EditorPanel GetCurrentPanelType(CharacterScreen screen)
        {
            if (panelTypeField != null)
            {
                return (CharacterScreen.EditorPanel)panelTypeField.GetValue(screen);
            }

            // Fallback: detect from active panels
            if (screen.attributePanel != null && screen.attributePanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Attributes;
            if (screen.skillPanel != null && screen.skillPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Skills;
            if (screen.traitsPanel != null && screen.traitsPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Traits;
            if (screen.dossierPanel != null && screen.dossierPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Dossier;
            if (screen.flavorPanel != null && screen.flavorPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Flavor;
            if (screen.addCharacterPanel != null && screen.addCharacterPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.AddCharacter;
            if (screen.partyPanel != null && screen.partyPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.Party;
            if (screen.usePremadePartyPanel != null && screen.usePremadePartyPanel.gameObject.activeInHierarchy)
                return CharacterScreen.EditorPanel.UseDefaultParty;

            return CharacterScreen.EditorPanel.Party;
        }

        private PC GetCurrentPC(CharacterScreen screen)
        {
            if (currentPCField != null)
                return currentPCField.GetValue(screen) as PC;
            return null;
        }

        // ========== Panel Change Detection ==========

        private void OnPanelChanged(CharacterScreen screen, CharacterScreen.EditorPanel newPanel)
        {
            lastPanelType = newPanel;
            skillsFocused = false;
            controlList.Clear();
            controlIndex = -1;

            BuildControlList(screen, newPanel);

            string panelName = GetPanelDisplayName(newPanel);
            string announcement = $"Character screen, {panelName}";

            // Add context hints per panel
            switch (newPanel)
            {
                case CharacterScreen.EditorPanel.UseDefaultParty:
                    announcement += ". Enter for default party, Escape for custom";
                    break;
                case CharacterScreen.EditorPanel.Party:
                    announcement += $". {GetPartyCount(screen)} rangers";
                    break;
                case CharacterScreen.EditorPanel.Attributes:
                    announcement += ". F to switch to skills, P for points remaining";
                    break;
                case CharacterScreen.EditorPanel.Skills:
                    announcement += ". F to switch to attributes, Page Up and Page Down for categories, P for points remaining";
                    break;
                case CharacterScreen.EditorPanel.Traits:
                    announcement += ". Enter to toggle, R for description";
                    break;
            }

            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[CharacterState] Panel changed to: {newPanel} ({controlList.Count} controls)");

            // Select first control
            if (controlList.Count > 0)
            {
                controlIndex = 0;
                UICamera.selectedObject = controlList[0];
            }

            // Reset announcement tracking
            initialAnnouncementDone = false;
            activationTime = Time.time;
        }

        private string GetPanelDisplayName(CharacterScreen.EditorPanel panel)
        {
            switch (panel)
            {
                case CharacterScreen.EditorPanel.UseDefaultParty: return "Use default party";
                case CharacterScreen.EditorPanel.Party: return "Party";
                case CharacterScreen.EditorPanel.AddCharacter: return "Add character";
                case CharacterScreen.EditorPanel.Attributes: return "Attributes";
                case CharacterScreen.EditorPanel.Skills: return "Skills";
                case CharacterScreen.EditorPanel.Traits: return "Traits";
                case CharacterScreen.EditorPanel.Dossier: return "Dossier";
                case CharacterScreen.EditorPanel.Flavor: return "Customization";
                default: return "Unknown";
            }
        }

        private int GetPartyCount(CharacterScreen screen)
        {
            if (screen.partyPanel == null) return 0;
            int count = 0;
            foreach (var entry in screen.partyPanel.partyEntries)
            {
                if (entry != null && entry.gameObject.activeInHierarchy)
                {
                    // Check if the entry has a character (not an empty slot)
                    var infoContainer = entry.infoContainer;
                    if (infoContainer != null && infoContainer.activeInHierarchy)
                        count++;
                }
            }
            return count;
        }

        // ========== Control List Building ==========

        private void BuildControlList(CharacterScreen screen, CharacterScreen.EditorPanel panel)
        {
            controlList.Clear();

            switch (panel)
            {
                case CharacterScreen.EditorPanel.UseDefaultParty:
                    BuildUseDefaultPartyControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Party:
                    BuildPartyControls(screen);
                    break;
                case CharacterScreen.EditorPanel.AddCharacter:
                    BuildAddCharacterControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Attributes:
                    if (skillsFocused)
                        BuildSkillControls(screen);
                    else
                        BuildAttributeControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Skills:
                    BuildSkillControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Traits:
                    BuildTraitControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Dossier:
                    BuildDossierControls(screen);
                    break;
                case CharacterScreen.EditorPanel.Flavor:
                    BuildFlavorControls(screen);
                    break;
            }
        }

        private void BuildUseDefaultPartyControls(CharacterScreen screen)
        {
            // This panel has 2 main buttons: Use Default and Create Custom
            if (screen.usePremadePartyPanel == null) return;

            var buttons = screen.usePremadePartyPanel.GetComponentsInChildren<UIButton>(false);
            foreach (var btn in buttons)
            {
                if (btn == null || !btn.gameObject.activeInHierarchy) continue;

                // Only include buttons that have meaningful labels
                UILabel label = btn.GetComponentInChildren<UILabel>();
                if (label == null) continue;

                string text = UITextExtractor.CleanText(label.text);
                if (string.IsNullOrEmpty(text) || text.Length <= 2) continue; // Skip "or", empty labels, etc.

                controlList.Add(btn.gameObject);
            }

            MelonLogger.Msg($"[CharacterState] UseDefaultParty controls: {controlList.Count}");
        }

        private void BuildPartyControls(CharacterScreen screen)
        {
            if (screen.partyPanel == null) return;

            foreach (var entry in screen.partyPanel.partyEntries)
            {
                if (entry != null && entry.gameObject.activeInHierarchy)
                {
                    controlList.Add(entry.gameObject);
                }
            }

            // Add Start Playing button if visible
            if (screen.startPlayingButton != null && screen.startPlayingButton.gameObject.activeInHierarchy)
            {
                controlList.Add(screen.startPlayingButton.gameObject);
            }

            MelonLogger.Msg($"[CharacterState] Party controls: {controlList.Count}");
        }

        private void BuildAddCharacterControls(CharacterScreen screen)
        {
            if (screen.addCharacterPanel == null) return;

            // Get entries from reflection (private entryList)
            if (addCharEntryListField != null)
            {
                var entries = addCharEntryListField.GetValue(screen.addCharacterPanel) as System.Collections.IList;
                if (entries != null)
                {
                    foreach (var entry in entries)
                    {
                        var component = entry as CHA_PremadeCharacterEntry;
                        if (component != null && component.gameObject.activeInHierarchy)
                        {
                            controlList.Add(component.gameObject);
                        }
                    }
                }
            }

            // Fallback: scan the entry container UIGrid
            if (controlList.Count == 0 && screen.addCharacterPanel.entryContainer != null)
            {
                var grid = screen.addCharacterPanel.entryContainer;
                for (int i = 0; i < grid.transform.childCount; i++)
                {
                    var child = grid.transform.GetChild(i);
                    if (child != null && child.gameObject.activeInHierarchy)
                    {
                        var premadeEntry = child.GetComponent<CHA_PremadeCharacterEntry>();
                        if (premadeEntry != null)
                        {
                            controlList.Add(child.gameObject);
                        }
                    }
                }
            }

            MelonLogger.Msg($"[CharacterState] AddCharacter controls: {controlList.Count}");
        }

        private void BuildAttributeControls(CharacterScreen screen)
        {
            if (screen.attributePanel == null || screen.attributePanel.attributeGrid == null) return;

            var grid = screen.attributePanel.attributeGrid;
            List<Transform> children = new List<Transform>();
            for (int i = 0; i < grid.transform.childCount; i++)
            {
                var child = grid.transform.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    children.Add(child);
                }
            }

            // Sort by name to match UIGrid order
            children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            foreach (var child in children)
            {
                controlList.Add(child.gameObject);
            }

            MelonLogger.Msg($"[CharacterState] Attribute controls: {controlList.Count}");
        }

        private void BuildSkillControls(CharacterScreen screen)
        {
            if (screen.skillPanel == null) return;

            // Get the active grid based on current category
            UIGrid activeGrid = GetActiveSkillGrid(screen);
            if (activeGrid == null) return;

            List<Transform> children = new List<Transform>();
            for (int i = 0; i < activeGrid.transform.childCount; i++)
            {
                var child = activeGrid.transform.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    children.Add(child);
                }
            }

            children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            foreach (var child in children)
            {
                controlList.Add(child.gameObject);
            }

            MelonLogger.Msg($"[CharacterState] Skill controls: {controlList.Count}");
        }

        private UIGrid GetActiveSkillGrid(CharacterScreen screen)
        {
            // Check which grid is active
            if (screen.skillPanel.combatGrid != null && screen.skillPanel.combatGrid.gameObject.activeInHierarchy)
                return screen.skillPanel.combatGrid;
            if (screen.skillPanel.knowledgeGrid != null && screen.skillPanel.knowledgeGrid.gameObject.activeInHierarchy)
                return screen.skillPanel.knowledgeGrid;
            if (screen.skillPanel.generalGrid != null && screen.skillPanel.generalGrid.gameObject.activeInHierarchy)
                return screen.skillPanel.generalGrid;

            // Default to combat
            return screen.skillPanel.combatGrid;
        }

        private string GetActiveSkillCategory(CharacterScreen screen)
        {
            if (screen.skillPanel.combatGrid != null && screen.skillPanel.combatGrid.gameObject.activeInHierarchy)
                return "Combat";
            if (screen.skillPanel.knowledgeGrid != null && screen.skillPanel.knowledgeGrid.gameObject.activeInHierarchy)
                return "Knowledge";
            if (screen.skillPanel.generalGrid != null && screen.skillPanel.generalGrid.gameObject.activeInHierarchy)
                return "General";
            return "Combat";
        }

        private void BuildTraitControls(CharacterScreen screen)
        {
            if (screen.traitsPanel == null) return;

            // Get trait editors from reflection
            if (traitEditorsField != null)
            {
                var editors = traitEditorsField.GetValue(screen.traitsPanel) as System.Collections.IList;
                if (editors != null)
                {
                    foreach (var editor in editors)
                    {
                        var component = editor as CHA_TraitEditor;
                        if (component != null && component.gameObject.activeInHierarchy)
                        {
                            controlList.Add(component.gameObject);
                        }
                    }
                }
            }

            // Fallback: scan traitGrid
            if (controlList.Count == 0 && screen.traitsPanel.traitGrid != null)
            {
                var grid = screen.traitsPanel.traitGrid;
                for (int i = 0; i < grid.transform.childCount; i++)
                {
                    var child = grid.transform.GetChild(i);
                    if (child != null && child.gameObject.activeInHierarchy)
                    {
                        controlList.Add(child.gameObject);
                    }
                }
            }

            MelonLogger.Msg($"[CharacterState] Trait controls: {controlList.Count}");
        }

        private void BuildDossierControls(CharacterScreen screen)
        {
            if (screen.dossierPanel == null) return;

            // Dossier has gender buttons + flavor panel inputs
            // Gender buttons (Male/Female)
            var buttons = screen.dossierPanel.GetComponentsInChildren<UIButton>(false);
            foreach (var btn in buttons)
            {
                if (btn != null && btn.gameObject.activeInHierarchy)
                {
                    controlList.Add(btn.gameObject);
                }
            }

            // Add flavor panel inputs if visible
            if (screen.flavorPanel != null)
            {
                if (screen.flavorPanel.nameInput != null && screen.flavorPanel.nameInput.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.nameInput.gameObject);
                if (screen.flavorPanel.ageInput != null && screen.flavorPanel.ageInput.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.ageInput.gameObject);
                if (screen.flavorPanel.religionList != null && screen.flavorPanel.religionList.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.religionList.gameObject);
                if (screen.flavorPanel.smokesList != null && screen.flavorPanel.smokesList.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.smokesList.gameObject);
                if (screen.flavorPanel.ethnicityList != null && screen.flavorPanel.ethnicityList.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.ethnicityList.gameObject);
                if (screen.flavorPanel.biographyInput != null && screen.flavorPanel.biographyInput.gameObject.activeInHierarchy)
                    controlList.Add(screen.flavorPanel.biographyInput.gameObject);
            }

            MelonLogger.Msg($"[CharacterState] Dossier controls: {controlList.Count}");
        }

        private void BuildFlavorControls(CharacterScreen screen)
        {
            // Flavor panel is similar to Dossier but standalone
            BuildDossierControls(screen);
        }

        // ========== Navigation ==========

        private void NavigateList(int direction)
        {
            if (controlList.Count == 0) return;

            int newIndex = controlIndex + direction;

            // Wrap around
            if (newIndex < 0) newIndex = controlList.Count - 1;
            if (newIndex >= controlList.Count) newIndex = 0;

            if (newIndex != controlIndex)
            {
                controlIndex = newIndex;
                UICamera.selectedObject = controlList[controlIndex];
                AnnounceCurrentControl();
            }
        }

        private void AnnounceCurrentControl(bool interrupt = true)
        {
            if (controlIndex < 0 || controlIndex >= controlList.Count) return;

            var obj = controlList[controlIndex];
            if (obj == null) return;

            string announcement = GetControlAnnouncement(obj);
            if (!string.IsNullOrEmpty(announcement) && announcement != lastAnnouncedText)
            {
                lastAnnouncedText = announcement;
                if (interrupt) ScreenReaderManager.SpeakInterrupt(announcement); else ScreenReaderManager.Speak(announcement);
                MelonLogger.Msg($"[CharacterState] Announce [{controlIndex}]: {announcement}");
            }
        }

        // ========== Control Announcements ==========

        private string GetControlAnnouncement(GameObject obj)
        {
            if (obj == null) return null;

            // Check for specific component types
            var attrEditor = obj.GetComponent<CHA_AttributeEditor>();
            if (attrEditor != null)
                return GetAttributeEditorAnnouncement(attrEditor);

            var skillEditor = obj.GetComponent<CHA_SkillEditor>();
            if (skillEditor != null)
                return GetSkillEditorAnnouncement(skillEditor);

            var traitEditor = obj.GetComponent<CHA_TraitEditor>();
            if (traitEditor != null)
                return GetTraitEditorAnnouncement(traitEditor);

            var partyEntry = obj.GetComponent<CHA_PartyEntry>();
            if (partyEntry != null)
                return GetPartyEntryAnnouncement(partyEntry);

            var premadeEntry = obj.GetComponent<CHA_PremadeCharacterEntry>();
            if (premadeEntry != null)
                return GetPremadeEntryAnnouncement(premadeEntry);

            // Check for UIInput
            var input = obj.GetComponent<UIInput>();
            if (input != null)
            {
                string label = FindLabelText(obj);
                string value = !string.IsNullOrEmpty(input.value) ? input.value : "empty";
                return $"{label}, {value}, text field";
            }

            // Check for UIPopupList
            var popupList = obj.GetComponent<UIPopupList>();
            if (popupList != null)
            {
                string label = FindLabelText(obj);
                string value = UITextExtractor.CleanText(popupList.value);
                return $"{label}, {value}, dropdown";
            }

            // Generic button
            var button = obj.GetComponent<UIButton>();
            if (button != null)
            {
                UILabel btnLabel = obj.GetComponentInChildren<UILabel>();
                string text = btnLabel != null ? UITextExtractor.CleanText(btnLabel.text) : obj.name;
                return $"{text}, button";
            }

            // Last resort: label text
            UILabel anyLabel = obj.GetComponentInChildren<UILabel>();
            if (anyLabel != null)
            {
                return UITextExtractor.CleanText(anyLabel.text);
            }

            return obj.name;
        }

        private string GetAttributeEditorAnnouncement(CHA_AttributeEditor editor)
        {
            try
            {
                string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.attribute;
                int value = 0;
                if (attrEditorCurrentValueField != null)
                    value = (int)attrEditorCurrentValueField.GetValue(editor);
                else if (editor.valueLabel != null)
                    int.TryParse(UITextExtractor.CleanText(editor.valueLabel.text), out value);

                return $"{name}, {value}, attribute";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing attribute: {ex.Message}");
                return "Attribute";
            }
        }

        private string GetSkillEditorAnnouncement(CHA_SkillEditor editor)
        {
            try
            {
                string name = editor.nameLabel != null ? UITextExtractor.CleanText(editor.nameLabel.text) : editor.skillName;
                int value = 0;
                if (skillEditorCurrentValueField != null)
                    value = (int)skillEditorCurrentValueField.GetValue(editor);
                else if (editor.levelLabel != null)
                    int.TryParse(UITextExtractor.CleanText(editor.levelLabel.text), out value);

                return $"{name}, level {value}, skill";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing skill: {ex.Message}");
                return "Skill";
            }
        }

        private string GetTraitEditorAnnouncement(CHA_TraitEditor editor)
        {
            try
            {
                // Get trait name
                string name = editor.traitName;
                if (string.IsNullOrEmpty(name))
                {
                    var nameBtn = editor.nameButton;
                    if (nameBtn != null)
                    {
                        var label = nameBtn.GetComponentInChildren<UILabel>();
                        if (label != null)
                            name = UITextExtractor.CleanText(label.text);
                    }
                }

                if (string.IsNullOrEmpty(name))
                    name = "Unknown trait";

                string checkedState = editor.checkbox != null ? (editor.checkbox.value ? "selected" : "not selected") : "";
                return $"{name}, {checkedState}, quirk";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing trait: {ex.Message}");
                return "Trait";
            }
        }

        private string GetPartyEntryAnnouncement(CHA_PartyEntry entry)
        {
            try
            {
                // Check if the slot has a character
                bool hasCharacter = entry.infoContainer != null && entry.infoContainer.activeInHierarchy;

                if (!hasCharacter)
                {
                    return "Empty slot, press Enter to add a ranger";
                }

                // Get character name from labels
                var labels = entry.GetComponentsInChildren<UILabel>();
                string name = "Unknown";
                foreach (var label in labels)
                {
                    if (label != null && !string.IsNullOrEmpty(label.text))
                    {
                        string text = UITextExtractor.CleanText(label.text);
                        if (!string.IsNullOrEmpty(text))
                        {
                            name = text;
                            break;
                        }
                    }
                }

                return $"{name}, party member. Enter to edit, Delete to remove";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing party entry: {ex.Message}");
                return "Party slot";
            }
        }

        private string GetPremadeEntryAnnouncement(CHA_PremadeCharacterEntry entry)
        {
            try
            {
                string name = entry.nameLabel != null ? UITextExtractor.CleanText(entry.nameLabel.text) : "Unknown";
                string spec = entry.specializationLabel != null ? UITextExtractor.CleanText(entry.specializationLabel.text) : "";

                string result = name;
                if (!string.IsNullOrEmpty(spec))
                    result += $", {spec}";

                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing premade entry: {ex.Message}");
                return "Character entry";
            }
        }

        private string FindLabelText(GameObject obj)
        {
            // Look for a UILabel as sibling or parent that acts as a label for this control
            UILabel label = obj.GetComponentInChildren<UILabel>();
            if (label != null)
                return UITextExtractor.CleanText(label.text);

            // Check parent
            if (obj.transform.parent != null)
            {
                label = obj.transform.parent.GetComponentInChildren<UILabel>();
                if (label != null)
                    return UITextExtractor.CleanText(label.text);
            }

            return obj.name;
        }

        // ========== Panel-Specific Input Handlers ==========

        private bool HandleCommonInput(CharacterScreen screen)
        {
            // Tab to announce current panel + context
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                string panelName = GetPanelDisplayName(lastPanelType);
                string context = "";
                if (skillsFocused)
                    context = " (Skills sub-area, F to switch back)";
                ScreenReaderManager.SpeakInterrupt($"{panelName}{context}, {controlIndex + 1} of {controlList.Count}");
                return true;
            }

            // C to announce character summary
            if (Input.GetKeyDown(KeyCode.C))
            {
                AnnounceCharacterSummary(screen);
                return true;
            }

            // N for next/done
            if (Input.GetKeyDown(KeyCode.N))
            {
                if (onDoneClickedMethod != null)
                {
                    onDoneClickedMethod.Invoke(screen, new object[] { null });
                    MelonLogger.Msg("[CharacterState] OnDoneClicked called via N key");
                }
                return true;
            }

            // Escape for back
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                screen.OnBackClicked();
                MelonLogger.Msg("[CharacterState] OnBackClicked called via Escape");
                return true;
            }

            return false;
        }

        private bool HandleUseDefaultPartyInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    controlList[controlIndex].SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    MelonLogger.Msg("[CharacterState] Activated button in UseDefaultParty");
                }
                return true;
            }

            return false;
        }

        private bool HandlePartyInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var obj = controlList[controlIndex];

                    // Check if it's a party entry
                    var partyEntry = obj.GetComponent<CHA_PartyEntry>();
                    if (partyEntry != null)
                    {
                        // OnPartyEntryClicked is private, use reflection
                        var method = typeof(CHA_PartyPanel).GetMethod("OnPartyEntryClicked",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (method != null)
                        {
                            method.Invoke(screen.partyPanel, new object[] { obj });
                        }
                        else
                        {
                            // Fallback: send click
                            obj.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                        }
                        MelonLogger.Msg("[CharacterState] Party entry clicked");
                    }
                    else
                    {
                        // It's a button (Start Playing, etc.)
                        obj.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    }
                }
                return true;
            }

            // Delete to remove character
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var partyEntry = controlList[controlIndex].GetComponent<CHA_PartyEntry>();
                    if (partyEntry != null && partyEntry.infoContainer != null && partyEntry.infoContainer.activeInHierarchy)
                    {
                        screen.partyPanel.OnDeleteCharacterClicked(partyEntry.deleteButton != null ? partyEntry.deleteButton.gameObject : controlList[controlIndex]);
                        MelonLogger.Msg("[CharacterState] Delete character requested");
                    }
                    else
                    {
                        ScreenReaderManager.SpeakInterrupt("Empty slot");
                    }
                }
                return true;
            }

            // S to start playing (shortcut)
            if (Input.GetKeyDown(KeyCode.S))
            {
                if (screen.startPlayingButton != null && screen.startPlayingButton.gameObject.activeInHierarchy && screen.startPlayingButton.isEnabled)
                {
                    screen.startPlayingButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    MelonLogger.Msg("[CharacterState] Start playing via S key");
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Not enough rangers to start");
                }
                return true;
            }

            return false;
        }

        private bool HandleAddCharacterInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);

                // Also trigger selection callback for preview
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var entry = controlList[controlIndex].GetComponent<CHA_PremadeCharacterEntry>();
                    if (entry != null && entry.selectCallback != null)
                    {
                        entry.selectCallback(entry);
                    }
                }
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var entry = controlList[controlIndex].GetComponent<CHA_PremadeCharacterEntry>();
                    if (entry != null)
                    {
                        entry.OnAddClicked(null);
                        MelonLogger.Msg("[CharacterState] Added premade character");
                    }
                }
                return true;
            }

            // R to read biography
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (screen.addCharacterPanel.biographyLabel != null)
                {
                    string bio = UITextExtractor.CleanText(screen.addCharacterPanel.biographyLabel.text);
                    if (!string.IsNullOrEmpty(bio))
                        ScreenReaderManager.SpeakInterrupt(bio);
                    else
                        ScreenReaderManager.SpeakInterrupt("No biography available");
                }
                return true;
            }

            return false;
        }

        private bool HandleAttributesInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Left/Right to adjust attribute value
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_AttributeEditor>();
                    if (editor != null)
                    {
                        if (Input.GetKeyDown(KeyCode.RightArrow))
                        {
                            if (editor.plusButton != null && editor.plusButton.isEnabled)
                            {
                                editor.plusButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                                // Re-announce with new value
                                AnnounceCurrentControl();
                            }
                            else
                            {
                                ScreenReaderManager.SpeakInterrupt("Maximum");
                            }
                        }
                        else
                        {
                            if (editor.minusButton != null && editor.minusButton.isEnabled)
                            {
                                editor.minusButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                                AnnounceCurrentControl();
                            }
                            else
                            {
                                ScreenReaderManager.SpeakInterrupt("Minimum");
                            }
                        }
                    }
                }
                return true;
            }

            // F to toggle to skills
            if (Input.GetKeyDown(KeyCode.F))
            {
                skillsFocused = true;
                BuildControlList(screen, lastPanelType);
                if (controlList.Count > 0)
                {
                    controlIndex = 0;
                    UICamera.selectedObject = controlList[0];
                }

                string category = GetActiveSkillCategory(screen);
                ScreenReaderManager.SpeakInterrupt($"Skills, {category} category. Page Up and Page Down to switch");
                AnnounceCurrentControl(interrupt: false);
                return true;
            }

            // P for points remaining
            if (Input.GetKeyDown(KeyCode.P))
            {
                AnnounceAttributePointsRemaining(screen);
                return true;
            }

            return false;
        }

        private bool HandleSkillsInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Left/Right to adjust skill value
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_SkillEditor>();
                    if (editor != null)
                    {
                        // Use the plus/minus buttons by finding them
                        var buttons = editor.GetComponentsInChildren<UIButton>(true);
                        UIButton plusBtn = null;
                        UIButton minusBtn = null;
                        foreach (var btn in buttons)
                        {
                            if (btn.gameObject.name.ToLower().Contains("plus") || btn.gameObject.name.ToLower().Contains("add"))
                                plusBtn = btn;
                            else if (btn.gameObject.name.ToLower().Contains("minus") || btn.gameObject.name.ToLower().Contains("sub"))
                                minusBtn = btn;
                        }

                        if (Input.GetKeyDown(KeyCode.RightArrow))
                        {
                            if (plusBtn != null && plusBtn.isEnabled)
                            {
                                plusBtn.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                                AnnounceCurrentControl();
                            }
                            else
                            {
                                ScreenReaderManager.SpeakInterrupt("Cannot increase");
                            }
                        }
                        else
                        {
                            if (minusBtn != null && minusBtn.isEnabled)
                            {
                                minusBtn.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                                AnnounceCurrentControl();
                            }
                            else
                            {
                                ScreenReaderManager.SpeakInterrupt("Cannot decrease");
                            }
                        }
                    }
                }
                return true;
            }

            // Page Up/Down to switch skill category
            if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown))
            {
                SwitchSkillCategory(screen, Input.GetKeyDown(KeyCode.PageDown) ? 1 : -1);
                return true;
            }

            // F to toggle back to attributes (only if we're in the attributes panel with skills focused)
            if (Input.GetKeyDown(KeyCode.F) && lastPanelType == CharacterScreen.EditorPanel.Attributes)
            {
                skillsFocused = false;
                BuildControlList(screen, lastPanelType);
                if (controlList.Count > 0)
                {
                    controlIndex = 0;
                    UICamera.selectedObject = controlList[0];
                }
                ScreenReaderManager.SpeakInterrupt("Attributes");
                AnnounceCurrentControl(interrupt: false);
                return true;
            }

            // P for points remaining
            if (Input.GetKeyDown(KeyCode.P))
            {
                AnnounceSkillPointsRemaining(screen);
                return true;
            }

            return false;
        }

        private bool HandleTraitsInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Enter or Space to toggle trait
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_TraitEditor>();
                    if (editor != null && editor.checkbox != null)
                    {
                        if (editor.checkboxButton != null && !editor.checkboxButton.isEnabled)
                        {
                            ScreenReaderManager.SpeakInterrupt("Locked");
                        }
                        else
                        {
                            editor.checkbox.value = !editor.checkbox.value;
                            string state = editor.checkbox.value ? "selected" : "not selected";
                            ScreenReaderManager.SpeakInterrupt(state);
                        }
                    }
                }
                return true;
            }

            // R to read trait description
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var editor = controlList[controlIndex].GetComponent<CHA_TraitEditor>();
                    if (editor != null)
                    {
                        // Get description from the trait via reflection
                        string desc = GetTraitDescription(editor);
                        if (!string.IsNullOrEmpty(desc))
                            ScreenReaderManager.SpeakInterrupt(desc);
                        else
                            ScreenReaderManager.SpeakInterrupt("No description available");
                    }
                }
                return true;
            }

            return false;
        }

        private bool HandleDossierInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            // Left/Right for gender or dropdown cycling
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var obj = controlList[controlIndex];

                    // Check for popup list
                    var popupList = obj.GetComponent<UIPopupList>();
                    if (popupList != null && popupList.items != null && popupList.items.Count > 0)
                    {
                        int dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1;
                        int idx = popupList.items.IndexOf(popupList.value);
                        int newIdx = (idx + dir + popupList.items.Count) % popupList.items.Count;
                        popupList.value = popupList.items[newIdx];
                        string val = UITextExtractor.CleanText(popupList.value);
                        ScreenReaderManager.SpeakInterrupt(val);
                        return true;
                    }

                    // Check for gender buttons
                    var button = obj.GetComponent<UIButton>();
                    if (button != null)
                    {
                        // Click the button to toggle
                        obj.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                        AnnounceCurrentControl();
                        return true;
                    }
                }
                return true;
            }

            // Enter to activate/focus
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    var obj = controlList[controlIndex];
                    var input = obj.GetComponent<UIInput>();
                    if (input != null)
                    {
                        input.isSelected = !input.isSelected;
                        ScreenReaderManager.SpeakInterrupt(input.isSelected ? "Editing" : "Done editing");
                    }
                    else
                    {
                        obj.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    }
                }
                return true;
            }

            return false;
        }

        private bool HandleFlavorInput(CharacterScreen screen)
        {
            return HandleDossierInput(screen);
        }

        private bool HandleGenericInput(CharacterScreen screen)
        {
            if (HandleCommonInput(screen)) return true;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                int dir = Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 1;
                NavigateList(dir);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (controlIndex >= 0 && controlIndex < controlList.Count)
                {
                    controlList[controlIndex].SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                }
                return true;
            }

            return false;
        }

        // ========== Skill Category Switching ==========

        private void SwitchSkillCategory(CharacterScreen screen, int direction)
        {
            if (screen.skillPanel == null) return;

            // Determine current category
            int current = 0;
            if (screen.skillPanel.combatGrid != null && screen.skillPanel.combatGrid.gameObject.activeInHierarchy)
                current = 0;
            else if (screen.skillPanel.knowledgeGrid != null && screen.skillPanel.knowledgeGrid.gameObject.activeInHierarchy)
                current = 1;
            else if (screen.skillPanel.generalGrid != null && screen.skillPanel.generalGrid.gameObject.activeInHierarchy)
                current = 2;

            int newCategory = (current + direction + 3) % 3;

            // Use the ShowCategory method via the mode buttons
            switch (newCategory)
            {
                case 0:
                    if (screen.skillPanel.combatButton != null)
                        screen.skillPanel.combatButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    break;
                case 1:
                    if (screen.skillPanel.knowledgeButton != null)
                        screen.skillPanel.knowledgeButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    break;
                case 2:
                    if (screen.skillPanel.generalButton != null)
                        screen.skillPanel.generalButton.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
                    break;
            }

            // Rebuild control list for new category
            BuildControlList(screen, lastPanelType);
            if (controlList.Count > 0)
            {
                controlIndex = 0;
                UICamera.selectedObject = controlList[0];
            }

            string[] categories = { "Combat", "Knowledge", "General" };
            ScreenReaderManager.SpeakInterrupt($"{categories[newCategory]} skills");

            // Brief delay then announce first control
            initialAnnouncementDone = false;
            activationTime = Time.time;
        }

        // ========== Points Remaining ==========

        private void AnnounceAttributePointsRemaining(CharacterScreen screen)
        {
            if (screen.attributePanel == null) return;

            try
            {
                // Look for a points label in the attribute panel
                var labels = screen.attributePanel.GetComponentsInChildren<UILabel>();
                foreach (var label in labels)
                {
                    string text = label.text;
                    if (text != null && (text.Contains("point") || text.Contains("Point") || text.Contains("remaining") || text.Contains("Remaining")))
                    {
                        ScreenReaderManager.SpeakInterrupt(UITextExtractor.CleanText(text));
                        return;
                    }
                }

                // Fallback: try the description panel's points display
                if (screen.attributePanel.descriptionPanel != null)
                {
                    var descLabels = screen.attributePanel.descriptionPanel.GetComponentsInChildren<UILabel>();
                    foreach (var label in descLabels)
                    {
                        string text = UITextExtractor.CleanText(label.text);
                        if (!string.IsNullOrEmpty(text))
                        {
                            ScreenReaderManager.SpeakInterrupt(text);
                            return;
                        }
                    }
                }

                ScreenReaderManager.SpeakInterrupt("Points information not available");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error getting attribute points: {ex.Message}");
            }
        }

        private void AnnounceSkillPointsRemaining(CharacterScreen screen)
        {
            if (screen.skillPanel == null) return;

            try
            {
                var labels = screen.skillPanel.GetComponentsInChildren<UILabel>();
                foreach (var label in labels)
                {
                    string text = label.text;
                    if (text != null && (text.Contains("point") || text.Contains("Point") || text.Contains("remaining") || text.Contains("Remaining")))
                    {
                        ScreenReaderManager.SpeakInterrupt(UITextExtractor.CleanText(text));
                        return;
                    }
                }

                ScreenReaderManager.SpeakInterrupt("Points information not available");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error getting skill points: {ex.Message}");
            }
        }

        // ========== Trait Description ==========

        private string GetTraitDescription(CHA_TraitEditor editor)
        {
            try
            {
                if (traitEditorTraitField != null)
                {
                    var trait = traitEditorTraitField.GetValue(editor) as Trait;
                    if (trait != null && !string.IsNullOrEmpty(trait.description))
                    {
                        return UITextExtractor.CleanText(Language.Localize(trait.description, false, false, string.Empty));
                    }
                }

                // Fallback: look for tooltip on the editor
                var tooltip = editor.GetComponent<UITooltip>();
                if (tooltip != null)
                {
                    var textField = typeof(UITooltip).GetField("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textField != null)
                    {
                        string text = textField.GetValue(tooltip) as string;
                        if (!string.IsNullOrEmpty(text))
                            return UITextExtractor.CleanText(text);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error getting trait description: {ex.Message}");
                return null;
            }
        }

        // ========== Character Summary ==========

        private void AnnounceCharacterSummary(CharacterScreen screen)
        {
            PC pc = GetCurrentPC(screen);

            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
            {
                pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
            }

            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No character selected");
                return;
            }

            List<string> parts = new List<string>();

            // Character name
            if (pc.pcTemplate != null)
            {
                string name = UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                parts.Add(name);
            }

            // Specialization
            if (pc.pcTemplate != null)
            {
                string spec = pc.pcTemplate.GetLocalizedSpecialization();
                if (!string.IsNullOrEmpty(spec))
                    parts.Add(UITextExtractor.CleanText(spec));
            }

            // Level
            if (pc.pcTemplate != null)
            {
                parts.Add($"Level {pc.pcTemplate.level}");
            }

            ScreenReaderManager.SpeakInterrupt(string.Join(", ", parts.ToArray()));
        }
    }
}
