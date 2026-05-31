using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    public static class TileCoordinateSystem
    {
        public const float SquareSize = 1.6f;

        private static FieldInfo fullMapField;
        private static Dictionary<Vector3, CombatAStarNode> cachedFullMap;
        private static CombatAStar cachedInstance;

        public static bool IsGridAvailable
        {
            get
            {
                return TryGetFullMap(out var map) && map.Count > 0;
            }
        }

        public static CombatAStarNode GetTileAtPosition(Vector3 worldPos)
        {
            if (!TryGetFullMap(out var fullMap)) return null;

            Vector3 id = new Vector3(
                Mathf.RoundToInt(worldPos.x / SquareSize),
                0f,
                Mathf.RoundToInt(worldPos.z / SquareSize));

            if (fullMap.TryGetValue(id, out var node))
                return node;

            // Exact miss — scan for the nearest node by world position.
            // Handles multi-floor scenes where the floor index isn't zero.
            CombatAStarNode best = null;
            float bestDist = float.PositiveInfinity;
            foreach (var candidate in fullMap.Values)
            {
                float d = Vector3.Distance(worldPos, candidate.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = candidate;
                }
            }

            return bestDist <= SquareSize ? best : null;
        }

        public static CombatAStarNode GetTileForMob(Mob mob)
        {
            if (mob == null) return null;

            if (MonoBehaviourSingleton<CombatManager>.HasInstance()
                && MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat
                && mob.currentSquare != null)
            {
                return mob.currentSquare;
            }

            return GetTileAtPosition(mob.transform.position);
        }

        public static int GetTileDistance(Vector3 a, Vector3 b)
        {
            var nodeA = GetTileAtPosition(a);
            var nodeB = GetTileAtPosition(b);
            if (nodeA != null && nodeB != null)
            {
                int dx = Mathf.Abs(Mathf.RoundToInt(nodeA.id.x - nodeB.id.x));
                int dz = Mathf.Abs(Mathf.RoundToInt(nodeA.id.z - nodeB.id.z));
                return Mathf.Max(dx, dz);
            }
            return Mathf.RoundToInt(Vector3.Distance(a, b) / SquareSize);
        }

        public static string GetDistanceText(Vector3 fromPos, Vector3 toPos)
        {
            if (ModConfig.UseTileDistances && IsGridAvailable)
            {
                int tiles = GetTileDistance(fromPos, toPos);
                return tiles == 1 ? "1 tile" : $"{tiles} tiles";
            }

            int meters = Mathf.RoundToInt(Vector3.Distance(fromPos, toPos));
            return $"{meters} meters";
        }

        public static int MetersToTiles(float meters)
        {
            return Mathf.RoundToInt(meters / SquareSize);
        }

        public static string GetRangeText(float meters)
        {
            if (ModConfig.UseTileDistances && IsGridAvailable)
            {
                int tiles = MetersToTiles(meters);
                return tiles == 1 ? "1 tile" : $"{tiles} tiles";
            }

            return $"{Mathf.RoundToInt(meters)} meters";
        }

        private static bool TryGetFullMap(out Dictionary<Vector3, CombatAStarNode> map)
        {
            if (!MonoBehaviourSingleton<CombatAStar>.HasInstance())
            {
                map = null;
                return false;
            }

            var instance = MonoBehaviourSingleton<CombatAStar>.GetInstance();

            if (instance != cachedInstance || cachedFullMap == null)
            {
                if (fullMapField == null)
                {
                    fullMapField = typeof(CombatAStar).GetField(
                        "fullMap",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fullMapField == null)
                    {
                        MelonLogger.Error("TileCoordinateSystem: could not reflect CombatAStar.fullMap");
                        map = null;
                        return false;
                    }
                }

                cachedFullMap = fullMapField.GetValue(instance) as Dictionary<Vector3, CombatAStarNode>;
                cachedInstance = instance;
            }

            map = cachedFullMap;
            return map != null;
        }
    }
}
