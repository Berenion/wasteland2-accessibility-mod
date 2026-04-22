using System.Reflection;
using HarmonyLib;
using MelonLoader;
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
                MelonLogger.Msg($"[KeypadMenu.OnGUI] Suppressed key: {Event.current.keyCode}");
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Diagnostic: logs when SetCallback is invoked so we can tell whether
    /// each freshly created KeypadMenu actually receives a callback. Also
    /// reflects on the callback's target object to log the `passcode` field
    /// (if any), since OnKeypadEnter compares number.ToString() == passcode
    /// and an empty/whitespace passcode would silently mismatch every entry.
    /// </summary>
    [HarmonyPatch(typeof(KeypadMenu), "SetCallback")]
    public class KeypadMenu_SetCallback_Logger
    {
        [HarmonyPostfix]
        public static void Postfix(KeypadMenu __instance, KeypadMenu.KeypadDelegate keypadCallback)
        {
            string target = keypadCallback != null && keypadCallback.Target != null
                ? keypadCallback.Target.GetType().Name
                : "<null>";
            string method = keypadCallback != null && keypadCallback.Method != null
                ? keypadCallback.Method.Name
                : "<null>";

            string passcodeInfo = "<no target>";
            if (keypadCallback != null && keypadCallback.Target != null)
            {
                var tgt = keypadCallback.Target;
                var passcodeField = tgt.GetType().GetField("passcode",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (passcodeField != null)
                {
                    var v = passcodeField.GetValue(tgt) as string;
                    passcodeInfo = v == null ? "<null>" : $"'{v}' (len={v.Length})";
                }
                else
                {
                    passcodeInfo = "<no passcode field>";
                }
            }

            MelonLogger.Msg($"[KeypadMenu.SetCallback] menu={__instance.GetInstanceID()} target={target} method={method} passcode={passcodeInfo}");
        }
    }

    /// <summary>
    /// Diagnostic: logs when ModalMessageMenu.SetMessage is invoked so we can
    /// see whether the "Incorrect Passcode" modal is being created at all
    /// after a wrong entry.
    /// </summary>
    [HarmonyPatch(typeof(ModalMessageMenu), "SetMessage", new[] { typeof(string), typeof(string), typeof(string), typeof(ModalMessageMenu.ModalButtonDelegate), typeof(string), typeof(ModalMessageMenu.ModalButtonDelegate) })]
    public class ModalMessageMenu_SetMessage_Logger
    {
        [HarmonyPostfix]
        public static void Postfix(ModalMessageMenu __instance, string title, string msg, string buttonYesLabel)
        {
            MelonLogger.Msg($"[ModalMessageMenu.SetMessage] menu={__instance.GetInstanceID()} title='{title}' msg='{msg}' yes='{buttonYesLabel}' active={__instance.gameObject.activeInHierarchy}");
        }
    }

    /// <summary>
    /// Diagnostic: logs when InteractableObject.Unlock is invoked so we can
    /// confirm whether the safe is actually being unlocked when the right
    /// passcode is entered.
    /// </summary>
    [HarmonyPatch(typeof(InteractableObject), "Unlock")]
    public class InteractableObject_Unlock_Logger
    {
        [HarmonyPrefix]
        public static void Prefix(InteractableObject __instance)
        {
            MelonLogger.Msg($"[InteractableObject.Unlock] type={__instance.GetType().Name} name={__instance.gameObject.name} isLocked={__instance.isLocked}");
        }
    }

    /// <summary>
    /// Diagnostic: logs when OnEnterClicked runs and whether the callback is
    /// null at that moment.
    /// </summary>
    [HarmonyPatch(typeof(KeypadMenu), "OnEnterClicked")]
    public class KeypadMenu_OnEnterClicked_Logger
    {
        private static readonly FieldInfo callbackField =
            typeof(KeypadMenu).GetField("callback", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPrefix]
        public static void Prefix(KeypadMenu __instance)
        {
            var cb = callbackField != null ? callbackField.GetValue(__instance) as System.Delegate : null;
            string target = cb != null && cb.Target != null ? cb.Target.GetType().Name : "<null>";
            string method = cb != null && cb.Method != null ? cb.Method.Name : "<null>";
            MelonLogger.Msg($"[KeypadMenu.OnEnterClicked] menu={__instance.GetInstanceID()} callback target={target} method={method}");
        }
    }
}
