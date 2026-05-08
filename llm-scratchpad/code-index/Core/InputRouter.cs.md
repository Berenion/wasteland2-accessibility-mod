File: Core/InputRouter.cs — central state-priority input router; called each frame from OnUpdate before Unity's Update.

namespace Wasteland2AccessibilityMod.Core  (line 5)

static class InputRouter  (line 12)
    // Static registry of IAccessibilityState; routes keyboard input by priority descending; fires OnActivated/OnDeactivated callbacks.

    private static readonly List<IAccessibilityState> states  (line 15)
    public static bool InputConsumedThisFrame { get; private set; }  (line 18)
    private static readonly HashSet<KeyCode> consumedKeys  (line 21)
    private static readonly Dictionary<IAccessibilityState, bool> previousActiveState  (line 24)

    // Registers a state and re-sorts the list by priority descending.
    public static void Register(IAccessibilityState state)  (line 31)
        // Sorts by priority descending after each registration.

    // Called every frame from OnUpdate; walks states top-down, fires activation callbacks, and lets the first active state that returns true from HandleInput consume input.
    public static void ProcessInput()  (line 46)
        // note: continues iterating even after input is consumed so all states receive activation/deactivation callbacks; also gates all state activity when CutsceneDetector.IsActive.

    // Mark a specific key as consumed this frame for per-key suppression checks.
    public static void MarkKeyConsumed(KeyCode key)  (line 94)

    // Check if a specific key was consumed by an accessibility state this frame.
    public static bool WasKeyConsumed(KeyCode key)  (line 102)

    // Check if any accessibility state is currently active; used by UIFocusPatches to suppress legacy announcements.
    public static bool IsAnyStateActive()  (line 112)

    // Check if any state with Priority >= 30 is active; used by InputSuppressor to decide whether to block game input.
    public static bool IsAnyMenuStateActive()  (line 126)
        // note: gated on Priority >= 30; used by InputSuppressor.
