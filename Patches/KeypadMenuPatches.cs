using HarmonyLib;
using UnityEngine;
using Wasteland2AccessibilityMod.States;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Suppresses KeypadMenu's native OnGUI input handling while KeypadState is
    /// active. KeypadState reads the same key events through Input.GetKeyDown
    /// and forwards them to AddToValue / OnEnterClicked itself, so leaving the
    /// game's OnGUI active would cause every numpad press and Enter to fire
    /// twice.
    /// </summary>
    [HarmonyPatch(typeof(KeypadMenu), "OnGUI")]
    public class KeypadMenu_OnGUI_Suppressor
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (KeypadState.Active && Event.current != null && Event.current.type == EventType.KeyDown)
            {
                return false;
            }
            return true;
        }
    }
}
