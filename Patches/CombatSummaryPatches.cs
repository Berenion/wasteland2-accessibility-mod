using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for reading the end-of-combat XP summary aloud.
    /// The game types out enemy names and scrambles XP numbers visually,
    /// which is inaccessible. This patch reads the full report immediately.
    /// Uses a prefix because the original method removes entries from the kill list.
    /// </summary>
    [HarmonyPatch(typeof(HUD_CombatSummary), "OnCombatEnded")]
    public class HUD_CombatSummary_OnCombatEnded_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return;

                var killList = MonoBehaviourSingleton<CombatManager>.GetInstance().killList;
                if (killList == null || killList.Count == 0) return;

                var parts = new List<string>();
                parts.Add("Combat summary");

                int totalXP = 0;
                foreach (var kill in killList)
                {
                    string name = UITextExtractor.CleanText(
                        Language.Localize(kill.name, false, false, string.Empty));
                    parts.Add(name + ", " + kill.xp + " XP");
                    totalXP += kill.xp;
                }

                parts.Add("Total: " + totalXP + " XP");

                string announcement = string.Join(". ", parts.ToArray());
                MelonLogger.Msg("[CombatSummary] " + announcement);
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CombatSummary] Error reading combat summary: " + ex.Message);
            }
        }
    }
}
