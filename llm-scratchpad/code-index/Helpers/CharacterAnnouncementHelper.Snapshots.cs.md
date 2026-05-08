File: Helpers/CharacterAnnouncementHelper.Snapshots.cs — derived stats, header/combat snapshots, character summary, and XP announcements.

namespace Wasteland2AccessibilityMod.Helpers  (line 6)

public static partial class CharacterAnnouncementHelper  (line 11)

    public static string FormatDerivedStatValue(int rawValue, DerivedStat.StatDisplayType displayType)  (line 15)
        // note: dispatch table maps displayType to formatted string (%, AP, F1 decimal for CombatMovement, meters, lbs, or raw).

    public static void AnnounceDerivedStat(PC pc, int index, bool interrupt = true)  (line 35)
        // note: looks up stat by DerivedStatNames[index]; announces "{displayName}, {value}, N of Total".

    public static void AnnounceDerivedStatDescription(PC pc, int index)  (line 75)
        // note: reads description from PCStatsManager.GetCharacteristic; appends trait base-stat tooltip when a PC is available.

    // Builds the derived-stats list as individually-browsable lines in DerivedStatNames order, formatted "{name}: {value}".
    public static List<string> BuildDerivedStatLines(PC pc)  (line 121)

    // Builds the always-visible CharacterInfoMenu header as individually-browsable lines: name+level+rank, HP, capacity, money, water, points-available, status effects.
    public static List<string> BuildHeaderSnapshotLines(PC pc)  (line 157)
        // note: HP line appends healthState when not Healthy; capacity line appends "near capacity" or "over encumbered" at 80%/100% ratio; one line per status effect via StatusEffectHelper.BuildEffectLine.

    // Builds a short "points available" hint for the auto-announcement when entering Attributes / Skills / Perks tab.
    public static string BuildPointsAvailableHint(PC pc, CharacterInfoMenu.InfoPanel panel)  (line 254)

    // Builds the combat snapshot as individually-browsable lines mirroring the StatDisplayList "Combat" view plus armor.
    public static List<string> BuildCombatSnapshotLines(PC pc)  (line 282)
        // note: includes current-weapon damage range (uses CalculateDamage when weapon instance is available); iterates a fixed set of combat stat keys; formats DerivedStat values via FormatDerivedStatValue.

    public static void AnnounceCharacterSummary(PC pc)  (line 346)

    public static string BuildXPAnnouncement(PC pc)  (line 373)
        // note: returns "max level" form when IsAtMaxLevel(); appends "Level up available" when CanLevelUp.

    public static void AnnounceXP(PC pc, bool interrupt = true)  (line 392)
