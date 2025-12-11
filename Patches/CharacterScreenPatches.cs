using HarmonyLib;
using UnityEngine;
using System;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for character screen accessibility
    /// Provides screen reader announcements for character stats, attributes, and derived values
    /// </summary>
    public static class CharacterScreenPatches
    {
        internal static string lastAnnouncedStat = "";

        /// <summary>
        /// Formats a stat name for better readability
        /// </summary>
        internal static string FormatStatName(string statName)
        {
            if (string.IsNullOrEmpty(statName))
                return "";

            // Clean up the stat name
            statName = UITextExtractor.CleanText(statName);

            // Replace common abbreviations
            statName = statName.Replace("AP", "Action Points")
                               .Replace("CON", "Constitution")
                               .Replace("HP", "Hit Points");

            return statName;
        }
    }

    /// <summary>
    /// Patch for CHA_AttributePanel - announces attribute points remaining
    /// </summary>
    [HarmonyPatch(typeof(CHA_AttributePanel), "PopulateData", new Type[] { typeof(PC) })]
    public class CHA_AttributePanel_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_AttributePanel __instance, PC newPC)
        {
            if (newPC != null && __instance.pointsRemainingLabel != null)
            {
                string pointsText = __instance.pointsRemainingLabel.text;
                if (!string.IsNullOrEmpty(pointsText))
                {
                    pointsText = UITextExtractor.CleanText(pointsText);
                    string announcement = $"{pointsText} attribute points available";
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for CHA_StatDisplayPanel.PopulateData - announces derived stats when panel is updated
    /// </summary>
    [HarmonyPatch(typeof(CHA_StatDisplayPanel), "PopulateData")]
    public class CHA_StatDisplayPanel_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_StatDisplayPanel __instance, PC pc)
        {
            if (pc == null) return;

            // Only announce if focus is on this panel or just opened
            if (UICamera.selectedObject != null && UICamera.selectedObject.transform.IsChildOf(__instance.transform))
            {
                // Announce one key stat as context
                if (__instance.apLabel != null)
                {
                    string apText = UITextExtractor.CleanText(__instance.apLabel.text);
                    string announcement = $"Action Points {apText}";
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for CHA_StatDisplay.SetValue - announces stat changes
    /// </summary>
    [HarmonyPatch(typeof(CHA_StatDisplay), "SetValue")]
    public class CHA_StatDisplay_SetValue_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_StatDisplay __instance, int newValue)
        {
            // Only announce if this stat is currently selected
            if (UICamera.selectedObject == __instance.gameObject && __instance.nameLabel != null && __instance.valueLabel != null)
            {
                string statName = UITextExtractor.CleanText(__instance.nameLabel.text);
                string statValue = UITextExtractor.CleanText(__instance.valueLabel.text);

                statName = CharacterScreenPatches.FormatStatName(statName);

                string announcement = $"{statName} {statValue}";

                // Avoid repeating the same announcement
                if (announcement != CharacterScreenPatches.lastAnnouncedStat)
                {
                    CharacterScreenPatches.lastAnnouncedStat = announcement;
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for CHA_StatDisplay.SetSelected - announces when a stat is focused
    /// </summary>
    [HarmonyPatch(typeof(CHA_StatDisplay), "SetSelected")]
    public class CHA_StatDisplay_SetSelected_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_StatDisplay __instance, bool selected)
        {
            if (selected && __instance.nameLabel != null && __instance.valueLabel != null)
            {
                string statName = UITextExtractor.CleanText(__instance.nameLabel.text);
                string statValue = UITextExtractor.CleanText(__instance.valueLabel.text);

                statName = CharacterScreenPatches.FormatStatName(statName);

                string announcement = $"{statName} {statValue}";

                if (announcement != CharacterScreenPatches.lastAnnouncedStat)
                {
                    CharacterScreenPatches.lastAnnouncedStat = announcement;
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for CharacterScreen panel switching - announces which panel is active
    /// </summary>
    [HarmonyPatch(typeof(CharacterScreen), "ShowPanel", new Type[] { typeof(CharacterScreen.EditorPanel), typeof(bool) })]
    public class CharacterScreen_ShowPanel_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterScreen __instance, CharacterScreen.EditorPanel panel, bool animate)
        {
            // Announce panel name
            string panelName = panel.ToString();

            // Make it more readable
            panelName = panelName.Replace("AddCharacter", "Add Character")
                                 .Replace("UseDefaultParty", "Use Default Party");

            string announcement = $"{panelName} panel";
            ScreenReaderManager.Speak(announcement, interrupt: false);
        }
    }

    /// <summary>
    /// Patch for skill panel to announce skill points
    /// </summary>
    [HarmonyPatch(typeof(CHA_SkillPanel), "PopulateData")]
    public class CHA_SkillPanel_PopulateData_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_SkillPanel __instance, PC newPC)
        {
            if (newPC != null && __instance.pointsRemainingLabel != null)
            {
                string pointsText = __instance.pointsRemainingLabel.text;
                if (!string.IsNullOrEmpty(pointsText))
                {
                    pointsText = UITextExtractor.CleanText(pointsText);
                    string announcement = $"{pointsText} skill points available";
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for attribute editor value changes - announces attribute increases/decreases
    /// </summary>
    [HarmonyPatch(typeof(CHA_AttributeEditor), "SetCurrentValue")]
    public class CHA_AttributeEditor_SetCurrentValue_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_AttributeEditor __instance, int newValue)
        {
            // Only announce if this editor is selected
            if (UICamera.selectedObject != null && UICamera.selectedObject.transform.IsChildOf(__instance.transform))
            {
                if (__instance.nameLabel != null)
                {
                    string attributeName = UITextExtractor.CleanText(__instance.nameLabel.text);
                    string announcement = $"{attributeName} {newValue}";
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }

    /// <summary>
    /// Patch for skill editor value changes - announces skill level increases/decreases
    /// </summary>
    [HarmonyPatch(typeof(CHA_SkillEditor), "SetCurrentValue")]
    public class CHA_SkillEditor_SetCurrentValue_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_SkillEditor __instance, int newValue)
        {
            // Only announce if this editor is selected
            if (UICamera.selectedObject != null && UICamera.selectedObject.transform.IsChildOf(__instance.transform))
            {
                if (__instance.nameLabel != null)
                {
                    string skillName = UITextExtractor.CleanText(__instance.nameLabel.text);
                    string announcement = $"{skillName} level {newValue}";
                    ScreenReaderManager.Speak(announcement, interrupt: false);
                }
            }
        }
    }
}
