using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Shared "how big a step did the user ask for" reading of the Shift / Ctrl modifiers,
    /// so sliders and +/- steppers behave the same way everywhere.
    ///
    /// Plain Left/Right is one step, Shift is a medium jump and Ctrl a large one — the
    /// Rimworld convention. PageUp/PageDown is deliberately NOT used for this: every state
    /// in this mod already binds it to tab switching or list cycling (options tabs,
    /// character-info tabs, combatant cycling, POI cycling, scanner cycling), so overloading
    /// it for value adjustment would collide in most of the places a slider appears.
    /// </summary>
    internal static class StepInput
    {
        public enum StepSize
        {
            Single,
            Medium,  // Shift
            Large    // Ctrl
        }

        /// <summary>Ctrl wins over Shift when both are held.</summary>
        public static StepSize Current()
        {
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return StepSize.Large;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                return StepSize.Medium;
            return StepSize.Single;
        }

        /// <summary>
        /// How many individual +/- presses a step is worth, for discrete steppers such as
        /// attribute and skill editors where each click is separately validated and priced.
        /// </summary>
        public static int Repeats(StepSize size)
        {
            switch (size)
            {
                case StepSize.Medium: return 5;
                case StepSize.Large: return 10;
                default: return 1;
            }
        }

        /// <summary>
        /// Fraction of a 0..1 slider's full range to move. Single returns 0 so the caller
        /// keeps using the control's own native step (1 / numberOfSteps), which is the right
        /// granularity for a stepped slider and shouldn't be second-guessed.
        /// </summary>
        public static float SliderFraction(StepSize size)
        {
            switch (size)
            {
                case StepSize.Medium: return 0.10f;
                case StepSize.Large: return 0.25f;
                default: return 0f;
            }
        }

        /// <summary>Spoken suffix so the user can tell which step size took effect.</summary>
        public static string Describe(StepSize size)
        {
            switch (size)
            {
                case StepSize.Medium: return "medium step";
                case StepSize.Large: return "large step";
                default: return null;
            }
        }
    }
}
