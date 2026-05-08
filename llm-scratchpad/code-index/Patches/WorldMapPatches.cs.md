File: Patches/WorldMapPatches.cs — Harmony patches for world map accessibility: suppresses game input during review cursor, announces POI discovery/arrival, radiation cloud detection, and radiation level changes.

namespace Wasteland2AccessibilityMod.Patches  (line 8)

// Suppresses WorldMapInput.Update when InputSuppressor.ShouldSuppressGameInput is true (review cursor active).
[HarmonyPatch(typeof(WorldMapInput), "Update")]
class WorldMapInput_Update_Patch  (line 15)
    [HarmonyPrefix]
    public static bool Prefix()  (line 18)

// Suppresses WorldMapCameraController.Update arrow-key scroll processing when ShouldSuppressUINavigation is true.
[HarmonyPatch(typeof(WorldMapCameraController), "Update")]
class WorldMapCameraController_Update_Patch  (line 34)
    [HarmonyPrefix]
    public static bool Prefix()  (line 37)

// Announces "Discovered TypeName: Name, N units, Direction" when a POI is discovered by the party.
[HarmonyPatch(typeof(WorldMapPOI), "Discover")]
class WorldMapPOI_Discover_Patch  (line 51)
    [HarmonyPostfix]
    public static void Postfix(WorldMapPOI __instance, bool forceDiscover)  (line 54)
        // note: computes distance and direction from WorldMapParty.instance; maps POIType enum to readable type name.

    private static string GetTypeName(POIType type)  (line 87)

// Logs POI instigate event; does not speak (DialogState handles the popup announcement).
[HarmonyPatch(typeof(WorldMapPOI), "Instigate")]
class WorldMapPOI_Instigate_Patch  (line 105)
    [HarmonyPostfix]
    public static void Postfix(WorldMapPOI __instance)  (line 108)

// Announces radiation cloud discovery once per cloud instance; resets on world map exit.
[HarmonyPatch(typeof(WorldMapRadiationCloud), "CheckDiscovery")]
class WorldMapRadiationCloud_CheckDiscovery_Patch  (line 132)
    private static HashSet<int> announcedClouds  (line 136)

    // Clears announcedClouds; call when leaving the world map.
    public static void Reset()  (line 141)

    [HarmonyPostfix]
    public static void Postfix(WorldMapRadiationCloud __instance)  (line 147)
        // note: deduplicates by instance ID; announces level and severity with direction.

// Announces radiation level changes as the party moves relative to clouds; uses SpeakInterrupt for in-cloud alerts.
[HarmonyPatch(typeof(HUD_WorldMapController), "SetRadiationLevel")]
class HUD_WorldMapController_SetRadiationLevel_Patch  (line 183)
    private static int lastAnnouncedLevel  (line 185)

    [HarmonyPostfix]
    public static void Postfix(int radiation)  (line 188)
        // note: deduplicates by level; maps 0 to "clear", 1-3 to warning proximity, 4-6 to in-zone severity.

// Utility helpers shared across world map patches.
internal static class WorldMapPatchUtils  (line 221)
    internal static float Vector2Distance(Vector3 a, Vector3 b)  (line 223)
        // note: ignores Y axis (XZ plane only), matching world map's flat layout.
