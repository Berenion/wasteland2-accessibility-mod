using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    public static class ModConfig
    {
        private static MelonPreferences_Category configCategory;
        private static MelonPreferences_Entry<bool> useClockPositionsEntry;

        public static bool UseClockPositions { get; private set; } = false;

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

            // Load saved preferences
            LoadConfig();

            MelonLogger.Msg($"Configuration loaded - Clock positions: {UseClockPositions}");
        }

        public static void LoadConfig()
        {
            UseClockPositions = useClockPositionsEntry.Value;
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
    }
}
