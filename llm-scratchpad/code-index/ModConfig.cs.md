File: ModConfig.cs — MelonPreferences-backed configuration for direction format, announcement order, and distance units

namespace Wasteland2AccessibilityMod  (line 3)

class ModConfig  (line 5)  [public static]

    private static MelonPreferences_Category configCategory  (line 7)
    private static MelonPreferences_Entry<bool> useClockPositionsEntry  (line 8)
    private static MelonPreferences_Entry<bool> objectNamesFirstEntry  (line 9)
    private static MelonPreferences_Entry<bool> useTileDistancesEntry  (line 10)

    public static bool UseClockPositions  (line 12)  [get; private set; default false]
    public static bool ObjectNamesFirst  (line 13)  [get; private set; default false]
    public static bool UseTileDistances  (line 14)  [get; private set; default true]

    public static void Initialize()  (line 16)
        // note: creates category "Wasteland2Accessibility" saved to UserData/Wasteland2Accessibility.cfg, then calls LoadConfig()

    public static void LoadConfig()  (line 51)

    public static void ToggleTileDistances()  (line 57)
        // note: persists to file and speaks the new mode via SpeakInterrupt

    public static void ToggleClockPositions()  (line 68)
        // note: persists to file and speaks the new mode via SpeakInterrupt

    public static void ToggleObjectNamesFirst()  (line 78)
        // note: persists to file and speaks the new mode via SpeakInterrupt
