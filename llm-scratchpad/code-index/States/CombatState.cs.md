File: States/CombatState.cs — accessibility state for combat: grid preview cursor, initiative tracker, action menus, target picking, free-aim, item targeting, party-info browser, combat log viewer.

namespace Wasteland2AccessibilityMod.States  (line 8)

class CombatState : IAccessibilityState  (line 16)
    // Top-level combat accessibility state. Priority 45. Active whenever CombatManager.inCombat
    // and no menu/dialog is open over combat. Contains six distinct browsing sub-modes managed by
    // boolean flags; HandleInput dispatches across them in priority order.

    // --- IAccessibilityState interface ---

    public string Name => "Combat"  (line 18)
    public int Priority => 45  (line 19)

    public bool IsActive { get; }  (line 127)
        // Returns true when CombatManager.inCombat and GUIManager reports no active menu.
        // note: intentionally does NOT check Drama.isConversationOn — combat barks set it
        //       true mid-combat; ConversationState (priority 50) handles real conversations above.

    public bool HandleInput()  (line 146)
        // note: large sequential dispatch; processes browsing sub-modes first (each consumes all
        //       input while active), then falls through to cursor movement and hotkeys.
        //       Order: initiative → actions menu → target actions → party info → log →
        //              item-targeting → free-aim → cursor movement / hotkeys.
        // Key bindings handled here:
        //   T              — open/close initiative tracker
        //   Tab            — open/close combat actions menu
        //   PageDown/Up    — cycle combatants (Ctrl+PageDown/Up switches category)
        //   L              — open combat log
        //   Arrow keys     — move cursor (Shift+L/R adjusts step size; Ctrl+Arrow moves until blocked)
        //   ]              — move current actor to cursor position
        //   Enter          — open target actions (hostile) or party info (ally) on cursor tile
        //   \              — detailed tile announcement
        //   Shift+Home     — jump cursor to current actor
        //   Shift+End      — announce distance from cursor to current actor
        //   Home           — jump cursor to selected combatant
        //   End            — announce distance to selected combatant
        //   K              — toggle tile announcement order (coords first vs object names first)
        //   ;              — toggle camera-follow
        //   I              — open inventory (player's turn only)
        //   Escape         — open pause menu

    public void OnActivated()  (line 726)
        // Resets cursor state and calls EnsureCursorReady on entry.

    public void OnDeactivated()  (line 734)
        // Clears all sub-mode state, lists, flags, and reflection-cached field values.

    // --- Reflection cache (static) ---

    private static FieldInfo curActorField  (line 22)
        // CombatManager.curActor (private)
    private static FieldInfo combatMapField  (line 23)
        // CombatAStar.map (private)
    private static FieldInfo fullMapField  (line 24)
        // CombatAStar.fullMap (private)
    private static FieldInfo actQueueField  (line 25)
        // CombatManager.actQueue (private)
        // note: four reflection lookups; each initialised lazily on first use.

    // --- Preview cursor state ---

    private Dictionary<Vector3, CombatAStarNode> combatMap  (line 28)
    private Dictionary<Vector3, CombatAStarNode> fullMap  (line 29)
    private Vector3 cursorGridId  (line 30)
    private Vector3 cursorPosition  (line 31)
    private bool cursorInitialized  (line 32)
    private Mob lastTrackedActor  (line 33)

    // --- Movement settings / constants ---

    private const float MOVE_REPEAT_DELAY = 0.25f  (line 36)
    private float lastMoveTime  (line 37)
    private int stepSize = 1  (line 41)
    private const int MIN_STEP_SIZE = 1  (line 42)
    private const int MAX_STEP_SIZE = 30  (line 43)
    private const int UNTIL_WALL_MAX_TILES = 100  (line 44)
    private float lastStepChangeTime  (line 45)
    private const float STEP_CHANGE_REPEAT_DELAY = 0.1f  (line 46)
    private const float GRID_SQUARE_SIZE = 1.6f  (line 49)
        // Must match CombatAStar.squareSize.
    private const float TILE_MATCH_RADIUS = GRID_SQUARE_SIZE * 0.75f  (line 50)
    private bool cameraFollowsCursor = true  (line 53)

    // --- Combatant cycling ---

    private List<Mob> combatantList  (line 56)
    private int combatantIndex = -1  (line 57)
    private int combatantCategory = 0  (line 58)
        // 0 = All, 1 = Enemies, 2 = Allies
    private static readonly string[] COMBATANT_CATEGORIES  (line 59)
    private static readonly string[] COVER_DIRECTIONS  (line 62)
        // Indices 0-3 map to N/E/S/W (forward/right/back/left per CombatAStarNode.cover array).
    private static readonly Vector3[] CARDINAL_DIRECTIONS  (line 65)
        // Indices 0-3: Up=North(+Z), Right=East(+X), Down=South(-Z), Left=West(-X).

    // --- Initiative browsing state ---

    private bool browsingInitiative  (line 74)
    private int initiativeIndex  (line 75)
    private List<InitiativeEntry> initiativeList  (line 76)

    // --- Nested class ---

    private class InitiativeEntry  (line 78)
        public string Name  (line 80)
        public bool IsHostile  (line 81)
        public bool IsCurrentActor  (line 82)
        public Mob Mob  (line 83)
        public string Details  (line 84)

    // --- Combat Actions Menu state ---

    private bool browsingActions  (line 88)
    private int actionIndex  (line 89)
    private List<CombatAction> actionList  (line 90)

    // --- Target Actions Menu state ---

    private bool browsingTargetActions  (line 93)
    private int targetActionIndex  (line 94)
    private List<CombatAction> targetActionList  (line 95)
    private List<string> targetInfoLines  (line 96)
    private int targetInfoIndex  (line 97)
    private int targetMenuTab  (line 98)
        // 0 = Actions tab, 1 = Info tab
    private Mob targetMob  (line 99)

    // --- Item Targeting Mode ---

    private bool itemTargetingMode  (line 102)
    private ItemInstance_Usable pendingItem  (line 103)
    private PC pendingItemUser  (line 104)

    // --- Free Aim Targeting Mode ---

    private bool freeAimMode  (line 107)
    private PC freeAimUser  (line 108)

    // --- Party Member Info ---

    private bool browsingPartyInfo  (line 111)
    private List<string> partyInfoLines  (line 112)
    private int partyInfoIndex  (line 113)

    // --- Combat Log Viewer ---

    private bool browsingLog  (line 116)
    private int logIndex  (line 117)

    // --- Nested class ---

    private class CombatAction  (line 119)
        public string Label  (line 121)
        public string Status  (line 122)
            // e.g. "3 AP", "weapon jammed"
        public bool IsEnabled  (line 123)
        public System.Action Execute  (line 124)

    // =====================================================================
    // Preview Cursor
    // =====================================================================

    private void EnsureCursorReady()  (line 771)
        // Loads the combat map via reflection and initialises the cursor.
        // When the current actor changes, queues a "X's turn" announcement (non-interrupt).
        // note: called every frame from HandleInput; safe to call repeatedly.

    private bool TryEnsureCombatMap()  (line 796)
        // Initialises combatMapField and fullMapField reflections; reads CombatAStar.map.
        // Returns false if the map is null or empty. Resets cursor if the map reference changed.
        // note: uses BindingFlags.NonPublic | BindingFlags.Instance reflection.

    private void InitializeCursorToMob(Mob mob)  (line 832)
        // Sets cursorGridId/cursorPosition to mob.currentSquare, falling back to FindNearestNode.

    private void JumpToCurrentActor()  (line 852)
        // Moves cursor to the current actor's tile and calls AnnounceTile.

    private void MoveCursor(int directionIndex, int tilesToMove)  (line 868)
        // Advances cursor up to tilesToMove tiles in CARDINAL_DIRECTIONS[directionIndex].
        // Stops at first missing node; classifies the block via GetNodeInFullMap / IdentifyObstruction.
        // For multi-tile moves, prefixes the announcement with actual tile count and block reason.

    private CombatAStarNode GetNodeAtGridId(Vector3 gridId)  (line 931)
        // Looks up a node by exact grid ID; if not found, tries alternate floor levels 0-5 at same X,Z.

    private CombatAStarNode FindNearestNode(Vector3 worldPos)  (line 952)
        // Linear scan of combatMap to find the closest node by XZ Euclidean distance.

    private CombatAStarNode GetNodeInFullMap(Vector3 gridId)  (line 975)
        // Same multi-floor lookup as GetNodeAtGridId but against fullMap.

    private string IdentifyObstruction(Vector3 worldPos)  (line 1000)
        // Three-pass raycast (down, sphere, horizontal) against all obstruction layers.
        // Returns a human-readable string describing what is blocking the tile.
        // note: uses InputManager.layerMask_* constants; last resort returns "Impassable".

    private string DescribeHitObject(RaycastHit hit)  (line 1038)
        // Maps layer name → readable string ("Wall", "Cover object", "Terrain", etc.).

    private string GetMeaningfulName(Transform trans)  (line 1073)
        // Walks transform hierarchy upward, skipping generic names (collider/mesh/trigger/blocker/etc.).

    private void AnnounceTile(bool detailed, string prefix = null)  (line 1102)
        // Builds announcement for the cursor tile: coordinates, occupants, cover, links,
        // distance/direction from current actor. In detailed mode adds AP move cost and weapon range.
        // note: respects ModConfig.ObjectNamesFirst for announcement order.

    private string FormatMobForTile(Mob mob)  (line 1192)
        // Returns name, faction, HP%, and state (unconscious/cover/crouching/hidden) for a mob.

    private string GetCoverDescription(CombatAStarNode node)  (line 1234)
        // Returns "Cover: north, east" etc. from node.cover bool array using COVER_DIRECTIONS.

    private string GetMovementCostInfo(CombatAStarNode targetNode, Mob actor)  (line 1249)
        // Calls CombatAStar.GetPathCost; adds stance cost if crouching; tells whether actor
        // can still attack after moving.  Only runs on player's turn.

    private string GetWeaponRangeInfo(Mob actor, float distance)  (line 1301)
        // Returns "Point blank range", "Optimal range", "Long range", or "Out of range".

    private bool IsOnCurrentTile(Vector3 worldPos)  (line 1328)
        // Returns true if worldPos is within TILE_MATCH_RADIUS of cursorPosition on both X and Z.

    private List<Mob> FindMobsOnTile()  (line 1335)
        // Iterates CombatManager.mobs plus Game.party (for unconscious PCs removed from mobs list).
        // note: includes DEAD mobs (for tile announcements) but excludes inactive non-dead GameObjects.

    // =====================================================================
    // Combatant Cycling (PageUp/PageDown)
    // =====================================================================

    private void BuildCombatantList()  (line 1393)
        // Populates combatantList filtered by combatantCategory (All/Enemies/Allies), sorted by
        // distance from current actor. Adds unconscious PCs (removed from cm.mobs by PC.KnockOut).
        // Excludes: dead, hidden, NPC.waitToJoinCombat, non-visible (fog of war) NPCs.

    private void CycleNextCombatant()  (line 1457)
        // Rebuilds list, wraps index forward, calls AnnounceCombatant.

    private void CyclePreviousCombatant()  (line 1470)
        // Rebuilds list, wraps index backward, calls AnnounceCombatant.

    private void AnnounceCombatant(Mob mob)  (line 1484)
        // Speaks FormatMobForCycle result via SpeakInterrupt.

    private void JumpToCombatant(Mob mob)  (line 1490)
        // Moves cursor to mob's tile and speaks FormatMobForCycle.

    private void JumpToSelectedCombatant()  (line 1510)
        // Guard-checks combatantIndex validity, then calls JumpToCombatant.

    private void AnnounceDistanceToSelectedCombatant()  (line 1528)
        // Guard-checks combatantIndex, then calls AnnounceDistanceToMob.

    private void AnnounceDistanceToCurrentActor()  (line 1546)
        // Calls AnnounceDistanceToMob for GetCurrentActor().

    private void AnnounceDistanceToMob(Mob mob)  (line 1557)
        // Announces "Name, N tiles <direction>" from cursor to mob position.

    private string FormatMobForCycle(Mob mob)  (line 1582)
        // Returns name, faction, HP%, state, distance/direction from current actor,
        // weapon range assessment, and position-in-list.

    private void NextCombatantCategory()  (line 1643)
        // Cycles combatantCategory forward, resets index, announces new category count.

    private void PreviousCombatantCategory()  (line 1652)
        // Cycles combatantCategory backward, resets index, announces new category count.

    private string GetMobName(Mob mob)  (line 1664)
        // Localises mob.template.displayName; falls back to GameObject name with cleanup.

    private string GetTargetableName(Targetable target)  (line 1681)
        // Casts to Mob first; otherwise cleans GameObject name.

    private void SnapCameraToCursor()  (line 1697)
        // Calls Game.cameraController.Snap with instant=false, noSFX=true.

    private void SuppressInput()  (line 1707)
        // Sets InputSuppressor.ShouldSuppressGameInput and ShouldSuppressUINavigation.

    // =====================================================================
    // Shared Helpers
    // =====================================================================

    private Mob GetCurrentActor()  (line 1717)
        // Reads CombatManager.curActor via lazily-initialised reflection (curActorField).

    private string GetDisplayName(string rawName)  (line 1731)
        // Localises and cleans a raw template name string.

    // =====================================================================
    // Combat Actions Menu (Tab key)
    // =====================================================================

    private void OpenActionsMenu()  (line 1741)
        // Calls BuildActionList; sets browsingActions=true; announces AP remaining and count.

    private void BuildActionList()  (line 1764)
        // Populates actionList for the current PC's turn. Fixed order:
        //   Reload (or Unjam) → Swap Weapons → Crouch/Stand → Ambush → Free Aim (if ranged) →
        //   Use Items (one entry per usable item in inventory) → End Turn.
        // Each entry captures its Execute lambda at build time.
        // note: dispatch table pattern — list of CombatAction objects with Execute delegates.

    private string GetAmmoInfo(PC pc)  (line 1961)
        // Returns "current of clipSize" ammo string from weapon's ranged instance.

    private string GetSwapWeaponInfo(PC pc)  (line 1977)
        // Returns "to <secondary weapon name>" or "no secondary weapon".

    private string FormatAction(CombatAction action)  (line 1992)
        // Returns "Label, Status, unavailable?, N of Total".

    private void AnnounceCurrentAction()  (line 2007)
        // Speaks FormatAction for actionList[actionIndex].

    private void ExecuteCurrentAction()  (line 2013)
        // Validates IsEnabled, speaks label, calls ExitActionsBrowse, invokes Execute().

    private void ExitActionsBrowse()  (line 2038)
        // Clears browsingActions and actionList; speaks "Actions closed".

    // =====================================================================
    // Item Targeting
    // =====================================================================

    private void EnterItemTargeting(ItemInstance_Usable item, PC user)  (line 2049)
        // Sets itemTargetingMode=true; announces prompt to move to character and press Enter.

    private void CancelItemTargeting()  (line 2061)
        // Clears mode and pending item; resets UseASIManager state.

    private Mob FindAliveMobOnTile()  (line 2071)
        // Returns first non-DEAD mob from FindMobsOnTile().

    private void ExecuteItemOnTarget(Mob target)  (line 2082)
        // Sets UseASIManager state and calls InputManager.PrepareUseItemActions.

    // =====================================================================
    // Party Member Info
    // =====================================================================

    private PC FindAllyOnTile()  (line 2126)
        // Returns first non-DEAD PC from FindMobsOnTile().

    private void OpenPartyMemberInfo(PC pc)  (line 2137)
        // Calls BuildPartyMemberInfo; sets browsingPartyInfo=true; announces header + first line.

    private void BuildPartyMemberInfo(PC pc)  (line 2155)
        // Populates partyInfoLines: Health, AP, Stance/Cover, Weapon (with ammo), Status Effects.

    private string FormatPartyInfoLine(int index)  (line 2218)
        // Returns "line text, N of Total".

    // =====================================================================
    // Free Aim Targeting
    // =====================================================================

    private void EnterFreeAim(PC user)  (line 2228)
        // Sets freeAimMode=true; announces prompt.

    private void CancelFreeAim()  (line 2235)
        // Clears mode and user; announces "Free aim cancelled".

    private void ExecuteFreeAimShot()  (line 2242)
        // Validates AP, range, and line-of-sight; publishes EventInfo_CommandAttack.
        // If no targetable on tile, fires an intentional miss at cursor world position.
        // note: explicitly validates range/LoS because the game silently rejects bad shots.

    private Targetable FindTargetableOnTile()  (line 2333)
        // Checks alive mobs first, then OverlapSphere for TargetableObject components (destructibles).
        // Returns null if tile is empty.

    // =====================================================================
    // Target Actions Menu (Enter on enemy)
    // =====================================================================

    private Mob FindHostileOnTile()  (line 2366)
        // Returns first alive mob that HatesParty() from FindMobsOnTile().

    private void OpenTargetActionsMenu(Mob target)  (line 2377)
        // Calls BuildTargetActionList and BuildTargetInfoLines; sets browsingTargetActions=true.
        // Announces target name, ammo stats, action count, and first action.

    private string GetAmmoStats(PC pc)  (line 2405)
        // Returns penetration, expansion multiplier, and armor-reduction from current ammo template.

    private void BuildTargetActionList()  (line 2433)
        // Populates targetActionList for the current PC vs targetMob.
        // Melee/AOE: single attack entry. Ranged: one entry per firing mode (iterates
        // firingModeInfos, temporarily setting firingModeIndex to compute correct hit%).
        // Appends precision-strike options via AddPrecisionStrikeOptions.
        // note: dispatch table pattern; all Execute lambdas capture pc and capturedTarget.

    private void AddPrecisionStrikeOptions(PC pc, Mob target, bool isThinking, string weaponName)  (line 2537)
        // Appends head/torso/arms/legs precision strike entries.
        // Temporarily sets pc.specialAttack/useSpecialAttack and firingModeIndex to compute
        // accurate hit%. Restores all modified state before returning.
        // note: uses PcSpecialAttackManager; only valid against NPC targets.

    private void ExecutePrecisionStrike(PC pc, NPC target, PrecisionShotZone zone)  (line 2649)
        // Sets ranged weapon to single-shot mode, assigns pc.specialAttack (NOT useSpecialAttack —
        // that bool is set later by AIAction_Attack.Update), publishes EventInfo_CommandAttack.

    private string GetWeaponDisplayName(ItemInstance_Weapon weapon)  (line 2686)
        // Returns localised display name; falls back to "fists" or "weapon".

    private string GetAttackStatus(PC pc, Mob target, bool isThinking)  (line 2700)
        // Returns "not your action", "weapon jammed", "out of ammo", or "".

    private bool CanPerformAttack(PC pc, Mob target)  (line 2708)
        // Calls pc.CanAttack(target, true, false); returns false on exception.

    private string BuildAttackInfo(PC pc, Mob target, int apCost)  (line 2724)
        // Build attack info string for melee/AOE weapons.
        // Returns "N AP, N% hit, N% crit, N to M damage" (damage via CalculateDamage).

    private string BuildAttackInfoForMode(PC pc, Mob target, int apCost, int ammoCost, int modePenalty)  (line 2770)
        // Build attack info string for a specific ranged firing mode.
        // Same structure as BuildAttackInfo; reads hit% with current firingModeIndex applied.

    private void ExecuteAttack(PC pc, Mob target, int firingModeIndex)  (line 2813)
        // Sets firing mode if ranged; sets inputManager.attackTypeSelected=true;
        // publishes EventInfo_CommandAttack.

    private string FormatTargetAction(CombatAction action)  (line 2845)
        // Returns "Label, Status, unavailable?, N of Total".

    private void AnnounceCurrentTargetAction()  (line 2860)
        // Speaks FormatTargetAction for targetActionList[targetActionIndex].

    private void ExecuteCurrentTargetAction()  (line 2866)
        // Validates IsEnabled, speaks label, calls ExitTargetActionsBrowse, invokes Execute().

    private void ExitTargetActionsBrowse()  (line 2891)
        // Clears all target-actions state fields and speaks "Target menu closed".

    private void BuildTargetInfoLines(Mob target)  (line 2902)
        // Populates targetInfoLines: name+type, HP, armor, evasion, conductive flag,
        // enemy weapon, AP, initiative, cover, distance from current actor, status effects.

    private string FormatInfoLine(int index)  (line 3008)
        // Returns "line text, N of Total".

    // =====================================================================
    // Combat Movement (Right Bracket)
    // =====================================================================

    private void MoveToCursor()  (line 3018)
        // Validates player turn, PC state, and tile availability.
        // Calls CombatAStar.IsNodeOpen (not node.occupant — that field goes stale).
        // Calls CombatAStar.Search with available AP, publishes EventInfo_CommandMove.
        // note: exactly mirrors InputManager's move-command flow.

    // =====================================================================
    // Combat Log Viewer (L key)
    // =====================================================================

    private void OpenCombatLog()  (line 3131)
        // Reads from HUD_Controller_QueueTextDescription_Patch.CombatLog; starts at newest entry.

    private string FormatLogEntry(List<string> log, int index)  (line 3148)
        // Returns "log text, N of Total" where N counts from newest (1 = most recent).

    // =====================================================================
    // Initiative Tracker
    // =====================================================================

    private void OpenInitiativeTracker()  (line 3160)
        // Calls BuildInitiativeList; announces summary and first entry.

    private bool IsMobRevealedToParty(CombatManager cm, Mob target)  (line 3181)
        // Calls CombatManager.IsTargetVisibleToFaction(target, Faction.Ranger); defaults to true on exception.

    private bool IsActivelyInCombat(Mob mob)  (line 3190)
        // Mirrors CombatManager.UpdateDisplayQueue's filter: PCs always pass; NPCs must have
        // engaged enemies, not be waitToJoinCombat, and not be in doNothing AI.

    private void BuildInitiativeList()  (line 3206)
        // Reads actQueue (private field via reflection) for remaining-turns order, then appends
        // cm.mobs not yet in actQueue, then bombs from cm.displayQueue.
        // Filters: dead, unconscious, not-actively-in-combat, unspotted hostiles.
        // note: two-pass approach ensures actQueue order is preserved at the top.

    private string BuildInitiativeMobDetails(Mob mob)  (line 3300)
        // Returns "HP/maxHP, AP remaining (if current actor), state flags".

    private string FormatEntry(InitiativeEntry entry)  (line 3326)
        // Returns "N. Name, current turn?, hostile/friendly, Details".

    private void CycleInitiativeForward()  (line 3346)
        // Wraps index forward; speaks FormatEntry.

    private void CycleInitiativeBackward()  (line 3353)
        // Wraps index backward; speaks FormatEntry.

    private void ExitInitiativeBrowse()  (line 3361)
        // Clears browsingInitiative and initiativeList; speaks "Initiative closed".

// --- Notable patterns ---
// State machine: six boolean browse-mode flags (browsingInitiative, browsingActions,
//   browsingTargetActions, browsingPartyInfo, browsingLog, itemTargetingMode) plus
//   freeAimMode; HandleInput dispatches through them in fixed priority order.
// Dispatch tables: BuildActionList and BuildTargetActionList build List<CombatAction>
//   with Execute delegates (closures capturing local variables for lambda safety).
// Reflection use: four private game fields accessed via BindingFlags.NonPublic|Instance:
//   CombatManager.curActor, CombatManager.actQueue, CombatAStar.map, CombatAStar.fullMap.
// Hardcoded constants: GRID_SQUARE_SIZE=1.6f (must match CombatAStar.squareSize),
//   UNTIL_WALL_MAX_TILES=100, step size range 1-30, MOVE_REPEAT_DELAY=0.25f,
//   precision-strike zone array (Head/Torso/Arms/Legs) with effect strings.
