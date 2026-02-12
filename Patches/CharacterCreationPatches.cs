using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Wasteland2AccessibilityMod.Patches
{
    // CHA_UsePremadePartyPanel_OnEnable_Patch removed - CharacterState handles panel announcements

    /// <summary>
    /// Harmony patch to announce difficulty selection descriptions
    /// Patches: public void SelectDifficulty(int difficultyLevel)
    /// </summary>
    [HarmonyPatch(typeof(DifficultySelectionMenu), "SelectDifficulty")]
    public class DifficultySelectionMenu_SelectDifficulty_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(DifficultySelectionMenu __instance, int difficultyLevel)
        {
            if (__instance == null || __instance.descriptionLabel == null) return;

            // Get the description text that was just set
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(description))
            {
                string cleanedText = UITextExtractor.CleanText(description);
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    // Announce the difficulty name and description
                    string difficultyName = difficultyLevel switch
                    {
                        0 => "Rookie",
                        1 => "Seasoned",
                        2 => "Ranger",
                        3 => "Legend",
                        _ => "Unknown"
                    };

                    string announcement = $"{difficultyName}, {difficultyLevel + 1} of 4. {cleanedText}";
                    ScreenReaderManager.SpeakInterrupt(announcement);
                }
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce skill descriptions in character creation/sheet
    /// Patches: public void SetSkill(string skillName, bool unknown = false, int level = -1)
    /// </summary>
    [HarmonyPatch(typeof(CHA_DescriptionPanel), "SetSkill")]
    public class CHA_DescriptionPanel_SetSkill_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_DescriptionPanel __instance, int level)
        {
            if (__instance == null || __instance.nameLabel == null || __instance.descriptionLabel == null) return;

            string name = __instance.nameLabel.text;
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(name))
            {
                string cleanedName = UITextExtractor.CleanText(name);
                string cleanedDesc = UITextExtractor.CleanText(description);

                // Build announcement with skill name and level (if provided)
                string announcement = cleanedName;

                // Add level if it was passed (not -1)
                if (level >= 0)
                {
                    announcement += $", Level {level}";
                }

                // Add cost information if reasonLabel exists and has text
                if (__instance.reasonLabel != null && !string.IsNullOrEmpty(__instance.reasonLabel.text))
                {
                    string costText = UITextExtractor.CleanText(__instance.reasonLabel.text);
                    if (!string.IsNullOrEmpty(costText))
                    {
                        announcement += ". " + costText;
                    }
                }

                // Add full description
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    announcement += ". " + cleanedDesc;
                }

                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce attribute descriptions in character creation/sheet
    /// Patches: public void SetAttribute(string attributeName, int level = -1)
    /// </summary>
    [HarmonyPatch(typeof(CHA_DescriptionPanel), "SetAttribute")]
    public class CHA_DescriptionPanel_SetAttribute_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_DescriptionPanel __instance, int level)
        {
            if (__instance == null || __instance.nameLabel == null || __instance.descriptionLabel == null) return;

            string name = __instance.nameLabel.text;
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(name))
            {
                string cleanedName = UITextExtractor.CleanText(name);
                string cleanedDesc = UITextExtractor.CleanText(description);

                // Build announcement with attribute name and level (if provided)
                string announcement = cleanedName;

                // Add level if it was passed (not -1)
                if (level >= 0)
                {
                    announcement += $", Level {level}";
                }

                // Add full description
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    announcement += ". " + cleanedDesc;
                }

                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce trait/quirk descriptions in character creation/sheet
    /// Patches: public void SetTrait(Trait trait, PC player, CHA_TraitEditor.TraitAvailability availability = CHA_TraitEditor.TraitAvailability.Available)
    /// </summary>
    [HarmonyPatch(typeof(CHA_DescriptionPanel), "SetTrait")]
    public class CHA_DescriptionPanel_SetTrait_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_DescriptionPanel __instance)
        {
            if (__instance == null || __instance.nameLabel == null || __instance.descriptionLabel == null) return;

            string name = __instance.nameLabel.text;
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(name))
            {
                string cleanedName = UITextExtractor.CleanText(name);
                string cleanedDesc = UITextExtractor.CleanText(description);

                // Build full announcement with trait name, description, and effects
                string announcement = cleanedName;

                // Add full main description
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    announcement += ". " + cleanedDesc;
                }

                // Add effects/bonus information if bonusLabel exists
                if (__instance.bonusLabel != null && !string.IsNullOrEmpty(__instance.bonusLabel.text))
                {
                    string bonusText = UITextExtractor.CleanText(__instance.bonusLabel.text);
                    if (!string.IsNullOrEmpty(bonusText))
                    {
                        announcement += ". " + bonusText;
                    }
                }

                // Add cost information if reasonLabel exists
                if (__instance.reasonLabel != null && !string.IsNullOrEmpty(__instance.reasonLabel.text))
                {
                    string costText = UITextExtractor.CleanText(__instance.reasonLabel.text);
                    if (!string.IsNullOrEmpty(costText))
                    {
                        announcement += ". " + costText;
                    }
                }

                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce skill levels when skills are selected in character creation
    /// Patches: public void OnSelect(bool isSelected)
    /// </summary>
    [HarmonyPatch(typeof(CHA_SkillEditor), "OnSelect")]
    public class CHA_SkillEditor_OnSelect_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_SkillEditor __instance, bool isSelected)
        {
            if (!isSelected || __instance == null) return;

            // Get skill name and level
            string skillName = "";
            string skillLevel = "";

            if (__instance.nameLabel != null)
            {
                skillName = UITextExtractor.CleanText(__instance.nameLabel.text);
            }

            if (__instance.levelLabel != null)
            {
                skillLevel = __instance.levelLabel.text;
            }

            if (!string.IsNullOrEmpty(skillName))
            {
                string announcement = skillName;
                if (!string.IsNullOrEmpty(skillLevel))
                {
                    announcement += $", Level {skillLevel}";
                }

                ScreenReaderManager.Speak(announcement);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce attribute values when attributes are selected
    /// Patches: private void OnSelect(bool isSelected)
    /// </summary>
    [HarmonyPatch(typeof(CHA_AttributeEditor), "OnSelect")]
    public class CHA_AttributeEditor_OnSelect_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_AttributeEditor __instance, bool isSelected)
        {
            if (!isSelected || __instance == null) return;

            // Get attribute name, value, and description (rating)
            string attributeName = "";
            string attributeValue = "";
            string attributeRating = "";

            if (__instance.nameLabel != null)
            {
                attributeName = UITextExtractor.CleanText(__instance.nameLabel.text);
            }

            if (__instance.valueLabel != null)
            {
                attributeValue = __instance.valueLabel.text;
            }

            if (__instance.descriptionLabel != null)
            {
                attributeRating = UITextExtractor.CleanText(__instance.descriptionLabel.text);
            }

            if (!string.IsNullOrEmpty(attributeName))
            {
                string announcement = attributeName;

                if (!string.IsNullOrEmpty(attributeValue))
                {
                    announcement += $", {attributeValue}";
                }

                if (!string.IsNullOrEmpty(attributeRating))
                {
                    announcement += $", {attributeRating}";
                }

                ScreenReaderManager.Speak(announcement);
            }
        }
    }
}
