using HarmonyLib;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patch to announce slider value changes
    /// Patches: public float value { get; set; }
    /// </summary>
    [HarmonyPatch(typeof(UIProgressBar), "value", MethodType.Setter)]
    public class UIProgressBar_SetValue_Patch
    {
        // Slider debouncing - track last slider and time
        private static UIProgressBar lastSlider = null;
        private static float lastSliderValue = 0f;
        private static float lastSliderAnnounceTime = 0f;
        private const float SLIDER_DEBOUNCE_TIME = 0.3f; // Wait 300ms before announcing

        [HarmonyPostfix]
        public static void Postfix(UIProgressBar __instance)
        {
            if (__instance == null) return;

            // Skip scrollbars - they inherit from UISlider but we don't want to announce scroll position
            if (__instance is UIScrollBar)
            {
                return;
            }

            // Don't announce during menu navigation - let menu states handle it
            if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
            {
                return;
            }

            // Only announce if this slider is the currently selected object
            if (UICamera.selectedObject != __instance.gameObject)
            {
                return;
            }

            float currentTime = Time.unscaledTime;
            float sliderValue = __instance.value;

            // Debouncing logic: only announce if this is a different slider or value changed significantly
            bool isDifferentSlider = lastSlider != __instance;
            bool valueChanged = Mathf.Abs(sliderValue - lastSliderValue) > 0.01f;
            bool enoughTimePassed = (currentTime - lastSliderAnnounceTime) >= SLIDER_DEBOUNCE_TIME;

            // For stepped sliders, announce every step change
            bool isSteppedSlider = __instance.numberOfSteps > 1;
            bool stepChanged = false;
            if (isSteppedSlider)
            {
                int currentStep = Mathf.RoundToInt(sliderValue * (__instance.numberOfSteps - 1));
                int lastStep = Mathf.RoundToInt(lastSliderValue * (__instance.numberOfSteps - 1));
                stepChanged = currentStep != lastStep;
            }

            // Only announce if: different slider OR (same slider AND (enough time passed OR step changed))
            if (!isDifferentSlider && !stepChanged && !enoughTimePassed)
            {
                // Update tracking but don't announce yet
                lastSlider = __instance;
                lastSliderValue = sliderValue;
                return;
            }

            // Update tracking
            lastSlider = __instance;
            lastSliderValue = sliderValue;
            lastSliderAnnounceTime = currentTime;

            // Get the slider's GameObject to find its name and label
            GameObject sliderGO = __instance.gameObject;
            string sliderName = sliderGO.name;

            // Try to find a label that describes this slider
            UILabel label = sliderGO.GetComponentInChildren<UILabel>();
            string labelText = "";
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = UITextExtractor.CleanText(label.text);
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

            ScreenReaderManager.Speak(announcement, interrupt: false);
        }
    }
}
