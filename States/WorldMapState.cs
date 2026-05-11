using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;
using Wasteland2AccessibilityMod.Patches;

namespace Wasteland2AccessibilityMod.States
{
    public class WorldMapState : IAccessibilityState
    {
        public string Name => "WorldMap";
        public int Priority => 20;

        // Review cursor
        private Vector3 cursorPosition;
        private bool cursorInitialized;
        private int stepSize = 15;
        private const int MIN_STEP_SIZE = 1;
        private const int MAX_STEP_SIZE = 100;

        // Input repeat delays
        private float lastMoveTime;
        private float lastStepChangeTime;
        private const float MOVE_REPEAT_DELAY = 0.25f;
        private const float STEP_CHANGE_REPEAT_DELAY = 0.1f;

        // Camera
        private bool cameraFollowsCursor = true;

        // Reflection for private fields
        private static FieldInfo intendedPathField;

        // Last announcement for repeat
        private string lastAnnouncement = "";

        // Cardinal directions: North, East, South, West
        private static readonly Vector3[] DIRECTIONS =
        {
            new Vector3(0, 0, 1),   // North (+Z)
            new Vector3(1, 0, 0),   // East (+X)
            new Vector3(0, 0, -1),  // South (-Z)
            new Vector3(-1, 0, 0)   // West (-X)
        };


        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;

                var game = MonoBehaviourSingleton<Game>.GetInstance();
                if (game.state != GameState.WorldMap) return false;

                if (WorldMapManager.instance == null) return false;
                if (WorldMapParty.instance == null) return false;

                if (Drama.isConversationOn) return false;

                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
                    return false;

                if (MonoBehaviourSingleton<InputManager>.HasInstance() &&
                    MonoBehaviourSingleton<InputManager>.GetInstance().IsInputFrozen())
                    return false;

