File: States/MapCursorState.cs — grid-based virtual map cursor (priority 30): exploration-mode keyboard navigation, tile announcement, context menus, actions menu, free-aim attack dispatch

namespace Wasteland2AccessibilityMod.States  (line 8)

class MapCursorState : IAccessibilityState  (line 17)
    // Priority 30. Active during exploration (no menus, no combat, no world map, no conversation).
    // Requires a valid CombatAStar grid. Arrow keys move the cursor one cell at a time (cardinal).
    // Announces tile coordinates and all visible, non-gated interactables/mobs on the tile.

    // --- IAccessibilityState interface ---
    public string Name => "MapCursor"  (line 19)
    public int Priority => 30  (line 20)

    public bool IsActive { get; }  (line 124)
        // note: false during WorldMap (WorldMapState owns that), conversations, combat,
        // any active GUIManager menu, or when CombatAStar grid is unavailable.

    public bool HandleInput()  (line 152)
        // note: large dispatch block — priority order:
        //   1. Space → tactical pause toggle
        //   2. browsingPartyInfo mode (Up/Down cycle lines, Escape/Enter close)
        //   3. browsingActions mode (Up/Down cycle, Enter execute, Escape/Tab close)
        //   4. contextMenuActive mode → delegates to HandleContextMenuInput()
        //   5. Normal cursor mode:
        //      Shift+Left/Right → adjust stepSize (1–30, 0.1 s repeat)
        //      Ctrl+Arrow → move until blocked (up to UNTIL_WALL_MAX_TILES=100)
        //      plain Arrow → move stepSize tiles in cardinal direction
        //      Backslash → detailed tile scan
        //      X → examine first examinable on tile
        //      Escape → cancel active attack/useItem ASI
        //      Enter → attack/useItem/useSkill if ASI active, else open PC info or context menu
        //      ] → move party to cursor
        //      Shift+Home → jump cursor to party leader
        //      Shift+End → announce distance/direction to party leader
        //      Home → jump cursor to selected interactable
        //      End → announce distance/direction to selected interactable
        //      K → toggle ObjectNamesFirst announcement order
        //      F → toggle camera-follows-cursor

    public void OnActivated()  (line 482)
        // Logs activation; initializes cursor to party position if not yet done.

    public void OnDeactivated()  (line 492)
        // Clears browsingActions and browsingPartyInfo state; logs deactivation.

    // --- Fields ---

    // Grid state
    private static FieldInfo fullMapField  (line 23)
        // note: cached reflection handle for CombatAStar.fullMap (private instance field)
    private Dictionary<Vector3, CombatAStarNode> fullMap  (line 24)
    private Vector3 cursorGridId  (line 25)
    private Vector3 cursorPosition  (line 26)
    private bool cursorInitialized  (line 27)
    private bool loggedNoParty  (line 28)

    // Movement settings
    private const float MOVE_REPEAT_DELAY = 0.25f  (line 31)
    private float lastMoveTime  (line 32)
    private int stepSize = 1  (line 36)
        // Adjusted with Shift+Left/Right at runtime (1–MAX_STEP_SIZE)
    private const int MIN_STEP_SIZE = 1  (line 37)
    private const int MAX_STEP_SIZE = 30  (line 38)
    private const int UNTIL_WALL_MAX_TILES = 100  (line 39)
        // Maximum tiles traversed per Ctrl+Arrow press
    private float lastStepChangeTime  (line 40)
    private const float STEP_CHANGE_REPEAT_DELAY = 0.1f  (line 41)

    // Grid settings
    private const float GRID_SQUARE_SIZE = 1.6f  (line 44)
    private const float TILE_MATCH_RADIUS = GRID_SQUARE_SIZE * 0.75f  (line 47)
        // Objects within this XZ distance of node center are considered "on" the tile

    // Camera follow
    private bool cameraFollowsCursor = true  (line 50)

    // Context menu state
    private bool contextMenuActive  (line 53)
    private List<ContextMenuOption> contextMenuOptions  (line 54)
    private int contextMenuIndex  (line 55)
    private InteractableNexus contextMenuTarget  (line 56)
    private List<PC> contextMenuPCs  (line 57)
        // PCs stored when tile has stacked party members + interactables
    private System.Action<PC> pendingPCSelectionCallback  (line 60)
        // Callback fired when user picks a PC from multi-PC selection (e.g. fieldMedic target)
    private List<Targetable> contextMenuTargetables  (line 62)
    private System.Action<Targetable> pendingTargetableSelectionCallback  (line 63)

    // Layer masks
    private static int floorLayerMask = -1  (line 66)
        // Lazy-initialized mask for Terrain/Floor/FadedFloor layers (floor level detection)

    // Actions menu (Tab key)
    private bool browsingActions  (line 69)
    private int actionIndex  (line 70)
    private List<ExplorationAction> actionList  (line 71)

    // Party member info browsing
    private bool browsingPartyInfo  (line 74)
    private List<string> partyInfoLines  (line 75)
    private int partyInfoIndex  (line 76)

    // --- Nested types ---

    private struct ContextMenuOption  (line 79)
        public string DisplayName  (line 81)
        public string ASIName  (line 82)
            // null = Poked (normal interact); "move" = move to; "select_N" = drill into Nth interactable;
            // "partyinfo_N" = open Nth PC info; "pcselect_N" = callback for Nth PC;
            // "targetselect_N" = callback for Nth targetable; "useItem_auto" = item-accepting object;
            // "examine" = mod-only examine fallback

    private class ExplorationAction  (line 85)
        public string Label  (line 87)
        public string Status  (line 88)
        public bool IsEnabled  (line 89)
        public System.Action Execute  (line 90)

    // --- Static lookup tables ---

    private static readonly Dictionary<string, string> SKILL_DISPLAY_NAMES  (line 94)
        // note: dispatch table mapping ASI skill names (e.g. "pickLock") to UI strings ("Pick Lock")
        // 13 entries: Poked/Interact, bruteForce, pickLock, alarmDisarm, safecrack,
        // animalWhisperer, demolitions, fieldMedic, outdoorsman, mechanicalRepair,
        // doctor, toasterRepair, computerTech

    private static readonly string[] COVER_DIRECTIONS  (line 112)
        // Indices 0–3 → "north", "east", "south", "west" (maps to CombatAStarNode.cover array)

    private static readonly string[] DIRECTION_NAMES  (line 115)
        // "north", "east", "south", "west" (cardinal direction labels for announcements)

    private static readonly Vector3[] CARDINAL_DIRECTIONS  (line 116)
        // note: hardcoded 4-element direction table:
        // [0]=Vector3.forward (Up arrow/North, +Z), [1]=Vector3.right (Right/East, +X),
        // [2]=Vector3.back (Down/South, -Z), [3]=Vector3.left (Left/West, -X)

    // --- Grid access ---

    private bool TryEnsureGrid()  (line 502)
        // note: always re-fetches via GetFullMap() each call — CombatAStar creates a new
        // dictionary per scene load so cached references go stale. Resets cursor and
        // logs grid XZ bounds when the map reference changes.

    private Dictionary<Vector3, CombatAStarNode> GetFullMap()  (line 533)
        // note: uses reflection (fullMapField) to read CombatAStar.fullMap (private instance field)

    // --- Initialization ---

    private void InitializeToPartyPosition()  (line 558)
        // Snaps cursorGridId/cursorPosition to the party leader's nearest grid node.
        // Falls back to raw grid-coordinate computation if no node is found.

    private CombatAStarNode FindNodeAtPosition(Vector3 worldPos)  (line 593)
        // note: three-stage lookup — (1) direct ID via GetFloorLevel raycast,
        // (2) try all floor levels 0–5 at same XZ, (3) linear scan for nearest XZ node.
        // Logs when falling back to stage 3.

    private int GetFloorLevel(Vector3 worldPos)  (line 638)
        // note: raycasts down through Terrain/Floor/FadedFloor layers; reads tag from
        // hit transform hierarchy: "2nd Floor"→1, …, "6th Floor"→5; default 0.

    // --- Movement ---

    private void MoveInDirection(int directionIndex, int tilesToMove)  (line 668)
        // note: single-step (tilesToMove≤1) moves freely including off-grid tiles.
        // Multi-step walks tile-by-tile and stops at first missing node, calling
        // IdentifyObstruction on the blocked position. Announces tile after move.

    // Looks up a node in fullMap at the given grid ID, checking all floor levels.
    private CombatAStarNode GetNodeAtGridId(Vector3 gridId)  (line 751)
        // note: tries exact ID first, then floors 0–5 at same XZ.

    private CombatAStarNode GetNeighborInDirection(CombatAStarNode node, Vector3 direction)  (line 772)
        // note: checks node.neighbors array and node.linkedNeighbor (door/ladder) for matching XZ.

    // --- Announcements ---

    private void AnnounceCurrentTile(bool detailed, string prefix = null)  (line 800)
        // note: builds announcement from: optional prefix, objects (mobs then interactables),
        // grid coordinates, obstruction (if no node), door/ladder link type, cover (detailed only),
        // distance+direction from party (detailed only). Order governed by ModConfig.ObjectNamesFirst.
        // In free-aim mode appends hit% and crit% to mob names.

    private string GetCoverDescription(CombatAStarNode node)  (line 925)
        // Returns "Cover: north, east, ..." or "No cover" from node.cover[] + COVER_DIRECTIONS.

    // --- Object Finding ---

    // Checks if a world position falls on the same grid tile as the current node.
    private bool IsOnCurrentTile(Vector3 worldPos)  (line 947)
        // note: XZ distance from cursorPosition, threshold = TILE_MATCH_RADIUS.

    // Checks if the target position is on the other side of a wall from the cursor.
    private bool IsBlockedByWall(Vector3 targetPos)  (line 961)
        // note: uses pathfinding grid neighbor connectivity — if object is near tile edge
        // and no walkable neighbor exists in that direction, it's wall-blocked.
        // Floor-embedded objects (Y < cursorNode.Y - 0.5) skip this check.

    private static bool IsDoor(Component c)  (line 1009)
        // note: checks for Door/Door_Swing/Door_Slide/DoorClass children; falls back to
        // NameLooksLikeDoor(name) and soundPackage type name heuristic.

    private static bool NameLooksLikeDoor(string name)  (line 1031)

    private static bool NameLooksLikeBlocker(string name)  (line 1041)
        // True for names containing "Blocker", "Obstacle", or "Vine" (destructible obstacles
        // whose open/closed state is irrelevant).

    private static bool LooksLikeOpenableDoor(InteractableNexus nexus, InteractableObject io)  (line 1049)
        // note: definitive check for real Unity door components, then name-based fallback;
        // explicitly returns false for WeakObstacle types and blockers.

    private static bool ResolveDoorOpen(InteractableNexus nexus, InteractableObject io)  (line 1067)
        // note: checks Door, DoorClass, Door_Slide, Door_Swing components in order;
        // falls back to io.isActive.

    private static string GetInteractableState(InteractableNexus nexus)  (line 1090)
        // Returns state suffix e.g. "open", "closed", "closed, locked", "destroyed", "exploded", or null.
        // "locked" only included if HasBeenExamined() returns true (avoids leaking lock state).

    private static bool HasBeenExamined(InteractableNexus nexus, InteractableObject io)  (line 1123)
        // True if SkillObject_Examine.examined is set (requires successful perception check).

    private static string AppendState(string baseName, InteractableNexus nexus)  (line 1130)
        // Appends GetInteractableState() suffix to a display name, or returns baseName unchanged.

    private List<InteractableNexus> FindInteractablesOnTile()  (line 1136)
        // note: filters InteractableNexus.interactables by isVisible, !isPC, FOW visibility,
        // perception gating, IsOnCurrentTile, and IsBlockedByWall (doors skip wall check).

    private static bool IsMobPerceptionGated(Mob mob)  (line 1168)
        // True if mob has a perception-gated InteractableNexus (e.g. disguised NPC).

    private List<Mob> FindMobsOnTile()  (line 1176)
        // note: searches game.npcs, game.partyFollowers, and game.party separately.
        // NPCs/followers filtered by active, alive, !hidden, !perceptionGated, FOW visibility,
        // IsOnCurrentTile, !IsBlockedByWall. PCs only filtered by active + IsOnCurrentTile.

    // --- Name Extraction ---

    private string GetMobName(Mob mob)  (line 1241)
        // Uses mob.template.displayName (localized) or gameObject.name as fallback.

    private string GetInteractableName(InteractableNexus interactable)  (line 1260)
        // note: priority chain — live mob via drama (skip), dead mob (name + ", dead"),
        // drama.name, SceneLoad component ("Exit"), skobExamine ("Examinable object"),
        // GameObject name. Appends state via AppendState().

    // --- Examine ---

    private void ExamineObjectOnTile()  (line 1316)
        // note: tries Drama.ExamineDrama (dry-run first), then CheckExamineDrama fallback
        // for no-Drama skobExamine objects. Also examines NPCs via Drama.ExamineDrama.

    // --- Context Menu ---

    private void OpenContextMenu()  (line 1396)
        // note: if multiple interactables on tile, shows object selection list first;
        // single interactable goes straight to BuildActionMenu.

    private void OpenTileSelectionMenu(List<PC> pcs, List<InteractableNexus> interactables)  (line 1439)
        // Builds a combined selection menu of party members (partyinfo_N) and interactables (select_N).

    private void BuildActionMenu(InteractableNexus target)  (line 1477)
        // note: reads drama.GetAllowedInteractions() — value 0 = failed/used (skip),
        // -1 = not yet prodded (treated same as 1 because mouse hover never fires).
        // Also adds UseItem option from ItemAcceptingObject scan (goto doneItemCheck exits loop early).
        // Adds Examine option for Drama objects and for no-Drama skobExamine with difficulty None
        // or already perceived. Executes immediately if 0 or 1 option; opens menu at ≥2.

    private bool HandleContextMenuInput()  (line 1638)
        // note: Up/Down cycle options. Enter dispatches via ASIName prefix:
        //   "partyinfo_N" → OpenPartyMemberInfo
        //   "pcselect_N"  → pendingPCSelectionCallback
        //   "targetselect_N" → pendingTargetableSelectionCallback
        //   "select_N"    → drill into Nth interactable via BuildActionMenu
        //   otherwise     → ExecuteInteraction
        // Escape/Backspace close menu.

    private void CloseContextMenu()  (line 1758)
        // Resets all context menu state including pending callbacks.

    private void OpenPCSelectionMenu(List<PC> pcs, string header, System.Action<PC> callback)  (line 1770)
        // Opens a context menu of party members; selection fires callback(pc). Uses "pcselect_N" ASINames.

    private void OpenTargetableSelectionMenu(List<Targetable> targets, List<string> names, string header, System.Action<Targetable> callback)  (line 1795)
        // Opens a context menu of Targetables; selection fires callback(target). Uses "targetselect_N" ASINames.

    private void ExecuteInteraction(InteractableNexus target, string asiName)  (line 1821)
        // note: prods the object (ProdIt) so locked containers report skills correctly.
        // Special-cases: "useItem_auto" → ExplorationState.TryUseItemOnObject,
        // "examine" → Drama.ExamineDrama or CheckExamineDrama,
        // real skill ASI → UseASIManager.SetActiveASIName then Drama.CheckInstigate.
        // Checks bInstigateBlocked before firing.

    // --- Move Party ---

    private void MovePartyToCursor()  (line 1944)
        // note: rebuilds inputManager.activePCs from selectedMobs before calling
        // MoveInFormation (Update is suppressed while MapCursorState is active, so
        // activePCs can be stale/contain destroyed entries).

    // --- Camera ---

    private void SnapCameraToCursor()  (line 1986)
        // Calls cameraController.Snap to cursor world position (non-instant, no zoom reset).

    private void JumpToSelectedInteractable()  (line 1997)
        // note: snaps to object's "natural tile" (raw-rounded from world position), not nearest
        // walkable node, to avoid 1-tile offset for wall/prop objects. Calls LogTileFinderForTarget.

    // Diagnostic: trace why the currently-selected interactable may or may not match the current tile.
    private void LogTileFinderForTarget(InteractableNexus target)  (line 2063)
        // note: logs all FindInteractablesOnTile filter results for a specific target to MelonLogger.

    private void AnnounceDistanceToSelected()  (line 2078)
        // Announces "[name], N tiles [direction]" from cursor to NavigationManager.SelectedInteractable.

    private void JumpToParty()  (line 2110)
        // Re-initializes cursor to party leader position, snaps camera, announces tile.

    private void AnnounceDistanceToParty()  (line 2126)
        // Announces "[leader name], N tiles [direction]" from cursor to party leader.

    // --- Obstruction Detection ---

    // Raycasts/checks what is at a world position where no grid node exists.
    private string IdentifyObstruction(Vector3 worldPos)  (line 2162)
        // note: three-stage raycast — downward from +10, sphere cast from +5, horizontal from cursor.
        // Delegates to DescribeHitObject. Returns "Impassable" if all miss.

    private string DescribeHitObject(RaycastHit hit)  (line 2199)
        // note: categorizes by layer name (Wall/FadedWall, Cover, Terrain) then falls back to
        // GetMeaningfulName. Returns null for perception-gated nexus on the hit object.

    // Walks up the transform hierarchy to find a meaningful name, skipping generic names.
    private string GetMeaningfulName(Transform trans)  (line 2253)
        // note: skips names containing "collider", "mesh", "trigger", "blocker", "default",
        // "cube", "plane", "quad". Returns first non-generic cleaned name up the hierarchy.

    // --- Actions Menu (Tab key) ---

    private void OpenActionsMenu()  (line 2284)
        // Calls BuildExplorationActionList; announces header + first item; sets browsingActions=true.

    private void BuildExplorationActionList()  (line 2306)
        // note: populates actionList in fixed order:
        //   1. All trained skills from UseASIManager.SkillASIs (level > 0) → SetActiveASIName
        //   2. Usable items from pc.inventory.GetUsableItems → SetActiveASIItem + "useItem" ASI
        //   3. Swap weapons (secondary weapon name in label) → OnSwapWeaponsClicked
        //   4. Crouch/Stand (disabled when in cover) → EventInfo_CommandChangeStance
        //   5. Reload or Unjam (ranged weapons only) → EventInfo_CommandReload / EventInfo_CommandUnjam
        //   6. Free Aim: per firing-mode for ranged (Single/Burst/Full Auto → "attack"/"coneattack"),
        //      single entry for melee/thrown/RPG ("attack"/"aoeattack")
        // Each Execute closure captures variables for correct lambda binding.

    private string FormatAction(ExplorationAction action)  (line 2585)
        // Returns "Label, Status[, unavailable], N of M" format for announcement.

    private void AnnounceCurrentAction()  (line 2600)
        // Speaks FormatAction for the currently indexed action via SpeakInterrupt.

    private void ExecuteCurrentAction()  (line 2606)
        // Calls ExitActionsBrowse() first, then action.Execute() in a try/catch.

    private void ExitActionsBrowse()  (line 2630)
        // Clears browsingActions and actionList; announces "Actions closed".

    // --- Use Item on Target ---

    private void UseItemOnTile()  (line 2639)
        // note: priority order — multiple PCs → OpenPCSelectionMenu, single PC → direct,
        // non-PC live mob → direct, interactable with Targetable component → HandleUsableItemClickOnTargetable,
        // interactable with Drama but no Targetable → InputManager.PrepareUseItemActions fallback.

    private void UseSkillOnTile(string activeASI)  (line 2726)
        // note: priority order — multiple PCs → OpenPCSelectionMenu, single PC → direct,
        // non-PC live mob → direct, interactable with Drama → HandleSkillClick.
        // Calls inputManager.HandleSkillClick(transform, doubleClick: false).

    // --- Free Aim Attack ---

    private void AttackMobOnTile()  (line 2788)
        // note: collects mob Targetables first (prefers mobs over destructibles).
        // ≥2 mobs → OpenTargetableSelectionMenu; 1 mob → PerformFreeAimAttack directly.
        // Falls back to TargetableObject (destructibles with curHP > 0) when no mobs present.

    private string ResolveTargetableName(Targetable target)  (line 2852)

    private void PerformFreeAimAttack(Targetable target, string targetName)  (line 2860)
        // note: mirrors AIBehaviour_PC.OnAttack gating:
        //   - melee: checks CanAttack at current position; if fails, calls TryFindAttackDestination
        //     (walk-and-attack feasibility) or reports no-LoS / out-of-range.
        //   - ranged: checks TargetVisible, then range vs (distance - additionalHitRange).
        // Sets inputManager.selectedTargetable, publishes EventInfo_CommandAttack.
        // For shotguns: sets coneAttack=true, aimDirection, coneTargets=[target].
        // Announces "Attacking/Moving to attack [name] with [weapon], N% hit, N% crit".

    // Mirrors AIBehaviour_PC.FindAttackDestination: walks navmesh path corners to check
    // whether vanilla's melee walk-and-attack fallback would fire.
    private static bool TryFindAttackDestination(PC pc, Targetable target)  (line 3031)
        // note: uses pc.navMeshAgent.CalculatePath, samples path corners at 75% weapon range
        // backoff, returns true if any corner yields CanAttack(target, position).

    // --- Party Member Info ---

    private PC FindPCOnTile()  (line 3070)
        // Returns first living PC from FindMobsOnTile (single-result convenience wrapper).

    private List<PC> FindPCsOnTile()  (line 3081)
        // Returns all living PCs from FindMobsOnTile.

    private void OpenPartyMemberInfo(PC pc)  (line 3093)
        // Calls BuildPartyMemberInfo; sets browsingPartyInfo=true; announces header + first line.

    private void BuildPartyMemberInfo(PC pc)  (line 3111)
        // note: populates partyInfoLines with:
        //   - Health: curHP/maxHP (%), healthState if not Healthy
        //   - Level/XP via CharacterAnnouncementHelper.BuildXPAnnouncement
        //   - Weapon name + ammo count (ranged only)
        //   - Status effects via StatusEffectHelper.BuildEffectLine

    private string FormatPartyInfoLine(int index)  (line 3170)
        // Returns "line text, N of M" position format.

    // --- Helpers ---

    private PC GetPartyLeader()  (line 3178)
        // Returns InputManager.GetFirstSelectedPlayer() or game.party[0] as fallback.

    private string GetPCDisplayName(PC pc)  (line 3198)
        // Prefers template.displayName, then pcTemplate.displayName, then pc.name.

    private void SuppressInput()  (line 3208)
        // Sets ShouldSuppressGameInput and ShouldSuppressUINavigation to true.
