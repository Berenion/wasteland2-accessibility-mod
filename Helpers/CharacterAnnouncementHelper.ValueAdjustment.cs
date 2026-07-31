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
            AdjustAttribute(editor, direction, 1, announceCallback);
        }

        /// <summary>
        /// Applies up to <paramref name="repeats"/> single steps, stopping early at the cap
        /// or when the points run out. Each step must go through the editor's own handler —
        /// the value is priced per point and validated per click, so there is no "set to N"
        /// shortcut to take. Only one announcement is made, at the end: firing the callback
        /// ten times would bury the result under nine stale readings.
        /// </summary>
        public static void AdjustAttribute(CHA_AttributeEditor editor, int direction, int repeats, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;
            if (repeats < 1) repeats = 1;

            int applied = 0;
            for (int i = 0; i < repeats; i++)
            {
                if (direction > 0)
                {
                    if (!editor.CanIncreaseValue()) break;
                    if (attrOnPlusClickedMethod == null) break;
                    attrOnPlusClickedMethod.Invoke(editor, new object[] { null });
                }
                else
                {
                    if (!editor.CanDecreaseValue()) break;
                    if (attrOnMinusClickedMethod == null) break;
                    attrOnMinusClickedMethod.Invoke(editor, new object[] { null });
                }
                applied++;
            }

            if (applied == 0)
            {
                ScreenReaderManager.SpeakInterrupt(direction > 0 ? "Maximum" : "Minimum");
                return;
            }

            announceCallback?.Invoke();
        }

        public static void AdjustSkill(CHA_SkillEditor editor, int direction, Action announceCallback)
        {
            AdjustSkill(editor, direction, 1, announceCallback);
        }

        /// <summary>
        /// Applies up to <paramref name="repeats"/> single steps. Unlike attributes there is
        /// no public CanDecreaseValue on CHA_SkillEditor — OnMinusClicked guards internally
        /// (currentValue &lt;= 0 || currentValue &lt;= initialValue) — so progress is detected
        /// by watching GetCurrentValue(), which also covers a raise refused because the next
        /// level costs more XP than remains. Announces once, at the end.
        /// </summary>
        public static void AdjustSkill(CHA_SkillEditor editor, int direction, int repeats, Action announceCallback)
        {
            EnsureReflectionCached();
            if (editor == null) return;
            if (repeats < 1) repeats = 1;

            var method = direction > 0 ? skillOnPlusClickedMethod : skillOnMinusClickedMethod;
            if (method == null) return;

            int applied = 0;
            for (int i = 0; i < repeats; i++)
            {
                int before = editor.GetCurrentValue();
                method.Invoke(editor, new object[] { null });
                if (editor.GetCurrentValue() == before) break;
                applied++;
            }

            if (applied == 0)
            {
                ScreenReaderManager.SpeakInterrupt(direction > 0 ? "Maximum" : "Minimum");
                return;
            }

            announceCallback?.Invoke();
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
