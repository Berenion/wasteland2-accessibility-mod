using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Accessibility state for combat. Provides a grid preview cursor that is
    /// always active during combat, plus an initiative tracker (T key).
    /// Arrow keys move one grid cell at a time reporting tile contents.
    /// Priority 45 - above MapCursorState(30) but below menu states(50+).
    /// </summary>
    public class CombatState : IAccessibilityState
    {
        public string Name => "Combat";
        public int Priority => 45;

        // Reflection cache
        private static FieldInfo curActorField;
        private static FieldInfo combatMapField;
        private static FieldInfo fullMapField;

        // --- Preview cursor state ---
        private Dictionary<Vector3, CombatAStarNode> combatMap;
        private Dictionary<Vector3, CombatAStarNode> fullMap;
        private Vector3 cursorGridId;
        private Vector3 cursorPosition;
        private bool cursorInitialized = false;
        private Mob lastTrackedActor = null;

        // Movement settings
        private const float MOVE_REPEAT_DELAY = 0.25f;
        private float lastMoveTime = 0f;

        // Grid settings (must match CombatAStar.squareSize)
        private const float GRID_SQUARE_SIZE = 1.6f;
        private const float TILE_MATCH_RADIUS = GRID_SQUARE_SIZE * 0.75f;

        // Camera follow
        private bool cameraFollowsCursor = true;

        // --- Combatant cycling (PageUp/PageDown) ---
        private List<Mob> combatantList = new List<Mob>();
        private int combatantIndex = -1;
        private int combatantCategory = 0; // 0=All, 1=Enemies, 2=Allies
        private static readonly string[] COMBATANT_CATEGORIES = { "All", "Enemies", "Allies" };

        // Cover direction names (indices 0-3: forward/right/back/left = N/E/S/W)
        private static readonly string[] COVER_DIRECTIONS = { "north", "east", "south", "west" };

        // Cardinal direction vectors for arrow keys
        private static readonly Vector3[] CARDINAL_DIRECTIONS =
        {
            Vector3.forward,  // Up arrow = North (+Z)
            Vector3.right,    // Right arrow = East (+X)
            Vector3.back,     // Down arrow = South (-Z)
            Vector3.left      // Left arrow = West (-X)
        };

        // Initiative browsing state
        private bool browsingInitiative = false;
        private int initiativeIndex = 0;
        private List<InitiativeEntry> initiativeList = new List<InitiativeEntry>();

        private class InitiativeEntry
        {
            public string Name;
            public bool IsHostile;
            public bool IsCurrentActor;
            public Mob Mob;
            public string Details;
        }

        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return false;
                if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return false;

                // Don't intercept input if a menu/dialog is open over combat
                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
                    return false;
                // Note: Do NOT check Drama.isConversationOn here — combat barks set it
                // true during combat, which would disable us. ConversationState (priority 50)
                // handles real conversations above us already.

                return true;
            }
        }

        public bool HandleInput()
        {
            // Ensure cursor is ready
            EnsureCursorReady();

            // T key: open/refresh initiative tracker (turn order)
            // T is mapped to "Center On Character" in cInput, so we must suppress game input
            if (Input.GetKeyDown(KeyCode.T))
            {
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (browsingInitiative)
                {
                    ExitInitiativeBrowse();
                }
                else
                {
                    OpenInitiativeTracker();
                }
                return true;
            }

            // While browsing initiative list, arrow keys cycle entries
            if (browsingInitiative)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    CycleInitiativeForward();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    CycleInitiativeBackward();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitInitiativeBrowse();
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                return true;
            }

            // --- Preview cursor: always active ---
            if (!cursorInitialized) return false;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            // Ctrl+PageDown/PageUp: switch combatant category
            if (ctrl && Input.GetKeyDown(KeyCode.PageDown))
            {
                NextCombatantCategory();
                SuppressInput();
                return true;
            }
            if (ctrl && Input.GetKeyDown(KeyCode.PageUp))
            {
                PreviousCombatantCategory();
                SuppressInput();
                return true;
            }

            // PageDown/PageUp: cycle combatants
            if (Input.GetKeyDown(KeyCode.PageDown))
            {
                CycleNextCombatant();
                SuppressInput();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.PageUp))
            {
                CyclePreviousCombatant();
                SuppressInput();
                return true;
            }

            // Arrow key movement
            float currentTime = Time.time;
            bool canMove = (currentTime - lastMoveTime) >= MOVE_REPEAT_DELAY;

            if (canMove)
            {
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    MoveCursor(0);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    MoveCursor(1);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    MoveCursor(2);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    MoveCursor(3);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
            }

            // Backslash: detailed tile announcement
            // (Space is "End Turn" in combat, so we avoid it)
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                AnnounceTile(detailed: true);
                SuppressInput();
                return true;
            }

            // Home: jump cursor to current actor
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpToCurrentActor();
                SuppressInput();
                return true;
            }

            // Semicolon: toggle camera follow
            // (F is "Headshot/Precision Shot" in combat, so we avoid it)
            if (Input.GetKeyDown(KeyCode.Semicolon))
            {
                cameraFollowsCursor = !cameraFollowsCursor;
                ScreenReaderManager.SpeakInterrupt(cameraFollowsCursor
                    ? "Camera follows cursor" : "Camera stationary");
                return true;
            }

            // Suppress arrow keys even during repeat delay
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                SuppressInput();
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[CombatState] Activated - combat started");
            cursorInitialized = false;
            lastTrackedActor = null;
            EnsureCursorReady();
        }

        public void OnDeactivated()
        {
            browsingInitiative = false;
            initiativeList.Clear();
            cursorInitialized = false;
            combatMap = null;
            fullMap = null;
            lastTrackedActor = null;
            combatantList.Clear();
            combatantIndex = -1;
            combatantCategory = 0;
            MelonLogger.Msg("[CombatState] Deactivated - combat ended");
        }

        // =====================================================================
        // Preview Cursor
        // =====================================================================

        /// <summary>
        /// Ensures the combat grid is loaded and the cursor is positioned.
        /// Auto-jumps to the current actor when the active actor changes.
        /// </summary>
        private void EnsureCursorReady()
        {
            if (!TryEnsureCombatMap()) return;

            Mob actor = GetCurrentActor();

            // If the actor changed (new turn), jump cursor to them
            if (actor != null && actor != lastTrackedActor)
            {
                lastTrackedActor = actor;
                InitializeCursorToMob(actor);
            }

            // Fallback: if cursor was never initialized, try current actor
            if (!cursorInitialized && actor != null)
            {
                InitializeCursorToMob(actor);
            }
        }

        private bool TryEnsureCombatMap()
        {
            if (!MonoBehaviourSingleton<CombatAStar>.HasInstance()) return false;
            var combatAStar = MonoBehaviourSingleton<CombatAStar>.GetInstance();

            if (combatMapField == null)
            {
                combatMapField = typeof(CombatAStar).GetField("map",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (combatMapField == null)
                {
                    MelonLogger.Error("[CombatState] Could not find CombatAStar.map via reflection");
                    return false;
                }
            }

            if (fullMapField == null)
            {
                fullMapField = typeof(CombatAStar).GetField("fullMap",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            var map = combatMapField.GetValue(combatAStar) as Dictionary<Vector3, CombatAStarNode>;
            if (map == null || map.Count == 0) return false;

            if (map != combatMap)
            {
                combatMap = map;
                fullMap = fullMapField?.GetValue(combatAStar) as Dictionary<Vector3, CombatAStarNode>;
                cursorInitialized = false;
                MelonLogger.Msg($"[CombatState] Combat grid loaded: {combatMap.Count} nodes");
            }

            return true;
        }

        private void InitializeCursorToMob(Mob mob)
        {
            if (combatMap == null) return;

            CombatAStarNode node = mob.currentSquare;
            if (node == null)
            {
                // Fallback: find nearest node to mob position
                node = FindNearestNode(mob.transform.position);
            }

            if (node != null)
            {
                cursorGridId = node.id;
                cursorPosition = node.position;
                cursorInitialized = true;
                MelonLogger.Msg($"[CombatState] Cursor initialized to {GetMobName(mob)} at grid {cursorGridId}");
            }
        }

        private void JumpToCurrentActor()
        {
            Mob actor = GetCurrentActor();
            if (actor == null)
            {
                ScreenReaderManager.SpeakInterrupt("No active actor");
                return;
            }

            InitializeCursorToMob(actor);
            if (cameraFollowsCursor) SnapCameraToCursor();
            AnnounceTile(detailed: false, prefix: GetMobName(actor));
        }

        // --- Grid Navigation ---

        private void MoveCursor(int directionIndex)
        {
            Vector3 direction = CARDINAL_DIRECTIONS[directionIndex];

            Vector3 newGridId = new Vector3(
                cursorGridId.x + direction.x,
                cursorGridId.y,
                cursorGridId.z + direction.z);

            CombatAStarNode node = GetNodeAtGridId(newGridId);
            if (node != null)
            {
                cursorGridId = node.id;
                cursorPosition = node.position;
                if (cameraFollowsCursor) SnapCameraToCursor();
                AnnounceTile(detailed: false);
                return;
            }

            // No combat node — determine why
            // Check if the tile exists in fullMap (walkable outside combat bounds)
            CombatAStarNode fullMapNode = GetNodeInFullMap(newGridId);
            if (fullMapNode != null)
            {
                // Tile exists in the world but is outside the combat boundary
                ScreenReaderManager.SpeakInterrupt("Edge of combat area");
                return;
            }

            // Not in fullMap either — there's an obstacle at this position
            // Compute the world position and identify what's there
            Vector3 blockedWorldPos = new Vector3(
                newGridId.x * GRID_SQUARE_SIZE,
                cursorPosition.y,
                newGridId.z * GRID_SQUARE_SIZE);
            string obstruction = IdentifyObstruction(blockedWorldPos);
            ScreenReaderManager.SpeakInterrupt(obstruction);
        }

        private CombatAStarNode GetNodeAtGridId(Vector3 gridId)
        {
            if (combatMap == null) return null;

            // Try exact ID
            CombatAStarNode node;
            if (combatMap.TryGetValue(gridId, out node))
                return node;

            // Try other floor levels at same X,Z
            for (int f = 0; f <= 5; f++)
            {
                if (f == (int)gridId.y) continue;
                Vector3 floorId = new Vector3(gridId.x, f, gridId.z);
                if (combatMap.TryGetValue(floorId, out node))
                    return node;
            }

            return null;
        }

        private CombatAStarNode FindNearestNode(Vector3 worldPos)
        {
            if (combatMap == null) return null;

            float bestDist = float.MaxValue;
            CombatAStarNode bestNode = null;
            foreach (var kvp in combatMap)
            {
                float dx = worldPos.x - kvp.Value.position.x;
                float dz = worldPos.z - kvp.Value.position.z;
                float dist = dx * dx + dz * dz;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestNode = kvp.Value;
                }
            }
            return bestNode;
        }

        /// <summary>
        /// Checks whether a grid ID exists in the full scene map (outside combat bounds).
        /// </summary>
        private CombatAStarNode GetNodeInFullMap(Vector3 gridId)
        {
            if (fullMap == null) return null;

            CombatAStarNode node;
            if (fullMap.TryGetValue(gridId, out node))
                return node;

            for (int f = 0; f <= 5; f++)
            {
                if (f == (int)gridId.y) continue;
                Vector3 floorId = new Vector3(gridId.x, f, gridId.z);
                if (fullMap.TryGetValue(floorId, out node))
                    return node;
            }

            return null;
        }

        // --- Obstruction Detection ---

        /// <summary>
        /// Raycasts to identify what is blocking a grid position where no
        /// walkable node exists.
        /// </summary>
        private string IdentifyObstruction(Vector3 worldPos)
        {
            int obstructionMask = InputManager.layerMask_Wall
                                | InputManager.layerMask_Cover
                                | InputManager.layerMask_FadedWall
                                | InputManager.layerMask_StaticMeshes
                                | InputManager.layerMask_DynamicObject
                                | InputManager.layerMask_Terrain
                                | InputManager.layerMask_Floor
                                | InputManager.layerMask_FadedFloor
                                | InputManager.layerMask_Default;

            RaycastHit hit;

            // Raycast down from above
            if (Physics.Raycast(worldPos + Vector3.up * 10f, Vector3.down, out hit, 20f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            // Sphere cast to catch objects a thin ray might miss
            if (Physics.SphereCast(worldPos + Vector3.up * 5f, 0.5f, Vector3.down, out hit, 10f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            // Horizontal raycast from current tile toward the blocked position
            Vector3 horizontalDir = (worldPos - cursorPosition).normalized;
            if (horizontalDir.sqrMagnitude > 0.01f &&
                Physics.Raycast(cursorPosition + Vector3.up * 0.5f, horizontalDir,
                    out hit, GRID_SQUARE_SIZE * 1.5f, obstructionMask))
            {
                return DescribeHitObject(hit);
            }

            return "Impassable";
        }

        private string DescribeHitObject(RaycastHit hit)
        {
            string layerName = LayerMask.LayerToName(hit.transform.gameObject.layer);
            string objectName = GetMeaningfulName(hit.transform);

            if (layerName == "Wall" || layerName == "FadedWall")
            {
                if (!string.IsNullOrEmpty(objectName))
                    return "Wall, " + objectName;
                return "Wall";
            }

            if (layerName == "Cover")
            {
                if (!string.IsNullOrEmpty(objectName))
                    return "Cover object, " + objectName;
                return "Cover object";
            }

            if (layerName == "Terrain")
                return "Terrain";

            if (!string.IsNullOrEmpty(objectName))
                return objectName;

            if (!string.IsNullOrEmpty(layerName))
                return layerName;

            return "Impassable";
        }

        /// <summary>
        /// Walks up the transform hierarchy to find a meaningful name,
        /// skipping generic names like "Collider", "Mesh", etc.
        /// </summary>
        private string GetMeaningfulName(Transform trans)
        {
            Transform current = trans;
            while (current != null)
            {
                string name = current.gameObject.name;
                if (!string.IsNullOrEmpty(name))
                {
                    string lower = name.ToLower();
                    if (lower.Contains("collider") || lower.Contains("mesh") ||
                        lower.Contains("trigger") || lower.Contains("blocker") ||
                        lower == "default" || lower.StartsWith("cube") ||
                        lower.StartsWith("plane") || lower.StartsWith("quad"))
                    {
                        current = current.parent;
                        continue;
                    }

                    name = name.Replace("_", " ").Replace("(Clone)", "").Trim();
                    if (name.Length > 0)
                        return name;
                }
                current = current.parent;
            }
            return null;
        }

        // --- Tile Announcements ---

        private void AnnounceTile(bool detailed, string prefix = null)
        {
            CombatAStarNode node = GetNodeAtGridId(cursorGridId);
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(prefix))
                parts.Add(prefix);

            // Grid coordinates
            string coords = (int)cursorGridId.x + ", " + (int)cursorGridId.z;
            if (cursorGridId.y > 0)
                coords += ", floor " + ((int)cursorGridId.y + 1);
            parts.Add(coords);

            // Occupants on this tile
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                parts.Add(FormatMobForTile(mob));
            }

            // Tile occupant from the node itself (if not already found)
            if (node != null && node.occupant != null && mobs.Count == 0)
            {
                string occupantName = GetTargetableName(node.occupant);
                if (!string.IsNullOrEmpty(occupantName))
                    parts.Add(occupantName);
            }

            if (node != null)
            {
                // Cover info (always shown — critical for combat decisions)
                string cover = GetCoverDescription(node);
                if (!string.IsNullOrEmpty(cover))
                    parts.Add(cover);

                // Linked nodes (doors/ladders)
                if (node.linkedNeighbor != null)
                {
                    string linkType = node.linkedNodeType == CombatAStarNode.LinkedNodeType.Door
                        ? "Door" : "Ladder";
                    parts.Add(linkType);
                }
            }

            // Distance and direction from current actor
            Mob actor = GetCurrentActor();
            if (actor != null && actor.currentSquare != null)
            {
                Vector3 actorId = actor.currentSquare.id;
                int tileDistX = Mathf.Abs((int)cursorGridId.x - (int)actorId.x);
                int tileDistZ = Mathf.Abs((int)cursorGridId.z - (int)actorId.z);
                int tileDist = Mathf.Max(tileDistX, tileDistZ);

                if (tileDist > 0)
                {
                    string direction = DirectionHelper.GetDirectionDescription(
                        actor.currentSquare.position, cursorPosition);
                    parts.Add(tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction);
                }
            }

            // Detailed mode: movement AP cost and attack range info
            if (detailed)
            {
                string movementInfo = GetMovementCostInfo(node, actor);
                if (!string.IsNullOrEmpty(movementInfo))
                    parts.Add(movementInfo);

                // Weapon range info for current actor
                if (actor != null)
                {
                    float distance = Vector3.Distance(cursorPosition, actor.transform.position);
                    string rangeInfo = GetWeaponRangeInfo(actor, distance);
                    if (!string.IsNullOrEmpty(rangeInfo))
                        parts.Add(rangeInfo);
                }
            }

            string announcement = string.Join(", ", parts.ToArray());
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private string FormatMobForTile(Mob mob)
        {
            var parts = new List<string>();
            string name = GetMobName(mob);
            parts.Add(name);

            // Faction
            if (mob is PC)
                parts.Add("party");
            else if (mob.HatesParty())
                parts.Add("hostile");
            else
                parts.Add("friendly");

            // HP
            float maxHP = mob.stats.GetMaxHP();
            if (maxHP > 0)
            {
                float hpPercent = (mob.curHP / maxHP) * 100f;
                parts.Add(hpPercent.ToString("F0") + "% HP");
            }

            // State
            if (mob.mobState == Mob.MobState.UNCONSCIOUS)
                parts.Add("unconscious");
            if (mob.inCover)
                parts.Add(mob.coverType == Cover.CoverType.Tall ? "tall cover" : "short cover");
            if (mob.isCrouching)
                parts.Add("crouching");
            if (mob.isHidden)
                parts.Add("hidden");

            return string.Join(" ", parts.ToArray());
        }

        private string GetCoverDescription(CombatAStarNode node)
        {
            if (node == null || node.cover == null) return null;

            var coverDirs = new List<string>();
            for (int i = 0; i < node.cover.Length && i < COVER_DIRECTIONS.Length; i++)
            {
                if (node.cover[i])
                    coverDirs.Add(COVER_DIRECTIONS[i]);
            }

            if (coverDirs.Count == 0) return null;
            return "Cover: " + string.Join(", ", coverDirs.ToArray());
        }

        private string GetMovementCostInfo(CombatAStarNode targetNode, Mob actor)
        {
            if (targetNode == null || actor == null) return null;
            if (actor.currentSquare == null) return null;
            if (actor.currentSquare == targetNode) return "Current position";

            // Only show movement cost during player's turn
            if (!MonoBehaviourSingleton<CombatManager>.GetInstance().isPlayersTurn) return null;

            try
            {
                var combatAStar = MonoBehaviourSingleton<CombatAStar>.GetInstance();
                float combatSpeed = actor.stats.GetCombatSpeed();
                if (combatSpeed <= 0) return "Cannot move";

                // Use the game's pathfinding to get AP cost
                int apCost = combatAStar.GetPathCost(combatSpeed,
                    actor.currentSquare.position, targetNode.position,
                    actor.CanUseLadders());

                if (apCost < 0) return "No path";

                // Include stance cost if crouching
                int stanceCost = 0;
                if (actor.isCrouching)
                    stanceCost = actor.stats.GetActionPointsToChangeStance();
                int totalCost = apCost + stanceCost;

                string result = totalCost + " AP to move";
                if (totalCost <= actor.combatActionPointsRemaining)
                {
                    int apAfter = actor.combatActionPointsRemaining - totalCost;
                    int attackCost = actor.stats.GetActionPointsToAttack();
                    if (apAfter >= attackCost)
                        result += ", can still attack";
                    else
                        result += ", cannot attack after";
                }
                else
                {
                    result += ", not enough AP";
                }

                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] GetMovementCostInfo error: {ex.Message}");
                return null;
            }
        }

        private string GetWeaponRangeInfo(Mob actor, float distance)
        {
            try
            {
                float pointBlank = actor.stats.GetPointBlankRange();
                float optimal = actor.stats.GetOptimalRange();
                float maxRange = actor.stats.GetAttackRange();

                if (maxRange <= 0) return null;

                if (distance <= pointBlank)
                    return "Point blank range";
                else if (distance <= optimal)
                    return "Optimal range";
                else if (distance <= maxRange)
                    return "Long range";
                else
                    return "Out of range";
            }
            catch
            {
                return null;
            }
        }

        // --- Object Finding ---

        private bool IsOnCurrentTile(Vector3 worldPos)
        {
            float dx = Mathf.Abs(worldPos.x - cursorPosition.x);
            float dz = Mathf.Abs(worldPos.z - cursorPosition.z);
            return dx <= TILE_MATCH_RADIUS && dz <= TILE_MATCH_RADIUS;
        }

        private List<Mob> FindMobsOnTile()
        {
            var onTile = new List<Mob>();
            if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return onTile;

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm.mobs == null) return onTile;

            foreach (var mob in cm.mobs)
            {
                if (mob == null || mob.gameObject == null) continue;
                if (!mob.gameObject.activeInHierarchy) continue;
                if (mob.mobState == Mob.MobState.DEAD) continue;

                if (IsOnCurrentTile(mob.transform.position))
                    onTile.Add(mob);
            }

            return onTile;
        }

        // =====================================================================
        // Combatant Cycling (PageUp/PageDown)
        // =====================================================================

        /// <summary>
        /// Builds the combatant list filtered by category, sorted by distance
        /// from the current actor.
        /// </summary>
        private void BuildCombatantList()
        {
            combatantList.Clear();

            if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return;
            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm.mobs == null) return;

            Mob actor = GetCurrentActor();
            Vector3 origin = actor != null ? actor.transform.position : cursorPosition;

            foreach (var mob in cm.mobs)
            {
                if (mob == null || mob.gameObject == null) continue;
                if (!mob.gameObject.activeInHierarchy) continue;
                if (mob.mobState == Mob.MobState.DEAD) continue;

                // Filter by category
                bool isEnemy = mob.HatesParty();
                bool isAlly = mob is PC || !isEnemy;

                if (combatantCategory == 1 && !isEnemy) continue; // Enemies only
                if (combatantCategory == 2 && !isAlly) continue;  // Allies only

                combatantList.Add(mob);
            }

            // Sort by distance from origin
            combatantList.Sort((a, b) =>
            {
                float distA = Vector3.Distance(origin, a.transform.position);
                float distB = Vector3.Distance(origin, b.transform.position);
                return distA.CompareTo(distB);
            });
        }

        private void CycleNextCombatant()
        {
            BuildCombatantList();
            if (combatantList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No " + COMBATANT_CATEGORIES[combatantCategory].ToLower() + " found");
                return;
            }

            combatantIndex = (combatantIndex + 1) % combatantList.Count;
            JumpToCombatant(combatantList[combatantIndex]);
        }

        private void CyclePreviousCombatant()
        {
            BuildCombatantList();
            if (combatantList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No " + COMBATANT_CATEGORIES[combatantCategory].ToLower() + " found");
                return;
            }

            combatantIndex--;
            if (combatantIndex < 0) combatantIndex = combatantList.Count - 1;
            JumpToCombatant(combatantList[combatantIndex]);
        }

        private void JumpToCombatant(Mob mob)
        {
            // Move cursor to this mob's tile
            CombatAStarNode node = mob.currentSquare;
            if (node == null)
                node = FindNearestNode(mob.transform.position);

            if (node != null)
            {
                cursorGridId = node.id;
                cursorPosition = node.position;
            }

            if (cameraFollowsCursor) SnapCameraToCursor();

            // Announce with full combat details
            string announcement = FormatMobForCycle(mob);
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private string FormatMobForCycle(Mob mob)
        {
            var parts = new List<string>();

            // Position in list
            parts.Add((combatantIndex + 1) + " of " + combatantList.Count);

            // Name and faction
            string name = GetMobName(mob);
            parts.Add(name);

            if (mob is PC)
                parts.Add("party");
            else if (mob.HatesParty())
                parts.Add("hostile");
            else
                parts.Add("friendly");

            // HP
            float maxHP = mob.stats.GetMaxHP();
            if (maxHP > 0)
            {
                float hpPercent = (mob.curHP / maxHP) * 100f;
                parts.Add(hpPercent.ToString("F0") + "% HP");
            }

            // State
            if (mob.mobState == Mob.MobState.UNCONSCIOUS)
                parts.Add("unconscious");
            if (mob.inCover)
                parts.Add(mob.coverType == Cover.CoverType.Tall ? "tall cover" : "short cover");
            if (mob.isCrouching)
                parts.Add("crouching");
            if (mob.isHidden)
                parts.Add("hidden");

            // Distance and direction from current actor
            Mob actor = GetCurrentActor();
            if (actor != null && actor != mob)
            {
                float distance = Vector3.Distance(actor.transform.position, mob.transform.position);
                int tileDist = 0;
                if (actor.currentSquare != null && mob.currentSquare != null)
                {
                    int dx = Mathf.Abs((int)actor.currentSquare.id.x - (int)mob.currentSquare.id.x);
                    int dz = Mathf.Abs((int)actor.currentSquare.id.z - (int)mob.currentSquare.id.z);
                    tileDist = Mathf.Max(dx, dz);
                }
                string direction = DirectionHelper.GetDirectionDescription(
                    actor.transform.position, mob.transform.position);
                parts.Add(tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction);

                // Weapon range assessment
                string rangeInfo = GetWeaponRangeInfo(actor, distance);
                if (!string.IsNullOrEmpty(rangeInfo))
                    parts.Add(rangeInfo);
            }

            return string.Join(", ", parts.ToArray());
        }

        private void NextCombatantCategory()
        {
            combatantCategory = (combatantCategory + 1) % COMBATANT_CATEGORIES.Length;
            combatantIndex = -1;
            BuildCombatantList();
            string count = combatantList.Count.ToString();
            ScreenReaderManager.SpeakInterrupt(COMBATANT_CATEGORIES[combatantCategory] + ", " + count + " found");
        }

        private void PreviousCombatantCategory()
        {
            combatantCategory--;
            if (combatantCategory < 0) combatantCategory = COMBATANT_CATEGORIES.Length - 1;
            combatantIndex = -1;
            BuildCombatantList();
            string count = combatantList.Count.ToString();
            ScreenReaderManager.SpeakInterrupt(COMBATANT_CATEGORIES[combatantCategory] + ", " + count + " found");
        }

        // --- Name Helpers ---

        private string GetMobName(Mob mob)
        {
            if (mob == null) return "Unknown";

            if (mob.template != null && !string.IsNullOrEmpty(mob.template.displayName))
            {
                return UITextExtractor.CleanText(
                    Language.Localize(mob.template.displayName, false, false, string.Empty));
            }

            string goName = mob.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
                return goName.Replace("_", " ").Replace("(Clone)", "").Trim();

            return "Unknown creature";
        }

        private string GetTargetableName(Targetable target)
        {
            if (target == null) return null;

            var mob = target as Mob;
            if (mob != null) return GetMobName(mob);

            string goName = target.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
                return goName.Replace("_", " ").Replace("(Clone)", "").Trim();

            return "Object";
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

        private void SuppressInput()
        {
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
        }

        // =====================================================================
        // Shared Helpers
        // =====================================================================

        private Mob GetCurrentActor()
        {
            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return null;

            if (curActorField == null)
            {
                curActorField = typeof(CombatManager).GetField("curActor",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return curActorField?.GetValue(cm) as Mob;
        }

        private string GetDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Unknown";
            return UITextExtractor.CleanText(Language.Localize(rawName, false, false, string.Empty));
        }

        // =====================================================================
        // Initiative Tracker
        // =====================================================================

        private void OpenInitiativeTracker()
        {
            BuildInitiativeList();

            if (initiativeList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No initiative data available");
                return;
            }

            browsingInitiative = true;
            initiativeIndex = 0;

            string summary = "Initiative order, " + initiativeList.Count + " combatants. ";
            summary += FormatEntry(initiativeList[0]);

            ScreenReaderManager.SpeakInterrupt(summary);
        }

        private void BuildInitiativeList()
        {
            initiativeList.Clear();

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return;

            Mob currentActor = GetCurrentActor();

            var displayQueue = cm.displayQueue;
            if (displayQueue == null || displayQueue.Count == 0) return;

            for (int i = 0; i < displayQueue.Count; i++)
            {
                var actor = displayQueue[i];
                if (actor == null) continue;

                var entry = new InitiativeEntry
                {
                    Name = GetDisplayName(actor.name),
                    IsHostile = actor.isHostile,
                    IsCurrentActor = false,
                    Mob = null,
                    Details = ""
                };

                if (actor.gameObject != null)
                {
                    var mob = actor.gameObject.GetComponent<Mob>();
                    if (mob != null)
                    {
                        entry.Mob = mob;
                        entry.IsCurrentActor = (mob == currentActor);
                        entry.Details = BuildInitiativeMobDetails(mob);
                    }
                }

                if (actor.name == "Bomb")
                {
                    entry.Name = "Bomb";
                    entry.Details = "explosive";
                }

                initiativeList.Add(entry);
            }
        }

        private string BuildInitiativeMobDetails(Mob mob)
        {
            var parts = new List<string>();

            float maxHP = mob.stats.GetMaxHP();
            if (maxHP > 0)
            {
                float hpPercent = (mob.curHP / maxHP) * 100f;
                parts.Add(mob.curHP.ToString("F0") + " of " + maxHP.ToString("F0") + " HP, " + hpPercent.ToString("F0") + "%");
            }

            if (GetCurrentActor() == mob)
                parts.Add(mob.combatActionPointsRemaining + " AP remaining");

            if (mob.mobState == Mob.MobState.UNCONSCIOUS)
                parts.Add("unconscious");
            if (mob.inCover)
                parts.Add(mob.coverType == Cover.CoverType.Tall ? "in tall cover" : "in short cover");
            if (mob.isCrouching)
                parts.Add("crouching");
            if (mob.isHidden)
                parts.Add("hidden");

            return string.Join(", ", parts.ToArray());
        }

        private string FormatEntry(InitiativeEntry entry)
        {
            var parts = new List<string>();

            int position = initiativeList.IndexOf(entry) + 1;

            if (entry.IsCurrentActor)
                parts.Add(position + ". " + entry.Name + ", current turn");
            else
                parts.Add(position + ". " + entry.Name);

            if (entry.Name != "Bomb")
                parts.Add(entry.IsHostile ? "hostile" : "friendly");

            if (!string.IsNullOrEmpty(entry.Details))
                parts.Add(entry.Details);

            return string.Join(", ", parts.ToArray());
        }

        private void CycleInitiativeForward()
        {
            if (initiativeList.Count == 0) return;
            initiativeIndex = (initiativeIndex + 1) % initiativeList.Count;
            ScreenReaderManager.SpeakInterrupt(FormatEntry(initiativeList[initiativeIndex]));
        }

        private void CycleInitiativeBackward()
        {
            if (initiativeList.Count == 0) return;
            initiativeIndex--;
            if (initiativeIndex < 0) initiativeIndex = initiativeList.Count - 1;
            ScreenReaderManager.SpeakInterrupt(FormatEntry(initiativeList[initiativeIndex]));
        }

        private void ExitInitiativeBrowse()
        {
            browsingInitiative = false;
            initiativeList.Clear();
            ScreenReaderManager.SpeakInterrupt("Initiative closed");
        }
    }
}
