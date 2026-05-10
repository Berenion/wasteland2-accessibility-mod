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
        private static FieldInfo actQueueField;

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

        // Step size: how many tiles each arrow press moves.
        // Adjusted with Shift+Left/Right. Ctrl+Arrow ignores this and moves until blocked.
        private int stepSize = 1;
        private const int MIN_STEP_SIZE = 1;
        private const int MAX_STEP_SIZE = 30;
        private const int UNTIL_WALL_MAX_TILES = 100;
        private float lastStepChangeTime = 0f;
        private const float STEP_CHANGE_REPEAT_DELAY = 0.1f;

        // Grid match radius (canonical tile size lives in TileCoordinateSystem.SquareSize)
        private const float TILE_MATCH_RADIUS = TileCoordinateSystem.SquareSize * 0.75f;

        // Camera follow
        private bool cameraFollowsCursor = true;

        // --- Combatant cycling (PageUp/PageDown) ---
        private List<Mob> combatantList = new List<Mob>();
        private int combatantIndex = -1;
        private int combatantCategory = 0; // 0=All, 1=Enemies, 2=Allies
        private static readonly string[] COMBATANT_CATEGORIES = { "All", "Enemies", "Allies" };


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

        // --- Combat Actions Menu (Tab key) ---
        private bool browsingActions = false;
        private int actionIndex = 0;
        private List<CombatAction> actionList = new List<CombatAction>();

        // --- Target Actions Menu (Enter on enemy) ---
        private bool browsingTargetActions = false;
        private int targetActionIndex = 0;
        private List<CombatAction> targetActionList = new List<CombatAction>();
        private List<string> targetInfoLines = new List<string>();
        private int targetInfoIndex = 0;
        private int targetMenuTab = 0; // 0 = Actions, 1 = Info
        private Mob targetMob = null;

        // --- Item Targeting Mode ---
        private bool itemTargetingMode = false;
        private ItemInstance_Usable pendingItem = null;
        private PC pendingItemUser = null;

        // --- Free Aim Targeting Mode ---
        private bool freeAimMode = false;
        private PC freeAimUser = null;

        // --- Party Member Info ---
        private bool browsingPartyInfo = false;
        private List<string> partyInfoLines = new List<string>();
        private int partyInfoIndex = 0;

        // --- Combat Log Viewer (L key) ---
        private bool browsingLog = false;
        private int logIndex = 0;

        private class CombatAction
        {
            public string Label;
            public string Status; // e.g. "3 AP", "unavailable"
            public bool IsEnabled;
            public System.Action Execute;
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

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (initiativeIndex >= 0 && initiativeIndex < initiativeList.Count)
                    {
                        var entry = initiativeList[initiativeIndex];
                        ExitInitiativeBrowse();
                        if (entry.Mob != null && entry.Mob.transform != null)
                        {
                            JumpToCombatant(entry.Mob);
                        }
                    }
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

            // Tab key: open/close combat actions menu
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (itemTargetingMode) CancelItemTargeting();
                if (freeAimMode) CancelFreeAim();

                if (browsingActions)
                {
                    ExitActionsBrowse();
                }
                else
                {
                    if (browsingInitiative) ExitInitiativeBrowse();
                    OpenActionsMenu();
                }
                return true;
            }

            // While browsing actions menu
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

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitActionsBrowse();
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                return true;
            }

            // While browsing target actions menu (Enter on enemy)
            if (browsingTargetActions)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                // Left/Right: switch tabs
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    targetMenuTab = 1 - targetMenuTab; // Toggle between 0 and 1
                    if (targetMenuTab == 0)
                    {
                        targetActionIndex = 0;
                        string header = "Actions, " + targetActionList.Count + " items";
                        if (targetActionList.Count > 0)
                            header += ". " + FormatTargetAction(targetActionList[0]);
                        ScreenReaderManager.SpeakInterrupt(header);
                    }
                    else
                    {
                        targetInfoIndex = 0;
                        string header = "Info, " + targetInfoLines.Count + " items";
                        if (targetInfoLines.Count > 0)
                            header += ". " + FormatInfoLine(0);
                        ScreenReaderManager.SpeakInterrupt(header);
                    }
                    return true;
                }

                // Up/Down: cycle within current tab
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (targetMenuTab == 0 && targetActionList.Count > 0)
                    {
                        targetActionIndex = (targetActionIndex + 1) % targetActionList.Count;
                        AnnounceCurrentTargetAction();
                    }
                    else if (targetMenuTab == 1 && targetInfoLines.Count > 0)
                    {
                        targetInfoIndex = (targetInfoIndex + 1) % targetInfoLines.Count;
                        ScreenReaderManager.SpeakInterrupt(FormatInfoLine(targetInfoIndex));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (targetMenuTab == 0 && targetActionList.Count > 0)
                    {
                        targetActionIndex--;
                        if (targetActionIndex < 0) targetActionIndex = targetActionList.Count - 1;
                        AnnounceCurrentTargetAction();
                    }
                    else if (targetMenuTab == 1 && targetInfoLines.Count > 0)
                    {
                        targetInfoIndex--;
                        if (targetInfoIndex < 0) targetInfoIndex = targetInfoLines.Count - 1;
                        ScreenReaderManager.SpeakInterrupt(FormatInfoLine(targetInfoIndex));
                    }
                    return true;
                }

                // Enter: execute action (only on Actions tab)
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (targetMenuTab == 0)
                        ExecuteCurrentTargetAction();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitTargetActionsBrowse();
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                return true;
            }

            // While browsing party member info
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
                    EventManager.ignoreNextBack = true;
                    ScreenReaderManager.SpeakInterrupt("Info closed");
                    return true;
                }

                return true;
            }

            // While browsing combat log
            if (browsingLog)
            {
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                var log = Patches.HUD_Controller_QueueTextDescription_Patch.CombatLog;

                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    if (log.Count > 0)
                    {
                        logIndex = (logIndex + 1) % log.Count;
                        ScreenReaderManager.SpeakInterrupt(FormatLogEntry(log, logIndex));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    if (log.Count > 0)
                    {
                        logIndex--;
                        if (logIndex < 0) logIndex = log.Count - 1;
                        ScreenReaderManager.SpeakInterrupt(FormatLogEntry(log, logIndex));
                    }
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.L))
                {
                    browsingLog = false;
                    EventManager.ignoreNextBack = true;
                    ScreenReaderManager.SpeakInterrupt("Log closed");
                    return true;
                }

                return true;
            }

            // --- Item targeting mode: move cursor to a mob and press Enter ---
            if (itemTargetingMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelItemTargeting();
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (cursorInitialized)
                    {
                        Mob targetOnTile = FindAliveMobOnTile();
                        if (targetOnTile != null)
                        {
                            ExecuteItemOnTarget(targetOnTile);
                        }
                        else
                        {
                            ScreenReaderManager.SpeakInterrupt("No character on this tile. Move to a character and press Enter, or Escape to cancel");
                        }
                    }
                    SuppressInput();
                    return true;
                }

                // Allow cursor movement while targeting - fall through to normal cursor handling
            }

            // --- Free aim targeting mode: move cursor and press Enter to shoot ---
            if (freeAimMode)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelFreeAim();
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if (cursorInitialized)
                    {
                        ExecuteFreeAimShot();
                    }
                    SuppressInput();
                    return true;
                }

                // Allow cursor movement while targeting - fall through to normal cursor handling
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

            // L key: open combat log
            if (Input.GetKeyDown(KeyCode.L))
            {
                OpenCombatLog();
                SuppressInput();
                return true;
            }

            // --- Arrow key cursor movement with step size and Ctrl-extend ---
            float currentTime = Time.time;
            bool shiftForArrows = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool canMove = (currentTime - lastMoveTime) >= MOVE_REPEAT_DELAY;

            // Shift+Left/Right: adjust step size (must come before plain arrow handling)
            if (shiftForArrows && !ctrl)
            {
                bool canChangeStep = (currentTime - lastStepChangeTime) >= STEP_CHANGE_REPEAT_DELAY;
                if (canChangeStep)
                {
                    if (Input.GetKey(KeyCode.RightArrow))
                    {
                        if (stepSize < MAX_STEP_SIZE)
                        {
                            stepSize++;
                            lastStepChangeTime = currentTime;
                            ScreenReaderManager.SpeakInterrupt("Step size " + stepSize);
                        }
                        SuppressInput();
                        return true;
                    }
                    if (Input.GetKey(KeyCode.LeftArrow))
                    {
                        if (stepSize > MIN_STEP_SIZE)
                        {
                            stepSize--;
                            lastStepChangeTime = currentTime;
                            ScreenReaderManager.SpeakInterrupt("Step size " + stepSize);
                        }
                        SuppressInput();
                        return true;
                    }
                }
                // Consume Shift+Left/Right even during repeat delay
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
                {
                    SuppressInput();
                    return true;
                }
            }

            if (canMove && !shiftForArrows)
            {
                // Ctrl+Arrow moves until blocked; plain arrow moves stepSize tiles.
                int tiles = ctrl ? UNTIL_WALL_MAX_TILES : stepSize;

                if (Input.GetKey(KeyCode.UpArrow))
                {
                    MoveCursor(0, tiles);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    MoveCursor(1, tiles);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    MoveCursor(2, tiles);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    MoveCursor(3, tiles);
                    lastMoveTime = currentTime;
                    SuppressInput();
                    return true;
                }
            }
            // Right bracket: move current actor to cursor position
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                MoveToCursor();
                SuppressInput();
                return true;
            }

            // Enter: open target actions on hostile, or info screen on ally
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Mob hostile = FindHostileOnTile();
                if (hostile != null)
                {
                    OpenTargetActionsMenu(hostile);
                    SuppressInput();
                    return true;
                }

                // Check for ally on tile - open info screen
                PC allyOnTile = FindAllyOnTile();
                if (allyOnTile != null)
                {
                    OpenPartyMemberInfo(allyOnTile);
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

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Shift+Home: jump cursor to current actor
            if (shift && Input.GetKeyDown(KeyCode.Home))
            {
                JumpToCurrentActor();
                SuppressInput();
                return true;
            }

            // Shift+End: announce distance/direction from cursor to current actor
            if (shift && Input.GetKeyDown(KeyCode.End))
            {
                AnnounceDistanceToCurrentActor();
                SuppressInput();
                return true;
            }

            // Home: jump cursor to selected combatant (from PageUp/Down cycling)
            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpToSelectedCombatant();
                SuppressInput();
                return true;
            }

            // End: announce distance/direction from cursor to selected combatant
            if (Input.GetKeyDown(KeyCode.End))
            {
                AnnounceDistanceToSelectedCombatant();
                SuppressInput();
                return true;
            }

            // K: toggle tile announcement order (coordinates first vs object names first)
            if (Input.GetKeyDown(KeyCode.K))
            {
                ModConfig.ToggleObjectNamesFirst();
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

            // I: open inventory (only on player's turn)
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (MonoBehaviourSingleton<CombatManager>.GetInstance().isPlayersTurn)
                {
                    MonoBehaviourSingleton<GUIManager>.GetInstance().ToggleCharacterInfoMenu(true);
                }
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // Escape: open pause menu (when not in any browsing mode)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                MonoBehaviourSingleton<GUIManager>.GetInstance().OpenPauseMenu();
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;
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
            browsingActions = false;
            actionList.Clear();
            browsingTargetActions = false;
            targetActionList.Clear();
            targetInfoLines.Clear();
            targetMenuTab = 0;
            targetMob = null;
            browsingLog = false;
            itemTargetingMode = false;
            pendingItem = null;
            pendingItemUser = null;
            freeAimMode = false;
            freeAimUser = null;
            browsingPartyInfo = false;
            partyInfoLines.Clear();
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

            // If the actor changed (new turn), jump cursor to them and announce
            if (actor != null && actor != lastTrackedActor)
            {
                lastTrackedActor = actor;
                InitializeCursorToMob(actor);

                // Queue (not interrupt) so the previous turn's combat log and floating
                // text finish speaking before we announce the new turn.
                string turnName = GetMobName(actor);
                ScreenReaderManager.Speak(turnName + "'s turn");
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

        private void MoveCursor(int directionIndex, int tilesToMove)
        {
            Vector3 direction = CardinalDirections.Vectors[directionIndex];
            Vector3 currentGridId = cursorGridId;
            Vector3 currentPosition = cursorPosition;
            int actualSteps = 0;
            string blockReason = null;

            for (int i = 0; i < tilesToMove; i++)
            {
                Vector3 newGridId = new Vector3(
                    currentGridId.x + direction.x,
                    currentGridId.y,
                    currentGridId.z + direction.z);

                CombatAStarNode node = GetNodeAtGridId(newGridId);
                if (node != null)
                {
                    currentGridId = node.id;
                    currentPosition = node.position;
                    actualSteps++;
                    continue;
                }

                // No combat node — determine why
                CombatAStarNode fullMapNode = GetNodeInFullMap(newGridId);
                if (fullMapNode != null)
                {
                    blockReason = "edge of combat area";
                }
                else
                {
                    Vector3 blockedWorldPos = new Vector3(
                        newGridId.x * TileCoordinateSystem.SquareSize,
                        currentPosition.y,
                        newGridId.z * TileCoordinateSystem.SquareSize);
                    blockReason = IdentifyObstruction(blockedWorldPos);
                }
                break;
            }

            if (actualSteps == 0)
            {
                ScreenReaderManager.SpeakInterrupt(blockReason ?? "Blocked");
                return;
            }

            cursorGridId = currentGridId;
            cursorPosition = currentPosition;
            if (cameraFollowsCursor) SnapCameraToCursor();

            // For multi-tile moves, prefix the announcement with the tile count
            // and any blocking reason so the user knows the move stopped early.
            string prefix = null;
            if (tilesToMove > 1)
            {
                prefix = actualSteps + (actualSteps == 1 ? " tile" : " tiles");
                if (blockReason != null)
                    prefix += ", " + blockReason;
            }
            AnnounceTile(detailed: false, prefix: prefix);
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
                    out hit, TileCoordinateSystem.SquareSize * 1.5f, obstructionMask))
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

            // Occupants on this tile
            var mobs = FindMobsOnTile();
            var mobParts = new List<string>();
            foreach (var mob in mobs)
            {
                mobParts.Add(FormatMobForTile(mob));
            }

            // Add coords and occupants in configured order
            if (ModConfig.ObjectNamesFirst && mobParts.Count > 0)
            {
                parts.AddRange(mobParts);
                parts.Add(coords);
            }
            else
            {
                parts.Add(coords);
                parts.AddRange(mobParts);
            }

            // Note: node.occupant is not used — it can be stale after mobs move.
            // FindMobsOnTile() above uses actual mob positions, which is always fresh.

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

            // Dead — short-circuit, no need for faction/HP/state
            if (mob.mobState == Mob.MobState.DEAD)
            {
                parts.Add("dead");
                return string.Join(" ", parts.ToArray());
            }

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
            for (int i = 0; i < node.cover.Length && i < CardinalDirections.Names.Length; i++)
            {
                if (node.cover[i])
                    coverDirs.Add(CardinalDirections.Names[i]);
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
                // Dead mobs may be deactivated — still include them for tile announcements
                if (!mob.gameObject.activeInHierarchy && mob.mobState != Mob.MobState.DEAD) continue;

                // Dead mobs with deactivated GameObjects may have stale transforms
                try
                {
                    if (IsOnCurrentTile(mob.transform.position))
                        onTile.Add(mob);
                }
                catch { }
            }

            // Unconscious party members are removed from cm.mobs by PC.KnockOut, so
            // we have to pull them from Game.party to keep them tile-discoverable.
            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null)
                {
                    foreach (var pc in party)
                    {
                        if (pc == null || pc.gameObject == null) continue;
                        if (pc.mobState != Mob.MobState.UNCONSCIOUS) continue;
                        if (onTile.Contains(pc)) continue;
                        if (!pc.gameObject.activeInHierarchy) continue;

                        try
                        {
                            if (IsOnCurrentTile(pc.transform.position))
                                onTile.Add(pc);
                        }
                        catch { }
                    }
                }
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
                if (mob.isHidden) continue;

                // NPCs must be visible (fog of war) and part of this combat
                if (mob is NPC)
                {
                    NPC npc = mob as NPC;
                    if (npc.waitToJoinCombat) continue;
                    if (FOWSystem.instance != null && !npc.fowRenderer.isVisible) continue;
                }

                // Filter by category
                bool isEnemy = mob.HatesParty();
                bool isAlly = mob is PC || !isEnemy;

                if (combatantCategory == 1 && !isEnemy) continue; // Enemies only
                if (combatantCategory == 2 && !isAlly) continue;  // Allies only

                combatantList.Add(mob);
            }

            // Unconscious party members are removed from cm.mobs by PC.KnockOut.
            // Re-add them so they're cycle-reachable for revival.
            if (MonoBehaviourSingleton<Game>.HasInstance() && combatantCategory != 1)
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null)
                {
                    foreach (var pc in party)
                    {
                        if (pc == null || pc.gameObject == null) continue;
                        if (pc.mobState != Mob.MobState.UNCONSCIOUS) continue;
                        if (!pc.gameObject.activeInHierarchy) continue;
                        if (combatantList.Contains(pc)) continue;

                        combatantList.Add(pc);
                    }
                }
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
            AnnounceCombatant(combatantList[combatantIndex]);
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
            AnnounceCombatant(combatantList[combatantIndex]);
        }

        private void AnnounceCombatant(Mob mob)
        {
            string announcement = FormatMobForCycle(mob);
            ScreenReaderManager.SpeakInterrupt(announcement);
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

        private void JumpToSelectedCombatant()
        {
            if (combatantList.Count == 0 || combatantIndex < 0 || combatantIndex >= combatantList.Count)
            {
                ScreenReaderManager.SpeakInterrupt("No combatant selected");
                return;
            }

            Mob mob = combatantList[combatantIndex];
            if (mob == null || mob.mobState == Mob.MobState.DEAD)
            {
                ScreenReaderManager.SpeakInterrupt("Selected combatant no longer valid");
                return;
            }

            JumpToCombatant(mob);
        }

        private void AnnounceDistanceToSelectedCombatant()
        {
            if (combatantList.Count == 0 || combatantIndex < 0 || combatantIndex >= combatantList.Count)
            {
                ScreenReaderManager.SpeakInterrupt("No combatant selected");
                return;
            }

            Mob mob = combatantList[combatantIndex];
            if (mob == null || mob.mobState == Mob.MobState.DEAD)
            {
                ScreenReaderManager.SpeakInterrupt("Selected combatant no longer valid");
                return;
            }

            AnnounceDistanceToMob(mob);
        }

        private void AnnounceDistanceToCurrentActor()
        {
            Mob actor = GetCurrentActor();
            if (actor == null)
            {
                ScreenReaderManager.SpeakInterrupt("No active actor");
                return;
            }
            AnnounceDistanceToMob(actor);
        }

        private void AnnounceDistanceToMob(Mob mob)
        {
            Vector3 targetPos = mob.transform.position;
            int targetGridX, targetGridZ;
            if (mob.currentSquare != null)
            {
                targetGridX = (int)mob.currentSquare.id.x;
                targetGridZ = (int)mob.currentSquare.id.z;
            }
            else
            {
                targetGridX = Mathf.RoundToInt(targetPos.x / TileCoordinateSystem.SquareSize);
                targetGridZ = Mathf.RoundToInt(targetPos.z / TileCoordinateSystem.SquareSize);
            }

            int tileDistX = Mathf.Abs((int)cursorGridId.x - targetGridX);
            int tileDistZ = Mathf.Abs((int)cursorGridId.z - targetGridZ);
            int tileDist = Mathf.Max(tileDistX, tileDistZ);
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, targetPos);

            string name = GetMobName(mob) ?? "Target";
            string announcement = name + ", " + tileDist + (tileDist == 1 ? " tile " : " tiles ") + direction;
            ScreenReaderManager.SpeakInterrupt(announcement);
        }

        private string FormatMobForCycle(Mob mob)
        {
            var parts = new List<string>();

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

            // Position in list (at end)
            parts.Add((combatantIndex + 1) + " of " + combatantList.Count);

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
        // Combat Actions Menu (Tab key)
        // =====================================================================

        private void OpenActionsMenu()
        {
            BuildActionList();

            if (actionList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No actions available");
                return;
            }

            browsingActions = true;
            actionIndex = 0;

            // Announce with current actor context
            PC currentPC = GetCurrentActor() as PC;
            string header = "Combat actions";
            if (currentPC != null)
                header += ", " + currentPC.combatActionPointsRemaining + " AP remaining";
            header += ", " + actionList.Count + " actions";

            ScreenReaderManager.SpeakInterrupt(header + ". " + FormatAction(actionList[0]));
        }

        private void BuildActionList()
        {
            actionList.Clear();

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return;
            if (!cm.isPlayersTurn) return;

            PC pc = cm.GetCurrentMob() as PC;
            if (pc == null) return;

            bool isThinking = pc.combatActionState == Mob.CombatActionState.THINKING
                           || pc.combatActionState == Mob.CombatActionState.STARTED;

            // --- Reload / Unjam ---
            if (pc.IsJammed())
            {
                int unjamAP = pc.stats.GetActionPointsToUnjam();
                bool canUnjam = pc.CanUnjam() && isThinking;
                actionList.Add(new CombatAction
                {
                    Label = "Unjam weapon",
                    Status = unjamAP + " AP",
                    IsEnabled = canUnjam,
                    Execute = () =>
                    {
                        var evt = ObjectPool.Get<EventInfo_CommandUnjam>();
                        evt.target = pc;
                        MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                    }
                });
            }
            else
            {
                int reloadAP = pc.stats.GetActionPointsToReload();
                bool canReload = pc.CanReload() && isThinking;
                string ammoInfo = GetAmmoInfo(pc);
                actionList.Add(new CombatAction
                {
                    Label = "Reload" + (string.IsNullOrEmpty(ammoInfo) ? "" : ", " + ammoInfo),
                    Status = reloadAP + " AP",
                    IsEnabled = canReload,
                    Execute = () =>
                    {
                        var evt = ObjectPool.Get<EventInfo_CommandReload>();
                        evt.mob = pc;
                        MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                    }
                });
            }

            // --- Swap Weapons ---
            {
                string weaponInfo = GetSwapWeaponInfo(pc);
                actionList.Add(new CombatAction
                {
                    Label = "Swap weapons" + (string.IsNullOrEmpty(weaponInfo) ? "" : ", " + weaponInfo),
                    Status = "0 AP",
                    IsEnabled = isThinking,
                    Execute = () =>
                    {
                        MonoBehaviourSingleton<InputManager>.GetInstance().OnSwapWeaponsClicked(pc);
                    }
                });
            }

            // --- Crouch / Stand ---
            {
                bool isCrouching = pc.isCrouching;
                int stanceAP = pc.stats.GetActionPointsToChangeStance();
                bool canChangeStance = isThinking
                    && pc.combatActionPointsRemaining >= stanceAP
                    && !pc.IsInCover();
                actionList.Add(new CombatAction
                {
                    Label = isCrouching ? "Stand up" : "Crouch",
                    Status = stanceAP + " AP" + (pc.IsInCover() ? ", in cover" : ""),
                    IsEnabled = canChangeStance,
                    Execute = () =>
                    {
                        var evt = ObjectPool.Get<EventInfo_CommandChangeStance>();
                        evt.pc = pc;
                        evt.crouch = !pc.isCrouching;
                        MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                    }
                });
            }

            // --- Ambush ---
            {
                int ambushAP = pc.stats.GetActionPointsToAmbush();
                bool canAmbush = isThinking
                    && pc.combatActionPointsRemaining >= ambushAP
                    && !pc.IsOutOfAmmo()
                    && !pc.IsJammed();
                actionList.Add(new CombatAction
                {
                    Label = "Ambush",
                    Status = ambushAP + " AP",
                    IsEnabled = canAmbush,
                    Execute = () =>
                    {
                        var evt = ObjectPool.Get<EventInfo_CommandAmbush>();
                        evt.mob = pc;
                        MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
                    }
                });
            }

            // --- Free Aim ---
            {
                bool hasRangedWeapon = pc.pcStats.GetWeaponInstance() is ItemInstance_WeaponRanged;
                bool notJammed = !pc.IsJammed();
                bool notOutOfAmmo = !pc.IsOutOfAmmo();
                int attackAP = pc.GetActionPointsToAttack();
                bool canFreeAim = isThinking && hasRangedWeapon && notJammed && notOutOfAmmo
                    && pc.combatActionPointsRemaining >= attackAP;

                if (hasRangedWeapon)
                {
                    var capturedPC = pc;
                    actionList.Add(new CombatAction
                    {
                        Label = "Free aim",
                        Status = attackAP + " AP",
                        IsEnabled = canFreeAim,
                        Execute = () =>
                        {
                            EnterFreeAim(capturedPC);
                        }
                    });
                }
            }

            // --- Use Items ---
            try
            {
                var usableItems = pc.inventory.GetUsableItems(true, true);
                for (int i = 0; i < usableItems.Count; i++)
                {
                    var item = usableItems[i];
                    if (item == null || item.template == null) continue;

                    var usableTemplate = item.template as ItemTemplate_Usable;
                    if (usableTemplate == null) continue;

                    int itemAP = usableTemplate.actionPoints;
                    bool canUse = isThinking
                        && pc.combatActionPointsRemaining >= itemAP
                        && usableTemplate.CanUse(pc);

                    string itemName = UITextExtractor.CleanText(
                        Language.Localize(item.template.displayName, false, false, string.Empty));

                    int count = pc.inventory.CountInInventory(item);
                    string label = "Use " + itemName;
                    if (count > 1)
                        label += ", " + count + " remaining";

                    string status = itemAP + " AP";

                    // Capture for closure
                    var capturedItem = item;
                    var capturedPC = pc;

                    actionList.Add(new CombatAction
                    {
                        Label = label,
                        Status = status,
                        IsEnabled = canUse,
                        Execute = () =>
                        {
                            EnterItemTargeting(capturedItem, capturedPC);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] Error building item list: {ex.Message}");
            }

            // --- End Turn ---
            {
                actionList.Add(new CombatAction
                {
                    Label = "End turn",
                    Status = pc.combatActionPointsRemaining + " AP remaining",
                    IsEnabled = isThinking,
                    Execute = () =>
                    {
                        MonoBehaviourSingleton<CombatManager>.GetInstance().EndCurrentTurn();
                    }
                });
            }
        }

        private string GetAmmoInfo(PC pc)
        {
            try
            {
                var weapon = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
                if (weapon == null) return null;
                int current = weapon.GetAmmoCount();
                int clipSize = weapon.GetClipSize();
                return current + " of " + clipSize;
            }
            catch
            {
                return null;
            }
        }

        private string GetSwapWeaponInfo(PC pc)
        {
            try
            {
                var secondary = pc.pcStats.GetSecondaryWeaponInstance();
                if (secondary == null || secondary.template == null) return "no secondary weapon";
                string name = Language.Localize(secondary.template.displayName, false, false, string.Empty);
                return "to " + UITextExtractor.CleanText(name);
            }
            catch
            {
                return null;
            }
        }

        private string FormatAction(CombatAction action)
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

            ScreenReaderManager.SpeakInterrupt(action.Label);
            ExitActionsBrowse();

            try
            {
                action.Execute();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] Action execution error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Action failed");
            }
        }

        private void ExitActionsBrowse()
        {
            browsingActions = false;
            actionList.Clear();
            ScreenReaderManager.SpeakInterrupt("Actions closed");
        }

        // =====================================================================
        // Item Targeting
        // =====================================================================

        private void EnterItemTargeting(ItemInstance_Usable item, PC user)
        {
            pendingItem = item;
            pendingItemUser = user;
            itemTargetingMode = true;

            string itemName = UITextExtractor.CleanText(
                Language.Localize(item.template.displayName, false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt("Select a target for " + itemName
                + ". Move to a character and press Enter, or Escape to cancel");
        }

        private void CancelItemTargeting()
        {
            itemTargetingMode = false;
            pendingItem = null;
            pendingItemUser = null;
            UseASIManager.SetActiveASIName(null);
            UseASIManager.SetActiveASIItem(null, null);
            ScreenReaderManager.SpeakInterrupt("Item use cancelled");
        }

        private Mob FindAliveMobOnTile()
        {
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                if (mob.mobState != Mob.MobState.DEAD)
                    return mob;
            }
            return null;
        }

        private void ExecuteItemOnTarget(Mob target)
        {
            if (pendingItem == null || pendingItemUser == null)
            {
                CancelItemTargeting();
                return;
            }

            string itemName = UITextExtractor.CleanText(
                Language.Localize(pendingItem.template.displayName, false, false, string.Empty));
            string targetName = GetMobName(target);

            try
            {
                // Set up UseASIManager state for the item use flow
                UseASIManager.SetActiveASIItem(pendingItem, pendingItemUser);

                var targetable = target as Targetable;
                if (targetable != null)
                {
                    InputManager.PrepareUseItemActions(
                        target.transform,
                        target.GetComponent<Drama>(),
                        targetable,
                        false);
                }

                ScreenReaderManager.SpeakInterrupt("Using " + itemName + " on " + targetName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] Item use error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Failed to use " + itemName);
            }

            itemTargetingMode = false;
            pendingItem = null;
            pendingItemUser = null;
        }

        // =====================================================================
        // Party Member Info
        // =====================================================================

        private PC FindAllyOnTile()
        {
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                if (mob is PC && mob.mobState != Mob.MobState.DEAD)
                    return mob as PC;
            }
            return null;
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

            // --- CON / AP ---
            partyInfoLines.Add("AP: " + pc.combatActionPointsRemaining + " of " + pc.stats.GetActionPoints());

            // --- Stance / Cover ---
            var stanceParts = new List<string>();
            if (pc.isCrouching) stanceParts.Add("crouching");
            if (pc.inCover)
                stanceParts.Add(pc.coverType == Cover.CoverType.Tall ? "tall cover" : "short cover");
            if (pc.isHidden) stanceParts.Add("hidden");
            if (stanceParts.Count > 0)
                partyInfoLines.Add("Stance: " + string.Join(", ", stanceParts.ToArray()));

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
                        if (pc.IsJammed()) weaponLine += ", jammed";
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

        // =====================================================================
        // Free Aim Targeting
        // =====================================================================

        private void EnterFreeAim(PC user)
        {
            freeAimUser = user;
            freeAimMode = true;
            ScreenReaderManager.SpeakInterrupt("Free aim. Move cursor to a target and press Enter to shoot, or Escape to cancel");
        }

        private void CancelFreeAim()
        {
            freeAimMode = false;
            freeAimUser = null;
            ScreenReaderManager.SpeakInterrupt("Free aim cancelled");
        }

        private void ExecuteFreeAimShot()
        {
            if (freeAimUser == null)
            {
                CancelFreeAim();
                return;
            }

            try
            {
                // Check AP
                int attackAP = freeAimUser.GetActionPointsToAttack();
                if (freeAimUser.combatActionPointsRemaining < attackAP)
                {
                    ScreenReaderManager.SpeakInterrupt("Not enough AP");
                    return;
                }

                // Look for a targetable on the tile: mob, destructible object, or explosive
                Targetable target = FindTargetableOnTile();

                // Validate range and line of sight before firing — the game rejects
                // these shots silently, so the user needs explicit feedback
                if (target != null)
                {
                    string tName = GetTargetableName(target);
                    float distance = Vector3.Distance(freeAimUser.transform.position, target.transform.position);
                    float attackRange = freeAimUser.stats.GetAttackRange();
                    if (distance > attackRange)
                    {
                        ScreenReaderManager.SpeakInterrupt(tName + " is out of range");
                        return;
                    }
                    try
                    {
                        if (!freeAimUser.TargetVisible(target))
                        {
                            ScreenReaderManager.SpeakInterrupt("No line of sight to " + tName);
                            return;
                        }
                    }
                    catch (Exception) { }
                }

                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                inputManager.ClearSelectedSquare();

                var evt = ObjectPool.Get<EventInfo_CommandAttack>();
                evt.pc = freeAimUser;
                evt.coneAttack = false;
                evt.specialAttack = false;
                evt.isAOESetup = false;

                if (target != null)
                {
                    evt.target = target;
                    evt.intentionalMiss = false;
                    string targetName = GetTargetableName(target);
                    ScreenReaderManager.SpeakInterrupt("Shooting at " + targetName);
                }
                else
                {
                    // Intentional miss - shoot at cursor position
                    evt.target = null;
                    evt.intentionalMiss = true;
                    evt.aimDirection = cursorPosition;
                    evt.meleeMoveToRange = false;
                    ScreenReaderManager.SpeakInterrupt("Shooting at ground");
                }

                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);

                if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                {
                    MonoBehaviourSingleton<CombatAStar>.GetInstance().ClearPath();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] Free aim error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Shot failed");
            }

            freeAimMode = false;
            freeAimUser = null;
        }

        /// <summary>
        /// Finds any Targetable on the current cursor tile: alive mobs, destructible objects,
        /// or explosives. Returns null if nothing targetable is found.
        /// </summary>
        private Targetable FindTargetableOnTile()
        {
            // First check for mobs (alive ones)
            Mob mob = FindAliveMobOnTile();
            if (mob != null) return mob;

            // Then check for TargetableObjects (destructibles, explosives, etc.)
            float tileRadius = 0.75f;
            Collider[] colliders = Physics.OverlapSphere(cursorPosition, tileRadius);
            foreach (var collider in colliders)
            {
                if (collider == null || collider.gameObject == null) continue;

                var targetableObj = collider.GetComponent<TargetableObject>();
                if (targetableObj != null && targetableObj.hasHitpoints)
                    return targetableObj;

                // Also check parent
                targetableObj = collider.GetComponentInParent<TargetableObject>();
                if (targetableObj != null && targetableObj.hasHitpoints)
                    return targetableObj;
            }

            return null;
        }

        // =====================================================================
        // Target Actions Menu (Enter on enemy)
        // =====================================================================

        /// <summary>
        /// Finds the first hostile mob on the current cursor tile.
        /// </summary>
        private Mob FindHostileOnTile()
        {
            var mobs = FindMobsOnTile();
            foreach (var mob in mobs)
            {
                if (mob.HatesParty() && mob.mobState != Mob.MobState.DEAD)
                    return mob;
            }
            return null;
        }

        private void OpenTargetActionsMenu(Mob target)
        {
            targetMob = target;
            BuildTargetActionList();
            BuildTargetInfoLines(target);

            if (targetActionList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No actions available against " + GetMobName(target));
                return;
            }

            browsingTargetActions = true;
            targetActionIndex = 0;
            targetInfoIndex = 0;
            targetMenuTab = 0; // Start on Actions tab

            string header = GetMobName(target);
            string ammoStats = GetAmmoStats(GetCurrentActor() as PC);
            if (!string.IsNullOrEmpty(ammoStats))
                header += ", " + ammoStats;
            ScreenReaderManager.SpeakInterrupt(header + ", Actions tab, " + targetActionList.Count
                + " actions, left or right for Info. " + FormatTargetAction(targetActionList[0]));
        }

        /// <summary>
        /// Returns loaded ammo stats: penetration, expansion, and armor reduction.
        /// </summary>
        private string GetAmmoStats(PC pc)
        {
            if (pc == null) return null;
            try
            {
                var ranged = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
                if (ranged == null || ranged.currentAmmo == null) return null;

                var ammoTemplate = ranged.currentAmmo.template as ItemTemplate_Ammo;
                if (ammoTemplate == null) return null;

                var parts = new List<string>();
                if (ammoTemplate.penetration > 0)
                    parts.Add(ammoTemplate.penetration + " penetration");
                if (ammoTemplate.expansionMultiplier > 1f)
                    parts.Add("x" + (ammoTemplate.expansionMultiplier * 100f).ToString("0") + "% expansion");
                if (ammoTemplate.armorReduction > 0)
                    parts.Add(ammoTemplate.armorReduction + " armor reduction");

                if (parts.Count == 0) return null;
                return string.Join(", ", parts.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private void BuildTargetActionList()
        {
            targetActionList.Clear();

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null || !cm.isPlayersTurn) return;

            PC pc = cm.GetCurrentMob() as PC;
            if (pc == null || targetMob == null) return;

            // Capture target as a local variable for lambda closures.
            // targetMob (field) gets cleared by ExitTargetActionsBrowse() before Execute() runs.
            Mob capturedTarget = targetMob;

            bool isThinking = pc.combatActionState == Mob.CombatActionState.THINKING
                           || pc.combatActionState == Mob.CombatActionState.STARTED;

            // Get weapon info
            var weaponInstance = pc.pcStats.GetWeaponInstance();
            var weaponTemplate = pc.pcStats.GetWeaponTemplate();
            string weaponName = GetWeaponDisplayName(weaponInstance);
            bool isMelee = weaponTemplate is ItemTemplate_WeaponMelee;
            bool isAoe = weaponTemplate is ItemTemplate_WeaponAoe;
            var rangedInstance = weaponInstance as ItemInstance_WeaponRanged;
            var rangedTemplate = weaponTemplate as ItemTemplate_WeaponRanged;

            // Compute shared attack info
            string attackStatus = GetAttackStatus(pc, targetMob, isThinking);

            if (isMelee || isAoe)
            {
                // Melee / AOE: single attack option
                int apCost = pc.GetActionPointsToAttack();
                string info = BuildAttackInfo(pc, targetMob, apCost);
                bool canAttack = isThinking && CanPerformAttack(pc, targetMob);

                targetActionList.Add(new CombatAction
                {
                    Label = "Attack with " + weaponName + ", " + info,
                    Status = attackStatus,
                    IsEnabled = canAttack,
                    Execute = () => ExecuteAttack(pc, capturedTarget, -1)
                });
            }
            else if (rangedInstance != null && rangedTemplate != null)
            {
                // Ranged: one entry per firing mode
                int savedMode = rangedInstance.firingModeIndex;

                for (int i = 0; i < rangedTemplate.firingModeInfos.Length; i++)
                {
                    var modeInfo = rangedTemplate.firingModeInfos[i];
                    int modeIndex = i;

                    // Temporarily set firing mode to calculate correct values
                    rangedInstance.firingModeIndex = i;
                    string modeName = rangedInstance.GetFiringModeName();

                    // Friendly name
                    string friendlyMode;
                    if (modeName == ItemTemplate_WeaponRanged.FiringModeInfo.Single)
                        friendlyMode = "Single shot";
                    else if (modeName == ItemTemplate_WeaponRanged.FiringModeInfo.Burst)
                        friendlyMode = "Burst";
                    else
                        friendlyMode = "Full auto";

                    int apCost = rangedInstance.GetActionPointsToAttack();
                    int ammoCost = rangedInstance.GetAmmoCostToFire();
                    int penalty = rangedInstance.GetFiringModePenalty();

                    // Compute hit% with this firing mode's penalty applied
                    string info = BuildAttackInfoForMode(pc, targetMob, apCost, ammoCost, penalty);

                    bool hasEnoughAP = isThinking && pc.combatActionPointsRemaining >= apCost;
                    bool hasEnoughAmmo = rangedInstance.GetAmmoCount() >= ammoCost;
                    bool canDo = hasEnoughAP && hasEnoughAmmo && !pc.IsJammed()
                                 && !pc.IsOutOfAmmo() && CanPerformAttack(pc, targetMob);

                    string status = apCost + " AP, " + ammoCost + " ammo";
                    if (!hasEnoughAP)
                        status += ", not enough AP";
                    else if (!hasEnoughAmmo)
                        status += ", not enough ammo";
                    else if (pc.IsJammed())
                        status += ", jammed";

                    targetActionList.Add(new CombatAction
                    {
                        Label = friendlyMode + " with " + weaponName + ", " + info,
                        Status = status,
                        IsEnabled = canDo,
                        Execute = () => ExecuteAttack(pc, capturedTarget, modeIndex)
                    });
                }

                // Restore original firing mode
                rangedInstance.firingModeIndex = savedMode;
            }

            // --- Precision Strike (body part targeting) ---
            AddPrecisionStrikeOptions(pc, capturedTarget, isThinking, weaponName);
        }

        private void AddPrecisionStrikeOptions(PC pc, Mob target, bool isThinking, string weaponName)
        {
            if (!MonoBehaviourSingleton<PcSpecialAttackManager>.HasInstance()) return;
            var psManager = MonoBehaviourSingleton<PcSpecialAttackManager>.GetInstance();

            // Target must be an NPC for precision strikes
            NPC npc = target as NPC;
            NPCTemplate npcTemplate = (npc != null) ? npc.npcTemplate : null;

            // Weapon must not be jammed/empty
            var rangedInstance = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
            bool weaponReady = rangedInstance != null && !rangedInstance.IsJammed() && !rangedInstance.IsEmpty();

            // Base attack AP (precision uses single shot mode)
            int baseAttackAP = pc.pcStats.GetActionPointsToAttack();

            var zones = new[]
            {
                new { Zone = PrecisionShotZone.Head,  Name = "Head",  Effect = "stuns or confuses" },
                new { Zone = PrecisionShotZone.Torso, Name = "Torso", Effect = "reduces armor" },
                new { Zone = PrecisionShotZone.Arms,  Name = "Arms",  Effect = "reduces chance to hit" },
                new { Zone = PrecisionShotZone.Legs,  Name = "Legs",  Effect = "reduces speed" },
            };

            foreach (var z in zones)
            {
                PrecisionShotSpecialAttack precisionAttack = psManager.GetPrecisionShotSpecialAttack(z.Zone);
                if (precisionAttack == null) continue;

                // Check NPC allows this zone
                bool zoneAllowed = NPCTemplate.GetAllowedPrecisionShot(npcTemplate, z.Zone);

                // Check weapon type compatibility
                bool weaponCompatible = precisionAttack.IsUsable(pc);

                // Use NPC-specific label override if available
                string zoneName = z.Name;
                if (npcTemplate != null)
                {
                    string labelOverride = NPCTemplate.GetPrecisionShotLabelOverride(npcTemplate, z.Zone);
                    if (!string.IsNullOrEmpty(labelOverride))
                        zoneName = UITextExtractor.CleanText(
                            Language.Localize(labelOverride, false, false, string.Empty));
                }

                // Calculate AP cost: base attack + precision bonus AP
                int totalAP = baseAttackAP + precisionAttack.actionPoints;

                // Calculate hit% with precision modifiers
                string hitInfo = "";
                try
                {
                    // Temporarily set special attack to get correct hit calculation
                    var savedSpecial = pc.specialAttack;
                    var savedUseSpecial = pc.useSpecialAttack;
                    pc.specialAttack = precisionAttack;
                    pc.useSpecialAttack = true;

                    // Force single shot for calculation
                    int savedMode = -1;
                    if (rangedInstance != null)
                    {
                        savedMode = rangedInstance.firingModeIndex;
                        rangedInstance.firingModeIndex = 0;
                    }

                    int hitChance = Mathf.Min(pc.GetChanceToHit(target, false), 100);
                    int critChance = Mathf.Min(pc.GetChanceToCriticalHit(target), 100);

                    // Restore
                    pc.specialAttack = savedSpecial;
                    pc.useSpecialAttack = savedUseSpecial;
                    if (rangedInstance != null && savedMode >= 0)
                        rangedInstance.firingModeIndex = savedMode;

                    hitInfo = hitChance + "% hit";
                    if (critChance > 0)
                        hitInfo += ", " + critChance + "% crit";
                }
                catch
                {
                    hitInfo = "hit unknown";
                }

                // Damage multiplier info
                string dmgInfo = "";
                if (precisionAttack.damageMultiplier != 1f)
                    dmgInfo = ", " + precisionAttack.damageMultiplier.ToString("0.##") + "x damage";

                bool canDo = isThinking && weaponReady && zoneAllowed && weaponCompatible
                    && pc.combatActionPointsRemaining >= totalAP && npc != null;

                string status = totalAP + " AP";
                if (!weaponCompatible)
                    status += ", weapon incompatible";
                else if (!zoneAllowed)
                    status += ", zone blocked";
                else if (pc.combatActionPointsRemaining < totalAP)
                    status += ", not enough AP";

                PrecisionShotZone capturedZone = z.Zone;
                targetActionList.Add(new CombatAction
                {
                    Label = "Precision strike " + zoneName + ", " + hitInfo + dmgInfo
                            + ", " + z.Effect,
                    Status = status,
                    IsEnabled = canDo,
                    Execute = () => ExecutePrecisionStrike(pc, npc, capturedZone)
                });
            }
        }

        private void ExecutePrecisionStrike(PC pc, NPC target, PrecisionShotZone zone)
        {
            try
            {
                var psManager = MonoBehaviourSingleton<PcSpecialAttackManager>.GetInstance();
                PrecisionShotSpecialAttack precisionAttack = psManager.GetPrecisionShotSpecialAttack(zone);

                // Force single shot mode
                var rangedInstance = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
                if (rangedInstance != null)
                {
                    rangedInstance.ChangeFiringMode(0);
                    pc.pcStats.RecalculateAllStats();
                }

                // Match the game's PrecisionAttackMenu.OnBodyPartClicked flow exactly:
                // Only set specialAttack (the object), NOT useSpecialAttack (the bool).
                // The bool is set later by AIAction_Attack.Update() at the right time.
                pc.specialAttack = precisionAttack;

                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                inputManager.ClearSelectedSquare();

                var evt = ObjectPool.Get<EventInfo_CommandAttack>();
                evt.pc = pc;
                evt.target = target;
                evt.coneAttack = false;
                evt.specialAttack = true;
                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] ExecutePrecisionStrike error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Precision strike failed");
            }
        }

        private string GetWeaponDisplayName(ItemInstance_Weapon weapon)
        {
            if (weapon == null || weapon.template == null) return "fists";
            try
            {
                return UITextExtractor.CleanText(
                    Language.Localize(weapon.template.displayName, false, false, string.Empty));
            }
            catch
            {
                return "weapon";
            }
        }

        private string GetAttackStatus(PC pc, Mob target, bool isThinking)
        {
            if (!isThinking) return "not your action";
            if (pc.IsJammed()) return "weapon jammed";
            if (pc.IsOutOfAmmo()) return "out of ammo";
            return "";
        }

        private bool CanPerformAttack(PC pc, Mob target)
        {
            // Check if PC can attack this target (line of sight, range, faction)
            try
            {
                return pc.CanAttack(target, true, false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Build attack info string for melee/AOE weapons.
        /// </summary>
        private string BuildAttackInfo(PC pc, Mob target, int apCost)
        {
            var parts = new List<string>();
            parts.Add(apCost + " AP");

            try
            {
                int hitChance = Mathf.Min(pc.GetChanceToHit(target, false), 100);
                parts.Add(hitChance + "% hit");

                int critChance = Mathf.Min(pc.GetChanceToCriticalHit(target), 100);
                if (critChance > 0)
                    parts.Add(critChance + "% crit");

                var weaponInstance = pc.pcStats.GetWeaponInstance();
                if (weaponInstance != null)
                {
                    int minDmg = weaponInstance.GetMinDamage();
                    int maxDmg = weaponInstance.GetMaxDamage();
                    try
                    {
                        Targetable.DamageMitigation mit;
                        minDmg = pc.CalculateDamage(target, pc.transform.position, minDmg,
                            out mit, weaponInstance.template as ItemTemplate_Weapon);
                        maxDmg = pc.CalculateDamage(target, pc.transform.position, maxDmg,
                            out mit, weaponInstance.template as ItemTemplate_Weapon);
                    }
                    catch { }

                    if (minDmg == maxDmg)
                        parts.Add(minDmg + " damage");
                    else
                        parts.Add(minDmg + " to " + maxDmg + " damage");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] BuildAttackInfo error: {ex.Message}");
            }

            return string.Join(", ", parts.ToArray());
        }

        /// <summary>
        /// Build attack info string for a specific ranged firing mode.
        /// </summary>
        private string BuildAttackInfoForMode(PC pc, Mob target, int apCost, int ammoCost, int modePenalty)
        {
            var parts = new List<string>();

            try
            {
                // GetChanceToHit uses the current firingModeIndex internally via GetFiringModePenalty
                int hitChance = Mathf.Min(pc.GetChanceToHit(target, false), 100);
                parts.Add(hitChance + "% hit");

                int critChance = Mathf.Min(pc.GetChanceToCriticalHit(target), 100);
                if (critChance > 0)
                    parts.Add(critChance + "% crit");

                var weaponInstance = pc.pcStats.GetWeaponInstance();
                if (weaponInstance != null)
                {
                    int minDmg = weaponInstance.GetMinDamage();
                    int maxDmg = weaponInstance.GetMaxDamage();
                    try
                    {
                        Targetable.DamageMitigation mit;
                        minDmg = pc.CalculateDamage(target, pc.transform.position, minDmg,
                            out mit, weaponInstance.template as ItemTemplate_Weapon);
                        maxDmg = pc.CalculateDamage(target, pc.transform.position, maxDmg,
                            out mit, weaponInstance.template as ItemTemplate_Weapon);
                    }
                    catch { }

                    if (minDmg == maxDmg)
                        parts.Add(minDmg + " damage");
                    else
                        parts.Add(minDmg + " to " + maxDmg + " damage");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] BuildAttackInfoForMode error: {ex.Message}");
            }

            return string.Join(", ", parts.ToArray());
        }

        private void ExecuteAttack(PC pc, Mob target, int firingModeIndex)
        {
            try
            {
                // Set firing mode if ranged
                if (firingModeIndex >= 0)
                {
                    var rangedInstance = pc.pcStats.GetWeaponInstance() as ItemInstance_WeaponRanged;
                    if (rangedInstance != null)
                        rangedInstance.ChangeFiringMode(firingModeIndex);
                }

                // Match the game's AttackModePanel.OpenTargetMenuOrAttackPreSelected flow
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                inputManager.ClearSelectedSquare();
                inputManager.attackTypeSelected = true;

                var evt = ObjectPool.Get<EventInfo_CommandAttack>();
                evt.pc = pc;
                evt.target = target;
                evt.coneAttack = false;
                evt.specialAttack = false;
                evt.isAOESetup = false;
                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] ExecuteAttack error: {ex}");
                ScreenReaderManager.SpeakInterrupt("Attack failed");
            }
        }

        private string FormatTargetAction(CombatAction action)
        {
            int position = targetActionList.IndexOf(action) + 1;
            string result = action.Label;

            if (!string.IsNullOrEmpty(action.Status))
                result += ", " + action.Status;

            if (!action.IsEnabled)
                result += ", unavailable";

            result += ", " + position + " of " + targetActionList.Count;
            return result;
        }

        private void AnnounceCurrentTargetAction()
        {
            if (targetActionIndex < 0 || targetActionIndex >= targetActionList.Count) return;
            ScreenReaderManager.SpeakInterrupt(FormatTargetAction(targetActionList[targetActionIndex]));
        }

        private void ExecuteCurrentTargetAction()
        {
            if (targetActionIndex < 0 || targetActionIndex >= targetActionList.Count) return;

            var action = targetActionList[targetActionIndex];
            if (!action.IsEnabled)
            {
                ScreenReaderManager.SpeakInterrupt(action.Label + ", unavailable");
                return;
            }

            ScreenReaderManager.SpeakInterrupt(action.Label);
            ExitTargetActionsBrowse();

            try
            {
                action.Execute();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] Target action execution error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Action failed");
            }
        }

        private void ExitTargetActionsBrowse()
        {
            browsingTargetActions = false;
            targetActionList.Clear();
            targetInfoLines.Clear();
            targetInfoIndex = 0;
            targetMenuTab = 0;
            targetMob = null;
            ScreenReaderManager.SpeakInterrupt("Target menu closed");
        }

        private void BuildTargetInfoLines(Mob target)
        {
            targetInfoLines.Clear();
            if (target == null) return;

            try
            {
                // Name and type
                string name = GetMobName(target);
                string mobType = "";
                if (target.template != null)
                {
                    var mt = target.template.mobType;
                    if (mt == MobTemplate.MobType.Human) mobType = "human";
                    else if (mt == MobTemplate.MobType.Animal) mobType = "animal";
                    else if (mt == MobTemplate.MobType.Synth) mobType = "synth";
                    else mobType = mt.ToString().ToLower();
                }
                string nameEntry = name;
                if (!string.IsNullOrEmpty(mobType))
                    nameEntry += ", " + mobType;
                if (target is NPC && (target as NPC).isPartyFollower)
                    nameEntry += ", party follower";
                targetInfoLines.Add(nameEntry);

                // HP
                float maxHP = target.stats.GetMaxHP();
                float curHP = Mathf.Max(target.curHP, 0f);
                if (maxHP > 0)
                {
                    float pct = (curHP / maxHP) * 100f;
                    targetInfoLines.Add("Health: " + curHP.ToString("F0") + " of "
                        + maxHP.ToString("F0") + ", " + pct.ToString("F0") + "%");
                }

                // Armor and evasion
                int armor = Mathf.Max(target.stats.GetArmor(), 0);
                int evasion = Mathf.Clamp(target.stats.GetChanceToEvade(), 0, 100);
                targetInfoLines.Add("Armor: " + armor);
                targetInfoLines.Add("Evasion: " + evasion + "%");

                // Conductive (takes extra electrical damage)
                if (target.template is NPCTemplate)
                {
                    var npcTemplate = target.template as NPCTemplate;
                    if (npcTemplate.conductive)
                        targetInfoLines.Add("Conductive: takes extra electrical damage");
                }

                // Enemy weapon
                var weaponTemplate = target.stats.GetWeaponTemplate();
                if (weaponTemplate != null)
                {
                    string weapName = UITextExtractor.CleanText(
                        Language.Localize(weaponTemplate.displayName, false, false, string.Empty));
                    string rangeCategory = target.stats.GetAttackRangeString();
                    targetInfoLines.Add("Weapon: " + weapName + ", " + weaponTemplate.minDamage
                        + " to " + weaponTemplate.maxDamage + " damage, " + rangeCategory + " range");
                }

                // AP and initiative
                int ap = target.stats.GetActionPoints();
                targetInfoLines.Add("Action points: " + ap);
                if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                    targetInfoLines.Add("AP remaining this turn: " + target.combatActionPointsRemaining);

                float initiative = target.stats.GetActionRechargeRate();
                targetInfoLines.Add("Combat initiative: " + initiative.ToString("F1"));

                // Cover state
                if (target.inCover)
                {
                    string coverType = target.coverType == Cover.CoverType.Tall ? "tall" : "short";
                    targetInfoLines.Add("In " + coverType + " cover");
                }

                if (target.isCrouching)
                    targetInfoLines.Add("Crouching");

                // Distance from current actor
                Mob actor = GetCurrentActor();
                if (actor != null)
                {
                    string distStr = TileCoordinateSystem.GetDistanceText(actor.transform.position, target.transform.position);
                    targetInfoLines.Add("Distance: " + distStr);
                }

                // Status effects
                if (target.template != null && target.template.statusEffects != null)
                {
                    foreach (var effect in target.template.statusEffects)
                    {
                        string line = Helpers.StatusEffectHelper.BuildEffectLine(effect);
                        if (!string.IsNullOrEmpty(line))
                            targetInfoLines.Add("Effect: " + line);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] BuildTargetInfoLines error: {ex.Message}");
                if (targetInfoLines.Count == 0)
                    targetInfoLines.Add("Info unavailable");
            }
        }

        private string FormatInfoLine(int index)
        {
            if (index < 0 || index >= targetInfoLines.Count) return "";
            return targetInfoLines[index] + ", " + (index + 1) + " of " + targetInfoLines.Count;
        }

        // =====================================================================
        // Combat Movement (Right Bracket)
        // =====================================================================

        private void MoveToCursor()
        {
            try
            {
                var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
                if (cm == null || !cm.isPlayersTurn)
                {
                    ScreenReaderManager.SpeakInterrupt("Not your turn");
                    return;
                }

                PC pc = cm.GetCurrentMob() as PC;
                if (pc == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No active character");
                    return;
                }

                if (pc.combatActionState != Mob.CombatActionState.THINKING)
                {
                    ScreenReaderManager.SpeakInterrupt("Character is busy");
                    return;
                }

                if (pc.currentSquare == null)
                {
                    ScreenReaderManager.SpeakInterrupt("Cannot determine character position");
                    return;
                }

                // Find the target node at cursor position
                CombatAStarNode targetNode = GetNodeAtGridId(cursorGridId);
                if (targetNode == null)
                {
                    ScreenReaderManager.SpeakInterrupt("No valid tile at cursor");
                    return;
                }

                // Check if already on this tile
                if (pc.currentSquare == targetNode)
                {
                    ScreenReaderManager.SpeakInterrupt("Already here");
                    return;
                }

                var combatAStar = MonoBehaviourSingleton<CombatAStar>.GetInstance();

                // Check if tile is occupied. Use IsNodeOpen, which consults the A*'s
                // current `closed` dict (refreshed each turn and on mob death). The raw
                // node.occupant field is persistent and goes stale when mobs move away.
                if (!combatAStar.IsNodeOpen(targetNode))
                {
                    ScreenReaderManager.SpeakInterrupt("Tile is occupied");
                    return;
                }

                int standCost = pc.GetActionPointsToStand();
                int availableAP = pc.combatActionPointsRemaining - standCost;

                // Search for a path
                var pathNodes = combatAStar.Search(
                    pc.currentSquare, targetNode,
                    true,   // useLadders
                    true,   // limitActionPoints
                    availableAP,
                    pc.stats.GetCombatSpeed());

                if (pathNodes == null || pathNodes.Count == 0)
                {
                    ScreenReaderManager.SpeakInterrupt("No path available, not enough AP or blocked");
                    return;
                }

                int pathCost = combatAStar.GetPathCost(pc.stats.GetCombatSpeed(), standCost);
                if (pathCost <= 0)
                {
                    ScreenReaderManager.SpeakInterrupt("Cannot move there");
                    return;
                }

                // Execute the move — match the game's InputManager flow exactly
                MonoBehaviourSingleton<GlobalFxHandler>.GetInstance().Combat_OnMove();

                var evt = ObjectPool.Get<EventInfo_CommandMove>();
                evt.mob = pc;
                evt.destination = targetNode.position;
                evt.path = combatAStar.VectorSmoothPath();
                evt.actionPointCost = pathCost;
                evt.sprint = false;
                evt.closeEnough = 0f;
                evt.activateDelay = 0f;
                evt.dontClearStack = false;
                evt.isPlayerInput = true;
                MonoBehaviourSingleton<EventManager>.GetInstance().Publish(evt);

                combatAStar.ClearPath();
                MonoBehaviourSingleton<CursorManager>.GetInstance().ClearCoverCursor();
                MonoBehaviourSingleton<Game>.GetInstance().cameraController.FollowPC(pc);

                int apAfter = pc.combatActionPointsRemaining - pathCost;
                ScreenReaderManager.SpeakInterrupt("Moving, " + pathCost + " AP, " + apAfter + " remaining");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CombatState] MoveToCursor error: {ex.Message}");
                ScreenReaderManager.SpeakInterrupt("Move failed");
            }
        }

        // =====================================================================
        // Combat Log Viewer (L key)
        // =====================================================================

        private void OpenCombatLog()
        {
            var log = Patches.HUD_Controller_QueueTextDescription_Patch.CombatLog;
            if (log.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("Combat log is empty");
                return;
            }

            browsingLog = true;
            // Start at the most recent entry
            logIndex = log.Count - 1;

            ScreenReaderManager.SpeakInterrupt("Combat log, " + log.Count + " entries. "
                + FormatLogEntry(log, logIndex));
        }

        private string FormatLogEntry(System.Collections.Generic.List<string> log, int index)
        {
            if (index < 0 || index >= log.Count) return "";
            // Show position from newest: entry 1 is the most recent
            int fromNewest = log.Count - index;
            return log[index] + ", " + fromNewest + " of " + log.Count;
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

        // True if any Ranger-faction mob (the player party) has line of sight to the target.
        // Filters out hostile enemies in the initiative list that haven't been spotted yet.
        private bool IsMobRevealedToParty(CombatManager cm, Mob target)
        {
            if (cm == null || target == null) return true;
            try { return cm.IsTargetVisibleToFaction(target, Faction.Ranger); }
            catch { return true; }
        }

        // Mirrors CombatManager.UpdateDisplayQueue's filter so our list matches the on-screen tracker.
        // PCs always count; NPCs must have engaged hostiles, not be waiting to join, and not be in doNothing AI.
        private bool IsActivelyInCombat(Mob mob)
        {
            if (mob == null) return false;
            if (mob is PC) return true;

            var npc = mob as NPC;
            if (npc == null) return true;
            if (npc.waitToJoinCombat) return false;
            if (!npc.HasEnemiesInCombat()) return false;

            var ai = mob.GetActiveAI() as AIBehaviour_NPCCombat;
            if (ai != null && ai.doNothing) return false;

            return true;
        }

        private void BuildInitiativeList()
        {
            initiativeList.Clear();

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return;

            Mob currentActor = GetCurrentActor();

            // Use actQueue (private) for accurate current-round turn order
            if (actQueueField == null)
            {
                actQueueField = typeof(CombatManager).GetField("actQueue",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            var actQueue = actQueueField?.GetValue(cm) as List<Mob>;
            var addedMobs = new HashSet<Mob>();

            // First: add mobs from actQueue (remaining turns this round)
            if (actQueue != null)
            {
                foreach (var mob in actQueue)
                {
                    if (mob == null) continue;
                    if (mob.mobState == Mob.MobState.DEAD || mob.mobState == Mob.MobState.UNCONSCIOUS) continue;
                    if (!IsActivelyInCombat(mob)) continue;

                    bool hostile = false;
                    if (mob is NPC) hostile = mob.HatesParty();

                    // Hide hostile enemies the party has not yet spotted
                    if (hostile && !IsMobRevealedToParty(cm, mob)) continue;

                    initiativeList.Add(new InitiativeEntry
                    {
                        Name = GetDisplayName(mob.template != null ? mob.template.displayName : mob.name),
                        IsHostile = hostile,
                        IsCurrentActor = (mob == currentActor),
                        Mob = mob,
                        Details = BuildInitiativeMobDetails(mob)
                    });
                    addedMobs.Add(mob);
                }
            }

            // Second: add remaining active combatants from cm.mobs not already in actQueue
            // These are combatants who already acted this round or are waiting to join
            if (cm.mobs != null)
            {
                foreach (var mob in cm.mobs)
                {
                    if (mob == null) continue;
                    if (addedMobs.Contains(mob)) continue;
                    if (mob.mobState == Mob.MobState.DEAD || mob.mobState == Mob.MobState.UNCONSCIOUS) continue;
                    if (!IsActivelyInCombat(mob)) continue;

                    bool hostile = false;
                    if (mob is NPC) hostile = mob.HatesParty();

                    // Hide hostile enemies the party has not yet spotted
                    if (hostile && !IsMobRevealedToParty(cm, mob)) continue;

                    initiativeList.Add(new InitiativeEntry
                    {
                        Name = GetDisplayName(mob.template != null ? mob.template.displayName : mob.name),
                        IsHostile = hostile,
                        IsCurrentActor = (mob == currentActor),
                        Mob = mob,
                        Details = BuildInitiativeMobDetails(mob)
                    });
                }
            }

            // Also include bombs from displayQueue
            var displayQueue = cm.displayQueue;
            if (displayQueue != null)
            {
                foreach (var actor in displayQueue)
                {
                    if (actor == null) continue;
                    if (actor.name != "Bomb") continue;
                    initiativeList.Add(new InitiativeEntry
                    {
                        Name = "Bomb",
                        IsHostile = true,
                        IsCurrentActor = false,
                        Mob = null,
                        Details = "explosive"
                    });
                }
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
