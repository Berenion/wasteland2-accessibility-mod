using MelonLoader;

[assembly: MelonInfo(typeof(Wasteland2AccessibilityMod.AccessibilityMod), "Wasteland 2 Accessibility Mod", "1.0.0", "AccessibilityModTeam")]
[assembly: MelonGame("inXile Entertainment", "Wasteland 2 Director's Cut")]

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Main mod class for Wasteland 2 Accessibility Mod
    /// Provides screen reader support for UI navigation
    /// </summary>
    public class AccessibilityMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Wasteland 2 Accessibility Mod v1.0.0");
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Features:");
            MelonLogger.Msg("  - Screen reader support for UI navigation");
            MelonLogger.Msg("  - Exploration mode interactable navigation");
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Keyboard Controls:");
            MelonLogger.Msg("  [ - Previous interactable");
            MelonLogger.Msg("  ] - Next interactable");
            MelonLogger.Msg("  \\ - Repeat last announcement");
            MelonLogger.Msg("  = - Toggle direction format (Cardinal/Clock)");
            MelonLogger.Msg("===========================================");

            // Initialize screen reader support
            ScreenReaderManager.Initialize();

            // Initialize configuration
            ModConfig.Initialize();
        }

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("Applying Harmony patches...");
        }

        public override void OnDeinitializeMelon()
        {
            ScreenReaderManager.Shutdown();
        }
    }
}
