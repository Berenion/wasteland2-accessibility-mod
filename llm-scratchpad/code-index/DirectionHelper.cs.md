File: DirectionHelper.cs — converts 3D world positions to human-readable direction strings (cardinal or clock-face)

namespace Wasteland2AccessibilityMod  (line 3)

enum DirectionFormat  (line 5)
    Cardinal   (line 7)  // North, Northeast, East, etc.
    Clock      (line 8)  // 12 o'clock, 1 o'clock, etc. (fixed to world directions)

class DirectionHelper  (line 11)  [public static]

    public static string GetDirectionDescription(Vector3 fromPos, Vector3 toPos)  (line 13)
        // note: delegates to GetClockPosition or GetCardinalDirection based on ModConfig.UseClockPositions

    private static string GetCardinalDirection(Vector3 fromPos, Vector3 toPos)  (line 25)
        // note: +Z=North, +X=East convention; maps Atan2 angle to 8 compass points; returns "at your location" if coincident

    private static string GetClockPosition(Vector3 fromPos, Vector3 toPos)  (line 54)
        // note: 12 o'clock=North, 3 o'clock=East; rounds angle to nearest 30-degree slot; returns "at your location" if coincident
