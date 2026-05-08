File: Patches/TutorialPatches.cs — Harmony patches for TUT_TutorialPopup: announces tutorial content on open, handles checkbox toggle announcements, and (prefix) determines whether close or next-page should be announced.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Announces tutorial title, message, and navigation hint ("next page" or "close") when popup is populated.
[HarmonyPatch(typeof(TUT_TutorialPopup), "PopulateData")]
class TUT_TutorialPopup_PopulateData_Patch  (line 16)
    [HarmonyPostfix]
    public static void Postfix(TUT_TutorialPopup __instance, TutorialEntryTemplate template)  (line 19)
        // note: appends "Press Enter or A button for next page" vs "to close" based on template.nextPageName.

// Announces "Tutorial closed" when okay is clicked unless a valid next page exists (in which case returns silently).
[HarmonyPatch(typeof(TUT_TutorialPopup), "OnOkayClicked")]
class TUT_TutorialPopup_OnOkayClicked_Patch  (line 80)
    [HarmonyPrefix]
    public static void Prefix(TUT_TutorialPopup __instance)  (line 83)
        // note: checks nextPageName and checkbox.value; only speaks "Tutorial closed" when truly closing.

// Announces "Tutorials disabled" or "Tutorials enabled" when the disable-tutorials checkbox is toggled.
[HarmonyPatch(typeof(TUT_TutorialPopup), "OnCheckboxToggled")]
class TUT_TutorialPopup_OnCheckboxToggled_Patch  (line 116)
    [HarmonyPostfix]
    public static void Postfix(TUT_TutorialPopup __instance)  (line 119)
