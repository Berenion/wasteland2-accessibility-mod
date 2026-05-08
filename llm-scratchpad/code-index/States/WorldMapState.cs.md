File: States/WorldMapState.cs — accessibility state for world-map mode; provides a review cursor, POI cycling, party movement, and water/radiation announcements; priority 20

namespace Wasteland2AccessibilityMod.States  (line 7)

class WorldMapState : IAccessibilityState  (line 9)

    // --- Identity ---
    public string Name => "WorldMap"  (line 11)
    public int Priority => 20  (line 12)

    // --- Fields ---
    private Vector3 cursorPosition  (line 15)
        // note: the review cursor, independent of the party's position; persists across menu open/close.
    private bool cursorInitialized  (line 16)
    private int stepSize = 15  (line 17)
    private const int MIN_STEP_SIZE = 1  (line 18)
    private const int MAX_STEP_SIZE = 100  (line 19)
    private float lastMoveTime  (line 22)
    private float lastStepChangeTime  (line 23)
    private const float MOVE_REPEAT_DELAY = 0.25f  (line 24)
    private const float STEP_CHANGE_REPEAT_DELAY = 0.1f  (line 25)
    private bool cameraFollowsCursor = true  (line 28)
    private static FieldInfo intendedPathField  (line 31)
        // note: cached reflection handle for WorldMapParty.intendedPath (private NavMeshPath); used in MovePartyToCursor to check path for radiation.
    private string lastAnnouncement  (line 34)
    private static readonly Vector3[] DIRECTIONS  (line 37)
        // note: North (+Z), East (+X), South (-Z), West (-X); indexed 0-3 matching DIRECTION_NAMES.
    private static readonly string[] DIRECTION_NAMES = { "north", "east", "south", "west" }  (line 45)
    private string lastMapName  (line 73)
        // note: used to distinguish genuine map changes (Arizona vs Los Angeles) from menu open/close cycles.

    // --- IsActive ---
    public bool IsActive { get; }  (line 47)
        // note: returns true only when all hold:
        //   Game singleton present AND game.state == GameState.WorldMap;
        //   WorldMapManager.instance != null AND WorldMapParty.instance != null;
        //   Drama.isConversationOn == false;
        //   no menu active in GUIManager;
        //   InputManager not frozen.

    // --- IAccessibilityState interface ---

    // Initializes cursor to party position on first activation or map change; on menu return, snaps camera to cursor.
    public void OnActivated()  (line 76)
        // note: detects map by Application.loadedLevelName ("LosAngelesWorldMap" -> "Los Angeles", else "Arizona").
        //   On first activation or map change: resets WorldMapProximityAlert, WorldMapRadiationCloud_CheckDiscovery_Patch,
        //   WorldMapNavigationManager, then speaks the full help string.
        //   On return from menu: only snaps camera if cameraFollowsCursor.

    // No-op except a log message; cursor position is intentionally preserved.
    public void OnDeactivated()  (line 111)

    // Full keyboard handler for world-map mode; returns true if input was consumed.
    public bool HandleInput()  (line 117)
        // note: large dispatch; full key map summary:
        //   F1-F7           — HandlePartySwitch (select party member)
        //   Shift+Right/Left— adjust stepSize (with repeat delay); consume even when delay not met
        //   Shift+Home      — JumpToParty
        //   Shift+End       — AnnounceDistanceToParty
        //   Shift+W         — AnnounceWaterCostToCursor
        //   Up/Down/Left/Right (no shift, with move repeat) — MoveCursor(N/S/E/W)
        //   Ctrl+PageUp/Down— WorldMapNavigationManager.PreviousCategory / NextCategory
        //   PageUp/Down     — WorldMapNavigationManager.CyclePrevious / CycleNext
        //   Home            — JumpToSelectedPOI
        //   End             — AnnounceDistanceToSelectedPOI
        //   ]               — MovePartyToCursor
        //   Backspace       — StopParty
        //   Enter           — InteractWithPOIAtCursor
        //   Space           — AnnounceCursorSummary
        //   Backslash       — repeat lastAnnouncement (or WorldMapNavigationManager.RepeatLastAnnouncement)
        //   W               — AnnounceWater
        //   F               — toggle cameraFollowsCursor
        //   R               — publish EventInfo_RadioAnswer (radio call)
        //   I               — GUIManager.ToggleCharacterInfoMenu
        //   Escape          — GUIManager.OpenPauseMenu
        //   Arrow keys (held, repeat delay not met) — consumed to prevent game processing.
        //   All calls SuppressInput() at entry (after party-switch check).

    // --- Private: cursor movement ---

    // Moves cursorPosition by stepSize in DIRECTIONS[directionIndex]; validates on NavMesh (SamplePosition radius 15).
    private void MoveCursor(int directionIndex)  (line 369)
        // note: on success, snaps camera (if cameraFollowsCursor), checks WorldMapProximityAlert.CheckProximity,
        //   falls back to GetEmptyTileAnnouncement if no alert. Speaks "Blocked, <direction>" on NavMesh miss.

    // Returns distance + direction from party to cursor, with radiation severity if cursor is inside a radiation zone.
    private string GetEmptyTileAnnouncement()  (line 407)
        // note: format "{N} units {direction} from party[, {severity} radiation]".

    // --- Private: camera ---

    // Calls WorldMapCameraController.instance.Snap(cursorPosition, false).
    private void SnapCameraToCursor()  (line 429)

    // --- Private: jump commands ---

    // Moves review cursor to WorldMapParty.instance.transform.position; resets proximity alert.
    private void JumpToParty()  (line 437)

    // Moves review cursor to WorldMapNavigationManager.GetSelectedPosition(); speaks proximity or "Jumped to selection".
    private void JumpToSelectedPOI()  (line 455)

    // --- Private: distance/resource announcements ---

    // Announces distance and direction from cursor to the currently selected POI.
    private void AnnounceDistanceToSelectedPOI()  (line 487)
        // note: format "{POI name}, {N} units, {direction} from cursor".

    // Announces current water supply as "Water, {current} of {max}".
    private void AnnounceWater()  (line 506)

    // Estimates water cost from party to cursor using party.sampleDistance; announces cost, available, and remaining.
    private void AnnounceWaterCostToCursor()  (line 522)
        // note: estimatedCost = ceil(distance / party.sampleDistance).

    // Announces distance and direction from cursor to party position.
    private void AnnounceDistanceToParty()  (line 554)

    // --- Private: party movement ---

    // Validates cursor on NavMesh, calls party.CalculatePath, checks intendedPath for radiation via reflection, calls party.StartMoving.
    private void MovePartyToCursor()  (line 572)
        // note: reads WorldMapParty.intendedPath (private) via cached FieldInfo to inspect path.corners for radiation.
        //   Announces "Moving party, {waterCost} water[. {radiationWarning}]".

    // Stops the party if it has an active NavMeshAgent path; speaks "Party stopped" or "Party is not moving".
    private void StopParty()  (line 627)

    // --- Private: POI interaction ---

    // Finds the closest visible WorldMapPOI within instigateRadius of cursor; enters it directly or paths the party to it.
    private void InteractWithPOIAtCursor()  (line 648)
        // note: POI list sourced from WorldMapInput.instance.pois, falls back to FindObjectsOfType.
        //   If party is already within instigateRadius: calls poi.OnClick() + poi.Instigate(), speaks "Entering {name}".
        //   Otherwise: StopAndClear, set targetPOI, CalculatePath, StartMoving, speaks "Moving to {name}, {waterCost} water".
        //   If closest POI is outside instigateRadius: speaks "{name} is {N} units away, too far to interact".

    // --- Private: cursor summary ---

    // Assembles and speaks step size, distance from party, radiation level, and nearest visible POI.
    private void AnnounceCursorSummary()  (line 724)

    // --- Private: input suppression ---

    // Checks F1-F7 keycodes; calls SelectPartyMember on first match and returns true.
    private bool HandlePartySwitch()  (line 785)

    // Makes PC at index the leader, adds to selection (preserving multi-select with Shift/Ctrl), speaks "{name}, {N} of {total}".
    private void SelectPartyMember(int index)  (line 799)

    // Sets ShouldSuppressGameInput and ShouldSuppressUINavigation to true.
    private void SuppressInput()  (line 841)

    // --- Private: utility ---

    // Returns the XZ-plane Euclidean distance between two Vector3 points (ignores Y).
    private static float Vector2Distance(Vector3 a, Vector3 b)  (line 849)
