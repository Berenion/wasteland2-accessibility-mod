File: Patches/UISliderPatches.cs — Patches UIProgressBar.value setter to announce slider value changes with debouncing; skips scrollbars and non-focused sliders.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Announces slider value when changed; debounces continuous sliders (300 ms) and announces every step for stepped sliders.
[HarmonyPatch(typeof(UIProgressBar), "value", MethodType.Setter)]
class UIProgressBar_SetValue_Patch  (line 11)
    private static UIProgressBar lastSlider  (line 14)
    private static float lastSliderValue  (line 15)
    private static float lastSliderAnnounceTime  (line 16)
    private const float SLIDER_DEBOUNCE_TIME  (line 17)

    [HarmonyPostfix]
    public static void Postfix(UIProgressBar __instance)  (line 20)
        // note: skips UIScrollBar instances; only fires when selectedObject matches the slider's gameObject; formats stepped sliders as "N of M", continuous as "N percent".
