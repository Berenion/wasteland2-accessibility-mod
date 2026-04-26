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
    /// Grid-based map cursor for exploring the game world. Always active during
    /// exploration when a grid is available. Arrow keys move one grid cell at a
    /// time in cardinal directions (N/E/S/W), announcing tile coordinates and
    /// whatever objects are on the tile.
    /// Priority 30 - below menu states but above exploration cycling.
    /// </summary>
    public class MapCursorState : IAccessibilityState
    {
        public string Name => "MapCursor";
        public int Priority => 30;

        // Grid state
        private static FieldInfo fullMapField;
        private Dictionary<Vector3, CombatAStarNode> fullMap;
        private Vector3 cursorGridId;       // Current grid coordinate (always valid)
        private Vector3 cursorPosition;     // World position of cursor
        private bool cursorInitialized = false;
        private bool loggedNoParty = false;

        // Movement settings
        private const float MOVE_REPEAT_DELAY = 0.25f;
        private float lastMoveTime = 0f;

        // Grid settings
        private const float GRID_SQUARE_SIZE = 1.6f;
        // Half-diagonal of a grid square — objects within this distance of
        // the node center are considered "on" this tile
        private const float TILE_MATCH_RADIUS = GRID_SQUARE_SIZE * 0.75f;

        // Camera follow
        private bool cameraFollowsCursor = true;

        // Context menu state
        private bool contextMenuActive = false;
        private List<ContextMenuOption> contextMenuOptions = new List<ContextMenuOption>();
        private int contextMenuIndex = -1;
        private InteractableNexus contextMenuTarget = null;
        private List<PC> contextMenuPCs = new List<PC>(); // PCs stored when tile has party members and interactables
        // Callback fired when the user picks a PC from a multi-PC selection menu
        // (e.g. choosing which stacked party member to heal with fieldMedic)
        private System.Action<PC> pendingPCSelectionCallback = null;
        // Targets stored when a tile has multiple valid free-aim targets (stacked enemies, etc.)
        private List<Targetable> contextMenuTargetables = new List<Targetable>();
        private System.Action<Targetable> pendingTargetableSelectionCallback = null;

        // Layer masks for floor detection raycasting
        private static int floorLayerMask = -1;

        // --- Actions Menu (Tab key) ---
        private bool browsingActions = false;
        private int actionIndex = 0;
        private List<ExplorationAction> actionList = new List<ExplorationAction>();

        // --- Party Member Info ---
        private bool browsingPartyInfo = false;
        private List<string> partyInfoLines = new List<string>();
        private int partyInfoIndex = 0;

        // Context menu option
        private struct ContextMenuOption
        {
            public string DisplayName;
            public string ASIName; // null = "Poked" (normal interact), "move" = move to
        }

        private class ExplorationAction
        {
            public string Label;
            public string Status;
            public bool IsEnabled;
            public System.Action Execute;
        }

        // Skill ASI to display name mapping
        private static readonly Dictionary<string, string> SKILL_DISPLAY_NAMES = new Dictionary<string, string>
        {
            { "Poked", "Interact" },
            { "bruteForce", "Brute Force" },
            { "pickLock", "Pick Lock" },
            { "alarmDisarm", "Alarm Disarm" },
            { "safecrack", "Safecrack" },
            { "animalWhisperer", "Animal Whisperer" },
            { "demolitions", "Demolitions" },
            { "fieldMedic", "Field Medic" },
            { "outdoorsman", "Outdoorsman" },
            { "mechanicalRepair", "Mechanical Repair" },
            { "doctor", "Doctor" },
            { "toasterRepair", "Toaster Repair" },
            { "computerTech", "Computer Tech" },
        };

        // Cover direction names (indices 0-3: forward/right/back/left = N/E/S/W)
        private static readonly string[] COVER_DIRECTIONS = { "north", "east", "south", "west" };

        // Direction names for arrow keys
        private static readonly string[] DIRECTION_NAMES = { "north", "east", "south", "west" };
        private static readonly Vector3[] CARDINAL_DIRECTIONS =
        {
            Vector3.forward,  // Up arrow = North (+Z)
            Vector3.right,    // Right arrow = East (+X)
            Vector3.back,     // Down arrow = South (-Z)
            Vector3.left      // Left arrow = West (-X)
        };

        public bool IsActive
        {
            get
            {
                // Only active during gameplay, not in menus
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;
                if (MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive()) return false;

                // Not on the world map (WorldMapState handles its own cursor)
                if (MonoBehaviourSingleton<Game>.HasInstance() &&
                    MonoBehaviourSingleton<Game>.GetInstance().state == GameState.WorldMap)
                    return false;

                // Not during conversations
                if (Drama.isConversationOn) return false;

                // Not in combat (combat has its own systems)
                if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                    MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                {
                    return false;
                }

                // Must have a grid available
                return TryEnsureGrid();
            }
        }

        public bool HandleInput()
        {
            // Tactical pause toggle (Space) - handle before anything else
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TacticalPauseManager.TogglePause();
                return true;
            }

            if (!cursorInitialized)
            {
                InitializeToPartyPosition();
                if (!cursorInitialized) return false;
            }

            // --- Party info browsing ---
            if (browsingPartyInfo)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    if (partyInfoLines.Count > 0)
                    {
                        partyInfoIndex = (partyInfoIndex + 1) % partyInfoLines.Count;
                        ScreenReaderManager.SpeakInterrupt(FormatPartyInfoLine(partyInfoIndex));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    if (partyInfoLines.Count > 0)
                    {
                        partyInfoIndex--;
                        if (partyInfoIndex < 0) partyInfoIndex = partyInfoLines.Count - 1;
                        ScreenReaderManager.SpeakInterrupt(FormatPartyInfoLine(partyInfoIndex));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)
                    || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    browsingPartyInfo = false;
                    partyInfoLines.Clear();
                    ScreenReaderManager.SpeakInterrupt("Info closed");
                    return true;
                }

                return true;
            }

            // --- Actions menu browsing (Tab) ---
            if (browsingActions)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    actionIndex = (actionIndex + 1) % actionList.Count;
                    AnnounceCurrentAction();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    actionIndex--;
                    if (actionIndex < 0) actionIndex = actionList.Count - 1;
                    AnnounceCurrentAction();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ExecuteCurrentAction();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
                {
                    ExitActionsBrowse();
                    return true;
                }

                return true;
            }

            // --- Context menu mode ---
            if (contextMenuActive)
            {
                return HandleContextMenuInput();
            }

            // --- Normal cursor mode ---
            // Suppress game input and button events so mapped keys (X=Swap Weapons,
            // Tab=Select Next Mob, etc.) don't fire their game actions
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            // Tab key: open actions menu (skills + items)
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                OpenActionsMenu();
                return true;
            }

            // Arrow key movement - grid-aligned cardinal directions
            // Use unscaledTime so cursor movement works during tactical pause (timeScale=0)
            float currentTime = Time.unscaledTime;
            bool canMove = (currentTime - lastMoveTime) >= MOVE_REPEAT_DELAY;

            if (canMove)
            {
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    MoveInDirection(0); // North
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    MoveInDirection(1); // East
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    MoveInDirection(2); // South
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    MoveInDirection(3); // West
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
            }

            // Backslash for detailed scan of current tile
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                AnnounceCurrentTile(detailed: true);
                return true;
            }

            // X key: examine the first examinable object on the tile
            if (Input.GetKeyDown(KeyCode.X))
            {
                ExamineObjectOnTile();
                return true;
            }

            // Escape: cancel active ASI (free aim or use item)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                string currentASI = UseASIManager.GetActiveASIName();
                if (currentASI == "attack" || currentASI == "aoeattack" || currentASI == "coneattack")
                {
                    UseASIManager.SetActiveASIName(null);
                    ScreenReaderManager.SpeakInterrupt("Free aim cancelled");
                    return true;
                }
                if (currentASI == "useItem")
                {
                    UseASIManager.SetActiveASIName(null);
                    ScreenReaderManager.SpeakInterrupt("Item use cancelled");
                    return true;
                }
            }

            // Enter: if attack ASI is active, attack mob on tile; if useItem ASI is active, use item on target; otherwise normal interaction
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                string activeASI = UseASIManager.GetActiveASIName();
                if (activeASI == "attack" || activeASI == "aoeattack" || activeASI == "coneattack")
                {
                    AttackMobOnTile();
                    return true;
                }

                // useItem ASI active (e.g. Shovel selected from Tab menu) — use item on target
                if (activeASI == "useItem" && UseASIManager.GetActiveASIItem() != null
                    && UseASIManager.GetActiveASIItem() is ItemInstance_Usable)
                {
                    UseItemOnTile();
                    return true;
                }

                // Skill ASI active (e.g. doctor, fieldMedic from Tab menu) — apply skill to target
                if (!string.IsNullOrEmpty(activeASI) && UseASIManager.IsSkillASI(activeASI))
                {
                    UseSkillOnTile(activeASI);
                    return true;
                }

                var pcsOnTile = FindPCsOnTile();
                if (pcsOnTile.Count > 0)
                {
                    var interactables = FindInteractablesOnTile();
                    if (pcsOnTile.Count > 1 || interactables.Count > 0)
                    {
                        // Multiple PCs, or PC(s) + interactable(s) — present a selection list
                        OpenTileSelectionMenu(pcsOnTile, interactables);
                        return true;
                    }
                    // Single PC, no interactables — open info directly
                    OpenPartyMemberInfo(pcsOnTile[0]);
                    SuppressInput();
                    return true;
                }

                OpenContextMenu();
                return true;
            }

            // ] to move party to cursor position
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                MovePartyToCursor();
                return true;
            }

            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+Home to jump cursor to party leader
            if (shiftHeld && Input.GetKeyDown(KeyCode.Home))
            {
                JumpToParty();
                return true;
            }

            // Shift+End to announce distance and direction from cursor to party leader
            if (shiftHeld && Input.GetKeyDown(KeyCode.End))
            {
                AnnounceDistanceToParty();
                return true;
            }

            // Home to jump cursor to selected interactable (from PageUp/Down cycling)
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpToSelectedInteractable();
                return true;
            }

            // End to announce distance and direction to selected interactable
            if (Input.GetKeyDown(KeyCode.End))
            {
                AnnounceDistanceToSelected();
                return true;
            }

            // K: toggle tile announcement order (coordinates first vs object names first)
            if (Input.GetKeyDown(KeyCode.K))
            {
                ModConfig.ToggleObjectNamesFirst();
                return true;
            }

            // F to toggle camera follow
            if (Input.GetKeyDown(KeyCode.F))
            {
                cameraFollowsCursor = !cameraFollowsCursor;
                string status = cameraFollowsCursor ? "Camera follows cursor" : "Camera stationary";
                ScreenReaderManager.SpeakInterrupt(status);
                return true;
            }

            // Suppress arrow keys even if not moving yet (repeat delay)
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                SuppressInput();
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[MapCursorState] Activated");
            if (!cursorInitialized)
            {
                InitializeToPartyPosition();
            }
        }

        public void OnDeactivated()
        {
            browsingActions = false;
            actionList.Clear();
            browsingPartyInfo = false;
            partyInfoLines.Clear();
            MelonLogger.Msg("[MapCursorState] Deactivated");
        }

        // --- Grid Access ---

        private bool TryEnsureGrid()
        {
            // Always re-fetch fullMap from the singleton — CombatAStar.SceneStart()
            // creates a new dictionary each scene load, so cached references go stale.
            var freshMap = GetFullMap();
            if (freshMap == null || freshMap.Count == 0)
                return false;

            if (freshMap != fullMap)
            {
                // Map changed (new scene) — reset cursor
                fullMap = freshMap;
                cursorInitialized = false;

                // Log grid stats
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var kvp in fullMap)
                {
                    if (kvp.Key.x < minX) minX = kvp.Key.x;
                    if (kvp.Key.x > maxX) maxX = kvp.Key.x;
                    if (kvp.Key.z < minZ) minZ = kvp.Key.z;
                    if (kvp.Key.z > maxZ) maxZ = kvp.Key.z;
                }
                MelonLogger.Msg($"[MapCursorState] Grid loaded: {fullMap.Count} nodes, " +
                    $"X range [{minX}-{maxX}], Z range [{minZ}-{maxZ}]");
            }

            return true;
        }

        private Dictionary<Vector3, CombatAStarNode> GetFullMap()
        {
            if (!MonoBehaviourSingleton<CombatAStar>.HasInstance())
                return null;

            var combatAStar = MonoBehaviourSingleton<CombatAStar>.GetInstance();
            if (combatAStar == null) return null;

            if (fullMapField == null)
            {
                fullMapField = typeof(CombatAStar).GetField("fullMap",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (fullMapField == null)
                {
                    MelonLogger.Error("[MapCursorState] Could not find fullMap field via reflection");
                    return null;
                }
            }

            return fullMapField.GetValue(combatAStar) as Dictionary<Vector3, CombatAStarNode>;
        }

        // --- Initialization ---

        private void InitializeToPartyPosition()
        {
            if (!TryEnsureGrid()) return;

            PC pc = GetPartyLeader();
            if (pc == null)
            {
                if (!loggedNoParty)
                {
                    MelonLogger.Msg("[MapCursorState] No party leader found");
                    loggedNoParty = true;
                }
                return;
            }
            loggedNoParty = false;

            Vector3 worldPos = pc.transform.position;
            CombatAStarNode node = FindNodeAtPosition(worldPos);
            if (node != null)
            {
                cursorGridId = node.id;
                cursorPosition = node.position;
            }
            else
            {
                // No node found — compute grid ID from world position directly
                int gridX = Mathf.RoundToInt(worldPos.x / GRID_SQUARE_SIZE);
                int gridZ = Mathf.RoundToInt(worldPos.z / GRID_SQUARE_SIZE);
                cursorGridId = new Vector3(gridX, 0, gridZ);
                cursorPosition = new Vector3(gridX * GRID_SQUARE_SIZE, worldPos.y, gridZ * GRID_SQUARE_SIZE);
            }
            cursorInitialized = true;
            MelonLogger.Msg($"[MapCursorState] Party at world ({worldPos.x:F1}, {worldPos.y:F1}, {worldPos.z:F1}) -> grid {cursorGridId}");
        }

        private CombatAStarNode FindNodeAtPosition(Vector3 worldPos)
        {
            if (fullMap == null) return null;

            // Try direct ID lookup first (works when grid origin aligns with world origin)
            int floor = GetFloorLevel(worldPos);
            int gridX = Mathf.RoundToInt(worldPos.x / GRID_SQUARE_SIZE);
            int gridZ = Mathf.RoundToInt(worldPos.z / GRID_SQUARE_SIZE);

            // Try multiple floor levels since GetFloorLevel raycast may fail
            for (int f = 0; f <= 5; f++)
            {
                Vector3 nodeId = new Vector3(gridX, f, gridZ);
                CombatAStarNode node;
                if (fullMap.TryGetValue(nodeId, out node))
                    return node;
            }

            // Fallback: find nearest node by XZ distance (ignore Y to avoid
            // floor height differences skewing the result)
            float bestDist = float.MaxValue;
            CombatAStarNode bestNode = null;
            foreach (var kvp in fullMap)
            {
                float dx = worldPos.x - kvp.Value.position.x;
                float dz = worldPos.z - kvp.Value.position.z;
                float dist = dx * dx + dz * dz; // squared XZ distance
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNode = kvp.Value;
                }
            }

            if (bestNode != null)
            {
                MelonLogger.Msg($"[MapCursorState] ID lookup failed for ({gridX},{floor},{gridZ}), " +
                    $"nearest node by XZ: {bestNode.id} at world ({bestNode.position.x:F1}, {bestNode.position.y:F1}, {bestNode.position.z:F1}), " +
                    $"dist={Mathf.Sqrt(bestDist):F1}");
            }

            return bestNode;
        }

        private int GetFloorLevel(Vector3 worldPos)
        {
            if (floorLayerMask < 0)
            {
                floorLayerMask = (1 << LayerMask.NameToLayer("Terrain"))
                               | (1 << LayerMask.NameToLayer("Floor"))
                               | (1 << LayerMask.NameToLayer("FadedFloor"));
            }

            RaycastHit hit;
            if (Physics.Raycast(worldPos + Vector3.up * 2f, Vector3.down, out hit, 5f, floorLayerMask))
            {
                Transform trans = hit.transform;
                while (trans != null)
                {
                    string tag = trans.tag;
                    if (tag == "2nd Floor") return 1;
                    if (tag == "3rd Floor") return 2;
                    if (tag == "4th Floor") return 3;
                    if (tag == "5th Floor") return 4;
                    if (tag == "6th Floor") return 5;
                    trans = trans.parent;
                }
            }

            return 0;
        }

        // --- Movement ---

        private void MoveInDirection(int directionIndex)
        {
            Vector3 direction = CARDINAL_DIRECTIONS[directionIndex];

            // Always move one grid step in the direction
            cursorGridId = new Vector3(
                cursorGridId.x + direction.x,
                cursorGridId.y,
                cursorGridId.z + direction.z);

            // Update world position — use node position if one exists, otherwise compute
            CombatAStarNode node = GetNodeAtGridId(cursorGridId);
            if (node != null)
            {
                cursorPosition = node.position;
            }
            else
            {
                cursorPosition = new Vector3(
                    cursorGridId.x * GRID_SQUARE_SIZE,
                    cursorPosition.y,
                    cursorGridId.z * GRID_SQUARE_SIZE);
            }

            if (cameraFollowsCursor) SnapCameraToCursor();
            AnnounceCurrentTile(detailed: false);
        }

        /// <summary>
        /// Looks up a node in fullMap at the given grid ID, checking all floor levels.
        /// </summary>
        private CombatAStarNode GetNodeAtGridId(Vector3 gridId)
        {
            if (fullMap == null) return null;

            // Try exact ID first
            CombatAStarNode node;
            if (fullMap.TryGetValue(gridId, out node))
                return node;

            // Try other floor levels at same X,Z
            for (int f = 0; f <= 5; f++)
            {
                if (f == (int)gridId.y) continue;
                Vector3 floorId = new Vector3(gridId.x, f, gridId.z);
                if (fullMap.TryGetValue(floorId, out node))
                    return node;
            }

            return null;
        }

        private CombatAStarNode GetNeighborInDirection(CombatAStarNode node, Vector3 direction)
        {
            if (node == null || node.neighbors == null) return null;

            float targetX = node.id.x + direction.x;
            float targetZ = node.id.z + direction.z;

            // Check regular neighbors
            for (int i = 0; i < node.neighbors.Length; i++)
            {
                var n = node.neighbors[i];
                if (n != null && n.id.x == targetX && n.id.z == targetZ)
                    return n;
            }

            // Check linked neighbor (door/ladder)
            if (node.linkedNeighbor != null &&
                node.linkedNeighbor.id.x == targetX &&
                node.linkedNeighbor.id.z == targetZ)
            {
                return node.linkedNeighbor;
            }

            return null;
        }

        // --- Announcements ---

        private void AnnounceCurrentTile(bool detailed, string prefix = null)
        {
            CombatAStarNode node = GetNodeAtGridId(cursorGridId);

            List<string> parts = new List<string>();

            if (!string.IsNullOrEmpty(prefix))
            {
                parts.Add(prefix);
            }

            // Grid coordinates
            string coords = ((int)cursorGridId.x) + ", " + ((int)cursorGridId.z);
            if (cursorGridId.y > 0)
            {
                coords += ", floor " + ((int)cursorGridId.y + 1);
            }

            // Objects on this tile
            var interactables = FindInteractablesOnTile();
            var mobs = FindMobsOnTile();
            var objectParts = new List<string>();

            // Check if we're in free aim mode for hit chance announcements
            string activeASIForTile = UseASIManager.GetActiveASIName();
            bool inFreeAim = activeASIForTile == "attack" || activeASIForTile == "aoeattack" || activeASIForTile == "coneattack";
            PC aimingPC = inFreeAim ? GetPartyLeader() : null;

            foreach (var mob in mobs)
            {
                string mobName = GetMobName(mob);
                if (!string.IsNullOrEmpty(mobName))
                {
                    // Add hit/crit chance when in free aim mode and mob is a valid target
                    if (inFreeAim && aimingPC != null && !(mob is PC) && mob.mobState != Mob.MobState.DEAD)
                    {
                        try
                        {
                            int hitChance = Mathf.Clamp(aimingPC.GetChanceToHit(mob, false), 0, 100);
                            int critChance = Mathf.Clamp(aimingPC.GetChanceToCriticalHit(mob), 0, 100);
                            mobName += ", " + hitChance + "% hit";
                            if (critChance > 0)
                                mobName += ", " + critChance + "% crit";
                        }
                        catch (Exception) { }
                    }
                    objectParts.Add(mobName);
                }
            }

            foreach (var interactable in interactables)
            {
                string name = GetInteractableName(interactable);
                if (!string.IsNullOrEmpty(name))
                {
                    objectParts.Add(name);
                }
            }

            // Identify obstruction if no grid node exists
            string obstruction = null;
            if (node == null)
            {
                obstruction = IdentifyObstruction(cursorPosition);
            }

            // Add coords, objects, and terrain in configured order
            if (ModConfig.ObjectNamesFirst)
            {
                // In object-names-first mode, terrain/obstruction also goes before coords
                if (!string.IsNullOrEmpty(obstruction))
                    parts.Add(obstruction);
                if (objectParts.Count > 0)
                    parts.AddRange(objectParts);
                parts.Add(coords);
            }
            else
            {
                parts.Add(coords);
                parts.AddRange(objectParts);
                if (!string.IsNullOrEmpty(obstruction))
                    parts.Add(obstruction);
            }

            if (node != null)
            {
                // Linked node info (doors/ladders)
                if (node.linkedNeighbor != null)
                {
                    string linkType = node.linkedNodeType == CombatAStarNode.LinkedNodeType.Door
                        ? "Door" : "Ladder";
                    parts.Add(linkType);
                }

                if (detailed)
                {
                    // Cover info
                    string cover = GetCoverDescription(node);
                    if (!string.IsNullOrEmpty(cover))
                        parts.Add(cover);
                }
            }

            if (detailed)
            {
                // Distance from party in tiles
                PC pc = GetPartyLeader();
                if (pc != null)
                {
                    CombatAStarNode partyNode = FindNodeAtPosition(pc.transform.position);
                    if (partyNode != null)
                    {
                        int tileDistX = Mathf.Abs((int)cursorGridId.x - (int)partyNode.id.x);
                        int tileDistZ = Mathf.Abs((int)cursorGridId.z - (int)partyNode.id.z);
                        int tileDist = Mathf.Max(tileDistX, tileDistZ);
                        string direction = DirectionHelper.GetDirectionDescription(pc.transform.position, cursorPosition);
                        parts.Add(tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction + " from party");
                    }
                }
            }

            string announcement = string.Join(", ", parts.ToArray());
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private string GetCoverDescription(CombatAStarNode node)
        {
            if (node == null || node.cover == null) return "No cover info";

            List<string> coverDirs = new List<string>();
            for (int i = 0; i < node.cover.Length && i < COVER_DIRECTIONS.Length; i++)
            {
                if (node.cover[i])
                    coverDirs.Add(COVER_DIRECTIONS[i]);
            }

            if (coverDirs.Count == 0) return "No cover";
            return "Cover: " + string.Join(", ", coverDirs.ToArray());
        }

        // --- Object Finding ---

        /// <summary>
        /// Checks if a world position falls on the same grid tile as the current node.
        /// Uses distance from the node's actual world position rather than computing
        /// grid IDs, which avoids issues with grid origin offsets.
        /// </summary>
        private bool IsOnCurrentTile(Vector3 worldPos)
        {
            float dx = Mathf.Abs(worldPos.x - cursorPosition.x);
            float dz = Mathf.Abs(worldPos.z - cursorPosition.z);
            // Use TILE_MATCH_RADIUS (75% of grid size) to catch objects that
            // sit near tile boundaries — objects aren't perfectly grid-aligned
            return dx <= TILE_MATCH_RADIUS && dz <= TILE_MATCH_RADIUS;
        }

        /// <summary>
        /// Checks if the target position is on the other side of a wall from the cursor,
        /// using the pathfinding grid's neighbor connectivity. If the object is near the
        /// tile edge and there's no walkable neighbor in that direction, a wall blocks it.
        /// </summary>
        private bool IsBlockedByWall(Vector3 targetPos)
        {
            CombatAStarNode cursorNode = GetNodeAtGridId(cursorGridId);
            if (cursorNode == null) return false;

            // Floor-embedded objects (hatches, grates, trapdoors) sit below the
            // walkable node's Y. A wall only separates things at floor height —
            // something below the floor can't be "behind a wall", so skip the
            // neighbor heuristic for those.
            if (targetPos.y < cursorNode.position.y - 0.5f) return false;

            float dx = targetPos.x - cursorPosition.x;
            float dz = targetPos.z - cursorPosition.z;

            // Threshold: if the object is beyond this distance from tile center
            // in a cardinal direction, check for a walkable neighbor that way.
            // Objects near tile edges in walled-off directions get rejected.
            float edgeThreshold = GRID_SQUARE_SIZE * 0.4f;

            bool blocked = false;

            if (dx > edgeThreshold)
            {
                // Object extends east — check for east neighbor
                if (GetNeighborInDirection(cursorNode, Vector3.right) == null)
                    blocked = true;
            }
            else if (dx < -edgeThreshold)
            {
                if (GetNeighborInDirection(cursorNode, Vector3.left) == null)
                    blocked = true;
            }

            if (dz > edgeThreshold)
            {
                // Object extends north — check for north neighbor
                if (GetNeighborInDirection(cursorNode, Vector3.forward) == null)
                    blocked = true;
            }
            else if (dz < -edgeThreshold)
            {
                if (GetNeighborInDirection(cursorNode, Vector3.back) == null)
                    blocked = true;
            }

            return blocked;
        }

        private static bool IsDoor(Component c)
        {
            if (c == null) return false;

            if (c.GetComponentInChildren<Door>() != null
             || c.GetComponentInChildren<Door_Swing>() != null
             || c.GetComponentInChildren<Door_Slide>() != null
             || c.GetComponentInChildren<DoorClass>() != null)
                return true;

            // Many "Simple_*_Door" objects are generic InteractableObjects whose
            // door-ness is encoded only in their GameObject name and SoundPackage
            // class (e.g. SP_Simple_Double_Door). Fall back to those signals.
            if (NameLooksLikeDoor(c.name)) return true;

            var io = c.GetComponent<InteractableObject>();
            if (io != null && io.soundPackage != null && NameLooksLikeDoor(io.soundPackage.GetType().Name))
                return true;

            return false;
        }

        private static bool NameLooksLikeDoor(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Gate", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<InteractableNexus> FindInteractablesOnTile()
        {
            List<InteractableNexus> onTile = new List<InteractableNexus>();
            FOWHelper.UpdateActivationTracking();

            foreach (var interactable in InteractableNexus.interactables)
            {
                if (interactable == null) continue;
                if (!interactable.isVisible) continue;
                if (interactable.isPC) continue;
                if (!FOWHelper.IsVisibleThroughFOW(interactable.transform.position)) continue;
                if (FOWHelper.IsPerceptionGated(interactable)) continue;

                if (IsOnCurrentTile(interactable.transform.position))
                {
                    // Doors occupy wall positions — the absent walkable neighbor
                    // that IsBlockedByWall keys on is usually the door itself
                    // sealing the passage. Skip the wall check for doors so
                    // they remain discoverable after a jump-to-interactable.
                    if (!IsDoor(interactable) && IsBlockedByWall(interactable.transform.position)) continue;
                    onTile.Add(interactable);
                }
            }

            return onTile;
        }

        private List<Mob> FindMobsOnTile()
        {
            List<Mob> onTile = new List<Mob>();

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var game = MonoBehaviourSingleton<Game>.GetInstance();

                if (game.npcs != null)
                {
                    foreach (var npc in game.npcs)
                    {
                        if (npc == null || npc.gameObject == null) continue;
                        if (!npc.gameObject.activeInHierarchy) continue;
                        if (npc.mobState == Mob.MobState.DEAD) continue;
                        if (npc.isHidden) continue;
                        if (!FOWHelper.IsVisibleThroughFOW(npc.transform.position)) continue;

                        if (IsOnCurrentTile(npc.transform.position))
                        {
                            if (IsBlockedByWall(npc.transform.position)) continue;
                            onTile.Add(npc);
                        }
                    }
                }

                if (game.partyFollowers != null)
                {
                    foreach (var follower in game.partyFollowers)
                    {
                        if (follower == null || follower.gameObject == null) continue;
                        if (!follower.gameObject.activeInHierarchy) continue;
                        if (follower.isHidden) continue;
                        if (!FOWHelper.IsVisibleThroughFOW(follower.transform.position)) continue;

                        if (IsOnCurrentTile(follower.transform.position))
                        {
                            if (IsBlockedByWall(follower.transform.position)) continue;
                            onTile.Add(follower);
                        }
                    }
                }

                if (game.party != null)
                {
                    foreach (var pc in game.party)
                    {
                        if (pc == null || pc.gameObject == null) continue;
                        if (!pc.gameObject.activeInHierarchy) continue;

                        if (IsOnCurrentTile(pc.transform.position))
                        {
                            onTile.Add(pc);
                        }
                    }
                }
            }

            return onTile;
        }

        // --- Name Extraction ---

        private string GetMobName(Mob mob)
        {
            if (mob == null) return null;

            if (mob.template != null && !string.IsNullOrEmpty(mob.template.displayName))
            {
                return UITextExtractor.CleanText(
                    Language.Localize(mob.template.displayName, false, false, string.Empty));
            }

            string goName = mob.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
            {
                return goName.Replace("_", " ").Replace("(Clone)", "").Trim();
            }

            return "Unknown creature";
        }

        private string GetInteractableName(InteractableNexus interactable)
        {
            if (interactable == null) return "Unknown";

            // Try to get name from Drama
            if (interactable.drama != null)
            {
                var mob = interactable.drama.GetMob();
                if (mob != null)
                {
                    // Dead bodies are lootable via the nexus path. Live mobs come
                    // through FindMobsOnTile, so skip them here to avoid duplication.
                    if (mob is PC || mob is NPC)
                    {
                        if (mob.isDead)
                        {
                            string deadName = GetMobName(mob) ?? "corpse";
                            return deadName + ", dead";
                        }
                        return null;
                    }
                }

                string dramaName = interactable.drama.name;
                if (!string.IsNullOrEmpty(dramaName))
                {
                    return UITextExtractor.CleanText(dramaName);
                }
            }

            // Try SceneLoad (exits/doors)
            var sceneLoad = interactable.GetComponent<SceneLoad>();
            if (sceneLoad != null)
            {
                return "Exit";
            }

            // Try SkillObject_Examine
            if (interactable.skobExamine != null)
            {
                return "Examinable object";
            }

            // Fallback to GameObject name
            string goName = interactable.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
            {
                goName = goName.Replace("_", " ").Replace("(Clone)", "").Trim();
                return goName;
            }

            return "Object";
        }

        // --- Examine ---

        private void ExamineObjectOnTile()
        {
            var interactables = FindInteractablesOnTile();
            var mobs = FindMobsOnTile();

            // Check interactables for examinable objects
            foreach (var nexus in interactables)
            {
                if (nexus == null) continue;

                // Try Drama.ExamineDrama for objects with SkillObject_Examine
                if (nexus.drama != null)
                {
                    var skobEx = nexus.drama.GetComponent<SkillObject_Examine>();
                    if (skobEx != null && !skobEx.hidden)
                    {
                        PC pc = GetPartyLeader();
                        if (pc == null) continue;

                        string name = GetInteractableName(nexus) ?? "Object";

                        // Check if ExamineDrama would succeed (dry run) before committing
                        if (Drama.ExamineDrama(nexus.drama, pc, dontExecute: true))
                        {
                            MelonLogger.Msg($"[MapCursorState] Examining: {name}");
                            ScreenReaderManager.SpeakInterrupt("Examining " + name);
                            Drama.ExamineDrama(nexus.drama, pc, dontExecute: false);
                            return;
                        }
                    }
                }

                // Fallback for no-Drama examine objects
                if (nexus.skobExamine != null && !nexus.skobExamine.hidden &&
                    nexus.drama == null &&
                    nexus.skobExamine.difficulty == SkillLevelCategory.None)
                {
                    string name = GetInteractableName(nexus) ?? "Object";
                    MelonLogger.Msg($"[MapCursorState] Examining (description): {name}");
                    ScreenReaderManager.SpeakInterrupt("Examining " + name);

                    if (MonoBehaviourSingleton<InputManager>.HasInstance())
                    {
                        MonoBehaviourSingleton<InputManager>.GetInstance().CheckExamineDrama(nexus.transform);
                    }
                    return;
                }
            }

            // Check NPCs on tile
            foreach (var mob in mobs)
            {
                if (mob is NPC)
                {
                    var drama = mob.GetComponent<Drama>();
                    if (drama != null)
                    {
                        PC pc = GetPartyLeader();
                        if (pc == null) continue;

                        if (Drama.ExamineDrama(drama, pc, dontExecute: true))
                        {
                            string name = GetMobName(mob);
                            MelonLogger.Msg($"[MapCursorState] Examining NPC: {name}");
                            ScreenReaderManager.SpeakInterrupt("Examining " + name);
                            Drama.ExamineDrama(drama, pc, dontExecute: false);
                            return;
                        }
                    }
                }
            }

            ScreenReaderManager.SpeakInterrupt("Nothing to examine");
        }

        // --- Context Menu ---

        private void OpenContextMenu()
        {
            var interactables = FindInteractablesOnTile();

            if (interactables.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("Nothing to interact with");
                return;
            }

            contextMenuOptions.Clear();

            // If multiple interactables on tile, add each as an interact option
            if (interactables.Count > 1)
            {
                foreach (var nexus in interactables)
                {
                    string name = GetInteractableName(nexus);
                    if (string.IsNullOrEmpty(name)) name = "Object";
                    // Store the interactable index so we can identify it later
                    contextMenuOptions.Add(new ContextMenuOption
                    {
                        DisplayName = name,
                        ASIName = "select_" + interactables.IndexOf(nexus)
                    });
                }

                contextMenuTarget = null; // Multiple targets — selection first
                contextMenuActive = true;
                contextMenuIndex = 0;

                string announcement = contextMenuOptions.Count + " objects. " + contextMenuOptions[0].DisplayName;
                ScreenReaderManager.SpeakInterrupt(announcement);
                MelonLogger.Msg($"[MapCursorState] Context menu opened with {contextMenuOptions.Count} objects");
                return;
            }

            // Single interactable — build action menu from allowed interactions
            var target = interactables[0];
            contextMenuTarget = target;
            BuildActionMenu(target);
        }

        private void OpenTileSelectionMenu(List<PC> pcs, List<InteractableNexus> interactables)
        {
            contextMenuOptions.Clear();
            contextMenuPCs.Clear();
            contextMenuPCs.AddRange(pcs);

            // Add each party member
            for (int i = 0; i < pcs.Count; i++)
            {
                string pcName = GetMobName(pcs[i]);
                contextMenuOptions.Add(new ContextMenuOption
                {
                    DisplayName = pcName + " (party member)",
                    ASIName = "partyinfo_" + i
                });
            }

            // Add each interactable
            foreach (var nexus in interactables)
            {
                string name = GetInteractableName(nexus);
                if (string.IsNullOrEmpty(name)) name = "Object";
                contextMenuOptions.Add(new ContextMenuOption
                {
                    DisplayName = name,
                    ASIName = "select_" + interactables.IndexOf(nexus)
                });
            }

            contextMenuTarget = null;
            contextMenuActive = true;
            contextMenuIndex = 0;

            string announcement = contextMenuOptions.Count + " things on tile. " + contextMenuOptions[0].DisplayName;
            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[MapCursorState] Tile selection menu opened with {contextMenuOptions.Count} options ({pcs.Count} PCs + {interactables.Count} interactables)");
        }

        private void BuildActionMenu(InteractableNexus target)
        {
            contextMenuOptions.Clear();
            string targetName = GetInteractableName(target) ?? "Object";

            if (target.drama != null)
            {
                var interactions = target.drama.GetAllowedInteractions();
                if (interactions != null)
                {
                    // Log all interactions for debugging
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var kvp in interactions)
                        parts.Add($"{kvp.Key}={kvp.Value}");
                    MelonLogger.Msg($"[MapCursorState] Interactions for {targetName}: {string.Join(", ", parts.ToArray())}");
                    MelonLogger.Msg($"[MapCursorState] Drama type: {target.drama.GetType().Name}, blockPoke={target.drama.blockPoke}, bInstigateBlocked={target.drama.bInstigateBlocked}");
                    var iObj = target.drama as InteractableObject;
                    if (iObj != null)
                        MelonLogger.Msg($"[MapCursorState] InteractableObject: IgnorePokedEvent={iObj.IgnorePokedEvent}, isActive={iObj.isActive}, isLocked={iObj.isLocked}");
                    var teleporter = target.drama as InteractableTeleporter;
                    if (teleporter != null)
                        MelonLogger.Msg($"[MapCursorState] Teleporter: mustActivateBeforeTeleport={teleporter.mustActivateBeforeTeleport}, followRules={teleporter.followRules}");
                    var invObj = target.drama as InteractableInventoryObject;
                    if (invObj == null) invObj = target.GetComponent<InteractableInventoryObject>();
                    if (invObj != null)
                        MelonLogger.Msg($"[MapCursorState] Container: empty={invObj.empty}, isActive={invObj.isActive}, isLocked={invObj.isLocked}");

                    // Add "Interact" (Poked) first if available
                    if (interactions.ContainsKey("Poked") && interactions["Poked"] == 1)
                    {
                        contextMenuOptions.Add(new ContextMenuOption
                        {
                            DisplayName = "Interact",
                            ASIName = null // null = Poked (default interaction)
                        });
                    }

                    // Add skill interactions
                    // Value meanings: 1 = available, 0 = failed/used, -1 = not yet prodded.
                    // The game sets prodded when the mouse hovers the object (UpdateCursor),
                    // which never happens with our keyboard cursor. Treat -1 the same as 1.
                    foreach (var kvp in interactions)
                    {
                        if (kvp.Key == "Poked") continue;
                        if (kvp.Value == 0) continue; // Skill check failed or already used

                        string displayName;
                        if (!SKILL_DISPLAY_NAMES.TryGetValue(kvp.Key, out displayName))
                            displayName = kvp.Key;

                        contextMenuOptions.Add(new ContextMenuOption
                        {
                            DisplayName = displayName,
                            ASIName = kvp.Key
                        });
                    }
                }
            }

            // Check for ItemAcceptingObject components — add "Use [item]" if a matching
            // item exists in party inventory (e.g. Shovel on diggable objects)
            if (target.gameObject != null)
            {
                var acceptors = target.gameObject.GetComponentsInChildren<ItemAcceptingObject>();
                if (acceptors != null && acceptors.Length > 0 && MonoBehaviourSingleton<Game>.HasInstance())
                {
                    var game = MonoBehaviourSingleton<Game>.GetInstance();
                    foreach (var acceptor in acceptors)
                    {
                        if (!acceptor.enabled || acceptor.desiredItemTemplate == null) continue;

                        // Search party for the matching item
                        for (int i = 0; i < game.party.Count; i++)
                        {
                            PC member = game.party[i];
                            if (!member.isConscious) continue;
                            ItemInstance item = member.inventory.inventory.GetInstanceOfTemplate(acceptor.desiredItemTemplate);
                            if (item != null)
                            {
                                string itemDisplayName = UITextExtractor.CleanText(
                                    Language.Localize(item.template.displayName, false, false, string.Empty));
                                contextMenuOptions.Add(new ContextMenuOption
                                {
                                    DisplayName = "Use " + itemDisplayName,
                                    ASIName = "useItem_auto"
                                });
                                MelonLogger.Msg($"[MapCursorState] Added Use {itemDisplayName} option for {targetName}");
                                goto doneItemCheck; // Only need one matching item option
                            }
                        }
                    }
                }
            }
            doneItemCheck:

            // Add Examine option for objects with SkillObject_Examine and a Drama handler
            if (target.drama != null)
            {
                var skobEx = target.drama.GetComponent<SkillObject_Examine>();
                if (skobEx != null && !skobEx.hidden &&
                    Drama.ExamineDrama(target.drama, GetPartyLeader(), dontExecute: true))
                {
                    contextMenuOptions.Add(new ContextMenuOption
                    {
                        DisplayName = "Examine",
                        ASIName = "examine"
                    });
                }
            }

            // For examine-only objects without Drama (difficulty None),
            // add an Examine option that uses CheckExamineDrama directly
            if (contextMenuOptions.Count == 0 && target.skobExamine != null &&
                target.skobExamine.difficulty == SkillLevelCategory.None)
            {
                contextMenuOptions.Add(new ContextMenuOption
                {
                    DisplayName = "Examine",
                    ASIName = "examine"
                });
            }

            if (contextMenuOptions.Count == 0)
            {
                // No actions available — just try default interact
                ExecuteInteraction(target, null);
                return;
            }

            if (contextMenuOptions.Count == 1)
            {
                // Only one action — execute immediately
                var option = contextMenuOptions[0];
                MelonLogger.Msg($"[MapCursorState] Single action for {targetName}: {option.DisplayName}");
                ExecuteInteraction(target, option.ASIName);
                return;
            }

            // Multiple actions — open menu
            contextMenuActive = true;
            contextMenuIndex = 0;

            string announcement = targetName + ". " + contextMenuOptions[0].DisplayName +
                ". " + contextMenuOptions.Count + " options";
            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[MapCursorState] Context menu for {targetName}: {contextMenuOptions.Count} options");
        }

        private bool HandleContextMenuInput()
        {
            // Up/Down to cycle options
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                contextMenuIndex--;
                if (contextMenuIndex < 0) contextMenuIndex = contextMenuOptions.Count - 1;
                ScreenReaderManager.SpeakInterrupt(contextMenuOptions[contextMenuIndex].DisplayName);
                SuppressInput();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                contextMenuIndex = (contextMenuIndex + 1) % contextMenuOptions.Count;
                ScreenReaderManager.SpeakInterrupt(contextMenuOptions[contextMenuIndex].DisplayName);
                SuppressInput();
                return true;
            }

            // Enter to select
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var option = contextMenuOptions[contextMenuIndex];

                // If this was a party member selection, open their info
                if (option.ASIName != null && option.ASIName.StartsWith("partyinfo_"))
                {
                    int pcIndex;
                    if (int.TryParse(option.ASIName.Substring(10), out pcIndex)
                        && pcIndex >= 0 && pcIndex < contextMenuPCs.Count)
                    {
                        PC pc = contextMenuPCs[pcIndex];
                        CloseContextMenu();
                        OpenPartyMemberInfo(pc);
                        SuppressInput();
                        return true;
                    }
                }

                // If this was a pending-action PC selection (e.g. skill/item target on stacked tile),
                // fire the callback with the chosen PC
                if (option.ASIName != null && option.ASIName.StartsWith("pcselect_"))
                {
                    int pcIndex;
                    if (int.TryParse(option.ASIName.Substring(9), out pcIndex)
                        && pcIndex >= 0 && pcIndex < contextMenuPCs.Count)
                    {
                        PC pc = contextMenuPCs[pcIndex];
                        var callback = pendingPCSelectionCallback;
                        CloseContextMenu();
                        if (callback != null) callback(pc);
                        SuppressInput();
                        return true;
                    }
                }

                // Targetable picker for free-aim attacks against stacked targets
                if (option.ASIName != null && option.ASIName.StartsWith("targetselect_"))
                {
                    int targetIndex;
                    if (int.TryParse(option.ASIName.Substring(13), out targetIndex)
                        && targetIndex >= 0 && targetIndex < contextMenuTargetables.Count)
                    {
                        Targetable t = contextMenuTargetables[targetIndex];
                        var callback = pendingTargetableSelectionCallback;
                        CloseContextMenu();
                        if (callback != null) callback(t);
                        SuppressInput();
                        return true;
                    }
                }

                // If this was a multi-object selection menu, drill into that object
                if (option.ASIName != null && option.ASIName.StartsWith("select_"))
                {
                    int objIndex;
                    if (int.TryParse(option.ASIName.Substring(7), out objIndex))
                    {
                        var interactables = FindInteractablesOnTile();
                        if (objIndex >= 0 && objIndex < interactables.Count)
                        {
                            contextMenuTarget = interactables[objIndex];
                            BuildActionMenu(contextMenuTarget);
                            return true;
                        }
                    }
                }

                // Execute the action
                if (contextMenuTarget != null)
                {
                    ExecuteInteraction(contextMenuTarget, option.ASIName);
                }
                CloseContextMenu();
                return true;
            }

            // Escape to close
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseContextMenu();
                ScreenReaderManager.SpeakInterrupt("Cancelled");
                return true;
            }

            // Suppress all other keys while menu is open
            SuppressInput();
            return true;
        }

        private void CloseContextMenu()
        {
            contextMenuActive = false;
            contextMenuOptions.Clear();
            contextMenuIndex = -1;
            contextMenuTarget = null;
            contextMenuPCs.Clear();
            pendingPCSelectionCallback = null;
            contextMenuTargetables.Clear();
            pendingTargetableSelectionCallback = null;
        }

        private void OpenPCSelectionMenu(List<PC> pcs, string header, System.Action<PC> callback)
        {
            contextMenuOptions.Clear();
            contextMenuPCs.Clear();
            contextMenuPCs.AddRange(pcs);

            for (int i = 0; i < pcs.Count; i++)
            {
                contextMenuOptions.Add(new ContextMenuOption
                {
                    DisplayName = GetMobName(pcs[i]),
                    ASIName = "pcselect_" + i
                });
            }

            pendingPCSelectionCallback = callback;
            contextMenuTarget = null;
            contextMenuActive = true;
            contextMenuIndex = 0;

            string announcement = header + ". " + contextMenuOptions[0].DisplayName + ". " + pcs.Count + " party members";
            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[MapCursorState] PC selection menu: {header} ({pcs.Count} PCs)");
        }

        private void OpenTargetableSelectionMenu(List<Targetable> targets, List<string> names,
            string header, System.Action<Targetable> callback)
        {
            contextMenuOptions.Clear();
            contextMenuTargetables.Clear();
            contextMenuTargetables.AddRange(targets);

            for (int i = 0; i < targets.Count; i++)
            {
                contextMenuOptions.Add(new ContextMenuOption
                {
                    DisplayName = names[i],
                    ASIName = "targetselect_" + i
                });
            }

            pendingTargetableSelectionCallback = callback;
            contextMenuTarget = null;
            contextMenuActive = true;
            contextMenuIndex = 0;

            string announcement = header + ". " + contextMenuOptions[0].DisplayName + ". " + targets.Count + " targets";
            ScreenReaderManager.SpeakInterrupt(announcement);
            MelonLogger.Msg($"[MapCursorState] Target selection menu: {header} ({targets.Count} targets)");
        }

        private void ExecuteInteraction(InteractableNexus target, string asiName)
        {
            if (target == null) return;

            PC pc = GetPartyLeader();
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No party leader");
                return;
            }

            // Set selectedInteractable so the game knows what we're targeting
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                MonoBehaviourSingleton<InputManager>.GetInstance().selectedInteractable = target;
            }

            // Prod the object so the game registers it as "seen" — normally happens
            // when the mouse cursor hovers over the object's Highlight component.
            // Without this, locked containers report skills as -1 (undiscovered)
            // and CheckInstigate may not process the interaction correctly.
            var interactableObj = target.drama as InteractableObject;
            if (interactableObj != null && !interactableObj.HasBeenProdded())
            {
                interactableObj.ProdIt();
            }

            string name = GetInteractableName(target) ?? "Object";

            // Handle "Use [item]" from the auto-detected item-accepting option
            if (asiName == "useItem_auto" && MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                if (ExplorationState.TryUseItemOnObject(target, inputManager, pc))
                    return;
                // If TryUseItemOnObject returned false, fall through to normal interaction
            }

            // Handle examine action - use Drama.ExamineDrama for Drama objects,
            // CheckExamineDrama for no-Drama description objects
            if (asiName == "examine")
            {
                if (target.drama != null)
                {
                    MelonLogger.Msg($"[MapCursorState] Examining: {name}");
                    ScreenReaderManager.SpeakInterrupt("Examining " + name);
                    Drama.ExamineDrama(target.drama, pc, dontExecute: false);
                }
                else if (target.skobExamine != null &&
                    target.skobExamine.difficulty == SkillLevelCategory.None)
                {
                    MelonLogger.Msg($"[MapCursorState] Examining (description): {name}");
                    ScreenReaderManager.SpeakInterrupt("Examining " + name);

                    if (MonoBehaviourSingleton<InputManager>.HasInstance())
                    {
                        MonoBehaviourSingleton<InputManager>.GetInstance().CheckExamineDrama(target.transform);
                    }
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Cannot examine " + name);
                }
                return;
            }

            if (target.drama == null) return;

            // Check if this object accepts items (e.g. dirt piles needing shovels).
            // Auto-use matching items from party inventory, or announce what's needed.
            // This runs before skill handling because locked item-accepting objects
            // often only expose "Examine" in GetAllowedInteractions, hiding the real
            // interaction (use item) behind the fallback.
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                if (ExplorationState.TryUseItemOnObject(target, MonoBehaviourSingleton<InputManager>.GetInstance(), pc))
                    return;
            }

            // Set the active ASI if this is a real skill interaction.
            // "examine" is a mod-only fallback from BuildActionMenu, not a valid game ASI.
            // Setting it would confuse Drama.CheckInstigate — the default poke/examine
            // fires naturally when ASI is null.
            bool isRealSkillASI = !string.IsNullOrEmpty(asiName) && asiName != "examine";
            if (isRealSkillASI)
            {
                UseASIManager.SetActiveASIName(asiName);
            }

            string displayAction;
            if (string.IsNullOrEmpty(asiName) || asiName == "examine")
            {
                displayAction = "Interacting with";
            }
            else if (SKILL_DISPLAY_NAMES.TryGetValue(asiName, out displayAction))
            {
                displayAction = "Using " + displayAction + " on";
            }
            else
            {
                displayAction = "Using " + asiName + " on";
            }

            MelonLogger.Msg($"[MapCursorState] {displayAction} {name}");
            ScreenReaderManager.SpeakInterrupt(displayAction + " " + name);

            // Check if interaction is blocked (e.g. perception-gated objects)
            if (target.drama.bInstigateBlocked)
            {
                MelonLogger.Msg($"[MapCursorState] Interaction blocked on {name} (bInstigateBlocked=true)");
                ScreenReaderManager.SpeakInterrupt("Cannot interact with " + name);
                if (isRealSkillASI)
                    UseASIManager.SetActiveASIName(null);
                return;
            }

            // Use the game's normal interaction system — PC walks to object and interacts
            Drama.CheckInstigate(target.drama, pc, false);
        }

        // --- Move Party ---

        private void MovePartyToCursor()
        {
            if (!MonoBehaviourSingleton<InputManager>.HasInstance()) return;

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            PC pc = GetPartyLeader();
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No party leader");
                return;
            }

            // InputManager.Update() is suppressed while MapCursor is active, so
            // activePCs goes stale. MoveInFormation iterates activePCs when ungrouped
            // (InputManager.GetMovementPCs), and a stale/destroyed entry NREs there.
            // Rebuild from selectedMobs as Update would.
            inputManager.activePCs.Clear();
            foreach (Mob mob in inputManager.selectedMobs)
            {
                if (mob != null && mob.asPartyPC != null)
                    inputManager.activePCs.Add(mob.asPartyPC);
            }
            if (inputManager.activePCs.Count == 0)
            {
                PC leader = MonoBehaviourSingleton<Game>.GetInstance().pcLeader;
                if (leader != null) inputManager.activePCs.Add(leader);
            }

            inputManager.MoveInFormation(inputManager.formation, cursorPosition,
                false, null, 0f, null, true);

            int tileDistX = Mathf.Abs((int)cursorGridId.x - Mathf.RoundToInt(pc.transform.position.x / GRID_SQUARE_SIZE));
            int tileDistZ = Mathf.Abs((int)cursorGridId.z - Mathf.RoundToInt(pc.transform.position.z / GRID_SQUARE_SIZE));
            int tileDist = Mathf.Max(tileDistX, tileDistZ);

            string subject = inputManager.isPartyGrouped ? "party" : GetPCDisplayName(pc);
            ScreenReaderManager.SpeakInterrupt("Moving " + subject + ", " + tileDist + (tileDist == 1 ? " tile" : " tiles"));
            MelonLogger.Msg($"[MapCursorState] Moving {subject} to cursor position {cursorPosition}");
        }

        // --- Camera ---

        private void SnapCameraToCursor()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var cameraController = MonoBehaviourSingleton<Game>.GetInstance().cameraController;
            if (cameraController == null) return;

            cameraController.Snap(cursorPosition, instant: false, resetPreviousZoom: false,
                charSnap: false, forceCharSnap: false, noSFX: true);
        }

        private void JumpToSelectedInteractable()
        {
            InteractableNexus selected = NavigationManager.SelectedInteractable;
            if (selected == null)
            {
                // No interactable selected — fall back to party position
                InitializeToPartyPosition();
                if (cursorInitialized)
                {
                    SnapCameraToCursor();
                    ScreenReaderManager.SpeakInterrupt("Returned to party");
                    AnnounceCurrentTile(detailed: false);
                }
                return;
            }

            Vector3 worldPos = selected.transform.position;

            // Snap to the object's natural tile (raw-rounded from world position).
            // This is where the object visually belongs, independent of whether
            // a walkable A* node exists there. Falling back to a "nearest node"
            // can place the cursor one tile off when the object's tile is
            // unwalkable (walls, props) and the nearest node is on a neighbor.
            int naturalX = Mathf.RoundToInt(worldPos.x / GRID_SQUARE_SIZE);
            int naturalZ = Mathf.RoundToInt(worldPos.z / GRID_SQUARE_SIZE);

            // If a node happens to exist at the natural tile, use its position
            // for an accurate Y (floor height). Otherwise synthesize from the
            // object's Y.
            CombatAStarNode naturalTileNode = null;
            if (fullMap != null)
            {
                for (int f = 0; f <= 5; f++)
                {
                    CombatAStarNode node;
                    if (fullMap.TryGetValue(new Vector3(naturalX, f, naturalZ), out node))
                    {
                        naturalTileNode = node;
                        break;
                    }
                }
            }

            if (naturalTileNode != null)
            {
                cursorGridId = naturalTileNode.id;
                cursorPosition = naturalTileNode.position;
            }
            else
            {
                cursorGridId = new Vector3(naturalX, 0, naturalZ);
                cursorPosition = new Vector3(naturalX * GRID_SQUARE_SIZE, worldPos.y, naturalZ * GRID_SQUARE_SIZE);
            }

            MelonLogger.Msg($"[JumpToSelected] target={selected.name} worldPos={worldPos} naturalTile=({naturalX},{naturalZ}) nodeFound={(naturalTileNode != null)} cursorGridId={cursorGridId} cursorPos={cursorPosition}");
            LogTileFinderForTarget(selected);

            SnapCameraToCursor();
            AnnounceCurrentTile(detailed: false);
        }

        /// <summary>
        /// Diagnostic: trace why the currently-selected interactable may or
        /// may not match the current tile, step by step through the filters
        /// in FindInteractablesOnTile.
        /// </summary>
        private void LogTileFinderForTarget(InteractableNexus target)
        {
            if (target == null) return;
            Vector3 tp = target.transform.position;
            float dx = Mathf.Abs(tp.x - cursorPosition.x);
            float dz = Mathf.Abs(tp.z - cursorPosition.z);
            bool isVis = target.isVisible;
            bool fowOk = FOWHelper.IsVisibleThroughFOW(tp);
            bool percGated = FOWHelper.IsPerceptionGated(target);
            bool onTile = dx <= TILE_MATCH_RADIUS && dz <= TILE_MATCH_RADIUS;
            bool wallBlocked = IsBlockedByWall(tp);
            bool isDoor = IsDoor(target);
            MelonLogger.Msg($"[TileTrace] {target.name}: tp={tp}, dx={dx:F2}, dz={dz:F2}, TILE_MATCH_RADIUS={TILE_MATCH_RADIUS}, isVisible={isVis}, fowOk={fowOk}, percGated={percGated}, onTile={onTile}, wallBlocked={wallBlocked}, isDoor={isDoor}, isPC={target.isPC}");
        }

        private void AnnounceDistanceToSelected()
        {
            InteractableNexus selected = NavigationManager.SelectedInteractable;
            if (selected == null)
            {
                ScreenReaderManager.SpeakInterrupt("No interactable selected");
                return;
            }

            // Compute tile distance from cursor to selected interactable
            Vector3 targetWorldPos = selected.transform.position;
            int targetGridX = Mathf.RoundToInt(targetWorldPos.x / GRID_SQUARE_SIZE);
            int targetGridZ = Mathf.RoundToInt(targetWorldPos.z / GRID_SQUARE_SIZE);

            // Try to get more accurate grid ID from a node lookup
            CombatAStarNode targetNode = FindNodeAtPosition(targetWorldPos);
            if (targetNode != null)
            {
                targetGridX = (int)targetNode.id.x;
                targetGridZ = (int)targetNode.id.z;
            }

            int tileDistX = Mathf.Abs((int)cursorGridId.x - targetGridX);
            int tileDistZ = Mathf.Abs((int)cursorGridId.z - targetGridZ);
            int tileDist = Mathf.Max(tileDistX, tileDistZ);
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, targetWorldPos);

            string name = GetInteractableName(selected);
            string announcement = (name ?? "Object") + ", " + tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction;
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private void JumpToParty()
        {
            PC pc = GetPartyLeader();
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No party leader");
                return;
            }

            InitializeToPartyPosition();
            if (!cursorInitialized) return;

            SnapCameraToCursor();
            AnnounceCurrentTile(detailed: false);
        }

        private void AnnounceDistanceToParty()
        {
            PC pc = GetPartyLeader();
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No party leader");
                return;
            }

            Vector3 targetWorldPos = pc.transform.position;
            int targetGridX = Mathf.RoundToInt(targetWorldPos.x / GRID_SQUARE_SIZE);
            int targetGridZ = Mathf.RoundToInt(targetWorldPos.z / GRID_SQUARE_SIZE);

            CombatAStarNode targetNode = FindNodeAtPosition(targetWorldPos);
            if (targetNode != null)
            {
                targetGridX = (int)targetNode.id.x;
                targetGridZ = (int)targetNode.id.z;
            }

            int tileDistX = Mathf.Abs((int)cursorGridId.x - targetGridX);
            int tileDistZ = Mathf.Abs((int)cursorGridId.z - targetGridZ);
            int tileDist = Mathf.Max(tileDistX, tileDistZ);
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, targetWorldPos);

            string name = UITextExtractor.CleanText(GetPCDisplayName(pc)) ?? "Party";
            string announcement = name + ", " + tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction;
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        // --- Obstruction Detection ---

        /// <summary>
        /// Raycasts/checks what is at a world position where no grid node exists.
        /// Uses multiple techniques to identify walls, objects, terrain features, etc.
        /// </summary>
        private string IdentifyObstruction(Vector3 worldPos)
        {
            // Broad layer mask: Wall, Cover, FadedWall, StaticMeshes, Terrain,
            // DynamicObject, Mob, Floor, FadedFloor, Default
            int obstructionMask = InputManager.layerMask_Wall
                                | InputManager.layerMask_Cover
                                | InputManager.layerMask_FadedWall
                                | InputManager.layerMask_StaticMeshes
                                | InputManager.layerMask_DynamicObject
                                | InputManager.layerMask_Terrain
                                | InputManager.layerMask_Floor
                                | InputManager.layerMask_FadedFloor
                                | InputManager.layerMask_Default;

            // Raycast down from above to find what's on the ground at this position
            RaycastHit hit;
            if (Physics.Raycast(worldPos + Vector3.up * 10f, Vector3.down, out hit, 20f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            // Try a sphere cast to catch objects that a thin ray might miss
            if (Physics.SphereCast(worldPos + Vector3.up * 5f, 0.5f, Vector3.down, out hit, 10f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            // Also try raycasting horizontally from the current node toward the target
            Vector3 horizontalDir = (worldPos - cursorPosition).normalized;
            if (Physics.Raycast(cursorPosition + Vector3.up * 0.5f, horizontalDir, out hit, GRID_SQUARE_SIZE * 1.5f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            return "Impassable";
        }

        private string DescribeHitObject(RaycastHit hit)
        {
            GameObject go = hit.transform.gameObject;
            int layer = go.layer;

            // Check layer for broad category
            string layerName = LayerMask.LayerToName(layer);

            // Try to get a meaningful name from the object or its parents
            string objectName = GetMeaningfulName(hit.transform);

            // If the raycast hit an object with an InteractableNexus that is
            // perception-gated, don't reveal its name — return null so it's
            // treated as generic terrain/obstruction instead.
            var nexus = go.GetComponent<InteractableNexus>();
            if (nexus == null) nexus = go.GetComponentInParent<InteractableNexus>();
            if (nexus != null && FOWHelper.IsPerceptionGated(nexus))
                return null;

            // Categorize by layer
            if (layerName == "Wall" || layerName == "FadedWall")
            {
                if (!string.IsNullOrEmpty(objectName))
                    return "Wall, " + objectName;
                return "Wall";
            }

            if (layerName == "Cover")
            {
                if (!string.IsNullOrEmpty(objectName))
                    return "Cover, " + objectName;
                return "Cover";
            }

            if (layerName == "Terrain")
            {
                return "Terrain";
            }

            // For other layers, try to use the object name
            if (!string.IsNullOrEmpty(objectName))
                return objectName;

            // Fallback
            if (!string.IsNullOrEmpty(layerName))
                return layerName;

            return "Impassable";
        }

        /// <summary>
        /// Walks up the transform hierarchy to find a meaningful name,
        /// skipping generic names like "Collider", "Mesh", numbers, etc.
        /// </summary>
        private string GetMeaningfulName(Transform trans)
        {
            Transform current = trans;
            while (current != null)
            {
                string name = current.gameObject.name;
                if (!string.IsNullOrEmpty(name))
                {
                    // Skip generic/internal names
                    string lower = name.ToLower();
                    if (lower.Contains("collider") || lower.Contains("mesh") ||
                        lower.Contains("trigger") || lower.Contains("blocker") ||
                        lower == "default" || lower.StartsWith("cube") ||
                        lower.StartsWith("plane") || lower.StartsWith("quad"))
                    {
                        current = current.parent;
                        continue;
                    }

                    // Clean up the name
                    name = name.Replace("_", " ").Replace("(Clone)", "").Trim();
                    if (name.Length > 0)
                        return name;
                }
                current = current.parent;
            }
            return null;
        }

        // --- Actions Menu (Tab key) ---

        private void OpenActionsMenu()
        {
            BuildExplorationActionList();

            if (actionList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No actions available");
                return;
            }

            browsingActions = true;
            actionIndex = 0;

            PC pc = GetPartyLeader();
            string header = "Actions";
            if (pc != null)
                header += " for " + GetPCDisplayName(pc);
            header += ", " + actionList.Count + " items";

            ScreenReaderManager.SpeakInterrupt(header + ". " + FormatAction(actionList[0]));
        }

        private void BuildExplorationActionList()
        {
            actionList.Clear();

            PC pc = GetPartyLeader();
            if (pc == null) return;

            // --- Trained Skills ---
            try
            {
                foreach (string skillName in UseASIManager.SkillASIs)
                {
                    if (pc.pcStats.GetSkillLevel(skillName) <= 0) continue;

                    string displayName;
                    if (!SKILL_DISPLAY_NAMES.TryGetValue(skillName, out displayName))
                        displayName = skillName;

                    // Try to get skill level for status
                    int level = pc.pcStats.GetSkillLevel(skillName);
                    string capturedSkillName = skillName;

                    actionList.Add(new ExplorationAction
                    {
                        Label = displayName,
                        Status = "level " + level,
                        IsEnabled = true,
                        Execute = () =>
                        {
                            UseASIManager.SetActiveASIName(capturedSkillName);
                            string targetHint = UseASIManager.IsSkillItemASI(capturedSkillName)
                                ? " active. Target a party member with Enter"
                                : " active. Target an object with Enter";
                            ScreenReaderManager.SpeakInterrupt(displayName + targetHint);
                            MelonLogger.Msg("[MapCursorState] Skill activated: " + capturedSkillName);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building skill list: " + ex.Message);
            }

            // --- Usable Items ---
            try
            {
                var usableItems = pc.inventory.GetUsableItems(true, true);
                for (int i = 0; i < usableItems.Count; i++)
                {
                    var item = usableItems[i];
                    if (item == null || item.template == null) continue;

                    var usableTemplate = item.template as ItemTemplate_Usable;
                    if (usableTemplate == null) continue;
                    if (!usableTemplate.CanUse(pc)) continue;

                    string itemName = UITextExtractor.CleanText(
                        Language.Localize(item.template.displayName, false, false, string.Empty));

                    int count = pc.inventory.CountInInventory(item);
                    string label = "Use " + itemName;
                    if (count > 1)
                        label += ", " + count + " remaining";

                    var capturedItem = item;
                    var capturedPC = pc;

                    actionList.Add(new ExplorationAction
                    {
                        Label = label,
                        Status = "item",
                        IsEnabled = true,
                        Execute = () =>
                        {
                            UseASIManager.SetActiveASIItem(capturedItem, capturedPC);
                            UseASIManager.SetActiveASIName("useItem");
                            ScreenReaderManager.SpeakInterrupt(itemName + " ready. Target a party member or object with Enter");
                            MelonLogger.Msg("[MapCursorState] Item activated: " + itemName);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building item list: " + ex.Message);
            }

            // --- Swap Weapons ---
            try
            {
                var secondary = pc.pcStats.GetSecondaryWeaponInstance();
                string swapInfo = "";
                if (secondary != null && secondary.template != null)
                    swapInfo = ", to " + UITextExtractor.CleanText(
                        Language.Localize(secondary.template.displayName, false, false, string.Empty));

                actionList.Add(new ExplorationAction
                {
                    Label = "Swap weapons" + swapInfo,
                    Status = "",
                    IsEnabled = true,
                    Execute = () =>
                    {
                        MonoBehaviourSingleton<InputManager>.GetInstance().OnSwapWeaponsClicked(pc);
                        ScreenReaderManager.SpeakInterrupt("Weapons swapped");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building swap weapons action: " + ex.Message);
            }

            // --- Crouch / Stand ---
            try
            {
                bool isCrouching = pc.isCrouching;
                bool canChangeStance = !pc.IsInCover();

                actionList.Add(new ExplorationAction
                {
                    Label = isCrouching ? "Stand up" : "Crouch",
                    Status = pc.IsInCover() ? "in cover" : "",
                    IsEnabled = canChangeStance,
                    Execute = () =>
                    {
                        var evt = ObjectPool.Get<EventInfo_CommandChangeStance>();
                        evt.pc = pc;
                        evt.crouch = !pc.isCrouching;
                        MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                        ScreenReaderManager.SpeakInterrupt(evt.crouch ? "Crouching" : "Standing");
                    }
                });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building crouch action: " + ex.Message);
            }

            // --- Reload ---
            try
            {
                var weaponInstance = pc.pcStats.GetWeaponInstance();
                var rangedWeapon = weaponInstance as ItemInstance_WeaponRanged;
                if (rangedWeapon != null)
                {
                    if (rangedWeapon.IsJammed())
                    {
                        actionList.Add(new ExplorationAction
                        {
                            Label = "Unjam weapon",
                            Status = "",
                            IsEnabled = pc.CanUnjam(),
                            Execute = () =>
                            {
                                var evt = ObjectPool.Get<EventInfo_CommandUnjam>();
                                evt.target = pc;
                                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                                ScreenReaderManager.SpeakInterrupt("Unjamming weapon");
                            }
                        });
                    }
                    else
                    {
                        int currentAmmo = rangedWeapon.GetAmmoCount();
                        int clipSize = rangedWeapon.GetClipSize();
                        bool canReload = pc.CanReload();
                        string ammoStatus = currentAmmo + " of " + clipSize;

                        actionList.Add(new ExplorationAction
                        {
                            Label = "Reload",
                            Status = ammoStatus,
                            IsEnabled = canReload,
                            Execute = () =>
                            {
                                var evt = ObjectPool.Get<EventInfo_CommandReload>();
                                evt.mob = pc;
                                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                                ScreenReaderManager.SpeakInterrupt("Reloading");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building reload action: " + ex.Message);
            }

            // --- Free Aim (weapon attack) ---
            try
            {
                ItemTemplate_Weapon weaponTemplate = pc.stats.GetWeaponTemplate();
                if (weaponTemplate != null)
                {
                    string weaponName = UITextExtractor.CleanText(
                        Language.Localize(weaponTemplate.displayName, false, false, string.Empty));

                    var weaponInstance = pc.pcStats.GetWeaponInstance();
                    var rangedWeapon = weaponInstance as ItemInstance_WeaponRanged;

                    if (rangedWeapon != null)
                    {
                        // Ranged weapon — add an action per fire mode
                        var rangedTemplate = weaponTemplate as ItemTemplate_WeaponRanged;
                        if (rangedTemplate != null && rangedTemplate.firingModeInfos != null)
                        {
                            for (int modeIdx = 0; modeIdx < rangedTemplate.firingModeInfos.Length; modeIdx++)
                            {
                                var modeInfo = rangedTemplate.firingModeInfos[modeIdx];
                                string modeName;
                                if (modeInfo.ammoCost == 1)
                                    modeName = "Single";
                                else if (modeInfo.ammoCost == rangedTemplate.clipSize)
                                    modeName = "Full Auto";
                                else
                                    modeName = "Burst";

                                int capturedModeIdx = modeIdx;
                                string asiName = (weaponTemplate is ItemTemplate_WeaponShotgun) ? "coneattack" : "attack";

                                int currentAmmo = rangedWeapon.GetAmmoCount();
                                bool hasAmmo = currentAmmo >= modeInfo.ammoCost;
                                bool isJammed = rangedWeapon.IsJammed();
                                string status = weaponName + ", " + currentAmmo + " ammo";
                                if (isJammed) status += ", jammed";
                                else if (!hasAmmo) status += ", not enough ammo";

                                actionList.Add(new ExplorationAction
                                {
                                    Label = "Free Aim, " + modeName,
                                    Status = status,
                                    IsEnabled = hasAmmo && !isJammed,
                                    Execute = () =>
                                    {
                                        rangedWeapon.ChangeFiringMode(capturedModeIdx);
                                        UseASIManager.SetActiveASIName(asiName);
                                        ScreenReaderManager.SpeakInterrupt(
                                            "Free aim, " + modeName + " with " + weaponName +
                                            ". Move to target and press Enter to attack");
                                        MelonLogger.Msg("[MapCursorState] Free aim activated: " +
                                            modeName + " with " + weaponName);
                                    }
                                });
                            }
                        }
                    }
                    else
                    {
                        // Melee or other weapon — single attack action
                        string asiName = (weaponTemplate.weaponType == WeaponType.Thrown ||
                                          weaponTemplate.weaponType == WeaponType.RPG)
                            ? "aoeattack" : "attack";

                        actionList.Add(new ExplorationAction
                        {
                            Label = "Free Aim",
                            Status = weaponName,
                            IsEnabled = true,
                            Execute = () =>
                            {
                                UseASIManager.SetActiveASIName(asiName);
                                ScreenReaderManager.SpeakInterrupt(
                                    "Free aim with " + weaponName +
                                    ". Move to target and press Enter to attack");
                                MelonLogger.Msg("[MapCursorState] Free aim activated: " + weaponName);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Error building free aim actions: " + ex.Message);
            }
        }

        private string FormatAction(ExplorationAction action)
        {
            int position = actionList.IndexOf(action) + 1;
            string result = action.Label;

            if (!string.IsNullOrEmpty(action.Status))
                result += ", " + action.Status;

            if (!action.IsEnabled)
                result += ", unavailable";

            result += ", " + position + " of " + actionList.Count;
            return result;
        }

        private void AnnounceCurrentAction()
        {
            if (actionIndex < 0 || actionIndex >= actionList.Count) return;
            ScreenReaderManager.SpeakInterrupt(FormatAction(actionList[actionIndex]));
        }

        private void ExecuteCurrentAction()
        {
            if (actionIndex < 0 || actionIndex >= actionList.Count) return;

            var action = actionList[actionIndex];
            if (!action.IsEnabled)
            {
                ScreenReaderManager.SpeakInterrupt(action.Label + ", unavailable");
                return;
            }

            ExitActionsBrowse();

            try
            {
                action.Execute();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[MapCursorState] Action execution error: " + ex.Message);
                ScreenReaderManager.SpeakInterrupt("Action failed");
            }
        }

        private void ExitActionsBrowse()
        {
            browsingActions = false;
            actionList.Clear();
            ScreenReaderManager.SpeakInterrupt("Actions closed");
        }

        // --- Use Item on Target ---

        private void UseItemOnTile()
        {
            if (!MonoBehaviourSingleton<InputManager>.HasInstance()) return;

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            string itemName = UITextExtractor.CleanText(
                Language.Localize(UseASIManager.GetActiveASIItem().template.displayName, false, false, string.Empty));

            // Party members first — healing items (trauma kits, pain relievers) target PCs
            var pcsOnTile = FindPCsOnTile();
            if (pcsOnTile.Count > 1)
            {
                OpenPCSelectionMenu(pcsOnTile, $"Use {itemName} on", (pc) =>
                {
                    string pcName = GetMobName(pc);
                    MelonLogger.Msg($"[MapCursorState] Using {itemName} on {pcName}");
                    ScreenReaderManager.SpeakInterrupt($"Using {itemName} on {pcName}");
                    MonoBehaviourSingleton<InputManager>.GetInstance().HandleUsableItemClickOnTargetable(pc);
                });
                return;
            }
            if (pcsOnTile.Count == 1)
            {
                var pc = pcsOnTile[0];
                string pcName = GetMobName(pc);
                MelonLogger.Msg($"[MapCursorState] Using {itemName} on {pcName}");
                ScreenReaderManager.SpeakInterrupt($"Using {itemName} on {pcName}");
                inputManager.HandleUsableItemClickOnTargetable(pc);
                return;
            }

            // Non-PC mobs (NPCs, enemies) — some items target them directly
            foreach (var mob in FindMobsOnTile())
            {
                if (mob is PC) continue;
                if (mob.mobState == Mob.MobState.DEAD) continue;
                string mobName = GetMobName(mob);
                MelonLogger.Msg($"[MapCursorState] Using {itemName} on {mobName}");
                ScreenReaderManager.SpeakInterrupt($"Using {itemName} on {mobName}");
                inputManager.HandleUsableItemClickOnTargetable(mob);
                return;
            }

            var interactables = FindInteractablesOnTile();

            if (interactables.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No target here");
                return;
            }

            // Find a targetable on the tile to use the item on
            Targetable targetable = null;
            string targetName = null;
            foreach (var nexus in interactables)
            {
                if (nexus == null || nexus.gameObject == null) continue;
                var t = nexus.gameObject.GetComponent<Targetable>();
                if (t != null)
                {
                    targetable = t;
                    targetName = GetInteractableName(nexus) ?? nexus.name;
                    break;
                }
            }

            if (targetable == null)
            {
                // Fallback: try using PrepareUseItemActions with the first interactable's transform/drama
                var nexus = interactables[0];
                if (nexus.drama != null)
                {
                    string name = GetInteractableName(nexus) ?? nexus.name;
                    MelonLogger.Msg($"[MapCursorState] Using {itemName} on {name} (no Targetable, using drama fallback)");
                    ScreenReaderManager.SpeakInterrupt($"Using {itemName} on {name}");
                    InputManager.PrepareUseItemActions(nexus.transform, nexus.drama, null, false);
                    return;
                }
                ScreenReaderManager.SpeakInterrupt("Cannot use item here");
                return;
            }

            MelonLogger.Msg($"[MapCursorState] Using {itemName} on {targetName}");
            ScreenReaderManager.SpeakInterrupt($"Using {itemName} on {targetName}");
            inputManager.HandleUsableItemClickOnTargetable(targetable);
        }

        private void UseSkillOnTile(string activeASI)
        {
            if (!MonoBehaviourSingleton<InputManager>.HasInstance()) return;

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            string skillName;
            if (!SKILL_DISPLAY_NAMES.TryGetValue(activeASI, out skillName))
                skillName = activeASI;

            // Party members first — doctor/fieldMedic target PCs
            var pcsOnTile = FindPCsOnTile();
            if (pcsOnTile.Count > 1)
            {
                string asiCapture = activeASI;
                string skillCapture = skillName;
                OpenPCSelectionMenu(pcsOnTile, $"{skillName} on", (pc) =>
                {
                    string pcName = GetMobName(pc);
                    MelonLogger.Msg($"[MapCursorState] Using {asiCapture} on {pcName}");
                    ScreenReaderManager.SpeakInterrupt($"Using {skillCapture} on {pcName}");
                    MonoBehaviourSingleton<InputManager>.GetInstance().HandleSkillClick(pc.transform, doubleClick: false);
                });
                return;
            }
            if (pcsOnTile.Count == 1)
            {
                var pc = pcsOnTile[0];
                string pcName = GetMobName(pc);
                MelonLogger.Msg($"[MapCursorState] Using {activeASI} on {pcName}");
                ScreenReaderManager.SpeakInterrupt($"Using {skillName} on {pcName}");
                inputManager.HandleSkillClick(pc.transform, doubleClick: false);
                return;
            }

            // Non-PC mobs (animals for animalWhisperer, etc.)
            foreach (var mob in FindMobsOnTile())
            {
                if (mob is PC) continue;
                if (mob.mobState == Mob.MobState.DEAD) continue;
                string mobName = GetMobName(mob);
                MelonLogger.Msg($"[MapCursorState] Using {activeASI} on {mobName}");
                ScreenReaderManager.SpeakInterrupt($"Using {skillName} on {mobName}");
                inputManager.HandleSkillClick(mob.transform, doubleClick: false);
                return;
            }

            // Interactable objects (doors, locks, safes, etc.)
            foreach (var nexus in FindInteractablesOnTile())
            {
                if (nexus == null || nexus.drama == null) continue;
                string name = GetInteractableName(nexus) ?? nexus.name;
                MelonLogger.Msg($"[MapCursorState] Using {activeASI} on {name}");
                ScreenReaderManager.SpeakInterrupt($"Using {skillName} on {name}");
                inputManager.HandleSkillClick(nexus.transform, doubleClick: false);
                return;
            }

            ScreenReaderManager.SpeakInterrupt("No target on this tile");
        }

        // --- Free Aim Attack ---

        private void AttackMobOnTile()
        {
            try
            {
                // Collect every valid mob target on the tile (NPCs, enemies, animals)
                var mobTargets = new List<Targetable>();
                var mobNames = new List<string>();
                foreach (var mob in FindMobsOnTile())
                {
                    if (mob is PC) continue;
                    if (mob.mobState == Mob.MobState.DEAD) continue;
                    mobTargets.Add(mob);
                    mobNames.Add(GetMobName(mob) ?? "Unknown");
                }

                // If there are mobs, prefer them over destructibles (matches prior behavior)
                if (mobTargets.Count >= 2)
                {
                    OpenTargetableSelectionMenu(mobTargets, mobNames, "Choose target",
                        (chosen) => PerformFreeAimAttack(chosen, ResolveTargetableName(chosen)));
                    return;
                }
                if (mobTargets.Count == 1)
                {
                    PerformFreeAimAttack(mobTargets[0], mobNames[0]);
                    return;
                }

                // No mobs — fall back to destructible objects (barrels, plants, etc.)
                var objTargets = new List<Targetable>();
                var objNames = new List<string>();
                foreach (var nexus in FindInteractablesOnTile())
                {
                    if (nexus == null || nexus.gameObject == null) continue;
                    var targetObj = nexus.gameObject.GetComponent<TargetableObject>();
                    if (targetObj == null) continue;
                    if (targetObj.curHP <= 0) continue;
                    objTargets.Add(targetObj);
                    string n = GetInteractableName(nexus);
                    if (string.IsNullOrEmpty(n)) n = nexus.gameObject.name;
                    objNames.Add(n);
                }

                if (objTargets.Count >= 2)
                {
                    OpenTargetableSelectionMenu(objTargets, objNames, "Choose target",
                        (chosen) => PerformFreeAimAttack(chosen, ResolveTargetableName(chosen)));
                    return;
                }
                if (objTargets.Count == 1)
                {
                    PerformFreeAimAttack(objTargets[0], objNames[0]);
                    return;
                }

                ScreenReaderManager.SpeakInterrupt("No target on this tile");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[MapCursorState] Error in AttackMobOnTile: " + ex.Message);
                ScreenReaderManager.SpeakInterrupt("Attack failed");
            }
        }

        private string ResolveTargetableName(Targetable target)
        {
            var mob = target as Mob;
            if (mob != null) return GetMobName(mob) ?? "Unknown";
            if (target != null && target.gameObject != null) return target.gameObject.name;
            return "target";
        }

        private void PerformFreeAimAttack(Targetable target, string targetName)
        {
            try
            {
                if (target == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No target");
                    return;
                }

                PC pc = GetPartyLeader();
                if (pc == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No ranger selected");
                    return;
                }

                ItemTemplate_Weapon weaponTemplate = pc.stats.GetWeaponTemplate();
                if (weaponTemplate == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No weapon equipped");
                    return;
                }

                var rangedWeapon = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
                if (rangedWeapon != null && rangedWeapon.IsJammed())
                {
                    ScreenReaderManager.SpeakInterrupt("Weapon is jammed");
                    return;
                }
                if (rangedWeapon != null && rangedWeapon.IsEmpty())
                {
                    ScreenReaderManager.SpeakInterrupt("Out of ammo");
                    return;
                }

                // Mirror vanilla's gating in AIBehaviour_PC.OnAttack so we publish iff
                // vanilla would have executed the attack — including the melee
                // walk-and-attack fallback (FindAttackDestination, AIBehaviour_PC.cs:389).
                // For ranged, vanilla rejects silently if CanAttack fails at the current
                // position, so the previous LoS+range pre-check matches.
                bool isMelee = weaponTemplate is ItemTemplate_WeaponMelee;
                bool willMoveAndAttack = false;

                if (isMelee)
                {
                    bool canAttackHere = false;
                    try { canAttackHere = pc.CanAttack(target, true, false); }
                    catch (Exception) { }

                    if (!canAttackHere)
                    {
                        bool isAttackable = false;
                        try { isAttackable = pc.IsAttackableTarget(target, false); }
                        catch (Exception) { }

                        if (isAttackable && TryFindAttackDestination(pc, target))
                        {
                            willMoveAndAttack = true;
                        }
                        else
                        {
                            // Vanilla would silently reject. Diagnose for parity with the
                            // cursor messages in Highlight.cs:572-588.
                            bool sightBlocked = false;
                            try { sightBlocked = !pc.TargetVisible(target); }
                            catch (Exception) { }

                            if (sightBlocked)
                            {
                                ScreenReaderManager.SpeakInterrupt("No line of sight to " + targetName);
                            }
                            else
                            {
                                string distStr = TileCoordinateSystem.GetDistanceText(pc.transform.position, target.transform.position);
                                string rangeStr = TileCoordinateSystem.GetRangeText(pc.stats.GetAttackRange());
                                ScreenReaderManager.SpeakInterrupt(
                                    targetName + " is out of range. Distance " + distStr +
                                    ", weapon range " + rangeStr);
                            }
                            return;
                        }
                    }
                }
                else
                {
                    try
                    {
                        if (!pc.TargetVisible(target))
                        {
                            ScreenReaderManager.SpeakInterrupt("No line of sight to " + targetName);
                            return;
                        }
                    }
                    catch (Exception) { }

                    float distance = Vector3.Distance(pc.transform.position, target.transform.position);
                    float additionalHitRange = 0f;
                    var targetMob = target as Mob;
                    if (targetMob != null)
                    {
                        try { additionalHitRange = targetMob.GetAdditionalHitRange(); }
                        catch (Exception) { }
                    }
                    float attackRange = pc.stats.GetAttackRange();
                    if (distance - additionalHitRange > attackRange)
                    {
                        string distStr = TileCoordinateSystem.GetDistanceText(pc.transform.position, target.transform.position);
                        string rangeStr = TileCoordinateSystem.GetRangeText(attackRange);
                        ScreenReaderManager.SpeakInterrupt(
                            targetName + " is out of range. Distance " + distStr +
                            ", weapon range " + rangeStr);
                        return;
                    }
                }

                // Set the InputManager's selected targetable
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                inputManager.selectedTargetable = target;

                // Publish attack command
                EventInfo_CommandAttack attackEvent = ObjectPool.Get<EventInfo_CommandAttack>();
                attackEvent.pc = pc;
                attackEvent.target = target;
                attackEvent.meleeMoveToRange = true;

                if (weaponTemplate is ItemTemplate_WeaponShotgun)
                {
                    attackEvent.coneAttack = true;
                    attackEvent.aimDirection = target.transform.position;
                    attackEvent.coneTargets = new Targetable[] { target };
                }

                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(attackEvent);

                string weapon = UITextExtractor.CleanText(
                    Language.Localize(weaponTemplate.displayName, false, false, string.Empty));
                string modeInfo = "";
                if (rangedWeapon != null)
                    modeInfo = ", " + rangedWeapon.GetFiringModeName();

                // Include hit/crit chance in the attack announcement
                string chanceInfo = "";
                try
                {
                    int hitChance = Mathf.Clamp(pc.GetChanceToHit(target, false), 0, 100);
                    int critChance = Mathf.Clamp(pc.GetChanceToCriticalHit(target), 0, 100);
                    chanceInfo = ", " + hitChance + "% hit";
                    if (critChance > 0)
                        chanceInfo += ", " + critChance + "% crit";
                }
                catch (Exception) { }

                string verb = willMoveAndAttack ? "Moving to attack " : "Attacking ";
                ScreenReaderManager.SpeakInterrupt(verb + targetName + " with " + weapon + modeInfo + chanceInfo);
                MelonLogger.Msg("[MapCursorState] Attack command: " + verb + targetName + " with " + weapon + modeInfo + chanceInfo);

                // Clear attack ASI
                UseASIManager.SetActiveASIName(null);
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[MapCursorState] Error in PerformFreeAimAttack: " + ex.Message);
                ScreenReaderManager.SpeakInterrupt("Attack failed");
            }
        }

        // Mirrors AIBehaviour_PC.FindAttackDestination (AIBehaviour_PC.cs:389): walks
        // the navmesh path from the PC toward the target's nearest edge and reports
        // whether any corner yields a position from which CanAttack succeeds. Used to
        // decide whether vanilla's melee walk-and-attack fallback would fire.
        private static bool TryFindAttackDestination(PC pc, Targetable target)
        {
            try
            {
                if (pc == null || target == null || pc.navMeshAgent == null) return false;

                Vector3 sourcePosition = target.transform.position;
                if (target is TargetableObject)
                {
                    sourcePosition = (target as TargetableObject).GetNearestEdge(pc.transform.position);
                }

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(sourcePosition, out hit, 5f, 1 << InputManager.navMeshLayerIndex_Default))
                    return false;

                NavMeshPath path = new NavMeshPath();
                pc.navMeshAgent.CalculatePath(hit.position, path);
                if (path.corners == null || path.corners.Length <= 0) return false;

                float backoff = pc.stats.GetAttackRange() * 0.75f;
                for (int i = 1; i < path.corners.Length; i++)
                {
                    Vector3 corner = path.corners[i];
                    Vector3 dir = (path.corners[i] - path.corners[i - 1]).normalized;
                    Vector3 position = corner - dir * backoff;
                    if (pc.CanAttack(target, position, false, false))
                        return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // --- Party Member Info ---

        private PC FindPCOnTile()
        {
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                if (mob is PC && mob.mobState != Mob.MobState.DEAD)
                    return mob as PC;
            }
            return null;
        }

        private List<PC> FindPCsOnTile()
        {
            var result = new List<PC>();
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                if (mob is PC && mob.mobState != Mob.MobState.DEAD)
                    result.Add(mob as PC);
            }
            return result;
        }

        private void OpenPartyMemberInfo(PC pc)
        {
            partyInfoLines.Clear();
            BuildPartyMemberInfo(pc);

            if (partyInfoLines.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt(GetMobName(pc) + ", no info available");
                return;
            }

            browsingPartyInfo = true;
            partyInfoIndex = 0;

            string header = GetMobName(pc) + " info, " + partyInfoLines.Count + " items";
            ScreenReaderManager.SpeakInterrupt(header + ". " + FormatPartyInfoLine(0));
        }

        private void BuildPartyMemberInfo(PC pc)
        {
            // --- Health ---
            float maxHP = pc.stats.GetMaxHP();
            float hpPercent = maxHP > 0 ? (pc.curHP / maxHP) * 100f : 0;
            string healthLine = "Health: " + Mathf.RoundToInt(pc.curHP) + " of " + Mathf.RoundToInt(maxHP)
                + " (" + hpPercent.ToString("F0") + "%)";

            if (pc.healthState != PC.HealthState.Healthy)
                healthLine += ", " + pc.healthState.ToString();

            partyInfoLines.Add(healthLine);

            // --- Level / XP ---
            try
            {
                string xpLine = CharacterAnnouncementHelper.BuildXPAnnouncement(pc);
                if (!string.IsNullOrEmpty(xpLine))
                    partyInfoLines.Add(xpLine);
            }
            catch { }

            // --- Weapon ---
            try
            {
                var weaponInstance = pc.pcStats.GetWeaponInstance();
                if (weaponInstance != null && weaponInstance.template != null)
                {
                    string weaponName = UITextExtractor.CleanText(
                        Language.Localize(weaponInstance.template.displayName, false, false, string.Empty));
                    string weaponLine = "Weapon: " + weaponName;

                    var ranged = weaponInstance as ItemInstance_WeaponRanged;
                    if (ranged != null)
                    {
                        weaponLine += ", " + ranged.GetAmmoCount() + " of " + ranged.GetClipSize() + " ammo";
                    }
                    partyInfoLines.Add(weaponLine);
                }
            }
            catch { }

            // --- Status Effects ---
            try
            {
                if (pc.template != null && pc.template.statusEffects != null
                    && pc.template.statusEffects.Count > 0)
                {
                    foreach (var effect in pc.template.statusEffects)
                    {
                        string line = Helpers.StatusEffectHelper.BuildEffectLine(effect);
                        if (!string.IsNullOrEmpty(line))
                            partyInfoLines.Add(line);
                    }
                }
            }
            catch { }
        }

        private string FormatPartyInfoLine(int index)
        {
            if (index < 0 || index >= partyInfoLines.Count) return "";
            return partyInfoLines[index] + ", " + (index + 1) + " of " + partyInfoLines.Count;
        }

        // --- Helpers ---

        private PC GetPartyLeader()
        {
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                var pc = MonoBehaviourSingleton<InputManager>.GetInstance().GetFirstSelectedPlayer();
                if (pc != null) return pc;
            }

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null && party.Count > 0)
                {
                    return party[0];
                }
            }

            return null;
        }

        private string GetPCDisplayName(PC pc)
        {
            if (pc == null) return "Unknown";
            if (pc.template != null && !string.IsNullOrEmpty(pc.template.displayName))
                return pc.template.displayName;
            if (pc.pcTemplate != null && !string.IsNullOrEmpty(pc.pcTemplate.displayName))
                return pc.pcTemplate.displayName;
            return pc.name ?? "Unknown";
        }

        private void SuppressInput()
        {
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
        }
    }
}
