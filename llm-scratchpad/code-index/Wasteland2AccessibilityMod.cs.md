File: Wasteland2AccessibilityMod.cs — main mod entry point; initializes subsystems and registers all IAccessibilityState instances with InputRouter

namespace Wasteland2AccessibilityMod  (line 8)

// Main mod class; provides screen reader support, keyboard navigation, and virtual map cursor
class AccessibilityMod : MelonMod  (line 14)  [public]

    public override void OnInitializeMelon()  (line 16)
        // note: initializes ScreenReaderManager, AudioAwareAnnouncementManager, ModConfig, CameraLock; registers all states with InputRouter in priority order; logs keybindings to console

    public override void OnLateInitializeMelon()  (line 82)
        // note: Harmony patches applied here by MelonLoader; calls CameraLock.ResetToNorth() to capture initial camera rotation

    public override void OnUpdate()  (line 90)
        // note: runs InputRouter.ProcessInput() BEFORE game Update; updates AudioAwareAnnouncementManager and FOWHelper.Tick() each frame

    public override void OnDeinitializeMelon()  (line 102)

// State registrations (from OnInitializeMelon):
// Priority 80 — ScannerState
// Priority 70 — DialogState
// Priority 60 — MainMenuState
// Priority 58 — KeypadState
// Priority 55 — GenericMenuState
// Priority 50 — ConversationState, InventoryState, ShopState, CharacterState, CharacterInfoState
// Priority 45 — CombatState
// Priority 30 — MapCursorState
// Priority 20 — WorldMapState
// Priority 10 — ExplorationState
