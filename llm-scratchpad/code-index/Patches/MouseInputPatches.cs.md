File: Patches/MouseInputPatches.cs — Blocks mouse-hover-driven side effects on 3D world objects to prevent Highlight.MouseOver from forcing the active ASI to "attack" mode while the accessibility grid cursor is in use.

namespace Wasteland2AccessibilityMod.Patches  (line 3)

// Unconditionally suppresses Highlight.MouseOver to stop vanilla code from clobbering UseASIManager.ActiveASIName every frame.
[HarmonyPatch(typeof(Highlight), "MouseOver")]
class Highlight_MouseOver_Block  (line 13)
    [HarmonyPrefix]
    public static bool Prefix()  (line 15)
        // note: always returns false — never allows the original to run.
