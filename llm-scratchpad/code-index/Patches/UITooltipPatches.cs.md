File: Patches/UITooltipPatches.cs — Harmony patches to announce tooltip content from UITooltip.SetText, TextTooltip.SetText, and TooltipManager.SetPopup; with deduplication to prevent repeated announcements.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Shared deduplication state for tooltip announcements.
public static class UITooltipPatches  (line 11)
    internal static string lastTooltipText  (line 13)
    internal static float lastTooltipTime  (line 14)
    internal const float TOOLTIP_DEBOUNCE_TIME  (line 15)

// Announces UITooltip text when SetText is called.
[HarmonyPatch(typeof(UITooltip), "SetText")]
class UITooltip_SetText_Patch  (line 23)
    [HarmonyPostfix]
    public static void Postfix(UITooltip __instance, string tooltipText)  (line 26)
        // note: uses Speak (non-interrupting) since tooltips are informational.

// Announces TextTooltip text when SetText is called.
[HarmonyPatch(typeof(TextTooltip), "SetText")]
class TextTooltip_SetText_Patch  (line 47)
    [HarmonyPostfix]
    public static void Postfix(TextTooltip __instance, string text)  (line 50)

// Announces tooltip text when TooltipManager.SetPopup activates a popup (catches TooltipCreator-based tooltips like difficulty selection).
[HarmonyPatch(typeof(TooltipManager), "SetPopup")]
class TooltipManager_SetPopup_Patch  (line 72)
    [HarmonyPostfix]
    public static void Postfix(GameObject popup)  (line 75)
        // note: tries TextTooltip.label then ItemInfoTooltip.nameLabel; deduplicates via UITooltipPatches shared state.
