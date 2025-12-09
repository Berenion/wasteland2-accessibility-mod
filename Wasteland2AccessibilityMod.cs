using MelonLoader;
using HarmonyLib;
using UnityEngine;
using System;
using System.Text.RegularExpressions;

[assembly: MelonInfo(typeof(Wasteland2AccessibilityMod.AccessibilityMod), "Wasteland 2 Accessibility Mod", "1.0.0", "AccessibilityModTeam")]
[assembly: MelonGame("inXile Entertainment", "Wasteland 2 Director's Cut")]

namespace Wasteland2AccessibilityMod
{
    public class AccessibilityMod : MelonMod
    {
        private static Tolk.Tolk screenReader;
        private static string lastSpokenText = "";
        private static string lastSource = "";

        // Slider debouncing - track last slider and time
        internal static UIProgressBar lastSlider = null;
        internal static float lastSliderValue = 0f;
        internal static float lastSliderAnnounceTime = 0f;
        internal const float SLIDER_DEBOUNCE_TIME = 0.3f; // Wait 300ms before announcing

        // Tooltip deduplication - prevent double announcements
        internal static string lastTooltipText = "";
        internal static float lastTooltipTime = 0f;
        internal const float TOOLTIP_DEBOUNCE_TIME = 0.5f; // Prevent same tooltip within 500ms

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Wasteland 2 Accessibility Mod v1.0.0");
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("Features:");
            MelonLogger.Msg("  - Screen reader support for UI navigation");
            MelonLogger.Msg("===========================================");

            // Initialize Tolk screen reader support
            try
            {
                screenReader = new Tolk.Tolk();
                screenReader.Load();
                string detectedReader = screenReader.DetectScreenReader();
                if (detectedReader != null)
                {
                    MelonLogger.Msg($"Screen reader detected: {detectedReader}");
                }
                else
                {
                    MelonLogger.Msg("No screen reader detected (Tolk loaded, will use SAPI if available)");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Failed to initialize Tolk: {ex.Message}");
                MelonLogger.Error("Screen reader support will be disabled. Make sure Tolk.dll is in the game directory.");
            }
        }

        public override void OnLateInitializeMelon()
        {
            MelonLogger.Msg("Applying Harmony patches...");
        }

        public override void OnDeinitializeMelon()
        {
            if (screenReader != null && screenReader.IsLoaded())
            {
                MelonLogger.Msg("Unloading Tolk screen reader...");
                screenReader.Unload();
            }
        }

        /// <summary>
        /// Handles focus change events from UI hooks
        /// </summary>
        internal static void HandleFocusChange(GameObject go, string source)
        {
            // Skip non-interactive visual elements (backgrounds, sprites, etc.)
            if (!IsInteractiveElement(go))
            {
                return;
            }

            string text = ExtractUIText(go);

            // Avoid double-speaking if both hooks fire for same element
            if (text == lastSpokenText && source != lastSource)
            {
                return;
            }

            lastSpokenText = text;
            lastSource = source;

            if (!string.IsNullOrEmpty(text))
            {
                SpeakText(text);
            }
        }

        /// <summary>
        /// Internal method to check if we should log debug messages
        /// </summary>
        private static bool ShouldLog()
        {
            // Only log important events, not every single announcement
            return false; // Set to true for debugging
        }

        /// <summary>
        /// Checks if a GameObject is an interactive UI element (not just decorative)
        /// </summary>
        private static bool IsInteractiveElement(GameObject go)
        {
            string name = go.name.ToLower();

            // Skip known non-interactive patterns first (for performance)
            if (name.Contains("background") || name == "label")
            {
                return false;
            }

            // Skip if it's only a UISprite with no interactive components
            if (go.GetComponent<UISprite>() != null &&
                go.GetComponent<UIButton>() == null &&
                go.GetComponent<UIToggle>() == null &&
                go.GetComponent<UIInput>() == null &&
                go.GetComponent<UISlider>() == null &&
                go.GetComponent<UIPopupList>() == null &&
                go.GetComponent<UIButtonKeys>() == null)
            {
                return false;
            }

            // Skip standalone sprite objects
            if (name.Contains("sprite") && !name.Contains("button") && !name.Contains("toggle"))
            {
                return false;
            }

            // Check for interactive components
            if (go.GetComponent<UIButton>() != null) return true;
            if (go.GetComponent<UIToggle>() != null) return true;
            if (go.GetComponent<UISlider>() != null) return true;
            if (go.GetComponent<UIInput>() != null) return true;
            if (go.GetComponent<UIPopupList>() != null) return true;

            // Check if it has UIButtonKeys (keyboard navigation component)
            if (go.GetComponent<UIButtonKeys>() != null) return true;

            // Check if it has a UILabel directly (might be a label-only interactive element)
            UILabel label = go.GetComponent<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                // If it has a label AND interactive children, it's likely interactive
                if (go.GetComponentInChildren<UIButton>() != null ||
                    go.GetComponentInChildren<UIToggle>() != null)
                {
                    return true;
                }
            }

