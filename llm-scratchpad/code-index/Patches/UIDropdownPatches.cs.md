File: Patches/UIDropdownPatches.cs — Announces dropdown menu options as they are highlighted by patching UIPopupList.Highlight.

namespace Wasteland2AccessibilityMod.Patches  (line 3)

// Announces the highlighted label text when a UIPopupList item is highlighted.
[HarmonyPatch(typeof(UIPopupList), "Highlight")]
class UIPopupList_Highlight_Patch  (line 10)
    [HarmonyPostfix]
    public static void Postfix(UILabel lbl)  (line 13)
        // note: cleans NGUI formatting before speaking; skips null/empty labels.
