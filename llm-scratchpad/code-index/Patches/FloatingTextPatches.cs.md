File: Patches/FloatingTextPatches.cs — Patches Targetable.PrintFloatingText to announce combat floating-text events (weapon stolen, ambush, trait procs, etc.) that are otherwise visual-only.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Reads floating text aloud with character name context; adds to CombatLog buffer.
[HarmonyPatch(typeof(Targetable), "PrintFloatingText", new[] { typeof(string), typeof(Color), typeof(Texture2D), typeof(bool) })]
class Targetable_PrintFloatingText_Patch  (line 14)
    [HarmonyPostfix]
    public static void Postfix(Targetable __instance, string text)  (line 17)
        // note: localizes mob template displayName for context; uses Speak (non-interrupting) to avoid cutting off combat descriptions.
