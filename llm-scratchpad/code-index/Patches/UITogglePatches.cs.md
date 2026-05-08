File: Patches/UITogglePatches.cs — Patches UIToggle.Set to announce checkbox/toggle state changes when the toggle is the currently focused UI object.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Announces "LabelText: On/Off" when a UIToggle changes state and is the selected object; skips during menu navigation.
[HarmonyPatch(typeof(UIToggle), "Set")]
class UIToggle_Set_Patch  (line 11)
    [HarmonyPostfix]
    public static void Postfix(UIToggle __instance, bool state)  (line 14)
        // note: skips when any menu is active; reads label from child UILabel or falls back to GameObject name.
