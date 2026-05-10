using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Manages proximity warnings as the review cursor moves across the world map.
    /// Tracks which items have been announced at each threshold to avoid repeats.
    /// </summary>
    public static class WorldMapProximityAlert
    {
        // Proximity thresholds for POI announcements (in world units)
        private const float FAR_THRESHOLD = 60f;
        private const float MEDIUM_THRESHOLD = 30f;
        private const float NEAR_THRESHOLD = 15f;

        // Reset threshold - when cursor moves beyond this distance from a POI, clear its alerts
        private const float RESET_DISTANCE = 80f;

        // Tracking sets for each threshold level to avoid repeat announcements
        // Key = POI instance ID, Value = highest threshold already announced
        private static Dictionary<int, float> announcedPOIs = new Dictionary<int, float>();

        // Track radiation cloud entry/exit
        private static HashSet<int> insideRadiationClouds = new HashSet<int>();
        private static HashSet<int> insideDiscoveryBoundary = new HashSet<int>();

        /// <summary>
        /// Check proximity to all known objects from the cursor position.
        /// Call this each time the cursor moves.
        /// Returns a combined announcement string, or empty if nothing to report.
        /// </summary>
        public static string CheckProximity(Vector3 cursorPosition)
        {
            List<string> alerts = new List<string>();

            // Check POI proximity
            string poiAlert = CheckPOIProximity(cursorPosition);
            if (!string.IsNullOrEmpty(poiAlert))
                alerts.Add(poiAlert);

            // Check radiation clouds
            string radiationAlert = CheckRadiationProximity(cursorPosition);
            if (!string.IsNullOrEmpty(radiationAlert))
                alerts.Add(radiationAlert);

            // Encounter zones are not visible to sighted players (editor-only gizmos),
            // so we don't announce them to respect fog of war parity.

            // Clean up distant POIs from tracking
            CleanupDistantEntries(cursorPosition);

            if (alerts.Count == 0)
                return "";

            return string.Join(". ", alerts.ToArray());
        }

        /// <summary>
        /// Check if a path (defined by NavMesh corners) crosses any radiation clouds.
        /// Returns a warning string, or empty if the path is clear.
        /// </summary>
        public static string CheckPathForRadiation(Vector3[] pathCorners)
        {
            if (pathCorners == null || pathCorners.Length < 2) return "";
            if (WorldMapManager.instance == null) return "";

            var clouds = WorldMapManager.instance.radiationClouds;
            if (clouds == null) return "";

            int highestLevel = 0;
            string cloudDirection = "";

            foreach (var cloud in clouds)
            {
                if (cloud == null) continue;

                for (int i = 0; i < pathCorners.Length - 1; i++)
                {
                    if (DoesSegmentCrossCloud(pathCorners[i], pathCorners[i + 1], cloud))
                    {
                        if (cloud.radiationLevel > highestLevel)
                        {
                            highestLevel = cloud.radiationLevel;
                            cloudDirection = DirectionHelper.GetDirectionDescription(
                                pathCorners[0], cloud.transform.position);
                        }
                    }
                }
            }

            if (highestLevel == 0) return "";

            string severity = highestLevel >= 3 ? "lethal" : highestLevel == 2 ? "high" : "low";
            return $"Warning: path crosses level {highestLevel} {severity} radiation, {cloudDirection}";
        }

        /// <summary>
        /// Check if a specific point is inside any radiation cloud.
        /// Returns the radiation level (0 = none).
        /// </summary>
        public static int GetRadiationLevelAtPoint(Vector3 point)
        {
            if (WorldMapManager.instance == null) return 0;

            var clouds = WorldMapManager.instance.radiationClouds;
            if (clouds == null) return 0;

            int highestLevel = 0;
            foreach (var cloud in clouds)
            {
                if (cloud == null) continue;
                if (IsPointInCloud(point, cloud) && cloud.radiationLevel > highestLevel)
                    highestLevel = cloud.radiationLevel;
            }

            return highestLevel;
        }

        /// <summary>
        /// Reset all tracking state. Call when the world map state is deactivated.
        /// </summary>
        public static void Reset()
        {
            announcedPOIs.Clear();
            insideRadiationClouds.Clear();
            insideDiscoveryBoundary.Clear();
        }

        private static string CheckPOIProximity(Vector3 cursorPosition)
        {
            WorldMapPOI[] pois = null;

            if (WorldMapInput.instance != null)
                pois = WorldMapInput.instance.pois;

            if (pois == null || pois.Length == 0)
                pois = Object.FindObjectsOfType(typeof(WorldMapPOI)) as WorldMapPOI[];

            if (pois == null) return "";

            // Find the closest POI that needs announcing at a new threshold
            WorldMapPOI closestNew = null;
            float closestNewDistance = float.MaxValue;
            float closestNewThreshold = 0f;

            foreach (var poi in pois)
            {
                if (poi == null) continue;
                if (!poi.IsVisible()) continue;

                int id = poi.GetInstanceID();
                Vector3 poiPos = poi.transform.position;
                float distance = Vector2Distance(cursorPosition, poiPos);

                // Determine which threshold we're at
                float currentThreshold = 0f;
                if (distance <= NEAR_THRESHOLD)
                    currentThreshold = NEAR_THRESHOLD;
                else if (distance <= MEDIUM_THRESHOLD)
                    currentThreshold = MEDIUM_THRESHOLD;
                else if (distance <= FAR_THRESHOLD)
                    currentThreshold = FAR_THRESHOLD;
                else
                    continue; // Too far, skip

                // Check if we've already announced at this threshold or closer
                float previousThreshold;
                if (announcedPOIs.TryGetValue(id, out previousThreshold))
                {
                    if (currentThreshold >= previousThreshold)
                        continue; // Already announced at this distance or closer
                }

                // This POI needs a new announcement
                if (distance < closestNewDistance)
                {
                    closestNew = poi;
                    closestNewDistance = distance;
                    closestNewThreshold = currentThreshold;
                }
            }

            if (closestNew == null) return "";

            // Update tracking
            announcedPOIs[closestNew.GetInstanceID()] = closestNewThreshold;

            // Build announcement
            string name = WorldMapNavigationManager.GetPOIName(closestNew);
            string distanceStr = $"{Mathf.RoundToInt(closestNewDistance)} units";
            string direction = DirectionHelper.GetDirectionDescription(cursorPosition, closestNew.transform.position);

            if (closestNewThreshold == NEAR_THRESHOLD)
                return $"{name}, {distanceStr}, {direction}, within reach";
            else
                return $"{name}, {distanceStr}, {direction}";
        }

        private static string CheckRadiationProximity(Vector3 cursorPosition)
        {
            if (WorldMapManager.instance == null) return "";

            var clouds = WorldMapManager.instance.radiationClouds;
            if (clouds == null) return "";

            List<string> alerts = new List<string>();

            foreach (var cloud in clouds)
            {
                if (cloud == null) continue;
                int id = cloud.GetInstanceID();

                bool inCloud = IsPointInCloud(cursorPosition, cloud);
                bool inDiscovery = IsPointInDiscoveryBoundary(cursorPosition, cloud);
                bool wasInCloud = insideRadiationClouds.Contains(id);
                bool wasInDiscovery = insideDiscoveryBoundary.Contains(id);

                if (inCloud && !wasInCloud)
                {
                    // Entered radiation cloud
                    insideRadiationClouds.Add(id);
                    string severity = cloud.radiationLevel >= 3 ? "lethal" : cloud.radiationLevel == 2 ? "high" : "low";
                    alerts.Add($"Entering level {cloud.radiationLevel} {severity} radiation zone");
                }
                else if (!inCloud && wasInCloud)
                {
                    // Exited radiation cloud
                    insideRadiationClouds.Remove(id);
                    alerts.Add("Leaving radiation zone");
                }
                else if (inDiscovery && !wasInDiscovery && !inCloud)
                {
                    // Entered discovery boundary (approaching radiation)
                    insideDiscoveryBoundary.Add(id);
                    string direction = DirectionHelper.GetDirectionDescription(
                        cursorPosition, cloud.transform.position);
                    alerts.Add($"Radiation ahead, {direction}");
                }
                else if (!inDiscovery && wasInDiscovery)
                {
                    // Left discovery boundary
                    insideDiscoveryBoundary.Remove(id);
                }

                // Update cloud tracking
                if (inCloud)
                    insideRadiationClouds.Add(id);
                if (inDiscovery)
                    insideDiscoveryBoundary.Add(id);
            }

            if (alerts.Count == 0) return "";
            return string.Join(". ", alerts.ToArray());
        }

        private static void CleanupDistantEntries(Vector3 cursorPosition)
        {
            // Remove POI tracking entries for POIs that are now far away
            List<int> toRemove = new List<int>();

            WorldMapPOI[] pois = null;
            if (WorldMapInput.instance != null)
                pois = WorldMapInput.instance.pois;
            if (pois == null)
                pois = Object.FindObjectsOfType(typeof(WorldMapPOI)) as WorldMapPOI[];

            if (pois == null)
            {
                announcedPOIs.Clear();
                return;
            }

            // Build a lookup of instance ID to POI for distance checks
            foreach (var kvp in announcedPOIs)
            {
                bool found = false;
                foreach (var poi in pois)
                {
                    if (poi != null && poi.GetInstanceID() == kvp.Key)
                    {
                        float distance = Vector2Distance(cursorPosition, poi.transform.position);
                        if (distance > RESET_DISTANCE)
                            toRemove.Add(kvp.Key);
                        found = true;
                        break;
                    }
                }
                if (!found)
                    toRemove.Add(kvp.Key);
            }

            foreach (int id in toRemove)
                announcedPOIs.Remove(id);
        }

        // --- Geometry helpers ---

        private static bool IsPointInCloud(Vector3 point, WorldMapRadiationCloud cloud)
        {
            Vector3 cloudPos = cloud.transform.position;
            Vector2 size = cloud.size;
            return point.x < cloudPos.x + size.x / 2f &&
                   point.x > cloudPos.x - size.x / 2f &&
                   point.z < cloudPos.z + size.y / 2f &&
                   point.z > cloudPos.z - size.y / 2f;
        }

        private static bool IsPointInDiscoveryBoundary(Vector3 point, WorldMapRadiationCloud cloud)
        {
            Vector3 cloudPos = cloud.transform.position;
            Vector2 size = cloud.size;
            float expandX = size.x * 1.5f;
            float expandZ = size.y * 1.5f;
            return point.x < cloudPos.x + expandX / 2f &&
                   point.x > cloudPos.x - expandX / 2f &&
                   point.z < cloudPos.z + expandZ / 2f &&
                   point.z > cloudPos.z - expandZ / 2f;
        }

        /// <summary>
        /// Line-segment vs AABB intersection test on the X/Z plane.
        /// Tests if the line from A to B crosses the radiation cloud's rectangle.
        /// </summary>
        public static bool DoesSegmentCrossCloud(Vector3 a, Vector3 b, WorldMapRadiationCloud cloud)
        {
            Vector3 cloudPos = cloud.transform.position;
            Vector2 size = cloud.size;

            float minX = cloudPos.x - size.x / 2f;
            float maxX = cloudPos.x + size.x / 2f;
            float minZ = cloudPos.z - size.y / 2f;
            float maxZ = cloudPos.z + size.y / 2f;

            // If either endpoint is inside the cloud, the segment crosses it
            if (a.x >= minX && a.x <= maxX && a.z >= minZ && a.z <= maxZ) return true;
            if (b.x >= minX && b.x <= maxX && b.z >= minZ && b.z <= maxZ) return true;

            // Parametric line-AABB intersection (Liang-Barsky algorithm on X/Z)
            float dx = b.x - a.x;
            float dz = b.z - a.z;

            float tMin = 0f;
            float tMax = 1f;

            // Check X slab
            if (Mathf.Abs(dx) < 0.0001f)
            {
                // Line is parallel to X slab
                if (a.x < minX || a.x > maxX) return false;
            }
            else
            {
                float t1 = (minX - a.x) / dx;
                float t2 = (maxX - a.x) / dx;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }

            // Check Z slab
            if (Mathf.Abs(dz) < 0.0001f)
            {
                if (a.z < minZ || a.z > maxZ) return false;
            }
            else
            {
                float t1 = (minZ - a.z) / dz;
                float t2 = (maxZ - a.z) / dz;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }

            return true;
        }

        private static float Vector2Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
