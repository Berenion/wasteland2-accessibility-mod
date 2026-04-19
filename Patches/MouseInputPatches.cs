using HarmonyLib;

namespace Wasteland2AccessibilityMod.Patches
{
    // Blocks mouse-hover-driven side effects on 3D world objects. The accessibility
    // mod navigates with a keyboard-driven grid cursor, so the physical mouse
    // position is irrelevant. Letting the vanilla code run causes Highlight.MouseOver
    // to force-set UseASIManager.ActiveASIName to "attack" every frame the mouse
    // cursor happens to hover a hostile (see Highlight.cs lines ~447-551), which
    // makes the mod's Escape cancel in MapCursorState announce "Free aim cancelled"
    // repeatedly without ever clearing the mode.
    [HarmonyPatch(typeof(Highlight), "MouseOver")]
    public class Highlight_MouseOver_Block
    {
        [HarmonyPrefix]
        public static bool Prefix() => false;
    }
}
