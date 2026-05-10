using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Math helpers for world-map geometry. World-map positions are 3D Vector3
    /// values but the world map only cares about the XZ plane, so distance and
    /// direction calculations ignore Y.
    /// </summary>
    public static class WorldMapMath
    {
        /// <summary>
        /// XZ-plane distance between two Vector3 positions. Used for all
        /// world-map distance comparisons (POIs, radiation clouds, party).
        /// </summary>
        public static float Vector2Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