            // Check for specific naming patterns that indicate interactive elements
            if (name.Contains("button") || name.Contains("toggle") || name.Contains("slider") ||
                name.Contains("input") || name.Contains("dropdown") || name.Contains("container"))
            {
                return true;
            }

            // Default: if it has a UILabel child, assume it might be interactive
            return go.GetComponentInChildren<UILabel>() != null;
        }

        /// <summary>
        /// Extracts text from a UI GameObject with context-aware type detection
        /// </summary>
        private static string ExtractUIText(GameObject go)
        {
            string labelText = "";
            string elementType = "";

            // Try to get UILabel directly on the object
            UILabel label = go.GetComponent<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = CleanText(label.text);
            }
            else
            {
                // Try to get UILabel from children
                label = go.GetComponentInChildren<UILabel>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    labelText = CleanText(label.text);
                }
            }

            // Context-aware type detection - only announce for non-obvious elements
            if (go.GetComponent<UISlider>() != null)
            {
                elementType = "Slider";
            }
            else if (go.GetComponent<UIToggle>() != null)
            {
                UIToggle toggle = go.GetComponent<UIToggle>();
                elementType = toggle.value ? "Checked" : "Unchecked";
            }
            else if (go.GetComponent<UIInput>() != null)
            {
                elementType = "Text Field";
            }
            else if (go.GetComponent<UIPopupList>() != null)
            {
                elementType = "Dropdown";
            }
            // Skip type for UIButton - it's obvious from context

            // Build final text
            if (string.IsNullOrEmpty(labelText))
            {
                labelText = go.name; // Fallback to GameObject name
            }

            if (!string.IsNullOrEmpty(elementType))
            {
                return $"{labelText}, {elementType}";
            }

            return labelText;
        }

        /// <summary>
        /// Removes NGUI formatting codes and special symbols from text
        /// </summary>
        internal static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Remove NGUI color/formatting codes like [FFFFFF], [-], [b], [/b], etc.
            text = Regex.Replace(text, @"\[/?[\w-]*\]", "");

            // Remove special formatting symbols
            text = text.Replace("<@>", ""); // Localization marker
            text = text.Replace("@", "");   // Other @ symbols
            text = text.Replace("\\n", " "); // Newline markers

            return text.Trim();
        }

        /// <summary>
        /// Sends text to the screen reader
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, interrupts current speech. Use true for focus changes, false for informational updates like tooltips.</param>
        internal static void SpeakText(string text, bool interrupt = true)
        {
            if (screenReader != null && screenReader.IsLoaded())
            {
                screenReader.Speak(text, interrupt: interrupt);

                // Only log if debugging is enabled
                if (ShouldLog())
                {
                    string interruptTag = interrupt ? "[INTERRUPT]" : "[QUEUE]";
                    MelonLogger.Msg($"[TTS] {interruptTag} {text}");
                }
            }
        }
    }

    /// <summary>
    /// Harmony patch to detect UI focus changes via UICamera.SetSelection
    /// Patches: protected static void SetSelection(GameObject go, ControlScheme scheme)
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "SetSelection")]
    public class UICamera_SetSelection_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go)
        {
            if (go == null) return;
            AccessibilityMod.HandleFocusChange(go, "SetSelection");
        }
    }

    /// <summary>
    /// Harmony patch to detect UI focus changes via UICamera.Notify
    /// Patches: public static void Notify(GameObject go, string funcName, object obj)
    /// </summary>
    [HarmonyPatch(typeof(UICamera), "Notify")]
    public class UICamera_Notify_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject go, string funcName, object obj)
        {
            // Only handle OnSelect(true) - when element gains focus
            if (funcName == "OnSelect" && obj is bool selected && selected && go != null)
            {
                AccessibilityMod.HandleFocusChange(go, "Notify");
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce dropdown menu options as they're highlighted
    /// Patches: private void Highlight(UILabel lbl, bool instant)
    /// </summary>
    [HarmonyPatch(typeof(UIPopupList), "Highlight")]
    public class UIPopupList_Highlight_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UILabel lbl)
        {
            if (lbl != null && !string.IsNullOrEmpty(lbl.text))
            {
                string cleanedText = AccessibilityMod.CleanText(lbl.text);
                AccessibilityMod.SpeakText(cleanedText);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce confirmation dialog content when it appears
    /// Patches: public virtual void SetMessage(string title, string msg, string buttonYesLabel, ModalButtonDelegate yesButtonDelegate, string buttonNoLabel, ModalButtonDelegate noButtonDelegate)
    /// </summary>
    [HarmonyPatch(typeof(ModalMessageMenu), "SetMessage", new Type[] {
        typeof(string), typeof(string), typeof(string),
        typeof(ModalMessageMenu.ModalButtonDelegate), typeof(string),
        typeof(ModalMessageMenu.ModalButtonDelegate)
    })]
    public class ModalMessageMenu_SetMessage_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string title, string msg, string buttonYesLabel, string buttonNoLabel)
        {
            // Build the full announcement: title + message + available options
            string announcement = "";

            if (!string.IsNullOrEmpty(title))
            {
                announcement += AccessibilityMod.CleanText(title) + ". ";
            }

            if (!string.IsNullOrEmpty(msg))
            {
                announcement += AccessibilityMod.CleanText(msg) + ". ";
            }

            // Announce available options
            if (!string.IsNullOrEmpty(buttonYesLabel))
            {
                announcement += "Options: " + buttonYesLabel;
                if (!string.IsNullOrEmpty(buttonNoLabel))
                {
                    announcement += " or " + buttonNoLabel;
                }
            }

            AccessibilityMod.SpeakText(announcement);
        }
    }

    /// <summary>
    /// Harmony patch to announce slider value changes
    /// Patches: public float value { get; set; }
    /// </summary>
    [HarmonyPatch(typeof(UIProgressBar), "value", MethodType.Setter)]
    public class UIProgressBar_SetValue_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UIProgressBar __instance)
        {
            if (__instance == null) return;

            // Skip scrollbars - they inherit from UISlider but we don't want to announce scroll position
            if (__instance is UIScrollBar)
            {
                return;
            }

            float currentTime = Time.unscaledTime;
            float sliderValue = __instance.value;

            // Debouncing logic: only announce if this is a different slider or value changed significantly
            bool isDifferentSlider = AccessibilityMod.lastSlider != __instance;
            bool valueChanged = Mathf.Abs(sliderValue - AccessibilityMod.lastSliderValue) > 0.01f;
            bool enoughTimePassed = (currentTime - AccessibilityMod.lastSliderAnnounceTime) >= AccessibilityMod.SLIDER_DEBOUNCE_TIME;

            // For stepped sliders, announce every step change
            bool isSteppedSlider = __instance.numberOfSteps > 1;
            bool stepChanged = false;
            if (isSteppedSlider)
            {
                int currentStep = Mathf.RoundToInt(sliderValue * (__instance.numberOfSteps - 1));
                int lastStep = Mathf.RoundToInt(AccessibilityMod.lastSliderValue * (__instance.numberOfSteps - 1));
                stepChanged = currentStep != lastStep;
            }

            // Only announce if: different slider OR (same slider AND (enough time passed OR step changed))
            if (!isDifferentSlider && !stepChanged && !enoughTimePassed)
            {
                // Update tracking but don't announce yet
                AccessibilityMod.lastSlider = __instance;
                AccessibilityMod.lastSliderValue = sliderValue;
                return;
            }

            // Update tracking
            AccessibilityMod.lastSlider = __instance;
            AccessibilityMod.lastSliderValue = sliderValue;
            AccessibilityMod.lastSliderAnnounceTime = currentTime;

            // Get the slider's GameObject to find its name and label
            GameObject sliderGO = __instance.gameObject;
            string sliderName = sliderGO.name;

            // Try to find a label that describes this slider
            UILabel label = sliderGO.GetComponentInChildren<UILabel>();
            string labelText = "";
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = AccessibilityMod.CleanText(label.text);
            }

            // Format the value as percentage or raw value
            string valueText;

            // Check if this is a stepped slider (discrete values) or continuous
            if (isSteppedSlider)
            {
                // For stepped sliders, announce the step number
                int currentStep = Mathf.RoundToInt(sliderValue * (__instance.numberOfSteps - 1)) + 1;
                valueText = $"{currentStep} of {__instance.numberOfSteps}";
            }
            else
            {
                // For continuous sliders, announce as percentage
                int percentage = Mathf.RoundToInt(sliderValue * 100f);
                valueText = $"{percentage} percent";
            }

            // Build announcement
            string announcement = "";
            if (!string.IsNullOrEmpty(labelText))
            {
                announcement = $"{labelText}: {valueText}";
            }
            else
            {
                announcement = $"{sliderName}: {valueText}";
            }

            AccessibilityMod.SpeakText(announcement);
        }
    }

    /// <summary>
    /// Harmony patch to announce the premade party selection panel
    /// Patches: public void OnEnable()
    /// </summary>
    [HarmonyPatch(typeof(CHA_UsePremadePartyPanel), "OnEnable")]
    public class CHA_UsePremadePartyPanel_OnEnable_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CHA_UsePremadePartyPanel __instance)
        {
            if (__instance == null) return;

            // Find any labels on the panel to announce
            UILabel[] labels = __instance.GetComponentsInChildren<UILabel>();
            string panelText = "";

            foreach (UILabel label in labels)
            {
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    string cleanedText = AccessibilityMod.CleanText(label.text);
                    if (!string.IsNullOrEmpty(cleanedText))
                    {
                        panelText += cleanedText + ". ";
                    }
                }
            }

            // Add instructions for controller users
            string announcement = panelText + "Press A to use default rangers, or press X to create custom party.";

            AccessibilityMod.SpeakText(announcement);
        }
    }

    /// <summary>
    /// Harmony patch to announce toggle (checkbox) state changes
    /// Patches: private void Set(bool state)
    /// </summary>
    [HarmonyPatch(typeof(UIToggle), "Set")]
    public class UIToggle_Set_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UIToggle __instance, bool state)
        {
            if (__instance == null) return;

            // Only announce if this toggle is part of the currently focused UI
            GameObject toggleGO = __instance.gameObject;

            // Get the toggle's label
            UILabel label = toggleGO.GetComponentInChildren<UILabel>();
            string labelText = "";
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = AccessibilityMod.CleanText(label.text);
            }
            else
            {
                // Fallback to GameObject name
                labelText = toggleGO.name;
            }

            // Announce the toggle state
            string stateText = state ? "On" : "Off";
            string announcement = $"{labelText}: {stateText}";

            AccessibilityMod.SpeakText(announcement);
        }
    }

    /// <summary>
    /// Harmony patch to announce UITooltip content
    /// Patches: protected virtual void SetText(string tooltipText)
    /// </summary>
    [HarmonyPatch(typeof(UITooltip), "SetText")]
    public class UITooltip_SetText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UITooltip __instance, string tooltipText)
        {
            if (__instance == null || string.IsNullOrEmpty(tooltipText)) return;

            // Clean and announce the tooltip text
            string cleanedText = AccessibilityMod.CleanText(tooltipText);

            if (!string.IsNullOrEmpty(cleanedText))
            {
                MelonLogger.Msg($"[Tooltip] {cleanedText}");
                // Tooltips should NOT interrupt - they're informational
                AccessibilityMod.SpeakText(cleanedText, interrupt: false);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce TextTooltip content
    /// Patches: public void SetText(string text)
    /// </summary>
    [HarmonyPatch(typeof(TextTooltip), "SetText")]
    public class TextTooltip_SetText_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(TextTooltip __instance, string text)
        {
            if (__instance == null || string.IsNullOrEmpty(text)) return;

            // Clean and announce the tooltip text
            string cleanedText = AccessibilityMod.CleanText(text);

            if (!string.IsNullOrEmpty(cleanedText))
            {
                MelonLogger.Msg($"[TextTooltip] {cleanedText}");
                // Tooltips should NOT interrupt - they're informational
                AccessibilityMod.SpeakText(cleanedText, interrupt: false);
            }
        }
    }

    /// <summary>
    /// Harmony patch to announce tooltips when they're set as the active popup
    /// Patches: public void SetPopup(GameObject popup, GameObject messageTarget = null)
    /// This catches tooltips created by TooltipCreator components (like in difficulty selection)
    /// </summary>
    [HarmonyPatch(typeof(TooltipManager), "SetPopup")]
    public class TooltipManager_SetPopup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(GameObject popup)
        {
            if (popup == null) return;

            string tooltipText = null;

            // Try to get TextTooltip component
            TextTooltip textTooltip = popup.GetComponent<TextTooltip>();
            if (textTooltip != null && textTooltip.label != null)
            {
                tooltipText = textTooltip.label.text;
            }

            // Try to get ItemInfoTooltip component if no TextTooltip
            if (string.IsNullOrEmpty(tooltipText))
            {
                ItemInfoTooltip itemTooltip = popup.GetComponent<ItemInfoTooltip>();
                if (itemTooltip != null && itemTooltip.nameLabel != null)
                {
                    tooltipText = itemTooltip.nameLabel.text;
                }
            }

            // Announce if we found tooltip text
            if (!string.IsNullOrEmpty(tooltipText))
            {
                string cleanedText = AccessibilityMod.CleanText(tooltipText);
                if (!string.IsNullOrEmpty(cleanedText))
                {
                    // Deduplication: don't announce same tooltip within debounce time
                    float currentTime = Time.unscaledTime;
                    bool isDifferentTooltip = cleanedText != AccessibilityMod.lastTooltipText;
                    bool enoughTimePassed = (currentTime - AccessibilityMod.lastTooltipTime) >= AccessibilityMod.TOOLTIP_DEBOUNCE_TIME;

                    if (isDifferentTooltip || enoughTimePassed)
                    {
                        AccessibilityMod.lastTooltipText = cleanedText;
                        AccessibilityMod.lastTooltipTime = currentTime;

                        // Tooltips should NOT interrupt - they're informational
                        AccessibilityMod.SpeakText(cleanedText, interrupt: false);
                    }
                }
            }
        }
    }

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
                string cleanedText = AccessibilityMod.CleanText(description);
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

                    string announcement = $"{difficultyName}. {cleanedText}";
                    AccessibilityMod.SpeakText(announcement, interrupt: true);
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
        public static void Postfix(CHA_DescriptionPanel __instance)
        {
            if (__instance == null || __instance.nameLabel == null || __instance.descriptionLabel == null) return;

            string name = __instance.nameLabel.text;
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(name))
            {
                string cleanedName = AccessibilityMod.CleanText(name);
                string cleanedDesc = AccessibilityMod.CleanText(description);

                // Announce skill name and first sentence of description
                string announcement = cleanedName;
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    // Get first sentence or first 200 characters
                    int dotIndex = cleanedDesc.IndexOf('.');
                    if (dotIndex > 0 && dotIndex < 200)
                    {
                        announcement += ". " + cleanedDesc.Substring(0, dotIndex + 1);
                    }
                    else if (cleanedDesc.Length > 200)
                    {
                        announcement += ". " + cleanedDesc.Substring(0, 200) + "...";
                    }
                    else
                    {
                        announcement += ". " + cleanedDesc;
                    }
                }

                AccessibilityMod.SpeakText(announcement, interrupt: true);
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
        public static void Postfix(CHA_DescriptionPanel __instance)
        {
            if (__instance == null || __instance.nameLabel == null || __instance.descriptionLabel == null) return;

            string name = __instance.nameLabel.text;
            string description = __instance.descriptionLabel.text;

            if (!string.IsNullOrEmpty(name))
            {
                string cleanedName = AccessibilityMod.CleanText(name);
                string cleanedDesc = AccessibilityMod.CleanText(description);

                // Announce attribute name and first sentence of description
                string announcement = cleanedName;
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    // Get first sentence or first 200 characters
                    int dotIndex = cleanedDesc.IndexOf('.');
                    if (dotIndex > 0 && dotIndex < 200)
                    {
                        announcement += ". " + cleanedDesc.Substring(0, dotIndex + 1);
                    }
                    else if (cleanedDesc.Length > 200)
                    {
                        announcement += ". " + cleanedDesc.Substring(0, 200) + "...";
                    }
                    else
                    {
                        announcement += ". " + cleanedDesc;
                    }
                }

                AccessibilityMod.SpeakText(announcement, interrupt: true);
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
                string cleanedName = AccessibilityMod.CleanText(name);
                string cleanedDesc = AccessibilityMod.CleanText(description);

                // Announce quirk/trait name and description
                string announcement = cleanedName;
                if (!string.IsNullOrEmpty(cleanedDesc))
                {
                    // For traits, include full description as they're usually short
                    if (cleanedDesc.Length > 300)
                    {
                        announcement += ". " + cleanedDesc.Substring(0, 300) + "...";
                    }
                    else
                    {
                        announcement += ". " + cleanedDesc;
                    }
                }

                AccessibilityMod.SpeakText(announcement, interrupt: true);
            }
        }
    }

}
