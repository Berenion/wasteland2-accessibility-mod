using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod
{
    public enum WorldMapCategory
    {
        All,
        Settlements,
        Sites,
        Caches,
        Water,
        Shrines,
        RadiationClouds
    }

    public static class WorldMapNavigationManager
    {
        private static List<object> filteredItems = new List<object>();
        private static int currentIndex = -1;
        private static string lastAnnouncement = "";
        private static object selectedItem = null;

        public static object SelectedItem => selectedItem;

        private static WorldMapCategory currentCategory = WorldMapCategory.All;
        private static readonly WorldMapCategory[] categoryOrder =
        {
            WorldMapCategory.All,
            WorldMapCategory.Settlements,
            WorldMapCategory.Sites,
            WorldMapCategory.Caches,
            WorldMapCategory.Water,
            WorldMapCategory.Shrines,
            WorldMapCategory.RadiationClouds
        };

        public static WorldMapCategory CurrentCategory => currentCategory;

        public static void NextCategory(Vector3 relativeTo)
        {
            int currentIdx = System.Array.IndexOf(categoryOrder, currentCategory);
            currentIdx = (currentIdx + 1) % categoryOrder.Length;
            currentCategory = categoryOrder[currentIdx];

            currentIndex = -1;
            selectedItem = null;

            UpdateFilteredList(relativeTo);
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = filteredItems.Count;
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            MelonLogger.Msg($"[WorldMapNav] Category changed to: {categoryName} ({count} items)");
        }

        public static void PreviousCategory(Vector3 relativeTo)
        {
            int currentIdx = System.Array.IndexOf(categoryOrder, currentCategory);
            currentIdx--;
            if (currentIdx < 0) currentIdx = categoryOrder.Length - 1;
            currentCategory = categoryOrder[currentIdx];

            currentIndex = -1;
            selectedItem = null;

            UpdateFilteredList(relativeTo);
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = filteredItems.Count;
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            MelonLogger.Msg($"[WorldMapNav] Category changed to: {categoryName} ({count} items)");
        }

        public static void CycleNext(Vector3 relativeTo)
        {
            UpdateFilteredList(relativeTo);

            MelonLogger.Msg($"[WorldMapNav] CycleNext: category={currentCategory}, filteredItems={filteredItems.Count}, relativeTo={relativeTo}");

            if (filteredItems.Count == 0)
            {
                string categoryName = GetCategoryDisplayName(currentCategory);
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()} found");
                currentIndex = -1;
                return;
            }

            currentIndex = (currentIndex + 1) % filteredItems.Count;
            SelectAndAnnounce(currentIndex, relativeTo);
        }

        public static void CyclePrevious(Vector3 relativeTo)
        {
            UpdateFilteredList(relativeTo);

            if (filteredItems.Count == 0)
            {
                string categoryName = GetCategoryDisplayName(currentCategory);
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()} found");
                currentIndex = -1;
                return;
            }

            currentIndex--;
            if (currentIndex < 0) currentIndex = filteredItems.Count - 1;
            SelectAndAnnounce(currentIndex, relativeTo);
        }

        public static void RepeatLastAnnouncement()
        {
            if (string.IsNullOrEmpty(lastAnnouncement))
            {
                ScreenReaderManager.SpeakInterrupt("No location selected");
                return;
            }
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        /// <summary>
        /// Gets the selected POI, or null if the selection is a radiation cloud or nothing.
        /// </summary>
        public static WorldMapPOI GetSelectedPOI()
        {
            return selectedItem as WorldMapPOI;
        }

        /// <summary>
        /// Gets the world position of the currently selected item (POI or radiation cloud).
        /// Returns null if nothing is selected.
        /// </summary>
        public static Vector3? GetSelectedPosition()
        {
            if (selectedItem is WorldMapPOI poi)
                return poi.transform.position;
            if (selectedItem is WorldMapRadiationCloud cloud)
                return cloud.transform.position;
            return null;
        }

        public static void Reset()
        {
            filteredItems.Clear();
            currentIndex = -1;
            selectedItem = null;
            lastAnnouncement = "";
            currentCategory = WorldMapCategory.All;
        }

        private static void SelectAndAnnounce(int index, Vector3 relativeTo)
        {
            if (index < 0 || index >= filteredItems.Count) return;

            object item = filteredItems[index];
            selectedItem = item;

            if (item is WorldMapPOI poi)
                AnnouncePOI(poi, relativeTo);
            else if (item is WorldMapRadiationCloud cloud)
                AnnounceRadiationCloud(cloud, relativeTo);
        }

        private static void AnnouncePOI(WorldMapPOI poi, Vector3 relativeTo)
        {
            string name = GetPOIName(poi);
            string typeName = GetPOITypeName(poi.type);
            Vector3 poiPos = poi.transform.position;
            float distance = WorldMapMath.Vector2Distance(relativeTo, poiPos);
            string distanceStr = $"{Mathf.RoundToInt(distance)} units";
            string direction = DirectionHelper.GetDirectionDescription(relativeTo, poiPos);

            // Calculate water cost if party is available
            string waterInfo = "";
            if (WorldMapParty.instance != null)
            {
                Vector3 partyPos = WorldMapParty.instance.transform.position;
                float partyDistance = WorldMapMath.Vector2Distance(partyPos, poiPos);
                int waterCost = EstimateWaterCost(partyDistance);
                if (waterCost > 0)
                    waterInfo = $", {waterCost} water from party";
            }

            lastAnnouncement = $"{name}, {typeName}, {distanceStr}, {direction}{waterInfo}";
            MelonLogger.Msg($"[WorldMapNav] Announcing: {lastAnnouncement}");
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        private static void AnnounceRadiationCloud(WorldMapRadiationCloud cloud, Vector3 relativeTo)
        {
            Vector3 cloudPos = cloud.transform.position;
            float distance = WorldMapMath.Vector2Distance(relativeTo, cloudPos);
            string distanceStr = $"{Mathf.RoundToInt(distance)} units";
            string direction = DirectionHelper.GetDirectionDescription(relativeTo, cloudPos);
            int level = cloud.radiationLevel;
            string severity = level >= 3 ? "lethal" : level == 2 ? "high" : "low";

            lastAnnouncement = $"Radiation cloud, level {level} {severity}, {distanceStr}, {direction}";
            MelonLogger.Msg($"[WorldMapNav] Announcing: {lastAnnouncement}");
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
        }

        private static void UpdateFilteredList(Vector3 relativeTo)
        {
            filteredItems.Clear();

            if (currentCategory == WorldMapCategory.RadiationClouds ||
                currentCategory == WorldMapCategory.All)
            {
                AddRadiationClouds(relativeTo);
            }

            if (currentCategory != WorldMapCategory.RadiationClouds)
            {
                AddPOIs(relativeTo);
            }

            // Sort by distance
            filteredItems = filteredItems
                .OrderBy(item =>
                {
                    Vector3 pos = GetItemPosition(item);
                    return WorldMapMath.Vector2Distance(relativeTo, pos);
                })
                .ToList();
        }

        private static void AddPOIs(Vector3 relativeTo)
        {
            WorldMapPOI[] pois = null;

            if (WorldMapInput.instance != null)
            {
                pois = WorldMapInput.instance.pois;
                MelonLogger.Msg($"[WorldMapNav] AddPOIs: WorldMapInput.instance.pois has {(pois != null ? pois.Length : 0)} entries");
            }
            else
            {
                MelonLogger.Msg("[WorldMapNav] AddPOIs: WorldMapInput.instance is null");
            }

            if (pois == null || pois.Length == 0)
            {
                pois = Object.FindObjectsOfType(typeof(WorldMapPOI)) as WorldMapPOI[];
                MelonLogger.Msg($"[WorldMapNav] AddPOIs: FindObjectsOfType found {(pois != null ? pois.Length : 0)} WorldMapPOI objects");
            }

            if (pois == null) return;

            int visibleCount = 0;
            int categoryMatchCount = 0;
            foreach (var poi in pois)
            {
                if (poi == null) continue;
                if (!poi.IsVisible()) continue;
                visibleCount++;
                if (!MatchesPOICategory(poi.type, currentCategory)) continue;
                categoryMatchCount++;

                filteredItems.Add(poi);
            }
            MelonLogger.Msg($"[WorldMapNav] AddPOIs: {visibleCount} visible, {categoryMatchCount} matching category {currentCategory}");
        }

        private static void AddRadiationClouds(Vector3 relativeTo)
        {
            if (WorldMapManager.instance == null) return;

            var clouds = WorldMapManager.instance.radiationClouds;
            if (clouds == null) return;

            foreach (var cloud in clouds)
            {
                if (cloud == null) continue;
                filteredItems.Add(cloud);
            }
        }

        private static bool MatchesPOICategory(POIType poiType, WorldMapCategory category)
        {
            switch (category)
            {
                case WorldMapCategory.All:
                    return true;
                case WorldMapCategory.Settlements:
                    return poiType == POIType.Location;
                case WorldMapCategory.Sites:
                    return poiType == POIType.Combat || poiType == POIType.Info;
                case WorldMapCategory.Caches:
                    return poiType == POIType.Cache;
                case WorldMapCategory.Water:
                    return poiType == POIType.Water;
                case WorldMapCategory.Shrines:
                    return poiType == POIType.Shrine;
                case WorldMapCategory.RadiationClouds:
                    return false; // Radiation clouds are not POIs
                default:
                    return true;
            }
        }

        private static Vector3 GetItemPosition(object item)
        {
            if (item is WorldMapPOI poi)
                return poi.transform.position;
            if (item is WorldMapRadiationCloud cloud)
                return cloud.transform.position;
            return Vector3.zero;
        }

        public static string GetPOIName(WorldMapPOI poi)
        {
            if (poi == null) return "Unknown";

            // Try localized label first
            if (!string.IsNullOrEmpty(poi.label))
            {
                string localized = Language.Localize(poi.label, false, false, string.Empty);
                if (!string.IsNullOrEmpty(localized))
                    return UITextExtractor.CleanText(localized);
            }

            // Fall back to cleaned GameObject name
            string name = poi.name;
            if (string.IsNullOrEmpty(name)) return "Unknown location";

            name = name.Replace("(Clone)", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"_\d+$", "");
            name = name.Replace("_", " ");
            return name.Trim();
        }

        private static string GetPOITypeName(POIType type)
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

        private static string GetCategoryDisplayName(WorldMapCategory category)
        {
            switch (category)
            {
                case WorldMapCategory.All: return "All";
                case WorldMapCategory.Settlements: return "Settlements";
                case WorldMapCategory.Sites: return "Sites";
                case WorldMapCategory.Caches: return "Caches";
                case WorldMapCategory.Water: return "Water";
                case WorldMapCategory.Shrines: return "Shrines";
                case WorldMapCategory.RadiationClouds: return "Radiation Clouds";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Estimates water cost based on straight-line distance.
        /// Actual cost may be higher due to NavMesh path bending.
        /// </summary>
        private static int EstimateWaterCost(float distance)
        {
            if (WorldMapParty.instance == null) return 0;

            // sampleDistance is a field on WorldMapParty
            float sampleDistance = WorldMapParty.instance.sampleDistance;
            if (sampleDistance <= 0) return 0;

            // Water cost = ceil(distance / sampleDistance * waterCost)
            // waterCost is always 1.0 per the decompiled code
            return Mathf.CeilToInt(distance / sampleDistance);
        }

    }
}
