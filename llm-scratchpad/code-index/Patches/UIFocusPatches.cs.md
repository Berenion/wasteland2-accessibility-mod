File: Patches/UIFocusPatches.cs — Harmony patches for UI focus changes via UICamera.SetSelection and UICamera.Notify; acts as a catch-all for focus events not handled by active InputRouter states.

namespace Wasteland2AccessibilityMod.Patches  (line 6)

// Static shared state and focus-change dispatcher; skips when any InputRouter state or menu is active.
public static class UIFocusPatches  (line 11)
    private static string lastSpokenText  (line 12)
    private static string lastSource  (line 13)

    // When true, focus patches will not announce selection changes (set by menu states).
    public static bool SuppressAnnouncements { get; set; }  (line 19)

    // Handles focus change events from UI hooks; filters non-interactive elements and deduplicates.
    internal static void HandleFocusChange(GameObject go, string source)  (line 24)
        // note: skips when SuppressAnnouncements is set, any state is active, any menu is active, or conversation is on.

// Postfix on UICamera.SetSelection to detect focus changes.
[HarmonyPatch(typeof(UICamera), "SetSelection")]
class UICamera_SetSelection_Patch  (line 81)
    [HarmonyPostfix]
    public static void Postfix(GameObject go)  (line 84)
        // note: delegates to UIFocusPatches.HandleFocusChange with source "SetSelection".

// Postfix on UICamera.Notify to detect OnSelect(true) focus events.
[HarmonyPatch(typeof(UICamera), "Notify")]
class UICamera_Notify_Patch  (line 96)
    [HarmonyPostfix]
    public static void Postfix(GameObject go, string funcName, object obj)  (line 99)
        // note: only handles funcName=="OnSelect" with obj==true; delegates to HandleFocusChange with source "Notify".
