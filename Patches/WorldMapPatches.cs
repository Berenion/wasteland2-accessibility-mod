using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Suppresses WorldMapInput.Update() when our accessibility state is handling input.
    /// This prevents the game from processing arrow keys (camera scroll), Space (pause/resume),
    /// and mouse clicks while the review cursor is active.
    /// </summary>
    [HarmonyPatch(typeof(WorldMapInput), "Update")]
    public class WorldMapInput_Update_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (InputSuppressor.ShouldSuppressGameInput)
            {
                return false; // Skip WorldMapInput.Update()
            }
            return true;
        }
    }

    /// <summary>
    /// Suppresses WorldMapCameraController.Update() arrow key scrolling when our state is active.
    /// The camera controller reads arrow keys directly for scrolling, which conflicts with
    /// our review cursor movement.
    /// </summary>
    [HarmonyPatch(typeof(WorldMapCameraController), "Update")]
    public class WorldMapCameraController_Update_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (InputSuppressor.ShouldSuppressUINavigation)
            {
                return false; // Skip camera scroll processing
            }
            return true;
        }
    }

    /// <summary>
    /// Announces when a POI is discovered by the party's perception radius.
    /// </summary>
    [HarmonyPatch(typeof(WorldMapPOI), "Discover")]
    public class WorldMapPOI_Discover_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(WorldMapPOI __instance, bool forceDiscover)
        {
            // Only announce if the POI is now discovered
            if (__instance == null) return;

            // Check if the discovery actually went through by checking wsDiscovered
            // The Discover method sets wsDiscovered if successful
            try
            {
                string name = WorldMapNavigationManager.GetPOIName(__instance);
                string typeName = GetTypeName(__instance.type);

                // Get direction from party
                string directionInfo = "";
                if (WorldMapParty.instance != null)
                {
                    Vector3 partyPos = WorldMapParty.instance.transform.position;
                    Vector3 poiPos = __instance.transform.position;
                    float distance = WorldMapMath.Vector2Distance(partyPos, poiPos);
                    string direction = DirectionHelper.GetDirectionDescription(partyPos, poiPos);
                    directionInfo = $", {Mathf.RoundToInt(distance)} units, {direction}";
                }

                string announcement = $"Discovered {typeName}: {name}{directionInfo}";
                ScreenReaderManager.Speak(announcement);
                ModLog.Debug($"[WorldMapPatches] {announcement}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[WorldMapPatches] Error in Discover postfix: {ex.Message}");
            }
        }

        private static string GetTypeName(POIType type)
        {
            switch (type)
            {
                case POIType.Location: return "settlement";
                case POIType.Combat: return "combat site";
                case POIType.Info: return "site";
                case POIType.Water: return "oasis";
                case POIType.Cache: return "cache";
                case POIType.Shrine: return "shrine";
                default: return "location";
            }
        }
    }

    /// <summary>
    /// Announces when arriving at a POI (Instigate is called).
    /// </summary>
    [HarmonyPatch(typeof(WorldMapPOI), "Instigate")]
    public class WorldMapPOI_Instigate_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(WorldMapPOI __instance)
        {
            if (__instance == null) return;

            try
            {
                string name = WorldMapNavigationManager.GetPOIName(__instance);

                // All POI types that trigger HUD_POIPanel are announced by DialogState
                // when the panel opens, so just log here to avoid duplicate/competing speech
                ModLog.Debug($"[WorldMapState] Instigating POI: {name}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[WorldMapPatches] Error in Instigate postfix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces radiation cloud discovery.
    /// </summary>
    [HarmonyPatch(typeof(WorldMapRadiationCloud), "CheckDiscovery")]
    public class WorldMapRadiationCloud_CheckDiscovery_Patch
    {
        // Track clouds we've already announced to prevent spam
        // (CheckDiscovery is called every frame while the party is within range)
        private static HashSet<int> announcedClouds = new HashSet<int>();

        /// <summary>
        /// Call this when leaving the world map to reset tracking.
        /// </summary>
        public static void Reset()
        {
            announcedClouds.Clear();
        }

        [HarmonyPostfix]
        public static void Postfix(WorldMapRadiationCloud __instance)
        {
            if (__instance == null) return;
            if (!__instance.radiationActive) return;

            int id = __instance.GetInstanceID();
            if (announcedClouds.Contains(id)) return;

            try
            {
                announcedClouds.Add(id);

                string directionInfo = "";
                if (WorldMapParty.instance != null)
                {
                    Vector3 partyPos = WorldMapParty.instance.transform.position;
                    Vector3 cloudPos = __instance.transform.position;
                    string direction = DirectionHelper.GetDirectionDescription(partyPos, cloudPos);
                    directionInfo = $", {direction}";
                }

                int level = __instance.radiationLevel;
                string severity = level >= 3 ? "lethal" : level == 2 ? "high" : "low";
                ScreenReaderManager.Speak($"Radiation detected, level {level} {severity}{directionInfo}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[WorldMapPatches] Error in radiation discovery postfix: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces radiation level changes as detected by the HUD geiger counter.
    /// </summary>
    [HarmonyPatch(typeof(HUD_WorldMapController), "SetRadiationLevel")]
    public class HUD_WorldMapController_SetRadiationLevel_Patch
    {
        private static int lastAnnouncedLevel = -1;

        [HarmonyPostfix]
        public static void Postfix(int radiation)
        {
            try
            {
                if (radiation == lastAnnouncedLevel) return;
                lastAnnouncedLevel = radiation;

                if (radiation == 0)
                {
                    ScreenReaderManager.Speak("Radiation clear");
                }
                else if (radiation <= 3)
                {
                    // Warning levels (approaching cloud): 1=far, 2=medium, 3=close
                    string proximity = radiation == 1 ? "distant" : radiation == 2 ? "nearby" : "imminent";
                    ScreenReaderManager.Speak($"Radiation warning, {proximity}");
                }
                else
                {
                    // Inside cloud: levels 4-6 map to cloud levels 1-3
                    int cloudLevel = radiation - 3;
                    string severity = cloudLevel >= 3 ? "lethal" : cloudLevel == 2 ? "high" : "low";
                    ScreenReaderManager.SpeakInterrupt($"In radiation zone, level {cloudLevel}, {severity}");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[WorldMapPatches] Error in radiation level postfix: {ex.Message}");
            }
        }
    }

}
