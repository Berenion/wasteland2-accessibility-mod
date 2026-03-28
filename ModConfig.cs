using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    public static class ModConfig
    {
        private static MelonPreferences_Category configCategory;
        private static MelonPreferences_Entry<bool> useClockPositionsEntry;
        private static MelonPreferences_Entry<bool> objectNamesFirstEntry;

        public static bool UseClockPositions { get; private set; } = false;
        public static bool ObjectNamesFirst { get; private set; } = false;

        public static void Initialize()
        {
            // Create configuration category
            configCategory = MelonPreferences.CreateCategory("Wasteland2Accessibility");
            configCategory.SetFilePath("UserData/Wasteland2Accessibility.cfg");

            // Create configuration entries
            useClockPositionsEntry = configCategory.CreateEntry(
                "UseClockPositions",
                false,
                "Use Clock Positions",
                "If true, uses clock positions (12 o'clock = North). If false, uses cardinal directions (North, South, etc.)."
            );

            objectNamesFirstEntry = configCategory.CreateEntry(
                "ObjectNamesFirst",
                false,
                "Object Names First",
                "If true, announces object/entity names before tile coordinates. If false, announces coordinates first."
            );

            // Load saved preferences
            LoadConfig();

            MelonLogger.Msg($"Configuration loaded - Clock positions: {UseClockPositions}, Object names first: {ObjectNamesFirst}");
        }

        public static void LoadConfig()
        {
            UseClockPositions = useClockPositionsEntry.Value;
            ObjectNamesFirst = objectNamesFirstEntry.Value;
        }

        public static void ToggleClockPositions()
        {
            UseClockPositions = !UseClockPositions;
            useClockPositionsEntry.Value = UseClockPositions;
            configCategory.SaveToFile();

            string format = UseClockPositions ? "clock positions" : "cardinal directions";
            MelonLogger.Msg($"Direction format changed to: {format}");
            ScreenReaderManager.SpeakInterrupt($"Using {format}");
        }
        public static void ToggleObjectNamesFirst()
        {
            ObjectNamesFirst = !ObjectNamesFirst;
            objectNamesFirstEntry.Value = ObjectNamesFirst;
            configCategory.SaveToFile();

            string mode = ObjectNamesFirst ? "object names first" : "coordinates first";
            MelonLogger.Msg($"Tile announcement order changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Using {mode}");
        }
    }
}
