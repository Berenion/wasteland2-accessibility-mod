using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Patches floating text to announce combat events through the screen reader.
    /// Floating text is used for important gameplay events like weapon stolen, ambush,
    /// attack of opportunity, trait procs, weapon jams, bleeding, etc.
    /// These are visual-only in the base game and never reach the description panel.
    /// </summary>
    [HarmonyPatch(typeof(Targetable), "PrintFloatingText", new[] { typeof(string), typeof(Color), typeof(Texture2D), typeof(bool) })]
    public class Targetable_PrintFloatingText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Targetable __instance, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return;

                string cleanedText = UITextExtractor.CleanText(text);
                if (string.IsNullOrEmpty(cleanedText)) return;

                // Get the character/object name for context
                string name = null;
                var mob = __instance as Mob;
                if (mob != null && mob.template != null)
                {
                    name = UITextExtractor.CleanText(
                        Language.Localize(mob.template.displayName, false, false, string.Empty));
                }

                string announcement = string.IsNullOrEmpty(name)
                    ? cleanedText
                    : name + ": " + cleanedText;

                ModLog.Debug($"[FloatingText] {announcement}");

                // Add to combat log for review
                HUD_Controller_QueueTextDescription_Patch.CombatLog.Add(announcement);

                // Use non-interrupting speech to avoid cutting off combat descriptions
                ScreenReaderManager.Speak(announcement);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[FloatingText] Error: {ex.Message}");
            }
        }
    }
}
