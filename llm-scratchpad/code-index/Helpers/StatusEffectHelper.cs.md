File: Helpers/StatusEffectHelper.cs — builds accessible status effect descriptions matching the game's StatusEffectTooltip display.

namespace Wasteland2AccessibilityMod.Helpers  (line 3)

static class StatusEffectHelper  (line 11)
    // Single public method; assembles a comma-joined description from all fields the game tooltip shows.

    // Builds a full accessible description for a single StatusEffect: name, type, buff/debuff, description, HP-over-time, stat modifiers, duration, removal info.
    public static string BuildEffectLine(StatusEffect effect)  (line 16)
        // note: combatSpeed stat modifier is divided by 100 (stored as int * 100) to produce the float the game displays; all other stat amounts are used as-is.
