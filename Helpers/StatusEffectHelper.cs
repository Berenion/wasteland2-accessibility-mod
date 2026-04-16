using System;
using System.Collections.Generic;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Builds accessible status effect descriptions matching the info shown
    /// in the game's StatusEffectTooltip.
    /// </summary>
    public static class StatusEffectHelper
    {
        /// <summary>
        /// Build a full accessible description for a single status effect,
        /// mirroring the information the game shows in its tooltip.
        /// </summary>
        public static string BuildEffectLine(StatusEffect effect)
        {
            if (effect == null) return null;

            // --- Name ---
            string effectName = null;
            if (!string.IsNullOrEmpty(effect.displayName))
            {
                effectName = UITextExtractor.CleanText(
                    Language.Localize(effect.displayName, false, false, string.Empty));
            }
            if (string.IsNullOrEmpty(effectName))
                effectName = effect.name;
            if (string.IsNullOrEmpty(effectName))
                return null;

            var parts = new List<string>();
            parts.Add(effectName);

            // --- Effect class (type) ---
            try
            {
                string effectType = StatusEffect.GetEffectTypeDisplayName(effect.effectClass);
                string cleanType = UITextExtractor.CleanText(effectType);
                if (!string.IsNullOrEmpty(cleanType)
                    && !cleanType.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                    && !cleanType.Equals(effectName, StringComparison.OrdinalIgnoreCase))
                {
                    parts.Add(cleanType);
                }
            }
            catch { }

            // --- Buff / debuff ---
            parts.Add(effect.positiveEffect ? "buff" : "debuff");

            // --- Description ---
            try
            {
                if (!string.IsNullOrEmpty(effect.description) && effect.description != string.Empty)
                {
                    string desc = UITextExtractor.CleanText(
                        Language.Localize(effect.description, false, false, string.Empty));
                    if (!string.IsNullOrEmpty(desc))
                        parts.Add(desc);
                }
            }
            catch { }

            // --- HP change over time ---
            try
            {
                int hpChange = effect.GetHPChange();
                if (hpChange != 0 && effect.secondsPerHitpointChange > 0)
                {
                    string sign = hpChange > 0 ? "+" : "";
                    string seconds = effect.secondsPerHitpointChange == 1 ? "second" : "seconds";
                    parts.Add(sign + hpChange + " CON every " + effect.secondsPerHitpointChange + " " + seconds);
                }
            }
            catch { }

            // --- Stat modifications ---
            try
            {
                if (effect.statEffects != null && effect.statEffects.Length > 0
                    && MonoBehaviourSingleton<PCStatsManager>.HasInstance())
                {
                    foreach (var stat in effect.statEffects)
                    {
                        if (stat == null) continue;
                        BaseStat characteristic = MonoBehaviourSingleton<PCStatsManager>.GetInstance()
                            .GetCharacteristic(stat.statName);
                        if (characteristic != null)
                        {
                            string statDisplay = Language.Localize(
                                characteristic.displayName, false, false, string.Empty);
                            int amount = stat.amount;
                            string sign = amount > 0 ? "+" : "";
                            string valueStr;
                            if (stat.statName == PCStatsManager.combatSpeed)
                            {
                                float scaled = (float)amount / 100f;
                                valueStr = sign + scaled.ToString("0.0");
                            }
                            else
                            {
                                valueStr = sign + amount.ToString();
                            }
                            string unit = characteristic.GetUnitString();
                            parts.Add(valueStr + unit + " " + UITextExtractor.CleanText(statDisplay));
                        }
                    }
                }
            }
            catch { }

            // --- Duration remaining ---
            try
            {
                if (effect.expiresByTurns && effect.turnsRemaining > 0)
                {
                    string turnWord = effect.turnsRemaining == 1 ? "turn" : "turns";
                    parts.Add(effect.turnsRemaining + " " + turnWord + " remaining");
                }
                else if (effect.expires)
                {
                    ulong msRemaining = effect.GetMillisecondsRemaining();
                    if (msRemaining > 0)
                    {
                        int secondsLeft = (int)(msRemaining / 1000);
                        if (secondsLeft > 0)
                        {
                            string secWord = secondsLeft == 1 ? "second" : "seconds";
                            parts.Add(secondsLeft + " " + secWord + " remaining");
                        }
                    }
                }
            }
            catch { }

            // --- Removal info ---
            try
            {
                if (effect.surgeonCanRemove)
                    parts.Add("removable by surgeon or item");
                else if (effect.cannotBeRemoved)
                    parts.Add("cannot be removed");
            }
            catch { }

            return string.Join(", ", parts.ToArray());
        }
    }
}
