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

        private static int floorLayerMask = -1;

        /// <summary>
        /// Which storey of a multi-floor scene <paramref name="worldPos"/> sits on, 0-based
        /// (0 = ground). Resolved by raycasting down onto floor geometry and reading the
        /// "2nd Floor" ... "6th Floor" tags the game marks storeys with, walking up the
        /// parent chain because the tag usually sits on an ancestor of the hit collider.
        ///
        /// Deliberately independent of CombatAStar: this is the only floor source that
        /// works in scenes with no combat grid, and the grid's own node IDs carry the same
        /// value in their y component (CombatAStar compares id.y to decide whether two
        /// tiles are on the same storey — see its path-smoothing and straight-line checks).
        /// Returns 0 when the raycast finds nothing, which is also the correct answer for
        /// every single-storey scene.
        /// </summary>
        public static int GetFloorLevel(Vector3 worldPos)
        {
            if (floorLayerMask < 0)
            {
                floorLayerMask = (1 << LayerMask.NameToLayer("Terrain"))
                               | (1 << LayerMask.NameToLayer("Floor"))
                               | (1 << LayerMask.NameToLayer("FadedFloor"));
            }

            RaycastHit hit;
            if (Physics.Raycast(worldPos + Vector3.up * 2f, Vector3.down, out hit, 5f, floorLayerMask))
            {
                Transform trans = hit.transform;
                while (trans != null)
                {
                    string tag = trans.tag;
                    if (tag == "2nd Floor") return 1;
                    if (tag == "3rd Floor") return 2;
                    if (tag == "4th Floor") return 3;
                    if (tag == "5th Floor") return 4;
                    if (tag == "6th Floor") return 5;
                    trans = trans.parent;
                }
            }

            return 0;
        }

        /// <summary>
        /// The grid coordinate (x, floor, z) for a world position, matching the id the
        /// combat grid gives its nodes. Pure arithmetic against a fixed tile size plus the
        /// floor raycast — no grid lookup — so it yields the same answer in every session
        /// for a given world position, and still answers in scenes with no grid at all.
        /// That stability is what makes it usable as a persistent key (see LocationLabels).
        /// </summary>
        public static Vector3 GetGridId(Vector3 worldPos)
        {
            return new Vector3(
                Mathf.RoundToInt(worldPos.x / SquareSize),
                GetFloorLevel(worldPos),
                Mathf.RoundToInt(worldPos.z / SquareSize));
        }

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
