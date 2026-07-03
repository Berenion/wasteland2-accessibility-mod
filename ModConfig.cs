using System;
using System.Collections.Generic;
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
        private static MelonPreferences_Entry<bool> announceLineOfSightEntry;
        private static MelonPreferences_Entry<bool> announcePartyStoppedEntry;
        private static MelonPreferences_Entry<bool> scannerCategorySoundsEntry;
        private static MelonPreferences_Entry<bool> debugLoggingEntry;

        public static bool UseClockPositions { get; private set; } = false;
        public static bool ObjectNamesFirst { get; private set; } = false;
        public static bool UseTileDistances { get; private set; } = true;
        public static bool ConveyElevation { get; private set; } = true;
        public static bool AnnounceLineOfSight { get; private set; } = false;
        public static bool AnnouncePartyStopped { get; private set; } = true;
        public static bool ScannerCategorySounds { get; private set; } = true;
        public static bool DebugLogging { get; private set; } = false;

        /// <summary>
        /// One boolean mod setting, described in menu-friendly terms. Drives the
        /// in-game accessibility settings menu (SettingsMenuState): it reads the
        /// current value for navigation read-out and flips it on toggle without
        /// speaking (the menu announces). The standalone quick-toggle hotkeys keep
        /// their own custom phrasing via the public Toggle* methods below.
        /// </summary>
        public sealed class Setting
        {
            private readonly Func<bool> get;
            private readonly Action flip;
            private readonly string onText;
            private readonly string offText;

            public string Label { get; }

            public Setting(string label, Func<bool> get, Action flip, string onText, string offText)
            {
                Label = label;
                this.get = get;
                this.flip = flip;
                this.onText = onText;
                this.offText = offText;
            }

            public bool Value => get();

            /// <summary>The current value as spoken text, e.g. "clock positions" / "tiles" / "on".</summary>
            public string ValueText => get() ? onText : offText;

            /// <summary>Navigation read-out, e.g. "Distance units, tiles".</summary>
            public string Describe() => $"{Label}, {ValueText}";

            /// <summary>Flip the value and persist it. Does not speak — the caller announces.</summary>
            public void Toggle() => flip();
        }

        /// <summary>
        /// All toggleable settings, in menu display order. Built in Initialize().
        /// </summary>
        public static List<Setting> Settings { get; private set; } = new List<Setting>();

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

            announceLineOfSightEntry = configCategory.CreateEntry(
                "AnnounceLineOfSight",
                false,
                "Announce Line Of Sight",
                "If true, the exploration tile cursor also announces whether the tile is within line of sight of the selected character (clear physics LOS within that character's perception range)."
            );

            announcePartyStoppedEntry = configCategory.CreateEntry(
                "AnnouncePartyStopped",
                true,
                "Announce Party Stopped",
                "If true, announces when the party finishes an ordered move and comes to rest during exploration or on the world map. When party members move separately (ungrouped), the member that stopped is named."
            );

            scannerCategorySoundsEntry = configCategory.CreateEntry(
                "ScannerCategorySounds",
                true,
                "Scanner Category Sounds",
                "If true, plays a short sound cue when a new item appears in the exploration scanner, with a distinct sound per category (characters, containers, objects, exits, examine, loot, and a generic cue for miscellaneous). Party members have no cue."
            );

            debugLoggingEntry = configCategory.CreateEntry(
                "DebugLogging",
                false,
                "Debug Logging",
                "If true, the mod writes its routine per-announcement / per-navigation traces (ModLog.Debug) to the MelonLoader log. Leave false for normal play so the log stays quiet and real warnings/errors are easy to spot. Turn on only when diagnosing an issue."
            );

            // Load saved preferences
            LoadConfig();

            // Build the menu-facing settings list (order = menu display order)
            Settings = new List<Setting>
            {
                new Setting("Direction format", () => UseClockPositions, FlipClockPositions, "clock positions", "cardinal directions"),
                new Setting("Tile announcement order", () => ObjectNamesFirst, FlipObjectNamesFirst, "object names first", "coordinates first"),
                new Setting("Distance units", () => UseTileDistances, FlipTileDistances, "tiles", "meters"),
                new Setting("Elevation announcements", () => ConveyElevation, FlipConveyElevation, "on", "off"),
                new Setting("Line of sight announcements", () => AnnounceLineOfSight, FlipLineOfSight, "on", "off"),
                new Setting("Party stopped notification", () => AnnouncePartyStopped, FlipPartyStopped, "on", "off"),
                new Setting("Scanner category sounds", () => ScannerCategorySounds, FlipScannerCategorySounds, "on", "off"),
                new Setting("Debug logging", () => DebugLogging, FlipDebugLogging, "on", "off"),
            };

            MelonLogger.Msg($"Configuration loaded - Clock positions: {UseClockPositions}, Object names first: {ObjectNamesFirst}, Tile distances: {UseTileDistances}, Convey elevation: {ConveyElevation}, Line of sight: {AnnounceLineOfSight}, Party stopped: {AnnouncePartyStopped}, Scanner sounds: {ScannerCategorySounds}, Debug logging: {DebugLogging}");
        }

        public static void LoadConfig()
        {
            UseClockPositions = useClockPositionsEntry.Value;
            ObjectNamesFirst = objectNamesFirstEntry.Value;
            UseTileDistances = useTileDistancesEntry.Value;
            ConveyElevation = conveyElevationEntry.Value;
            AnnounceLineOfSight = announceLineOfSightEntry.Value;
            AnnouncePartyStopped = announcePartyStoppedEntry.Value;
            ScannerCategorySounds = scannerCategorySoundsEntry.Value;
            DebugLogging = debugLoggingEntry.Value;
        }

        // ===== No-speech flips (flip value + persist). Shared by the quick-toggle
        // hotkeys (which then speak their own phrasing) and the settings menu (which
        // announces via Setting.Describe). =====

        private static void FlipClockPositions()
        {
            UseClockPositions = !UseClockPositions;
            useClockPositionsEntry.Value = UseClockPositions;
            configCategory.SaveToFile();
        }

        private static void FlipObjectNamesFirst()
        {
            ObjectNamesFirst = !ObjectNamesFirst;
            objectNamesFirstEntry.Value = ObjectNamesFirst;
            configCategory.SaveToFile();
        }

        private static void FlipTileDistances()
        {
            UseTileDistances = !UseTileDistances;
            useTileDistancesEntry.Value = UseTileDistances;
            configCategory.SaveToFile();
        }

        private static void FlipConveyElevation()
        {
            ConveyElevation = !ConveyElevation;
            conveyElevationEntry.Value = ConveyElevation;
            configCategory.SaveToFile();
        }

        private static void FlipLineOfSight()
        {
            AnnounceLineOfSight = !AnnounceLineOfSight;
            announceLineOfSightEntry.Value = AnnounceLineOfSight;
            configCategory.SaveToFile();
        }

        private static void FlipPartyStopped()
        {
            AnnouncePartyStopped = !AnnouncePartyStopped;
            announcePartyStoppedEntry.Value = AnnouncePartyStopped;
            configCategory.SaveToFile();
        }

        private static void FlipScannerCategorySounds()
        {
            ScannerCategorySounds = !ScannerCategorySounds;
            scannerCategorySoundsEntry.Value = ScannerCategorySounds;
            configCategory.SaveToFile();
        }

        private static void FlipDebugLogging()
        {
            DebugLogging = !DebugLogging;
            debugLoggingEntry.Value = DebugLogging;
            configCategory.SaveToFile();
        }

        // ===== Quick-toggle hotkeys: flip + speak custom phrasing. =====

        public static void ToggleTileDistances()
        {
            FlipTileDistances();
            string mode = UseTileDistances ? "tiles" : "meters";
            MelonLogger.Msg($"Distance units changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Distance in {mode}");
        }

        public static void ToggleClockPositions()
        {
            FlipClockPositions();
            string format = UseClockPositions ? "clock positions" : "cardinal directions";
            MelonLogger.Msg($"Direction format changed to: {format}");
            ScreenReaderManager.SpeakInterrupt($"Using {format}");
        }

        public static void ToggleObjectNamesFirst()
        {
            FlipObjectNamesFirst();
            string mode = ObjectNamesFirst ? "object names first" : "coordinates first";
            MelonLogger.Msg($"Tile announcement order changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Using {mode}");
        }

        public static void ToggleConveyElevation()
        {
            FlipConveyElevation();
            string mode = ConveyElevation ? "on" : "off";
            MelonLogger.Msg($"Convey elevation changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Elevation announcements {mode}");
        }

        public static void ToggleLineOfSight()
        {
            FlipLineOfSight();
            string mode = AnnounceLineOfSight ? "on" : "off";
            MelonLogger.Msg($"Line of sight announcements changed to: {mode}");
            ScreenReaderManager.SpeakInterrupt($"Line of sight announcements {mode}");
        }
    }
}
