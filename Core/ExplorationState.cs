using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Handles keyboard input during exploration mode.
    /// Provides interactable cycling (PageUp/Down), category filtering (Ctrl+PageUp/Down),
    /// announcement repeat (\), direction toggle (=), and scrap query (').
    /// Lowest priority (10) - only handles input when no menus or cursor are active.
    /// </summary>
    public class ExplorationState : IAccessibilityState
    {
        public string Name => "Exploration";
        public int Priority => 10;

        public bool IsActive
        {
            get
            {
                // Must have game instance
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;

                Game game = MonoBehaviourSingleton<Game>.GetInstance();

                // Only active during gameplay or random encounters
                if (game.state != GameState.Gameplay && game.state != GameState.RandomEncounter)
                    return false;

                // Not during conversations or cutscenes
                if (Drama.isConversationOn || Drama.isCutsceneOn) return false;

                // Not during combat
                if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                    MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                    return false;

                // Not when menus are open
                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
                    return false;

                // Not when input is frozen
                if (MonoBehaviourSingleton<InputManager>.HasInstance() &&
                    MonoBehaviourSingleton<InputManager>.GetInstance().IsInputFrozen())
                    return false;

                return true;
            }
        }

        public bool HandleInput()
        {
            // Camera lock toggle (F10) - works in exploration mode
            if (Input.GetKeyDown(KeyCode.F10))
            {
                CameraLock.Toggle();
                return true;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Ctrl+PageDown = next category
            if (ctrl && Input.GetKeyDown(KeyCode.PageDown))
            {
                NavigationManager.NextCategory();
                return true;
            }

            // Ctrl+PageUp = previous category
            if (ctrl && Input.GetKeyDown(KeyCode.PageUp))
            {
                NavigationManager.PreviousCategory();
                return true;
            }

            // PageDown = next interactable
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.CycleNext();
                return true;
            }

            // PageUp = previous interactable
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.CyclePrevious();
                return true;
            }

            // Repeat last announcement (\)
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                NavigationState.lastKeyboardNavigationTime = Time.time;
                NavigationManager.RepeatLastAnnouncement();
                return true;
            }

            // Toggle direction format (=)
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                ModConfig.ToggleClockPositions();
                return true;
            }

            // Announce party scrap (')
            if (Input.GetKeyDown(KeyCode.Quote))
            {
                AnnouncePartyScrap();
                return true;
            }

            // F1-F7: switch party members
            if (HandlePartySwitch())
            {
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // Toggle group mode (G) - rebound from Space
            if (Input.GetKeyDown(KeyCode.G))
            {
                ToggleGroupMode();
                return true;
            }

            // Enter = interact with selected interactable
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InteractWithSelected();
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // Block Space from triggering "Toggle Group Mode" via EventManager
            if (Input.GetKeyDown(KeyCode.Space))
            {
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // R: radio call
            if (Input.GetKeyDown(KeyCode.R))
            {
                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(ObjectPool.Get<EventInfo_RadioAnswer>());
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // I: open inventory
            if (Input.GetKeyDown(KeyCode.I))
            {
                MonoBehaviourSingleton<GUIManager>.GetInstance().ToggleCharacterInfoMenu(true);
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // Escape: open pause menu
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                MonoBehaviourSingleton<GUIManager>.GetInstance().OpenPauseMenu();
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            // No special activation behavior needed
        }

        public void OnDeactivated()
        {
            // No special deactivation behavior needed
        }

        private static bool HandlePartySwitch()
        {
            for (int i = 0; i < 7; i++)
            {
                KeyCode key = KeyCode.F1 + i;
                if (Input.GetKeyDown(key))
                {
                    SelectPartyMember(i);
                    return true;
                }
            }
            return false;
        }

        private static void SelectPartyMember(int index)
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance() ||
                !MonoBehaviourSingleton<InputManager>.HasInstance())
                return;

            var party = MonoBehaviourSingleton<Game>.GetInstance().party;
            if (party == null || party.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No party members");
                return;
            }

            if (index >= party.Count)
            {
                ScreenReaderManager.SpeakInterrupt($"No party member at position {index + 1}, {party.Count} available");
                return;
            }

            PC pc = party[index];
            if (pc == null) return;

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();

            // Mirror the game's native Select Player handling from InputManager.OnButtonDown
            bool wasSelected = pc.isSelected;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift) &&
                !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
            {
                inputManager.ClearSelection();
            }

            PC previousLeader = MonoBehaviourSingleton<Game>.GetInstance().pcLeader;
            pc.MakeLeader();
            inputManager.AddToSelection(pc);
            if (previousLeader != null)
                previousLeader.ShowSelectedFX(previousLeader.isSelected);

            // If already selected, center camera on them
            if (wasSelected && MonoBehaviourSingleton<HUD_Controller>.HasInstance())
            {
                MonoBehaviourSingleton<HUD_Controller>.GetInstance().PartyMemberCenterCamera(pc, true);
            }

            string name = UITextExtractor.CleanText(
                Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt($"{name}, {index + 1} of {party.Count}");
            MelonLogger.Msg($"[ExplorationState] Selected party member {index + 1}: {name}");
        }

        private static void InteractWithSelected()
        {
            try
            {
                InteractableNexus nexus = NavigationManager.SelectedInteractable;
                if (nexus == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No object selected");
                    return;
                }

                if (!MonoBehaviourSingleton<InputManager>.HasInstance() ||
                    !MonoBehaviourSingleton<Game>.HasInstance())
                    return;

                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                PC pc = MonoBehaviourSingleton<Game>.GetInstance().GetFirstSelectedPC();
                if (pc == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No ranger selected");
                    return;
                }

                // Ensure the game's selectedInteractable is set
                inputManager.selectedInteractable = nexus;

                // Follow the same interaction logic as the game's HandleSkillClick:
                // - Objects with Drama: CheckInstigate makes the PC walk there and interact
                //   (this respects distance, skill checks on arrival, etc.)
                // - Objects without Drama but with examine (difficulty None): CheckExamineDrama
                //   (these are simple description objects with no skill requirement)
                // - Objects with examine that have a difficulty > None are revealed passively
                //   by the perception sphere, not by clicking — so no action needed
                if (nexus.drama != null)
                {
                    // Check if this object accepts items (e.g. dirt piles needing shovels).
                    // If a matching item is in party inventory, use it automatically instead
                    // of falling through to examine/poke.
                    if (TryUseItemOnObject(nexus, inputManager, pc))
                        return;

                    MelonLogger.Msg($"[ExplorationState] Interacting with: {nexus.name}");
                    Drama.CheckInstigate(nexus.drama, pc, false);
                    return;
                }

                if (nexus.skobExamine != null &&
                    nexus.skobExamine.difficulty == SkillLevelCategory.None)
                {
                    MelonLogger.Msg($"[ExplorationState] Examining (no difficulty): {nexus.name}");
                    inputManager.CheckExamineDrama(nexus.transform);
                    return;
                }

                ScreenReaderManager.SpeakInterrupt("Cannot interact with this object");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error interacting with selected: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the object has ItemAcceptingObject components and a matching item
        /// exists in party inventory. If so, sets up UseASIManager and triggers the
        /// game's use-item flow (PC walks to object and uses item).
        /// If the object needs items but none are available, announces what's needed.
        /// Returns true if the interaction was handled (item used OR missing-item announced).
        /// </summary>
        internal static bool TryUseItemOnObject(InteractableNexus nexus, InputManager inputManager, PC pc)
        {
            var acceptors = nexus.gameObject.GetComponentsInChildren<ItemAcceptingObject>();
            if (acceptors == null || acceptors.Length == 0)
                return false;

            // Collect needed item names for announcement if none are found
            var neededItems = new System.Collections.Generic.List<string>();

            foreach (var acceptor in acceptors)
            {
                if (!acceptor.enabled || acceptor.desiredItemTemplate == null)
                    continue;

                bool found = false;

                // Search all party members for the matching item
                var game = MonoBehaviourSingleton<Game>.GetInstance();
                for (int i = 0; i < game.party.Count; i++)
                {
                    PC member = game.party[i];
                    if (!member.isConscious) continue;

                    ItemInstance item = member.inventory.inventory.GetInstanceOfTemplate(acceptor.desiredItemTemplate);
                    if (item != null)
                    {
                        MelonLogger.Msg($"[Accessibility] Using item '{item.template.displayName}' from {member.name} on {nexus.name}");
                        ScreenReaderManager.SpeakInterrupt($"Using {item.template.displayName} on {nexus.name}");

                        // Set up the use-item ASI state (this sets ASI name to "useItem")
                        UseASIManager.SetActiveASIItem(item, member);

                        // Trigger the game's item-use-on-targetable flow
                        Targetable targetable = nexus.gameObject.GetComponent<Targetable>();
                        if (targetable != null)
                        {
                            inputManager.HandleUsableItemClickOnTargetable(targetable);
                        }
                        else
                        {
                            // Fallback: use PrepareUseItemActions directly with Drama
                            InputManager.PrepareUseItemActions(nexus.transform, nexus.drama, null, false);
                        }
                        return true;
                    }
                }

                if (!found)
                {
                    string displayName = acceptor.desiredItemTemplate.displayName;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        string cleanName = UITextExtractor.CleanText(displayName);
                        if (!string.IsNullOrEmpty(cleanName))
                            neededItems.Add(cleanName);
                    }
                }
            }

            // No matching items found — announce what's needed, but return false
            // so the caller still triggers Drama.CheckInstigate (which makes the PC
            // walk to the object and triggers the game's own description text like
            // "If only you had a shovel to dig into this pile of dirt.")
            if (neededItems.Count > 0)
            {
                string itemList = string.Join(", ", neededItems.ToArray());
                MelonLogger.Msg($"[Accessibility] Object {nexus.name} requires items: {itemList}");
                ScreenReaderManager.SpeakInterrupt($"Requires {itemList}");
            }

            return false;
        }

        private static void ToggleGroupMode()
        {
            try
            {
                if (!MonoBehaviourSingleton<InputManager>.HasInstance()) return;

                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                inputManager.TogglePartyIsGrouped();

                string mode = inputManager.isPartyGrouped ? "grouped" : "ungrouped";
                MelonLogger.Msg($"Toggle group mode: {mode}");
                ScreenReaderManager.SpeakInterrupt($"Party {mode}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error toggling group mode: {ex.Message}");
            }
        }

        private static void AnnouncePartyScrap()
        {
            try
            {
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

                int scrap = MonoBehaviourSingleton<Game>.GetInstance().partyCurrency;
                string announcement = $"{scrap} scrap";

                MelonLogger.Msg($"Announcing party scrap: {scrap}");
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error announcing scrap: {ex.Message}");
            }
        }
    }
}
