using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Handles the safe / passcode keypad popup (KeypadMenu).
    /// Priority 58 - above GenericMenuState (55) so the keypad gets specialized
    /// digit-entry handling, below DialogState (70) so the "Incorrect Passcode"
    /// modal that appears after a wrong entry takes over cleanly.
    /// </summary>
    public class KeypadState : IAccessibilityState
    {
        public string Name => "Keypad";
        public int Priority => 58;

        // Set while this state is active so the OnGUI suppressor patch can block
        // the keypad's native key handling and avoid double-entry.
        public static bool Active = false;

        private KeypadMenu cachedMenu;
        private static readonly FieldInfo currentValueField =
            typeof(KeypadMenu).GetField("currentValue", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo addToValueMethod =
            typeof(KeypadMenu).GetMethod("AddToValue", BindingFlags.NonPublic | BindingFlags.Instance);

        public bool IsActive
        {
            get
            {
                var menu = UnityEngine.Object.FindObjectOfType<KeypadMenu>();
                return menu != null && menu.gameObject.activeInHierarchy;
            }
        }

        public bool HandleInput()
        {
            if (cachedMenu == null || !cachedMenu.gameObject.activeInHierarchy)
            {
                cachedMenu = UnityEngine.Object.FindObjectOfType<KeypadMenu>();
                if (cachedMenu == null) return false;
            }

            // Suppress all other input paths while typing into the keypad
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            // Digits: top-row 0-9 and numpad 0-9
            for (int d = 0; d <= 9; d++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d))
                {
                    MelonLogger.Msg($"[KeypadState] Digit key pressed: {d}");
                    EnterDigit(d);
                    return true;
                }
            }

            // Backspace deletes last digit
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                MelonLogger.Msg("[KeypadState] Backspace pressed");
                Backspace();
                return true;
            }

            // C clears the field (matches the on-screen Clear button)
            if (Input.GetKeyDown(KeyCode.C))
            {
                MelonLogger.Msg("[KeypadState] C pressed - clearing");
                cachedMenu.OnClearClicked();
                ScreenReaderManager.SpeakInterrupt("Cleared");
                return true;
            }

            // Enter submits
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                int value = cachedMenu.GetValue();
                string raw = currentValueField != null
                    ? (currentValueField.GetValue(cachedMenu) as string ?? string.Empty)
                    : "<unknown>";
                MelonLogger.Msg($"[KeypadState] Enter pressed, GetValue={value}, currentValue='{raw}'");
                ScreenReaderManager.SpeakInterrupt($"Submitting {value}");
                cachedMenu.OnEnterClicked(null);
                return true;
            }

            // Escape cancels
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                MelonLogger.Msg("[KeypadState] Escape pressed - cancelling");
                ScreenReaderManager.SpeakInterrupt("Cancelled");
                cachedMenu.OnCancelClicked();
                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            Active = true;
            cachedMenu = UnityEngine.Object.FindObjectOfType<KeypadMenu>();
            MelonLogger.Msg($"[KeypadState] Activated, menu={cachedMenu}, currentValueField={currentValueField != null}, addToValueMethod={addToValueMethod != null}");
            if (cachedMenu != null)
            {
                MelonLogger.Msg($"[KeypadState] Menu active={cachedMenu.gameObject.activeInHierarchy}, isTopMenu={cachedMenu.isTopMenu}, displayLabel={cachedMenu.displayLabel != null}");
            }
            ScreenReaderManager.SpeakInterrupt(
                "Enter passcode. Type digits, Backspace to delete, C to clear, Enter to submit, Escape to cancel.");
        }

        public void OnDeactivated()
        {
            Active = false;
            cachedMenu = null;
            MelonLogger.Msg("[KeypadState] Deactivated");
        }

        private void EnterDigit(int digit)
        {
            if (cachedMenu == null) return;

            // Respect the keypad's own 8-digit limit
            string current = currentValueField != null
                ? (currentValueField.GetValue(cachedMenu) as string ?? string.Empty)
                : string.Empty;
            if (current.Length >= 8)
            {
                ScreenReaderManager.SpeakInterrupt("Maximum length reached");
                return;
            }

            if (addToValueMethod != null)
            {
                addToValueMethod.Invoke(cachedMenu, new object[] { digit });
            }
            string after = currentValueField != null
                ? (currentValueField.GetValue(cachedMenu) as string ?? string.Empty)
                : "<unknown>";
            MelonLogger.Msg($"[KeypadState] EnterDigit({digit}) => currentValue='{after}'");
            ScreenReaderManager.SpeakInterrupt(digit.ToString());
        }

        private void Backspace()
        {
            if (cachedMenu == null || currentValueField == null) return;

            string current = currentValueField.GetValue(cachedMenu) as string ?? string.Empty;
            if (current.Length == 0)
            {
                ScreenReaderManager.SpeakInterrupt("Empty");
                return;
            }

            string trimmed = current.Substring(0, current.Length - 1);
            currentValueField.SetValue(cachedMenu, trimmed);
            if (cachedMenu.displayLabel != null)
            {
                cachedMenu.displayLabel.text = trimmed.Length > 0 ? trimmed : "0";
            }

            if (trimmed.Length == 0)
                ScreenReaderManager.SpeakInterrupt("Empty");
            else
                ScreenReaderManager.SpeakInterrupt(trimmed[trimmed.Length - 1].ToString());
        }
    }
}
