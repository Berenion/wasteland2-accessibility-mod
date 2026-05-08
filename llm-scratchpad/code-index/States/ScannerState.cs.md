File: States/ScannerState.cs — environment scan feature that categorizes and announces nearby enemies, NPCs, party, loot, exits, and interactables (priority 80, always-listening hotkey).

namespace Wasteland2AccessibilityMod.States  (line 8)

class ScannerState : IAccessibilityState  (line 15)

    // --- Interface properties ---
    public string Name => "Scanner"  (line 17)
    public int Priority => 80  (line 18)

    // --- Constants ---
    private const float SHORT_RANGE = 10f  (line 21)
    private const float MEDIUM_RANGE = 25f  (line 22)
    private const float LONG_RANGE = 50f  (line 23)
        // note: hardcoded distance constants; cycled in order by CycleScanRange().

    // --- Fields ---
    private float currentScanRange = MEDIUM_RANGE  (line 26)
    private int rangeIndex = 1  (line 27)
        // note: 0=short, 1=medium, 2=long; cycles via (rangeIndex + 1) % 3.
    private bool scanInProgress = false  (line 30)
    private int currentCategory = 0  (line 31)
    private List<ScanResult> lastScanResults = new List<ScanResult>()  (line 32)
    private static readonly string[] CATEGORY_NAMES = { "Enemies", "NPCs", "Party", "Loot", "Exits", "Interactables" }  (line 35)
        // note: hardcoded dispatch table — order determines category cycling and AnnounceScanSummary output order.

    // --- Nested type ---
    private class ScanResult  (line 44)
        public string Name  (line 46)
        public string Category  (line 47)
        public float Distance  (line 48)
        public string Direction  (line 49)
        public Vector3 Position  (line 50)
        public object Source  (line 51)
            // note: loosely typed; holds Mob, NPC, PC, InteractableNexus, or SceneLoad reference.

    // --- Interface property ---
    // Active only during an in-progress scan; returns false while any menu or conversation is open.
    public bool IsActive { get; }  (line 54)
        // note: guards on GUIManager.IsAnyMenuActive() and Drama.isConversationOn.
        //       S key is also handled inside HandleInput before the IsActive guard, making ScannerState
        //       effectively always-listening for its hotkey.

    // --- Interface methods ---
    // S triggers a fresh scan; arrows cycle categories; Enter selects closest; R cycles range; Esc/S exits scan.
    public bool HandleInput()  (line 68)
        // note: S-key scan check runs before IsActive check (lines 71-80) — this is intentional so the scanner
        //       starts from any gameplay state. Suppresses UINavigation during active scan.

    public void OnActivated()  (line 129)
    public void OnDeactivated()  (line 134)

    // --- Private scan orchestration ---
    // Guards FOW readiness and party leader, then calls all ScanFor* methods and announces a summary.
    private void PerformScan()  (line 141)
        // note: scan origin is the party leader's transform.position. Sets scanInProgress=true before scanning.

    // Delegates to FOWHelper.IsVisibleThroughFOW.
    private bool IsVisibleThroughFOW(Vector3 position)  (line 175)

    // --- Per-category scan methods (each appends to lastScanResults) ---

    // Finds active, visible, in-range non-PC Mobs; classifies Faction.Bad NPCs and non-NPC mobs as hostile.
    private void ScanForEnemies(Vector3 origin)  (line 180)
        // note: uses FindObjectsOfType<Mob>(); skips PC subclass; uses IsVisibleThroughFOW filter.

    // Finds active, visible, in-range NPCs with faction != Faction.Bad.
    private void ScanForNPCs(Vector3 origin)  (line 228)

    // Iterates Game.party and Game.partyFollowers; skips scanner PC itself and dead members; appends ", unconscious" for UNCONSCIOUS state.
    private void ScanForParty(Vector3 origin, PC scanner)  (line 263)

    // Scans InteractableNexus.interactables for InteractableInventoryObject components with at least one allowed interaction.
    private void ScanForLoot(Vector3 origin)  (line 334)
        // note: checks FOWHelper.IsPerceptionGated(nexus). Tries nexus.drama first, falls back to nexus.GetComponent.
        //       Iterates GetAllowedInteractions() dictionary — only includes containers with a non-zero interaction value.

    // Finds active, visible SceneLoad components in range.
    private void ScanForExits(Vector3 origin)  (line 386)
        // note: uses exit.sceneName for label; falls back to "Exit".

    // Finds InteractableNexus entries not already placed by previous scan methods; uses IsAlreadyCategorized proximity check.
    private void ScanForInteractables(Vector3 origin)  (line 419)

    // --- Private helpers ---
    // Returns true if any existing ScanResult is within 1 unit of the interactable's position.
    private bool IsAlreadyCategorized(InteractableNexus interactable)  (line 448)
        // note: proximity threshold is hardcoded 1f.

    // Resolves a display name from drama.name, skobExamine presence, or cleaned GameObject name.
    private string GetInteractableName(InteractableNexus interactable)  (line 461)

    // Builds per-category counts and speaks a summary with navigation hint; sets currentCategory to first non-empty category.
    private void AnnounceScanSummary()  (line 492)
        // note: calls TileCoordinateSystem.GetRangeText() for human-readable range label.
        //       If nothing found, sets scanInProgress=false immediately.

    // Returns index of the first CATEGORY_NAMES entry that has results.
    private int FindFirstNonEmptyCategory()  (line 544)

    // Returns results for the given category name, sorted ascending by distance.
    private List<ScanResult> GetResultsForCategory(string category)  (line 556)

    // Advances currentCategory to next non-empty entry (wraps), announces it.
    private void CycleCategoryForward()  (line 571)

    // Retreats currentCategory to previous non-empty entry (wraps), announces it.
    private void CycleCategoryBackward()  (line 583)

    // Speaks category name, count, up to first 5 results with distance/direction, and "N more" if applicable.
    private void AnnounceCurrentCategory()  (line 596)
        // note: announces at most 5 items; surplus reported as "and N more".

    // Selects the closest result in the current category: sets InputManager.selectedInteractable and snaps the camera.
    private void SelectClosestInCategory()  (line 626)
        // note: resolves InteractableNexus via direct cast or GetComponent on the Source MonoBehaviour.
        //       Calls CameraController.Snap() with instant=false. Sets scanInProgress=false after selection.

    // Cycles rangeIndex 0→1→2→0, updates currentScanRange, then immediately re-runs PerformScan().
    private void CycleScanRange()  (line 672)
        // note: auto-rescans so the result list is never stale after a range change.

    // Clears results, sets scanInProgress=false, speaks "Scan mode off".
    private void ExitScanMode()  (line 692)

    // Returns first selected player from InputManager; falls back to Game.party[0].
    private PC GetPartyLeader()  (line 699)
