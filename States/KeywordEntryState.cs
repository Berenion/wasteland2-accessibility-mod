using System.Reflection;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Accessible text entry for the conversation "custom keyword" box (ModalInputMenu).
    /// This is the same modal the game opens for gamepad users (ConversationHUD.OnButtonDown,
    /// "Controller Y" -> GUIManager.DisplayInputMenuOK("Custom Keyword", ..., OnCustomKeywordEntered)),
    /// reused here so a screen-reader user can type secret keywords / passwords that the game
    /// never surfaces as buttons (e.g. Red's computer access code "Rosebud").
    ///
    /// Priority 72 - above DialogState (70) so this state owns the ModalInputMenu instead of
    /// DialogState treating it as a generic yes/no modal. DialogState explicitly skips
    /// ModalInputMenu (see DialogState.IsModalDialogOpen) so the two never fight.
    ///
    /// Like KeypadState, this state takes full ownership of typing: it writes characters into
    /// the modal's UIInput via reflection and blocks the native UIInput processing (blockUIInput)
    /// so keystrokes aren't captured twice.
    /// </summary>
    public class KeywordEntryState : AccessibilityStateBase
    {
        public override string Name => "KeywordEntry";
        public override int Priority => 72;

        public override string GetHelpText()
        {
            return "Keyword entry. Type the secret keyword or password, Enter submits, " +
                   "Backspace deletes the last character, Escape cancels.";
        }

        /// <summary>
        /// Set while this state owns input. Checked by the UIInput Harmony patches
        /// (see CharacterScreenPatches) so the modal's own UIInput doesn't also capture keys.
        /// </summary>
        public static bool blockUIInput = false;

        private ModalInputMenu cachedMenu;

        private static readonly FieldInfo uiInputMValueField =
            typeof(UIInput).GetField("mValue", BindingFlags.NonPublic | BindingFlags.Instance);

        public override bool IsActive
        {
            get
            {
                var menu = Helpers.SceneQueryCache.Find<ModalInputMenu>();
                return menu != null && menu.gameObject.activeInHierarchy;
            }
        }

        public override bool HandleInput()
        {
            if (cachedMenu == null || !cachedMenu.gameObject.activeInHierarchy)
            {
                cachedMenu = UnityEngine.Object.FindObjectOfType<ModalInputMenu>();
                if (cachedMenu == null) return false;
            }

            // Own all input while typing into the keyword box.
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            UIInput input = cachedMenu.input;
            if (input == null) return true;

            // Enter submits the keyword (routes to ConversationHUD.OnCustomKeywordEntered).
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                string value = input.value ?? "";
                ScreenReaderManager.SpeakInterrupt(
                    !string.IsNullOrEmpty(value) ? $"Submitting {value}" : "Empty");
                Submit();
                return true;
            }

            // Escape cancels by submitting an empty value (OnCustomKeywordEntered ignores empty
            // and closes the modal), leaving the conversation untouched.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ScreenReaderManager.SpeakInterrupt("Cancelled");
                SetValue("");
                Submit();
                return true;
            }

            // Process typed characters (Input.inputString delivers backspace as '\b').
            string current = input.value ?? "";
            bool changed = false;
            string typed = Input.inputString;
            if (!string.IsNullOrEmpty(typed))
            {
                foreach (char c in typed)
                {
                    if (c == '\b')
                    {
                        if (current.Length > 0)
                        {
                            current = current.Substring(0, current.Length - 1);
                            changed = true;
                        }
                    }
                    else if (c >= ' ' && c != '') // Printable
                    {
                        if (input.characterLimit <= 0 || current.Length < input.characterLimit)
                        {
                            current += c;
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                SetValue(current);
                if (current.Length > 0)
                    ScreenReaderManager.SpeakInterrupt(current[current.Length - 1].ToString());
                else
                    ScreenReaderManager.SpeakInterrupt("Empty");
            }

            return true;
        }

        public override void OnActivated()
        {
            blockUIInput = true;
            cachedMenu = UnityEngine.Object.FindObjectOfType<ModalInputMenu>();

            string title = "Enter keyword or password";
            if (cachedMenu != null && cachedMenu.titleLabel != null &&
                !string.IsNullOrEmpty(cachedMenu.titleLabel.text))
            {
                title = UITextExtractor.CleanText(cachedMenu.titleLabel.text);
            }

            ScreenReaderManager.SpeakInterrupt(
                $"{title}. Type your keyword or password, Backspace to delete, Enter to submit, Escape to cancel.");

            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            blockUIInput = false;
            cachedMenu = null;
            base.OnDeactivated();
        }

        /// <summary>Writes the value into the modal's UIInput without triggering native onChange.</summary>
        private void SetValue(string value)
        {
            if (cachedMenu == null || cachedMenu.input == null) return;

            if (uiInputMValueField != null)
            {
                uiInputMValueField.SetValue(cachedMenu.input, value);
                cachedMenu.input.UpdateLabel();
            }
            else
            {
                cachedMenu.input.value = value;
            }
        }

        /// <summary>Clicks the modal's OK button, which fires OnCustomKeywordEntered and closes.</summary>
        private void Submit()
        {
            if (cachedMenu == null) return;
            if (cachedMenu.yesButton != null)
            {
                cachedMenu.yesButton.gameObject.SendMessage(
                    "OnClick", SendMessageOptions.DontRequireReceiver);
            }
            blockUIInput = false;
            cachedMenu = null;
        }
    }
}
