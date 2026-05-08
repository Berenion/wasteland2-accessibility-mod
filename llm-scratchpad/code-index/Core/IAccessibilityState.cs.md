File: Core/IAccessibilityState.cs — contract for all priority-ordered accessibility input states.

namespace Wasteland2AccessibilityMod.Core  (line 1)

interface IAccessibilityState  (line 8)
    // Contract for all states registered with InputRouter; checked in priority order each frame.

    // Display name for logging and debugging.
    string Name { get; }  (line 13)

    // Priority for input routing. Higher values checked first.
    // Scanner=80, Dialog=70, Conversation/Inventory/Character=50, MapCursor=30, Exploration=10
    int Priority { get; }  (line 19)

    // Whether this state should currently receive input; checked every frame before HandleInput.
    bool IsActive { get; }  (line 25)

    // Process keyboard input; return true to consume input and block lower-priority states.
    bool HandleInput()  (line 31)

    // Called when this state transitions from inactive to active; use for initial announcements.
    void OnActivated()  (line 37)

    // Called when this state transitions from active to inactive; use for cleanup.
    void OnDeactivated()  (line 43)
