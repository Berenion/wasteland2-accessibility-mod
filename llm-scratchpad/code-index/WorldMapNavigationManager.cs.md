File: WorldMapNavigationManager.cs — keyboard cycling through world map POIs and radiation clouds with category filtering and water-cost estimation

namespace Wasteland2AccessibilityMod  (line 7)

enum WorldMapCategory  (line 9)  [public]
    All              (line 11)
    Settlements      (line 12)
    Sites            (line 13)
    Caches           (line 14)
    Water            (line 15)
    Shrines          (line 16)
    RadiationClouds  (line 17)

class WorldMapNavigationManager  (line 20)  [public static]

    private static List<object> filteredItems  (line 22)
    private static int currentIndex  (line 23)
    private static string lastAnnouncement  (line 24)
    private static object selectedItem  (line 25)

    public static object SelectedItem => selectedItem  (line 27)

    private static WorldMapCategory currentCategory  (line 29)
    private static readonly WorldMapCategory[] categoryOrder  (line 30)

    public static WorldMapCategory CurrentCategory => currentCategory  (line 40)

    public static void NextCategory(Vector3 relativeTo)  (line 42)
    public static void PreviousCategory(Vector3 relativeTo)  (line 59)
    public static void CycleNext(Vector3 relativeTo)  (line 77)
    public static void CyclePrevious(Vector3 relativeTo)  (line 95)
    public static void RepeatLastAnnouncement()  (line 112)

    // Gets the selected POI, or null if the selection is a radiation cloud or nothing
    public static WorldMapPOI GetSelectedPOI()  (line 125)

    // Gets the world position of the currently selected item (POI or radiation cloud); returns null if nothing selected
    public static Vector3? GetSelectedPosition()  (line 133)
        // note: uses C# 'is' pattern matching on object type; returns null (nullable Vector3) if no selection

    public static void Reset()  (line 143)

    private static void SelectAndAnnounce(int index, Vector3 relativeTo)  (line 152)

    private static void AnnouncePOI(WorldMapPOI poi, Vector3 relativeTo)  (line 165)
        // note: includes water cost from party position via EstimateWaterCost; uses Vector2Distance (X/Z plane only)

    private static void AnnounceRadiationCloud(WorldMapRadiationCloud cloud, Vector3 relativeTo)  (line 190)

    private static void UpdateFilteredList(Vector3 relativeTo)  (line 204)
        // note: radiation clouds added only for All or RadiationClouds category; POIs added for all non-RadiationClouds; sorted by Vector2Distance

    private static void AddPOIs(Vector3 relativeTo)  (line 229)
        // note: prefers WorldMapInput.instance.pois; falls back to FindObjectsOfType; logs visibility/category counts

    private static void AddRadiationClouds(Vector3 relativeTo)  (line 266)

    private static bool MatchesPOICategory(POIType poiType, WorldMapCategory category)  (line 280)

    private static Vector3 GetItemPosition(object item)  (line 303)

    // Gets the localized display name for a POI; falls back to cleaned GameObject name
    public static string GetPOIName(WorldMapPOI poi)  (line 312)

    private static string GetPOITypeName(POIType type)  (line 334)
    private static string GetCategoryDisplayName(WorldMapCategory category)  (line 348)

    // Estimates water cost based on straight-line distance (actual path may cost more due to NavMesh bending)
    private static int EstimateWaterCost(float distance)  (line 367)
        // note: uses WorldMapParty.instance.sampleDistance; returns 0 if sampleDistance <= 0

    // 2D distance on the X/Z plane (ignoring Y), matching the game's distance checks
    private static float Vector2Distance(Vector3 a, Vector3 b)  (line 383)
