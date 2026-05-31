using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Announces when the selected player character changes.
    /// Patches PC.MakeLeader() which is called when switching characters outside combat.
    /// </summary>
    [HarmonyPatch(typeof(PC), "MakeLeader")]
    public class PC_MakeLeader_Patch
    {
        private static PC previousLeader = null;

        [HarmonyPostfix]
        public static void Postfix(PC __instance)
        {
            try
            {
                // Only announce if this PC actually became the leader
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

                PC currentLeader = MonoBehaviourSingleton<Game>.GetInstance().pcLeader;

                // Check if this PC is now the leader and it's different from before
                if (currentLeader != __instance) return;
                if (currentLeader == previousLeader) return;

                previousLeader = currentLeader;

                // Get character name
                string name = GetCharacterName(__instance);

                MelonLogger.Msg($"Character selected: {name}");
                ScreenReaderManager.SpeakInterrupt(name);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in PC_MakeLeader_Patch: {ex.Message}");
            }
        }

        private static string GetCharacterName(PC pc)
        {
            if (pc == null) return "Unknown";

            // Try template display name first
            if (pc.template != null && !string.IsNullOrEmpty(pc.template.displayName))
            {
                return pc.template.displayName;
            }

            // Fall back to pcTemplate
            if (pc.pcTemplate != null && !string.IsNullOrEmpty(pc.pcTemplate.displayName))
            {
                return pc.pcTemplate.displayName;
            }

            // Last resort: game object name
            return pc.name ?? "Unknown";
        }
    }
}
