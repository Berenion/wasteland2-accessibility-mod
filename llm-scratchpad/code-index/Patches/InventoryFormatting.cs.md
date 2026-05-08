File: Patches/InventoryFormatting.cs — formatting and calculation helpers for inventory item announcements; replicates ItemInfoBox/WeaponRangeTooltip/AttackModePanel logic so a blind player hears the same numbers a sighted player sees.

namespace Wasteland2AccessibilityMod.Patches  (line 6)

public static class InventoryFormatting  (line 13)

    internal static string lastAnnouncedItem  (line 15)

    // Formats an ItemInstance for screen reader announcement; adds quantity, type, weight, and equipped status when detailed=true.
    internal static string FormatItemAnnouncement(ItemInstance item, bool detailed = false)  (line 21)

    // Formats comprehensive item details for announcement (called on selection, not hover); covers weapons, armor, ammo, consumables, mods, components, trinkets, and junk.
    internal static string FormatDetailedItemInfo(ItemInstance item, PC pc = null)  (line 79)
        // note: weapon branch computes PC-aware accuracy and crit via ComputeAccuracyPercent/ComputeCritChancePercent; ranged weapons include ammo count, reserve ammo, caliber, range brackets, and firing modes; consumables use skill-bonus-adjusted heal values.

    // Formats equipment slot as "{slotName} slot, {item info}" or "{slotName} slot, empty".
    internal static string FormatEquipmentSlot(EquipmentSlot slot, ItemInstance item)  (line 324)

    // Lines for each entry in ItemTemplate_Equipment.stats; mirrors ItemInfoBox.BuildBonusString; skips raw "armor" value for armor pieces.
    internal static List<string> BuildModifierLines(ItemInstance item)  (line 341)

    // Lines for each entry in ItemTemplate_Equipment.requiredAttributeValues, annotated with "(met)"/"(not met)" for the current PC.
    internal static List<string> BuildRequirementLines(ItemInstance item, PC currentPC)  (line 365)
        // note: falls back to first selected PC when currentPC is null.

    // Returns "X% chance to apply Y [or Z]" for weapons with status effects, or null.
    internal static string BuildAfflictorLine(ItemInstance item)  (line 392)

    // Localized display name of a weapon's skill (e.g. "Assault Rifle"), or null.
    internal static string GetWeaponSkillDisplayName(ItemTemplate_Weapon wt)  (line 415)

    // Caliber display string for a ranged weapon, or null.
    internal static string GetWeaponCaliberDisplay(ItemTemplate_Weapon wt)  (line 428)

    // Replicates ItemInfoBox.OnWeaponSelected accuracy calculation for the given PC; returns -1 when not applicable.
    internal static int ComputeAccuracyPercent(ItemInstance_Weapon weapon, PC pc)  (line 442)
        // note: includes weapon-mod bonuses, bonus ranged hit chance, trait multipliers, and missing-attribute penalty; Thrown/RPG always return 100.

    // Replicates ItemInfoBox.OnWeaponSelected crit calculation; energy weapons / Thrown / RPG return 0.
    internal static int ComputeCritChancePercent(ItemInstance_Weapon weapon, PC pc)  (line 475)

    // One line per UseEffects entry in a consumable with skill-bonus-adjusted heal values; also adds "Requires X skill of Y" and "Requirements not met" lines.
    internal static List<string> BuildConsumableEffectLines(ItemInstance_Usable item, PC pc)  (line 499)
        // note: mirrors ItemInfoBox.BuildFieldMedicString; handles Heal, HealPercent, Resurrect, StatusEffect, KillEffect, KillEffectClass effect types.

    // Lines describing mod slots of an equipment item: one line per slot with installed mod name or "empty"; mirrors ItemInfoBox.SetModDisplay.
    internal static List<string> BuildModSlotLines(ItemInstance item)  (line 614)

    // Info lines for a weapon mod item: stat bonuses, slot type, attribute/weaponSmith requirements, and allowed-weapon list.
    internal static List<string> BuildWeaponModLines(ItemInstance_Mod item, PC pc)  (line 647)
        // note: mirrors ItemInfoBox.BuildModBonusString; weaponSmith requirement is looked up via GetSkillLevel, not GetCharacteristic.

    // Line describing an XP-giver consumable (e.g. skill book) — "+1 Surgeon skill" or "+10 Surgeon skill" for maxSkillOut items.
    internal static string BuildXPGiverLine(ItemTemplate_UsableXPGiver xpGiver)  (line 708)

    // Lines for each acquired trait on the PC that has a custom tooltip for the item; mirrors ItemInfoBox using pcTemplate.GetTraitItemModifierString.
    internal static List<string> BuildTraitItemModifierLines(ItemInstance item, PC pc)  (line 727)
        // note: splits the raw string on newlines; prefixes each with "Trait: ".

    // Lines for energy weapons: descriptive note and above/below armor threshold damage multipliers; mirrors ItemInfoBox.OnWeaponSelected lines 503-547.
    internal static List<string> BuildEnergyWeaponLines(ItemTemplate_Weapon wt)  (line 755)
        // note: melee energy weapons read energyPenetratedMultiplier for both thresholds (matches the game's display, not a bug).

    private static string FormatModStatBonus(string key, float value)  (line 786)
        // note: flags "negative" stats (jam chance, reload AP, attack AP) as penalties when value > 0.

    // One line per firing mode: "Single: 3 AP, 1 ammo", annotating the currently-selected mode with "(current)"; mirrors AttackModePanel.
    internal static List<string> BuildFiringModeLines(ItemInstance_WeaponRanged weapon)  (line 816)
        // note: only the current mode's AP cost includes installed-mod bonuses; other modes use template AP.

    // Three lines for point-blank, optimal, and maximum range brackets with %-to-hit modifiers; mirrors WeaponRangeTooltip.SetWeapon.
    internal static List<string> BuildRangeBracketLines(ItemInstance_WeaponRanged weapon, PC pc)  (line 854)
        // note: applies point-blank trait hooks via OnPCCalculateWeaponPointBlankChanceToHit when a PC is supplied.

    // Operational per-weapon stats: AP to attack, AP to reload (ranged), chance-to-jam (ranged), and jammed flag; includes mod bonuses.
    internal static List<string> BuildWeaponOperationalLines(ItemInstance_Weapon weapon)  (line 894)

    // Line showing the PC's reserve ammo for this weapon's caliber (sum across inventory), or null.
    internal static string BuildReserveAmmoLine(ItemInstance_WeaponRanged weapon, PC pc)  (line 923)

    // Vendor-aware item value; applies 30% sell rate and barter skill adjustment when a vendor screen is open.
    internal static int ComputeItemValue(ItemInstance item)  (line 937)
        // note: returns quantity directly for ItemInstance_Currency; applies barter adjustment only when GUIManager.IsVendorScreenOpen.

    internal static string FormatStatModifier(string key, int value)  (line 958)
        // note: combatSpeed is displayed as a decimal fraction (value / 100); negative-is-good stats are flagged with "(penalty)".

    private static string GetStatDisplayName(string key)  (line 989)
        // note: tries PCStatsManager.GetCharacteristic first, then PrintableNameHelper.PrintNames; returns raw key as last resort.
