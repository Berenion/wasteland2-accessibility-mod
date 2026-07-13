using MelonLoader;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.States;

[assembly: MelonInfo(typeof(Wasteland2AccessibilityMod.AccessibilityMod), "Wasteland 2 Accessibility Mod", Wasteland2AccessibilityMod.AccessibilityMod.Version, "AccessibilityModTeam")]
[assembly: MelonGame("inXile Entertainment", "Wasteland 2 Director's Cut")]

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Main mod class for Wasteland 2 Accessibility Mod.
    /// Provides screen reader support, keyboard navigation, and virtual map cursor.
    /// </summary>
    public class AccessibilityMod : MelonMod
    {
        /// <summary>
        /// Single source of truth for the mod version. Used by the MelonInfo
        /// attribute, the startup banner, and the spoken launch announcement.
        /// </summary>
        public const string Version = "0.8.5";

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg($"Wasteland 2 Accessibility Mod v{Version} (beta)");
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Features:");
            MelonLogger.Msg("  - Screen reader support for UI navigation");
            MelonLogger.Msg("  - Unified keyboard input routing");
            MelonLogger.Msg("  - Exploration interactable navigation");
            MelonLogger.Msg("  - Camera rotation lock (North = Up)");
            MelonLogger.Msg("  - Audio-aware announcements");
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Keyboard Controls:");
            MelonLogger.Msg("  [ ] - Cycle interactables");
            MelonLogger.Msg("  \\ - Repeat last announcement");
            MelonLogger.Msg("  = - Toggle direction format");
            MelonLogger.Msg("  ' - Announce party scrap");
            MelonLogger.Msg("  PgUp/PgDn - Cycle categories");
            MelonLogger.Msg("  Space - Tactical pause (freeze enemies)");
            MelonLogger.Msg("  F10 - Toggle camera lock");
            MelonLogger.Msg("  M - Toggle map cursor mode");
            MelonLogger.Msg("  G - Toggle group mode");
            MelonLogger.Msg("  T - Initiative/turn order (in combat)");
            MelonLogger.Msg("  Shift+S - Accessibility settings menu");
            MelonLogger.Msg("  Shift+/ - Read controls for current context");
            MelonLogger.Msg("===========================================");

            // Initialize screen reader support
            ScreenReaderManager.Initialize();

            // Initialize audio-aware announcement manager
            AudioAwareAnnouncementManager.Instance.Initialize();

            // Initialize configuration
            ModConfig.Initialize();

            // Initialize camera lock system
            CameraLock.Initialize();

            // Register accessibility states with input router (order doesn't matter - sorted by priority)
            // Phase 1: Core exploration
            InputRouter.Register(new ExplorationState());  // Priority 10 - exploration cycling

            // Phase 2: Menu/dialog states
            InputRouter.Register(new SettingsMenuState()); // Priority 90 - modal mod-settings menu (Shift+S)
            InputRouter.Register(new KeywordEntryState()); // Priority 72 - conversation custom-keyword/password text box
            InputRouter.Register(new DialogState());       // Priority 70 - modal dialogs (highest menu priority)
            InputRouter.Register(new GameOverState());     // Priority 62 - game-over screen (party wiped) options
            InputRouter.Register(new MainMenuState());     // Priority 60 - main menu navigation
            InputRouter.Register(new ComputerGameState()); // Priority 59 - Snake Easter-egg computer sonification
            InputRouter.Register(new KeypadState());       // Priority 58 - safe/passcode keypad popup
            InputRouter.Register(new ItemResultState());   // Priority 56 - field-strip result popup (overlays inventory)
            InputRouter.Register(new GenericMenuState());  // Priority 55 - generic popup menus (Options, Load/Save, etc.)
            InputRouter.Register(new ConversationState()); // Priority 50 - dialogue navigation
            InputRouter.Register(new InventoryState());    // Priority 50 - inventory navigation
            InputRouter.Register(new ShopState());         // Priority 50 - vendor/shop navigation
            InputRouter.Register(new CharacterState());    // Priority 50 - character creation navigation
            InputRouter.Register(new CharacterInfoState()); // Priority 50 - in-game character info (Attributes/Skills/Traits/Dossier)

            // Phase 2b: Combat state
            InputRouter.Register(new CombatState());  // Priority 45 - combat initiative tracker and accessibility

            // Phase 3: Map cursor and world map
            InputRouter.Register(new MapCursorState());    // Priority 30 - virtual map cursor (M to toggle)
            InputRouter.Register(new WorldMapState());     // Priority 20 - world map review cursor and navigation

            MelonLogger.Msg("[Core] Input router initialized with all states (including MainMenu)");
        }

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("Applying Harmony patches...");

            // Capture initial camera rotation for lock
            CameraLock.ResetToNorth();
        }

        public override void OnUpdate()
        {
            // Process accessibility input FIRST (before game's Update methods run)
            InputRouter.ProcessInput();

            // Maintain auto-pause for inventory/loot/vendor screens (after input
            // so a manual Space toggle this frame wins over the auto-pause check)
            TacticalPauseManager.Tick();

            // Update the audio-aware announcement manager every frame
            AudioAwareAnnouncementManager.Instance.Update();

            // Track whether FOW has had an unpaused frame to converge since last LoadMap
            FOWHelper.Tick();

            // Announce when the party finishes an ordered move (exploration / world map)
            PartyStopMonitor.Tick();

            // Play a per-category cue when a new item enters the exploration scanner
            ScannerCueMonitor.Tick();
        }

        public override void OnDeinitializeMelon()
        {
            ScreenReaderManager.Shutdown();
        }
    }
}
