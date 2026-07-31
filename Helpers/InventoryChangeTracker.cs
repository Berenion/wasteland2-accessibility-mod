namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// A monotonic version stamp bumped whenever any on-screen inventory grid reconciles
    /// itself with its underlying inventory.
    ///
    /// The mod's inventory-facing states cache a list of INV_DragDropItem objects and used
    /// to mark it stale only at their OWN action sites (transfer, equip, drop, party switch).
    /// Anything that changed an inventory by another route left that cache wrong: a script
    /// granting or taking a quest item, a consumable used from elsewhere, ammo spent, an
    /// item auto-stacking, or a plain mouse drag alongside keyboard navigation. The readout
    /// then described items that were no longer there — or missed ones that were.
    ///
    /// Rather than enumerate every mutation site, this hangs off the game's own
    /// reconciliation point. InventoryContainer subscribes to EventInfo_InventoryModified
    /// and EventInfo_WeaponChanged (InventoryContainer.cs:65) and merely sets
    /// shouldCheckConsistency; Update() then calls ConsistencyCheck(), which rebuilds the
    /// grid's item objects and clears the flag (InventoryContainer.cs:823). That method is
    /// therefore the single place where "the grid the mod is reading has changed" becomes
    /// true, and it runs once per change rather than per frame.
    ///
    /// States compare <see cref="Version"/> against their own stored copy each frame and
    /// rebuild when it moves. A counter rather than an event keeps this compatible with the
    /// polling architecture and avoids subscribe/unsubscribe lifetime bugs.
    /// </summary>
    internal static class InventoryChangeTracker
    {
        /// <summary>Increments on every reconciliation. Wraps harmlessly — states only test equality.</summary>
        public static int Version { get; private set; }

        public static void Bump()
        {
            unchecked { Version++; }
        }

        /// <summary>
        /// True when <paramref name="seen"/> is behind, updating it to the current version.
        /// Callers pass their own stored stamp by ref: `if (InventoryChangeTracker.HasChanged(ref myStamp)) isDirty = true;`
        /// </summary>
        public static bool HasChanged(ref int seen)
        {
            if (seen == Version) return false;
            seen = Version;
            return true;
        }
    }
}
