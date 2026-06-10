using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Announces enemy movement during combat. The game emits nothing to the
    /// description panel for enemy moves — this is visual-only, which is
    /// inaccessible. We capture the starting square in StartedMoving, then
    /// announce and log the move in FinishedMoving.
    /// </summary>
    [HarmonyPatch(typeof(Mob), "StartedMoving")]
    public class Mob_StartedMoving_Patch
    {
        // Start-of-move snapshot, keyed by mob instance. FinishedMoving consumes it.
        public static readonly Dictionary<Mob, Vector3> MoveStart = new Dictionary<Mob, Vector3>();

        [HarmonyPostfix]
        public static void Postfix(Mob __instance)
        {
            try
            {
                if (__instance == null || __instance is PC) return;
                if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return;
                if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return;
                if (__instance.currentSquare == null) return;

                MoveStart[__instance] = __instance.currentSquare.id;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CombatMovement] StartedMoving error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Mob), "FinishedMoving", new[] { typeof(int) })]
    public class Mob_FinishedMoving_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Mob __instance)
        {
            try
            {
                if (__instance == null || __instance is PC) return;
                if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return;
                if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return;
                if (__instance.currentSquare == null) return;

                Vector3 startId;
                bool hadStart = Mob_StartedMoving_Patch.MoveStart.TryGetValue(__instance, out startId);
                Mob_StartedMoving_Patch.MoveStart.Remove(__instance);
                if (!hadStart) return;

                Vector3 endId = __instance.currentSquare.id;
                if (startId == endId) return;

                // Only announce if the destination is visible to the party. This
                // mirrors the game's own behavior — enemies that end their move in
                // fog of war "disappear" visually and shouldn't leak position via
                // TTS. FOWHelper.IsVisibleThroughFOW returns true when FOWSystem
                // isn't loaded, so non-FOW scenes still announce.
                if (FOWHelper.IsFOWReady()
                    && !FOWHelper.IsVisibleThroughFOW(__instance.currentSquare.position))
                {
                    return;
                }

                string name = GetMobName(__instance);
                string announcement = name + " moves from " + FormatCoords(startId)
                    + " to " + FormatCoords(endId);

                ModLog.Debug("[CombatMovement] " + announcement);
                HUD_Controller_QueueTextDescription_Patch.CombatLog.Add(announcement);
                ScreenReaderManager.Speak(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CombatMovement] FinishedMoving error: {ex.Message}");
            }
        }

        private static string GetMobName(Mob mob)
        {
            if (mob.template != null && !string.IsNullOrEmpty(mob.template.displayName))
            {
                return UITextExtractor.CleanText(
                    Language.Localize(mob.template.displayName, false, false, string.Empty));
            }
            if (mob.gameObject != null && !string.IsNullOrEmpty(mob.gameObject.name))
                return mob.gameObject.name.Replace("_", " ").Replace("(Clone)", "").Trim();
            return "Enemy";
        }

        private static string FormatCoords(Vector3 id)
        {
            string s = (int)id.x + ", " + (int)id.z;
            if (id.y > 0) s += ", floor " + ((int)id.y + 1);
            return s;
        }
    }
}
