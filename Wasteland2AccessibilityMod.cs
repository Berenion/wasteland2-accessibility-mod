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
                MelonLogger.Msg($"[{source}] Skipping non-interactive element: {go.name}");
                return;
            }

            string text = ExtractUIText(go);

            // Avoid double-speaking if both hooks fire for same element
            if (text == lastSpokenText && source != lastSource)
            {
                MelonLogger.Msg($"[{source}] Skipping duplicate: {text}");
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
                MelonLogger.Msg($"[Filter] Skipping UISprite-only element: {go.name}");
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
        internal static void SpeakText(string text)
        {
            if (screenReader != null && screenReader.IsLoaded())
            {
                // Use interrupt=true to stop previous speech and speak new text immediately
                screenReader.Speak(text, interrupt: true);
                MelonLogger.Msg($"[TTS] {text}");
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
            MelonLogger.Msg($"[SetSelection] Focus changed to: {go.name}");
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
                MelonLogger.Msg($"[Notify] OnSelect(true) for: {go.name}");
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
                MelonLogger.Msg($"[Dropdown] Highlighted option: {cleanedText}");
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

            MelonLogger.Msg($"[ModalDialog] {announcement}");
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

            MelonLogger.Msg($"[Slider] {announcement}");
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

            MelonLogger.Msg($"[CharacterCreation] {announcement}");
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

            MelonLogger.Msg($"[Toggle] {announcement}");
            AccessibilityMod.SpeakText(announcement);
        }
    }

}
