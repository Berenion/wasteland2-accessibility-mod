File: Patches/KeypadMenuPatches.cs — Suppresses KeypadMenu's native OnGUI input handling while KeypadState is active to prevent double-firing of numpad and Enter key events.

namespace Wasteland2AccessibilityMod.Patches  (line 3)

// Returns false (suppresses original) for KeyDown events when KeypadState is active, preventing double input.
[HarmonyPatch(typeof(KeypadMenu), "OnGUI")]
class KeypadMenu_OnGUI_Suppressor  (line 15)
    [HarmonyPrefix]
    public static bool Prefix()  (line 17)
        // note: only suppresses EventType.KeyDown; passes through all other event types.
