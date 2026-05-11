namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Interface for all accessibility states that can receive keyboard input.
    /// States are checked in priority order by the InputRouter.
    /// Only the highest-priority active state receives input each frame.
    /// </summary>
    public interface IAccessibilityState
    {
        /// <summary>
        /// Display name for logging and debugging.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Priority for input routing. Higher values are checked first.
        /// Scanner=80, Dialog=70, MainMenu=60, Keypad=58, GenericMenu=55,
        /// CharacterInfo=54, Character=53, Conversation=52, Inventory=51, Shop=50,
        /// Combat=45, MapCursor=30, WorldMap=20, Exploration=10.
        /// The 50-54 cluster is mutually exclusive in practice via IsActive;
        /// values are spread (alphabetical descending) because List&lt;T&gt;.Sort
        /// is not stable, so identical priorities can reorder between runs.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Whether this state should currently receive input.
        /// Checked every frame by InputRouter before calling HandleInput.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Process keyboard input for this frame.
        /// Return true if input was consumed (prevents lower-priority states from processing).
        /// </summary>
        bool HandleInput();

        /// <summary>
        /// Called when this state transitions from inactive to active.
        /// Use for initial announcements or state setup.
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called when this state transitions from active to inactive.
        /// Use for cleanup.
        /// </summary>
        void OnDeactivated();
    }
}
