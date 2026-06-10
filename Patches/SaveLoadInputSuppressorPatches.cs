using HarmonyLib;
using MelonLoader;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// PREFIX patches on SaveLoadScreen methods that prevent the game's native
    /// input from firing save/load actions when GenericMenuState is handling input.
    ///
    /// Lives in Patches/ (not Core/) because these patches inherently know about
    /// GenericMenuState — keeping them here avoids Core having a reverse
    /// dependency on States.
    /// </summary>
    public static class SaveLoadScreenSuppressor
    {
        /// <summary>
        /// When true, the next OnSaveClicked/OnLoadClicked call is allowed through.
        /// Set by GenericMenuState.ActivateSelected() right before calling these methods.
        /// Reset after the call passes through.
        /// </summary>
        public static bool AllowNextAction { get; set; }
    }

    [HarmonyPatch(typeof(SaveLoadScreen), "OnSaveClicked")]
    public class SaveLoadScreen_OnSaveClicked_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SaveLoadScreenSuppressor.AllowNextAction)
            {
                SaveLoadScreenSuppressor.AllowNextAction = false;
                return true; // Allow - our mod requested this
            }
            if (GenericMenuState.blockUIInput)
            {
                ModLog.Debug("[SaveLoadScreen] Blocked native OnSaveClicked");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SaveLoadScreen), "OnLoadClicked")]
    public class SaveLoadScreen_OnLoadClicked_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SaveLoadScreenSuppressor.AllowNextAction)
            {
                SaveLoadScreenSuppressor.AllowNextAction = false;
                return true; // Allow - our mod requested this
            }
            if (GenericMenuState.blockUIInput)
            {
                ModLog.Debug("[SaveLoadScreen] Blocked native OnLoadClicked");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SaveLoadScreen), "OnButtonDown")]
    public class SaveLoadScreen_OnButtonDown_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix(string buttonName)
        {
            if (GenericMenuState.blockUIInput &&
                (buttonName == "Attack Current Target" || buttonName == "Controller A"))
            {
                ModLog.Debug($"[SaveLoadScreen] Blocked native OnButtonDown({buttonName})");
                return false;
            }
            return true;
        }
    }
}
