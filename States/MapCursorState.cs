using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

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

        // Layer masks for floor detection raycasting
        private static int floorLayerMask = -1;

        // Context menu option
        private struct ContextMenuOption
        {
            public string DisplayName;
            public string ASIName; // null = "Poked" (normal interact), "move" = move to
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
            if (!cursorInitialized)
            {
                InitializeToPartyPosition();
                if (!cursorInitialized) return false;
            }

            // --- Context menu mode ---
            if (contextMenuActive)
            {
                return HandleContextMenuInput();
            }

            // --- Normal cursor mode ---

            // Arrow key movement - grid-aligned cardinal directions
            float currentTime = Time.time;
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

            // Space for detailed scan of current tile
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AnnounceCurrentTile(detailed: true);
                return true;
            }

            // Enter to open context menu for objects on tile
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OpenContextMenu();
                return true;
            }

            // ] to move party to cursor position
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                MovePartyToCursor();
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
            parts.Add(coords);

            // Objects on this tile
            var interactables = FindInteractablesOnTile();
            var mobs = FindMobsOnTile();

            foreach (var mob in mobs)
            {
                string mobName = GetMobName(mob);
                if (!string.IsNullOrEmpty(mobName))
                    parts.Add(mobName);
            }

            foreach (var interactable in interactables)
            {
                string name = GetInteractableName(interactable);
                if (!string.IsNullOrEmpty(name))
                    parts.Add(name);
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
            else
            {
                // No grid node here — identify what's at this position
                string obstruction = IdentifyObstruction(cursorPosition);
                if (!string.IsNullOrEmpty(obstruction))
                    parts.Add(obstruction);
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

        private List<InteractableNexus> FindInteractablesOnTile()
        {
            List<InteractableNexus> onTile = new List<InteractableNexus>();

            foreach (var interactable in InteractableNexus.interactables)
            {
                if (interactable == null || !interactable.isVisible) continue;
                if (interactable.isPC) continue;

                if (IsOnCurrentTile(interactable.transform.position))
                {
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

                        if (IsOnCurrentTile(npc.transform.position))
                        {
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

                        if (IsOnCurrentTile(follower.transform.position))
                        {
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
                    // Skip mobs — already covered by FindMobsOnTile
                    if (mob is PC || mob is NPC) return null;
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

        private void BuildActionMenu(InteractableNexus target)
        {
            contextMenuOptions.Clear();
            string targetName = GetInteractableName(target) ?? "Object";

            if (target.drama != null)
            {
                var interactions = target.drama.GetAllowedInteractions();
                if (interactions != null)
                {
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
                    foreach (var kvp in interactions)
                    {
                        if (kvp.Key == "Poked") continue;
                        if (kvp.Value != 1) continue; // Only available skills

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

            string name = GetInteractableName(target) ?? "Object";

            // Handle no-Drama examine objects (difficulty None) via CheckExamineDrama
            // These are simple description objects with no skill requirement
            if (asiName == "examine" && target.skobExamine != null &&
                target.drama == null &&
                target.skobExamine.difficulty == SkillLevelCategory.None)
            {
                MelonLogger.Msg($"[MapCursorState] Examining (no difficulty): {name}");
                ScreenReaderManager.SpeakInterrupt("Examining " + name);

                if (MonoBehaviourSingleton<InputManager>.HasInstance())
                {
                    MonoBehaviourSingleton<InputManager>.GetInstance().CheckExamineDrama(target.transform);
                }
                return;
            }

            if (target.drama == null) return;

            // Set the active ASI if this is a skill interaction
            if (!string.IsNullOrEmpty(asiName))
            {
                UseASIManager.SetActiveASIName(asiName);
            }

            string displayAction;
            if (string.IsNullOrEmpty(asiName))
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

            // Trigger the game's interaction system — PC will walk to object and interact
            // This respects distance (PC must walk there), skill checks, etc.
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

            // Jump cursor to the selected interactable's grid position
            CombatAStarNode targetNode = FindNodeAtPosition(worldPos);
            if (targetNode != null)
            {
                cursorGridId = targetNode.id;
                cursorPosition = targetNode.position;
            }
            else
            {
                // No grid node — compute grid ID from world position
                int gridX = Mathf.RoundToInt(worldPos.x / GRID_SQUARE_SIZE);
                int gridZ = Mathf.RoundToInt(worldPos.z / GRID_SQUARE_SIZE);
                cursorGridId = new Vector3(gridX, 0, gridZ);
                cursorPosition = new Vector3(gridX * GRID_SQUARE_SIZE, worldPos.y, gridZ * GRID_SQUARE_SIZE);
            }

            // Verify the interactable is actually on the tile we landed on.
            // Objects near tile boundaries can round to the wrong grid cell.
            if (!IsOnCurrentTile(worldPos))
            {
                // Snap directly to the interactable's position instead
                int gridX = Mathf.FloorToInt(worldPos.x / GRID_SQUARE_SIZE + 0.5f);
                int gridZ = Mathf.FloorToInt(worldPos.z / GRID_SQUARE_SIZE + 0.5f);

                // Try adjacent tiles to find one that contains the object
                int bestX = gridX, bestZ = gridZ;
                float bestDist = float.MaxValue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        float tileX = (gridX + dx) * GRID_SQUARE_SIZE;
                        float tileZ = (gridZ + dz) * GRID_SQUARE_SIZE;
                        float dist = (worldPos.x - tileX) * (worldPos.x - tileX) +
                                     (worldPos.z - tileZ) * (worldPos.z - tileZ);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestX = gridX + dx;
                            bestZ = gridZ + dz;
                        }
                    }
                }

                MelonLogger.Msg($"[MapCursorState] Jump correction: object at ({worldPos.x:F2}, {worldPos.z:F2}) " +
                    $"was on tile ({cursorGridId.x}, {cursorGridId.z}), moved to ({bestX}, {bestZ})");

                cursorGridId = new Vector3(bestX, cursorGridId.y, bestZ);
                cursorPosition = new Vector3(bestX * GRID_SQUARE_SIZE, cursorPosition.y, bestZ * GRID_SQUARE_SIZE);
            }

            SnapCameraToCursor();
            AnnounceCurrentTile(detailed: false);
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
