using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Centralized logging wrapper.
    ///
    /// Debug() is gated behind ModConfig.DebugLogging so the routine per-announcement /
    /// per-navigation / per-event chatter (~350 traces) stays out of the log during normal
    /// play. That keeps the log quiet so Warning/Error — the things that mean something
    /// actually broke — are not buried. Turn DebugLogging on in the .cfg to get the traces back.
    ///
    /// Info/Warning/Error always log. Lives in the root namespace so every sub-namespace
    /// (States, Patches, Helpers, Core) can call it unqualified, same as ModConfig.
    /// </summary>
    public static class ModLog
    {
        /// <summary>Routine trace. Only reaches the log when DebugLogging is enabled.</summary>
        public static void Debug(string message)
        {
            if (ModConfig.DebugLogging)
                MelonLogger.Msg(message);
        }

        /// <summary>Always-on informational line (startup banner, mode changes, etc.).</summary>
        public static void Info(string message)
        {
            MelonLogger.Msg(message);
        }

        public static void Warning(string message)
        {
            MelonLogger.Warning(message);
        }

        public static void Error(string message)
        {
            MelonLogger.Error(message);
        }
    }
}
