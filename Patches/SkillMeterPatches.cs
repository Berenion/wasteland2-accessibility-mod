using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Wasteland 2's metered skill actions (item use, lockpicking, healing, alarm
    /// disarm, etc.) play a silent 5-second progress bar. A sighted player sees
    /// the bar; a screen-reader user gets only the initial "X is using Y" line
    /// and then five seconds of silence — which leads them to open another UI
    /// and accidentally interrupt the action. This patch appends a "please wait"
    /// hint so the user knows to hold off.
    /// </summary>
    [HarmonyPatch(typeof(SkillMetered), "PrepareMeter")]
    public class SkillMetered_PrepareMeter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SkillMetered __instance, PC pc, float duration)
        {
            try
            {
                // Skip very short meters (combat 1-second actions, skipMeter cases)
                // where waiting isn't really a concern.
                if (duration < 2f) return;

                // If AddComponent<ChallengeMeter> returned null, PrintUsingString
                // was never queued and we'd be announcing a wait for nothing.
                var meter = Traverse.Create(__instance).Field("meter").GetValue();
                if (meter == null) return;

                MelonLogger.Msg("[SkillMeter] Announcing wait hint");
                ScreenReaderManager.Speak("please wait");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SkillMeter] Error: {ex.Message}");
            }
        }
    }
}
