using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    public static class ModConfig
    {
        private static MelonPreferences_Category configCategory;
        private static MelonPreferences_Entry<bool> useClockPositionsEntry;
        private static MelonPreferences_Entry<bool> objectNamesFirstEntry;
        private static MelonPreferences_Entry<bool> useTileDistancesEntry;
        private static MelonPreferences_Entry<bool> conveyElevationEntry;

        public static bool UseClockPositions { get; private set; } = false;
        public static bool ObjectNamesFirst { get; private set; } = false;
        public static bool UseTileDistances { get; private set; } = true;
        public static bool ConveyElevation { get; private set; } = true;

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

            useTileDistancesEntry = configCategory.CreateEntry(
                "UseTileDistances",
                true,
                "Use Tile Distances",
                "If true, reports distances in grid tiles (1 tile = 1.6 meters) when a combat grid is available in the current scene. If false, always reports meters."
            );

            conveyElevationEntry = configCategory.CreateEntry(
                "ConveyElevation",
                true,
                "Convey Elevation",
                "If true, announces terrain height changes and height relative to the party as the exploration cursor moves (for finding ramps and edges). Combat always announces elevation regardless of this setting."
            );

            // Load saved preferences
            LoadConfig();

            MelonLogger.Msg($"Configuration loaded - Clock positions: {UseClockPositions}, Object names first: {ObjectNamesFirst}, Tile distances: {UseTileDistances}, Convey elevation: {ConveyElevation}");
        }

        public static void LoadConfig()
        {
            UseClockPositions = useClockPositionsEntry.Value;
            ObjectNamesFirst = objectNamesFirstEntry.Value;
            UseTileDistances = useTileDistancesEntry.Value;
            ConveyElevation = conveyElevationEntry.Value;
        }

        public static void ToggleTileDistances()
        {
            UseTileDistances = !UseTileDistances;
            useTileDistancesEntry.Value = UseTileDistances;
            configCategory.SaveToFile();

            string mode = UseTileDistances ? "tiles" : "meters";
            MelonLogger.Msg($"Distance units changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Distance in {mode}");
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

        public static void ToggleConveyElevation()
        {
            ConveyElevation = !ConveyElevation;
            conveyElevationEntry.Value = ConveyElevation;
            configCategory.SaveToFile();

            string mode = ConveyElevation ? "on" : "off";
            MelonLogger.Msg($"Convey elevation changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Elevation announcements {mode}");
        }
    }
}
