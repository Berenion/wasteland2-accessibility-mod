using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Provides screen reader announcements for character screens
    /// including skills, attributes, and character stats panels.
    /// Lower priority than DialogState but same as Inventory/Conversation.
    /// </summary>
    public class CharacterState : IAccessibilityState
    {
        public string Name => "Character";
        public int Priority => 50;

        private GameObject lastSelectedObject;

        public bool IsActive
        {
            get
            {
                // Check for CharacterScreen (character creation/editing)
                var charScreen = CharacterScreen.instance;
                if (charScreen != null && charScreen.gameObject.activeInHierarchy)
                {
                    // Only active if we're in a character editing panel, not inventory
                    // (InventoryState handles the inventory panel)
                    var chaInvPanel = UnityEngine.Object.FindObjectOfType<CHA_InventoryPanel>();
                    if (chaInvPanel != null && chaInvPanel.gameObject.activeInHierarchy)
                    {
                        return false; // Let InventoryState handle this
                    }

                    return true;
                }

                return false;
            }
        }

        public bool HandleInput()
        {
            // Monitor for selection changes
            CheckSelectionChanged();

            // Arrow keys - let game handle but track changes
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                lastSelectedObject = null; // Force re-check
                return false;
            }

            // Tab to announce current panel info
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                AnnounceCurrentPanelInfo();
                return true;
            }

            // C to announce character stats summary
            if (Input.GetKeyDown(KeyCode.C))
            {
                AnnounceCharacterSummary();
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            lastSelectedObject = null;

            AnnounceCurrentPanel();
            MelonLogger.Msg("[CharacterState] Activated");
        }

        public void OnDeactivated()
        {
            lastSelectedObject = null;
            MelonLogger.Msg("[CharacterState] Deactivated");
        }

        private void CheckSelectionChanged()
        {
            GameObject selected = UICamera.selectedObject;
            if (selected == lastSelectedObject) return;
            lastSelectedObject = selected;

            if (selected == null) return;

            // Check for skill editor
            var skillEditor = selected.GetComponent<CHA_SkillEditor>();
            if (skillEditor != null)
            {
                AnnounceSkillEditor(skillEditor);
                return;
            }

            // Check for attribute editor
            var attrEditor = selected.GetComponent<CHA_AttributeEditor>();
            if (attrEditor != null)
            {
                AnnounceAttributeEditor(attrEditor);
                return;
            }

            // Check for trait editor
            var traitEditor = selected.GetComponent<CHA_TraitEditor>();
            if (traitEditor != null)
            {
                AnnounceTraitEditor(traitEditor);
                return;
            }

            // Generic button check
            var button = selected.GetComponent<UIButton>();
            if (button != null)
            {
                UILabel label = selected.GetComponentInChildren<UILabel>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    string text = UITextExtractor.CleanText(label.text);
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReaderManager.Speak($"{text}, button", interrupt: true);
                    }
                }
            }
        }

        private void AnnounceCurrentPanel()
        {
            var charScreen = CharacterScreen.instance;
            if (charScreen == null) return;

            string panelName = GetPanelName(charScreen);
            ScreenReaderManager.Speak($"Character screen, {panelName}", interrupt: true);
        }

        private string GetPanelName(CharacterScreen screen)
        {
            // Try to get from active panel
            if (screen.attributePanel != null && screen.attributePanel.gameObject.activeInHierarchy)
                return "Attributes panel";
            if (screen.skillPanel != null && screen.skillPanel.gameObject.activeInHierarchy)
                return "Skills panel";
            if (screen.traitsPanel != null && screen.traitsPanel.gameObject.activeInHierarchy)
                return "Traits panel";
            if (screen.dossierPanel != null && screen.dossierPanel.gameObject.activeInHierarchy)
                return "Dossier panel";
            if (screen.partyPanel != null && screen.partyPanel.gameObject.activeInHierarchy)
                return "Party panel";
            if (screen.addCharacterPanel != null && screen.addCharacterPanel.gameObject.activeInHierarchy)
                return "Add character panel";
            if (screen.flavorPanel != null && screen.flavorPanel.gameObject.activeInHierarchy)
                return "Customization panel";

            return "Character panel";
        }

        private void AnnounceSkillEditor(CHA_SkillEditor editor)
        {
            if (editor == null) return;

            try
            {
                // Get skill name from the label
                UILabel nameLabel = editor.GetComponentInChildren<UILabel>();
                string skillName = nameLabel != null ? UITextExtractor.CleanText(nameLabel.text) : "Unknown skill";

                // Try to get skill level
                List<string> parts = new List<string>();
                parts.Add(skillName);

                // Look for level display
                var levelLabels = editor.GetComponentsInChildren<UILabel>();
                foreach (var label in levelLabels)
                {
                    if (label != nameLabel && !string.IsNullOrEmpty(label.text))
                    {
                        string text = UITextExtractor.CleanText(label.text);
                        if (int.TryParse(text, out int level))
                        {
                            parts.Add($"level {level}");
                            break;
                        }
                    }
                }

                ScreenReaderManager.Speak(string.Join(", ", parts.ToArray()), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing skill: {ex.Message}");
            }
        }

        private void AnnounceAttributeEditor(CHA_AttributeEditor editor)
        {
            if (editor == null) return;

            try
            {
                UILabel nameLabel = editor.GetComponentInChildren<UILabel>();
                string attrName = nameLabel != null ? UITextExtractor.CleanText(nameLabel.text) : "Unknown attribute";

                List<string> parts = new List<string>();
                parts.Add(attrName);

                // Look for value display
                var labels = editor.GetComponentsInChildren<UILabel>();
                foreach (var label in labels)
                {
                    if (label != nameLabel && !string.IsNullOrEmpty(label.text))
                    {
                        string text = UITextExtractor.CleanText(label.text);
                        if (int.TryParse(text, out int value))
                        {
                            parts.Add($"value {value}");
                            break;
                        }
                    }
                }

                ScreenReaderManager.Speak(string.Join(", ", parts.ToArray()), interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing attribute: {ex.Message}");
            }
        }

        private void AnnounceTraitEditor(CHA_TraitEditor editor)
        {
            if (editor == null) return;

            try
            {
                UILabel nameLabel = editor.GetComponentInChildren<UILabel>();
                string traitName = nameLabel != null ? UITextExtractor.CleanText(nameLabel.text) : "Unknown trait";

                ScreenReaderManager.Speak($"{traitName}, trait", interrupt: true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CharacterState] Error announcing trait: {ex.Message}");
            }
        }

        private void AnnounceCurrentPanelInfo()
        {
            var charScreen = CharacterScreen.instance;
            if (charScreen == null) return;

            string panelName = GetPanelName(charScreen);
            ScreenReaderManager.Speak(panelName, interrupt: true);
        }

        private void AnnounceCharacterSummary()
        {
            // Get current PC from character screen or game
            PC pc = null;

            var charScreen = CharacterScreen.instance;
            if (charScreen != null)
            {
                // Use reflection to get currentPC
                var field = typeof(CharacterScreen).GetField("currentPC",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    pc = field.GetValue(charScreen) as PC;
                }
            }

            if (pc == null && MonoBehaviourSingleton<Game>.HasInstance())
            {
                pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
            }

            if (pc == null)
            {
                ScreenReaderManager.Speak("No character selected", interrupt: true);
                return;
            }

            List<string> parts = new List<string>();

            // Character name
            if (pc.pcTemplate != null)
            {
                string name = UITextExtractor.CleanText(Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
                parts.Add(name);
            }

            // Health state
            string healthStr = PC.HEALTH_STRINGS[(int)pc.healthState];
            parts.Add(UITextExtractor.CleanText(Language.Localize(healthStr, false, false, string.Empty)));

            // HP
            parts.Add($"HP {Mathf.RoundToInt(pc.curHP)}");

            // Action points if in combat
            if (MonoBehaviourSingleton<CombatManager>.HasInstance() && MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
            {
                if (pc.pcStats != null)
                {
                    parts.Add($"AP {pc.pcStats.GetActionPoints()}");
                }
            }

            ScreenReaderManager.Speak(string.Join(", ", parts.ToArray()), interrupt: true);
        }
    }
}
