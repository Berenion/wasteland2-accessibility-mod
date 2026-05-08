using System;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    // Value adjustment for attribute / skill / trait editors. Each operation
    // invokes the original NGUI handler via the cached reflection set up in
    // CharacterAnnouncementHelper.cs and announces boundary cases ("Maximum",
    // "Minimum", "Locked", "Cannot select").
    public static partial class CharacterAnnouncementHelper
    {
        // ========== Value Adjustment ==========

        public static void AdjustAttribute(CHA_AttributeEditor editor, int direction, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (direction > 0)
            {
                if (editor.CanIncreaseValue())
                {
                    if (attrOnPlusClickedMethod != null)
                        attrOnPlusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Maximum");
                }
            }
            else
            {
                if (editor.CanDecreaseValue())
                {
                    if (attrOnMinusClickedMethod != null)
                        attrOnMinusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
                else
                {
                    ScreenReaderManager.SpeakInterrupt("Minimum");
                }
            }
        }

        public static void AdjustSkill(CHA_SkillEditor editor, int direction, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (direction > 0)
            {
                if (skillOnPlusClickedMethod != null)
                {
                    skillOnPlusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
            }
            else
            {
                if (skillOnMinusClickedMethod != null)
                {
                    skillOnMinusClickedMethod.Invoke(editor, new object[] { null });
                    announceCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Invokes the trait's pressed callback (sets currentEditor in CHA_TraitsPanel)
        /// then toggles the checkbox. Required to maintain proper game state.
        /// </summary>
        public static void ToggleTrait(CHA_TraitEditor editor)
        {
            EnsureReflectionCached();
            if (editor == null) return;

            if (editor.checkboxButton != null && !editor.checkboxButton.isEnabled)
            {
                ScreenReaderManager.SpeakInterrupt("Locked");
                return;
            }

            // Must call pressedCallback BEFORE toggling checkbox
            if (pressedCallbackField != null)
            {
                var callback = pressedCallbackField.GetValue(editor) as Delegate;
                callback?.DynamicInvoke(editor);
            }

            bool before = editor.checkbox.value;
            editor.checkbox.gameObject.SendMessage("OnClick", SendMessageOptions.DontRequireReceiver);
            bool after = editor.checkbox.value;
            if (before != after)
            {
                string state = after ? "selected" : "not selected";
                ScreenReaderManager.SpeakInterrupt(state);
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Cannot select");
            }
        }
    }
}
