using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for reading diary/letter/note content aloud when opened.
    /// BeekersDiaryMenu is the GUIScreen used for all readable items (letters, notes, diaries).
    /// </summary>
    [HarmonyPatch(typeof(BeekersDiaryMenu), "PopulateData")]
    public class BeekersDiaryMenu_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BeekersDiaryMenu __instance)
        {
            try
            {
                string title = __instance.titleLabel != null ? __instance.titleLabel.text : null;
                string entry = __instance.entryLabel != null ? __instance.entryLabel.text : null;

                title = UITextExtractor.CleanText(title);
                entry = UITextExtractor.CleanText(entry);

                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(entry))
                    return;

                string announcement = "";
                if (!string.IsNullOrEmpty(title))
                    announcement = title;

                if (!string.IsNullOrEmpty(entry))
                {
                    if (announcement.Length > 0)
                        announcement += ". ";
                    announcement += entry;
                }

                MelonLogger.Msg($"[DiaryPatches] Diary opened: {title}");
                ScreenReaderManager.SpeakInterrupt(announcement);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in BeekersDiaryMenu_PopulateData_Patch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Announces page content when navigating multi-page diaries.
    /// </summary>
    [HarmonyPatch(typeof(BeekersDiaryMenu), "SetPage")]
    public class BeekersDiaryMenu_SetPage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(BeekersDiaryMenu __instance)
        {
            try
            {
                string entry = __instance.entryLabel != null ? __instance.entryLabel.text : null;
                entry = UITextExtractor.CleanText(entry);

                if (string.IsNullOrEmpty(entry))
                    return;

                MelonLogger.Msg("[DiaryPatches] Diary page changed");
                ScreenReaderManager.SpeakInterrupt(entry);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in BeekersDiaryMenu_SetPage_Patch: {ex.Message}");
            }
        }
    }
}
