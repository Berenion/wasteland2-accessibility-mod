using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Shared fog of war visibility check used across the mod to avoid
    /// announcing objects that sighted players cannot see.
    /// </summary>
    public static class FOWHelper
    {
        /// <summary>
        /// Checks if a world position is currently visible (not hidden by fog of war).
        /// Returns true if no FOW system exists (e.g. world map).
        /// </summary>
        public static bool IsVisibleThroughFOW(Vector3 position)
        {
            if (FOWSystem.instance == null) return true;
            return FOWSystem.instance.IsVisible(position);
        }
    }
}
