using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Conveys vertical elevation to the player. World-space Y in Wasteland 2 is in
    /// meters (the same units the combat grid's 1.6 m tiles use).
    ///
    /// The combat hit-chance effect comes straight from Mob.GetChanceToHitEnvironmentBonus:
    /// an attacker firing from >=2 m above the target gets +25, from >=2 m below gets -15,
    /// and otherwise nothing. That delta is the raw transform Y difference, NOT the A* grid
    /// floor index, so it captures crates, ledges, balconies and slopes on the same floor.
    /// We mirror those exact thresholds here so what the player hears matches the math.
    /// </summary>
    public static class ElevationHelper
    {
        // Mirrors Mob.GetChanceToHitEnvironmentBonus.
        public const float HeightAdvantageThreshold = 2f;
        public const int HighGroundBonus = 25;
        public const int LowGroundPenalty = -15;

        // Below this, treat the ground as level — keeps flat terrain silent so a spoken
        // height delta always means a real ramp or edge.
        private const float MinReportableMeters = 1f;

        /// <summary>
        /// Hit-chance bonus an attacker firing from <paramref name="fromY"/> gets against a
        /// target at <paramref name="targetY"/>. Mirrors the game exactly.
        /// </summary>
        public static int GetHitChanceBonus(float fromY, float targetY)
        {
            float dy = fromY - targetY;
            if (dy >= HeightAdvantageThreshold) return HighGroundBonus;
            if (dy <= -HeightAdvantageThreshold) return LowGroundPenalty;
            return 0;
        }

        /// <summary>
        /// "up N meters" / "down N meters" for the height change between two tiles, or null
        /// when the change rounds to under a meter. Used as cursor-move feedback: silence
        /// means flat, a spoken delta means a ramp (small steady steps) or edge (big jump).
        /// </summary>
        public static string DescribeChange(float fromY, float toY)
        {
            float dy = toY - fromY;
            int meters = Mathf.RoundToInt(Mathf.Abs(dy));
            if (meters < MinReportableMeters) return null;
            return (dy > 0f ? "up " : "down ") + meters + (meters == 1 ? " meter" : " meters");
        }

        /// <summary>
        /// Raw "N meters above/below {reference}", or null when within a meter. Used in
        /// exploration, where no combat bonus applies.
        /// </summary>
        public static string DescribeRelativeRaw(float subjectY, float referenceY, string referenceLabel)
        {
            float dy = subjectY - referenceY;
            int meters = Mathf.RoundToInt(Mathf.Abs(dy));
            if (meters < MinReportableMeters) return null;
            return meters + (meters == 1 ? " meter " : " meters ")
                + (dy > 0f ? "above " : "below ") + referenceLabel;
        }

        /// <summary>
        /// Tactical phrasing with the hit-chance number, for attacking from
        /// <paramref name="fromY"/> against a target at <paramref name="targetY"/>:
        /// "high ground, plus 25% to hit" / "low ground, minus 15% to hit", or null when
        /// the +/-2 m threshold isn't met. Used in combat.
        /// </summary>
        public static string DescribeTactical(float fromY, float targetY)
        {
            int bonus = GetHitChanceBonus(fromY, targetY);
            if (bonus > 0) return "high ground, plus " + bonus + "% to hit";
            if (bonus < 0) return "low ground, minus " + (-bonus) + "% to hit";
            return null;
        }
    }
}
