using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Cardinal direction lookup tables shared across map cursor, combat,
    /// and world map navigation. Index 0..3 is N/E/S/W, matching how the
    /// game stores cover (forward/right/back/left) and how arrow keys are
    /// dispatched (Up/Right/Down/Left).
    /// </summary>
    public static class CardinalDirections
    {
        /// <summary>Spoken names: north, east, south, west.</summary>
        public static readonly string[] Names = { "north", "east", "south", "west" };

        /// <summary>
        /// World-space unit vectors for each cardinal: +Z (forward) =
        /// north, +X (right) = east, -Z (back) = south, -X (left) = west.
        /// </summary>
        public static readonly Vector3[] Vectors =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };
    }
}