                return true;
            }
        }

        // Track which map we were on to detect actual map changes vs menu open/close
        private string lastMapName = "";

        public void OnActivated()
        {
            string mapName = "Arizona";
            if (Application.loadedLevelName == "LosAngelesWorldMap")
                mapName = "Los Angeles";

            bool mapChanged = mapName != lastMapName;
            lastMapName = mapName;

            if (!cursorInitialized || mapChanged)
            {
                // First activation or map change: initialize cursor to party position
                if (WorldMapParty.instance != null)
                {
                    cursorPosition = WorldMapParty.instance.transform.position;
                    cursorInitialized = true;
                }

                WorldMapProximityAlert.Reset();
                WorldMapRadiationCloud_CheckDiscovery_Patch.Reset();
                WorldMapNavigationManager.Reset();

                ScreenReaderManager.SpeakInterrupt(
                    $"World map, {mapName}. Arrows to explore, Page Up Down for locations, Shift Left Right for step size, W for water, Shift W for water cost to cursor, step size {stepSize}");
            }
            else
            {
                // Returning from a menu: restore camera to cursor position
                if (cameraFollowsCursor)
                    SnapCameraToCursor();
            }

            MelonLogger.Msg($"[WorldMapState] Activated on {mapName}, cursorInitialized={cursorInitialized}, mapChanged={mapChanged}");
        }

        public void OnDeactivated()
        {
            // Don't reset cursor position - preserve it for when we return
            MelonLogger.Msg("[WorldMapState] Deactivated");
        }

        public bool HandleInput()
        {
            if (!cursorInitialized) return false;

            // F1-F7: switch party members
            if (HandlePartySwitch())
            {
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            SuppressInput();

            float currentTime = Time.time;

            // --- Step size adjustment: Shift+Left/Right ---
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
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
                            ScreenReaderManager.SpeakInterrupt($"Step size {stepSize}");
                        }
                        return true;
                    }
                    if (Input.GetKey(KeyCode.LeftArrow))
                    {
                        if (stepSize > MIN_STEP_SIZE)
                        {
                            stepSize--;
                            lastStepChangeTime = currentTime;
                            ScreenReaderManager.SpeakInterrupt($"Step size {stepSize}");
                        }
                        return true;
                    }
                }

                // Shift+Home: Jump cursor to party position
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    MelonLogger.Msg("[WorldMapState] Shift+Home detected, jumping to party");
                    JumpToParty();
                    return true;
                }

                // Shift+End: Announce distance from cursor to party
                if (Input.GetKeyDown(KeyCode.End))
                {
                    MelonLogger.Msg("[WorldMapState] Shift+End detected, announcing distance to party");
                    AnnounceDistanceToParty();
                    return true;
                }

                // Shift+W: Estimate water cost from party to cursor
                if (Input.GetKeyDown(KeyCode.W))
                {
                    AnnounceWaterCostToCursor();
                    return true;
                }

                // Consume shift+arrow even if repeat delay not met
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
                    return true;

                // Don't consume other shift combinations
            }

            // --- Arrow key cursor movement ---
            bool canMove = (currentTime - lastMoveTime) >= MOVE_REPEAT_DELAY;
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!shiftHeld && canMove)
            {
                if (Input.GetKey(KeyCode.UpArrow))
                {
                    MoveCursor(0);
                    lastMoveTime = currentTime;
                    return true;
                }
                if (Input.GetKey(KeyCode.RightArrow))
                {
                    MoveCursor(1);
                    lastMoveTime = currentTime;
                    return true;
                }
                if (Input.GetKey(KeyCode.DownArrow))
                {
                    MoveCursor(2);
                    lastMoveTime = currentTime;
                    return true;
                }
                if (Input.GetKey(KeyCode.LeftArrow))
                {
                    MoveCursor(3);
                    lastMoveTime = currentTime;
                    return true;
                }
            }

            // --- POI cycling: PageUp/PageDown ---
            bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

            if (ctrlHeld)
            {
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    MelonLogger.Msg("[WorldMapState] Ctrl+PageUp detected, previous category");
                    WorldMapNavigationManager.PreviousCategory(cursorPosition);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    MelonLogger.Msg("[WorldMapState] Ctrl+PageDown detected, next category");
                    WorldMapNavigationManager.NextCategory(cursorPosition);
                    return true;
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    MelonLogger.Msg($"[WorldMapState] PageDown detected, cycling next POI. Cursor at {cursorPosition}");
                    WorldMapNavigationManager.CycleNext(cursorPosition);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    MelonLogger.Msg($"[WorldMapState] PageUp detected, cycling previous POI. Cursor at {cursorPosition}");
                    WorldMapNavigationManager.CyclePrevious(cursorPosition);
                    return true;
                }
            }

            // --- Home: Jump cursor to selected POI ---
            if (Input.GetKeyDown(KeyCode.Home))
            {
                MelonLogger.Msg("[WorldMapState] Home detected (no shift), jumping to selected POI");
                JumpToSelectedPOI();
                return true;
            }

            // --- End: Announce distance from cursor to selected POI ---
            if (Input.GetKeyDown(KeyCode.End))
            {
                MelonLogger.Msg("[WorldMapState] End detected (no shift), announcing distance to selected POI");
                AnnounceDistanceToSelectedPOI();
                return true;
            }

            // --- ] Move party to cursor position ---
            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                MovePartyToCursor();
                return true;
            }

            // --- Backspace: Stop party movement ---
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                StopParty();
                return true;
            }

            // --- Enter: Interact with POI at cursor ---
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                InteractWithPOIAtCursor();
                return true;
            }

            // --- Space: Announce cursor summary (suppress game's Space) ---
            if (Input.GetKeyDown(KeyCode.Space))
            {
                AnnounceCursorSummary();
                return true;
            }

            // --- Backslash: Repeat last announcement ---
            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                if (!string.IsNullOrEmpty(lastAnnouncement))
                    ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
                else
                    WorldMapNavigationManager.RepeatLastAnnouncement();
                return true;
            }

            // --- W: Announce water supply ---
            if (Input.GetKeyDown(KeyCode.W))
            {
                AnnounceWater();
                return true;
            }

            // --- F: Toggle camera follow ---
            if (Input.GetKeyDown(KeyCode.F))
            {
                cameraFollowsCursor = !cameraFollowsCursor;
                string status = cameraFollowsCursor ? "Camera follows cursor" : "Camera stationary";
                ScreenReaderManager.SpeakInterrupt(status);
                if (cameraFollowsCursor)
                    SnapCameraToCursor();
                return true;
            }

            // R: radio call
            if (Input.GetKeyDown(KeyCode.R))
            {
                if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
                {
                    MonoBehaviourSingleton<EventManager>.GetInstance().Publish(ObjectPool.Get<EventInfo_RadioAnswer>());
                }
                InputSuppressor.ShouldSuppressButtonEvents = true;
                return true;
            }

            // I: open inventory
            if (Input.GetKeyDown(KeyCode.I))
            {
                MonoBehaviourSingleton<GUIManager>.GetInstance().ToggleCharacterInfoMenu();
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

            // Suppress arrow keys even when repeat delay not met
            if (!shiftHeld && (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
            {
                return true;
            }

            return false;
        }

        // --- Cursor Movement ---

        private void MoveCursor(int directionIndex)
        {
            Vector3 direction = DIRECTIONS[directionIndex];
            Vector3 newPosition = cursorPosition + direction * stepSize;

            // Validate position on NavMesh
            NavMeshHit hit;
            if (NavMesh.SamplePosition(newPosition, out hit, 15f, 1))
            {
                cursorPosition = hit.position;

                if (cameraFollowsCursor)
                    SnapCameraToCursor();

                // Check proximity alerts
                string proximityAlert = WorldMapProximityAlert.CheckProximity(cursorPosition);

                // Build announcement
                string announcement = "";
                if (!string.IsNullOrEmpty(proximityAlert))
                {
                    announcement = proximityAlert;
                }
                else
                {
                    // Nothing nearby - announce distance from party
                    announcement = GetEmptyTileAnnouncement();
                }

                lastAnnouncement = announcement;
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt($"Blocked, {CardinalDirections.Names[directionIndex]}");
            }
        }

        private string GetEmptyTileAnnouncement()
        {
            if (WorldMapParty.instance == null) return "Empty";

            Vector3 partyPos = WorldMapParty.instance.transform.position;
            float distance = WorldMapMath.Vector2Distance(cursorPosition, partyPos);
            string direction = DirectionHelper.GetDirectionDescription(partyPos, cursorPosition);

            // Check if inside radiation
            int radLevel = WorldMapProximityAlert.GetRadiationLevelAtPoint(cursorPosition);
            string radInfo = "";
            if (radLevel > 0)
            {
                string severity = radLevel >= 3 ? "lethal" : radLevel == 2 ? "high" : "low";
                radInfo = $", {severity} radiation";
            }

            return $"{Mathf.RoundToInt(distance)} units {direction} from party{radInfo}";
        }

        // --- Camera ---

        private void SnapCameraToCursor()
        {
            if (WorldMapCameraController.instance == null) return;
            WorldMapCameraController.instance.Snap(cursorPosition, false);
        }

        // --- Jump commands ---

        private void JumpToParty()
        {
            if (WorldMapParty.instance == null)
            {
                ScreenReaderManager.SpeakInterrupt("Party not found");
                return;
            }

            cursorPosition = WorldMapParty.instance.transform.position;
            WorldMapProximityAlert.Reset();

            if (cameraFollowsCursor)
                SnapCameraToCursor();

            lastAnnouncement = "Cursor at party position";
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
            MelonLogger.Msg("[WorldMapState] Jumped cursor to party");
        }

        private void JumpToSelectedPOI()
        {
            Vector3? selectedPos = WorldMapNavigationManager.GetSelectedPosition();
            if (selectedPos == null)
            {
                ScreenReaderManager.SpeakInterrupt("No location selected");
                return;
            }

            cursorPosition = selectedPos.Value;
            WorldMapProximityAlert.Reset();

            if (cameraFollowsCursor)
                SnapCameraToCursor();

            // Announce what's at the cursor now
            string proximityAlert = WorldMapProximityAlert.CheckProximity(cursorPosition);
            if (!string.IsNullOrEmpty(proximityAlert))
            {
                lastAnnouncement = $"Jumped to selection. {proximityAlert}";
            }
            else
            {
                lastAnnouncement = "Jumped to selection";
            }
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);

            MelonLogger.Msg($"[WorldMapState] Jumped cursor to selected POI at {cursorPosition}");
        }

        // --- Distance announcements ---

        private void AnnounceDistanceToSelectedPOI()
        {
            Vector3? selectedPos = WorldMapNavigationManager.GetSelectedPosition();
            if (selectedPos == null)
            {
                ScreenReaderManager.SpeakInterrupt("No location selected");
                return;
            }

            float distance = WorldMapMath.Vector2Distance(cursorPosition, selectedPos.Value);
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, selectedPos.Value);

            WorldMapPOI poi = WorldMapNavigationManager.GetSelectedPOI();
            string name = poi != null ? WorldMapNavigationManager.GetPOIName(poi) : "Selected location";

            lastAnnouncement = $"{name}, {Mathf.RoundToInt(distance)} units, {direction} from cursor";
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        private void AnnounceWater()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance())
            {
                ScreenReaderManager.SpeakInterrupt("Water info unavailable");
                return;
            }

            var game = MonoBehaviourSingleton<Game>.GetInstance();
            int current = Mathf.FloorToInt(game.water);
            int max = Mathf.FloorToInt(game.GetMaxWater());

            lastAnnouncement = $"Water, {current} of {max}";
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        private void AnnounceWaterCostToCursor()
        {
            var party = WorldMapParty.instance;
            if (party == null)
            {
                ScreenReaderManager.SpeakInterrupt("Party not found");
                return;
            }

            float distance = WorldMapMath.Vector2Distance(party.transform.position, cursorPosition);
            float sampleDistance = party.sampleDistance;
            if (sampleDistance <= 0)
            {
                ScreenReaderManager.SpeakInterrupt("Water cost unavailable");
                return;
            }

            int estimatedCost = Mathf.CeilToInt(distance / sampleDistance);

            string costInfo = $"Estimated {estimatedCost} water to cursor";

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                int current = Mathf.FloorToInt(MonoBehaviourSingleton<Game>.GetInstance().water);
                int remaining = current - estimatedCost;
                costInfo += $", {current} available, {remaining} remaining";
            }

            lastAnnouncement = costInfo;
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        private void AnnounceDistanceToParty()
        {
            if (WorldMapParty.instance == null)
            {
                ScreenReaderManager.SpeakInterrupt("Party not found");
                return;
            }

            Vector3 partyPos = WorldMapParty.instance.transform.position;
            float distance = WorldMapMath.Vector2Distance(cursorPosition, partyPos);
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, partyPos);

            lastAnnouncement = $"Party, {Mathf.RoundToInt(distance)} units, {direction} from cursor";
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        // --- Party movement ---

        private void MovePartyToCursor()
        {
            var party = WorldMapParty.instance;
            if (party == null)
            {
                ScreenReaderManager.SpeakInterrupt("Party not available");
                return;
            }

            // Validate cursor position is on NavMesh
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(cursorPosition, out hit, 15f, 1))
            {
                ScreenReaderManager.SpeakInterrupt("Cannot move to this position");
                return;
            }

            party.StopAndClear();
            party.targetPOI = null;
            int waterCost = party.CalculatePath(hit.position);

            // Check if path crosses radiation (intendedPath is private, use reflection)
            string radiationWarning = "";
            try
            {
                if (intendedPathField == null)
                    intendedPathField = typeof(WorldMapParty).GetField("intendedPath", BindingFlags.NonPublic | BindingFlags.Instance);

                if (intendedPathField != null)
                {
                    var path = intendedPathField.GetValue(party) as NavMeshPath;
                    if (path != null && path.corners != null && path.corners.Length > 1)
                    {
                        radiationWarning = WorldMapProximityAlert.CheckPathForRadiation(path.corners);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[WorldMapState] Could not check path for radiation: {ex.Message}");
            }

            // Build announcement
            string announcement = $"Moving party, {waterCost} water";
            if (!string.IsNullOrEmpty(radiationWarning))
                announcement += $". {radiationWarning}";

            lastAnnouncement = announcement;
            ScreenReaderManager.SpeakInterrupt(announcement);

            party.StartMoving();

            MelonLogger.Msg($"[WorldMapState] Moving party to {hit.position}, water cost: {waterCost}");
        }

        private void StopParty()
        {
            var party = WorldMapParty.instance;
            if (party == null) return;

            // Check if party is actually moving
            var agent = party.GetComponent<NavMeshAgent>();
            if (agent != null && agent.hasPath)
            {
                party.StopAndClear();
                ScreenReaderManager.SpeakInterrupt("Party stopped");
                MelonLogger.Msg("[WorldMapState] Party stopped");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Party is not moving");
            }
        }

        // --- POI Interaction ---

        private void InteractWithPOIAtCursor()
        {
            // Find closest visible POI to cursor
            WorldMapPOI[] pois = null;
            if (WorldMapInput.instance != null)
                pois = WorldMapInput.instance.pois;
            if (pois == null || pois.Length == 0)
                pois = Object.FindObjectsOfType(typeof(WorldMapPOI)) as WorldMapPOI[];

            if (pois == null || pois.Length == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No locations found");
                return;
            }

            WorldMapPOI closestPOI = null;
            float closestDistance = float.MaxValue;

            foreach (var poi in pois)
            {
                if (poi == null) continue;
                if (!poi.IsVisible()) continue;

                float distance = WorldMapMath.Vector2Distance(cursorPosition, poi.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPOI = poi;
                }
            }

            if (closestPOI == null || closestDistance > closestPOI.instigateRadius)
            {
                if (closestPOI != null)
                {
                    string name = WorldMapNavigationManager.GetPOIName(closestPOI);
                    ScreenReaderManager.SpeakInterrupt(
                        $"{name} is {Mathf.RoundToInt(closestDistance)} units away, too far to interact");
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("No location within reach");
                }
                return;
            }

            // Check if party is already within range
            var party = WorldMapParty.instance;
            if (party == null) return;

            string poiName = WorldMapNavigationManager.GetPOIName(closestPOI);
            float partyDistance = WorldMapMath.Vector2Distance(party.transform.position, closestPOI.transform.position);

            if (partyDistance <= closestPOI.instigateRadius)
            {
                // Party is at the POI - interact directly
                closestPOI.OnClick();
                closestPOI.Instigate();
                ScreenReaderManager.SpeakInterrupt($"Entering {poiName}");
                MelonLogger.Msg($"[WorldMapState] Instigating POI: {poiName}");
            }
            else
            {
                // Path party to POI
                party.StopAndClear();
                party.targetPOI = closestPOI;
                int waterCost = party.CalculatePath(closestPOI.transform.position);
                party.StartMoving();

                ScreenReaderManager.SpeakInterrupt($"Moving to {poiName}, {waterCost} water");
                MelonLogger.Msg($"[WorldMapState] Pathing to POI: {poiName}, water: {waterCost}");
            }
        }

        // --- Cursor summary ---

        private void AnnounceCursorSummary()
        {
            var parts = new System.Collections.Generic.List<string>();

            // Step size
            parts.Add($"Step size {stepSize}");

            // Distance from party
            if (WorldMapParty.instance != null)
            {
                Vector3 partyPos = WorldMapParty.instance.transform.position;
                float partyDist = WorldMapMath.Vector2Distance(cursorPosition, partyPos);
                string partyDir = DirectionHelper.GetDirectionDescription(partyPos, cursorPosition);
                parts.Add($"{Mathf.RoundToInt(partyDist)} units {partyDir} from party");
            }

            // Radiation at cursor
            int radLevel = WorldMapProximityAlert.GetRadiationLevelAtPoint(cursorPosition);
            if (radLevel > 0)
            {
                string severity = radLevel >= 3 ? "lethal" : radLevel == 2 ? "high" : "low";
                parts.Add($"In level {radLevel} {severity} radiation");
            }

            // Nearest POI
            WorldMapPOI[] pois = null;
            if (WorldMapInput.instance != null)
                pois = WorldMapInput.instance.pois;
            if (pois == null)
                pois = Object.FindObjectsOfType(typeof(WorldMapPOI)) as WorldMapPOI[];

            if (pois != null)
            {
                WorldMapPOI nearest = null;
                float nearestDist = float.MaxValue;

                foreach (var poi in pois)
                {
                    if (poi == null || !poi.IsVisible()) continue;
                    float d = WorldMapMath.Vector2Distance(cursorPosition, poi.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = poi;
                    }
                }

                if (nearest != null)
                {
                    string name = WorldMapNavigationManager.GetPOIName(nearest);
                    string dir = DirectionHelper.GetDirectionDescription(cursorPosition, nearest.transform.position);
                    parts.Add($"Nearest: {name}, {Mathf.RoundToInt(nearestDist)} units, {dir}");
                }
            }

            lastAnnouncement = string.Join(". ", parts.ToArray());
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        // --- Input suppression ---

        private bool HandlePartySwitch()
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

        private void SelectPartyMember(int index)
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

            string name = UITextExtractor.CleanText(
                Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            ScreenReaderManager.SpeakInterrupt($"{name}, {index + 1} of {party.Count}");
            MelonLogger.Msg($"[WorldMapState] Selected party member {index + 1}: {name}");
        }

        private void SuppressInput()
        {
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
        }

    }
}
