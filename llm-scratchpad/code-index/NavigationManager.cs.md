File: NavigationManager.cs — keyboard cycling through in-world interactables with category filtering, FOW/perception gating, and highlight management

namespace Wasteland2AccessibilityMod  (line 8)

enum InteractableCategory  (line 12)  [public]
    All         (line 14)
    Party       (line 15)
    Characters  (line 16)
    Containers  (line 17)
    Objects     (line 18)
    Exits       (line 19)
    Examine     (line 20)
    Loot        (line 21)
    Misc        (line 22)

class NavigationManager  (line 25)  [public static]

    private static List<InteractableNexus> filteredInteractables  (line 27)
    private static int currentIndex  (line 28)
    private static string lastAnnouncement  (line 29)
    private static InteractableNexus lastAnnouncedInteractable  (line 30)
    private static InteractableNexus selectedInteractable  (line 34)

    // The currently selected interactable from cycling (PageUp/PageDown); used by MapCursorState to jump cursor.
    public static InteractableNexus SelectedInteractable => selectedInteractable  (line 40)

    private static InteractableCategory currentCategory  (line 43)
    private static readonly InteractableCategory[] categoryOrder  (line 44)

    public static InteractableCategory CurrentCategory => currentCategory  (line 57)

    // Cycles to the next category (Page Down)
    public static void NextCategory()  (line 62)

    // Cycles to the previous category (Page Up)
    public static void PreviousCategory()  (line 90)

    private static string GetCategoryDisplayName(InteractableCategory category)  (line 116)

    public static void CycleNext()  (line 133)
    public static void CyclePrevious()  (line 157)

    public static void RepeatLastAnnouncement()  (line 182)
        // note: re-announces selectedInteractable (cycling selection), NOT lastAnnouncedInteractable (proximity); refreshes distance/direction

    // Announces an interactable with name, distance, and direction.
    public static void AnnounceInteractable(InteractableNexus nexus, bool isFromCycling = false)  (line 219)
        // note: isFromCycling=true updates selectedInteractable; proximity calls must pass false to avoid overwriting keyboard selection

    private static void SelectAndAnnounce(int index)  (line 248)
        // note: applies CursorOver highlight via GetHighlight(); clears previous highlight; calls AnnounceInteractable with isFromCycling=true

    private static void UpdateFilteredList()  (line 277)
        // note: calls FOWHelper.UpdateActivationTracking(); handles Party category separately via UpdatePartyList; emits verbose ShortcutDoor diagnostics; sorts by distance; calls FOWHelper.IsVisibleThroughFOW and IsPerceptionGated

    private static void UpdatePartyList(Vector3 playerPos)  (line 403)
        // note: PCs are not in InteractableNexus.interactables when conscious; reads Game.party and Game.partyFollowers directly; falls back to Drama component to find nexus

    private static bool MatchesCategory(InteractableNexus nexus, InteractableCategory category)  (line 465)
        // note: Misc is computed by exclusion — if it matches any other category it returns false; Containers checks GetAllowedInteractions() for "Poked"==1 or non-zero skill interactions

    private static string GetInteractableName(InteractableNexus nexus)  (line 626)

    private static string CleanGameObjectName(string name)  (line 651)
        // note: strips zone-marker prefix (AZ_, CA_, LA_) via regex, trailing _N suffixes, and underscores
