using MelonLoader;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Convenience base for IAccessibilityState implementations. Provides
    /// uniform "[ClassName] Activated" / "[ClassName] Deactivated" lifecycle
    /// logging so individual states don't repeat that boilerplate and can't
    /// forget the deactivation half (the previous mix had some states logging
    /// only activation, some only deactivation, some neither).
    ///
    /// Subclasses override Name, Priority, IsActive, and HandleInput.
    /// They can override OnActivated / OnDeactivated for setup or cleanup
    /// work — call base.OnActivated() / base.OnDeactivated() at the end
    /// so the lifecycle log line still fires.
    /// </summary>
    public abstract class AccessibilityStateBase : IAccessibilityState
    {
        public abstract string Name { get; }
        public abstract int Priority { get; }
        public abstract bool IsActive { get; }
        public abstract bool HandleInput();

        public virtual void OnActivated()
        {
            ModLog.Debug($"[{GetType().Name}] Activated");
        }

        public virtual void OnDeactivated()
        {
            ModLog.Debug($"[{GetType().Name}] Deactivated");
        }

        /// <summary>
        /// Default control help. States override this with context-specific keys.
        /// The baseline keys listed here hold in nearly every list and menu.
        /// </summary>
        public virtual string GetHelpText()
        {
            return "Baseline keys: Up and Down move between items, Enter activates or confirms, " +
                   "Escape closes or goes back, Home and End jump to first and last, " +
                   "backslash repeats the last announcement.";
        }
    }
}
