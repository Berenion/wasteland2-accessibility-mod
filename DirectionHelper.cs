using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    public enum DirectionFormat
    {
        Cardinal,  // North, Northeast, East, etc.
        Clock      // 12 o'clock, 1 o'clock, etc. (fixed to world directions)
    }

    public static class DirectionHelper
    {
        public static string GetDirectionDescription(Vector3 fromPos, Vector3 toPos)
        {
            if (ModConfig.UseClockPositions)
            {
                return GetClockPosition(fromPos, toPos);
            }
            else
            {
                return GetCardinalDirection(fromPos, toPos);
            }
        }

        private static string GetCardinalDirection(Vector3 fromPos, Vector3 toPos)
        {
            // Get direction vector (horizontal plane only)
            Vector3 direction = toPos - fromPos;
            direction.y = 0; // Ignore vertical component

            if (direction.sqrMagnitude < 0.01f)
            {
                return "at your location";
            }

            // Calculate angle from north
            // In Unity/Wasteland 2: +Z = North, +X = East, -Z = South, -X = West
            float rawAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            // Add 180 degrees to flip directions
            float angle = rawAngle + 180f;
            if (angle >= 360f) angle -= 360f;
            if (angle < 0) angle += 360f;

            // Map to 8 cardinal directions
            if (angle >= 337.5f || angle < 22.5f) return "north";
            if (angle >= 22.5f && angle < 67.5f) return "northeast";
            if (angle >= 67.5f && angle < 112.5f) return "east";
            if (angle >= 112.5f && angle < 157.5f) return "southeast";
            if (angle >= 157.5f && angle < 202.5f) return "south";
            if (angle >= 202.5f && angle < 247.5f) return "southwest";
            if (angle >= 247.5f && angle < 292.5f) return "west";
            return "northwest";
        }

        private static string GetClockPosition(Vector3 fromPos, Vector3 toPos)
        {
            // Get direction vector (horizontal plane only)
            Vector3 direction = toPos - fromPos;
            direction.y = 0; // Ignore vertical component

            if (direction.sqrMagnitude < 0.01f)
            {
                return "at your location";
            }

            // Calculate angle from north
            // 12 o'clock = North (0/360 degrees)
            // 3 o'clock = East (90 degrees)
            // 6 o'clock = South (180 degrees)
            // 9 o'clock = West (270 degrees)
            float rawAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

            // Add 180 degrees to flip directions
            float angle = rawAngle + 180f;
            if (angle >= 360f) angle -= 360f;
            if (angle < 0) angle += 360f;

            // Convert angle to clock position (12 positions)
            // Each hour represents 30 degrees (360 / 12 = 30)
            int clockHour = Mathf.RoundToInt(angle / 30f);

            // Handle 0 -> 12
            if (clockHour == 0) clockHour = 12;
            if (clockHour > 12) clockHour = 12; // Safety check

            return $"{clockHour} o'clock";
        }
    }
}
