File: Core/ExplorationState.cs — IAccessibilityState for overworld exploration; lowest priority (10); handles interactable navigation, party commands, and camera lock.

namespace Wasteland2AccessibilityMod.Core  (line 4)

class ExplorationState : IAccessibilityState  (line 12)
    // Lowest-priority state (10); active only during Gameplay/RandomEncounter, outside combat, menus, frozen input, and cutscenes.

    public string Name { get; }  (line 14)
    public int Priority { get; }  (line 15)
    public bool IsActive { get; }  (line 17)
        // note: gates on GameState.Gameplay|RandomEncounter, !Drama.isConversationOn, !inCombat, !IsAnyMenuActive, !IsInputFrozen.

    public bool HandleInput()  (line 52)
        // note: handles F10 (camera lock), Ctrl+PageUp/Down (category), PageUp/Down (cycle), Backslash (repeat), Equals (direction toggle), K (tile order), Quote (scrap), F1-F7 (party select), G (group), Enter (interact), Space (tactical pause), Backspace (stop), R (radio), I (inventory), Escape (pause menu).
    public void OnActivated()  (line 184)
    public void OnDeactivated()  (line 189)
        // Calls TacticalPauseManager.ForceResumeIfPaused() when leaving exploration context.

    private static void StopPartyMovement()  (line 196)
        // Iterates party navMeshAgents and calls Stop/ResetPath on any that have a path.
    private static bool HandlePartySwitch()  (line 222)
        // Iterates KeyCode.F1–F1+6; delegates to SelectPartyMember(i).
    private static void SelectPartyMember(int index)  (line 236)
        // Mirrors InputManager.OnButtonDown "Select Player" logic: clears selection, calls MakeLeader/AddToSelection, centers camera if already selected.
    private static void InteractWithSelected()  (line 286)
        // Reads NavigationManager.SelectedInteractable; routes to TryUseItemOnObject, Drama.CheckInstigate, or CheckExamineDrama based on nexus components.
    // Checks ItemAcceptingObject components; if matching item in party inventory, sets up UseASIManager and triggers HandleUsableItemClickOnTargetable or PrepareUseItemActions.
    // Returns false (not true) when items are needed but missing, so caller still triggers Drama.CheckInstigate for the game's own feedback text.
    internal static bool TryUseItemOnObject(InteractableNexus nexus, InputManager inputManager, PC pc)  (line 363)
        // note: returns false when required items are absent — announces what's needed then lets Drama.CheckInstigate run for the game's own "you need a shovel" text.
    private static void ToggleGroupMode()  (line 436)
    private static void AnnouncePartyScrap()  (line 455)
