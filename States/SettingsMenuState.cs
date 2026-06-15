using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// A self-contained, modal list of the mod's accessibility settings. Opened by the
    /// global Shift+S hotkey (detected in InputRouter, mirroring the Shift+/ help key) so
    /// every toggleable setting lives in one discoverable place instead of a separate
    /// per-setting hotkey. The standalone quick-toggle hotkeys (= clock, K names-first,
    /// H elevation, L line-of-sight) are kept for power users; this menu is an additional
    /// front-end over the same ModConfig values.
    ///
    /// Priority 90 - above every other state, including the text-entry states (KeywordEntry
    /// 72, Dialog 70). While open it is fully modal: it owns all input and suppresses game
    /// input so nothing underneath reacts. Open/close is driven entirely by the static
    /// isOpen flag, which only this state and InputRouter touch.
    /// </summary>
    public class SettingsMenuState : AccessibilityStateBase
    {
        public override string Name => "SettingsMenu";
        public override int Priority => 90;

        private static bool isOpen = false;
        private int index = 0;

        public override bool IsActive => isOpen;

        public override string GetHelpText()
        {
            return "Accessibility settings. Up and Down move between settings, " +
                   "Enter or Left and Right toggle the focused setting, " +
                   "Home and End jump to first and last, backslash repeats the current setting, " +
                   "Escape or Shift+S closes.";
        }

        /// <summary>Flip the menu open/closed. Called by InputRouter on the global hotkey.</summary>
        public static void Toggle()
        {
            isOpen = !isOpen;
        }

        public static void Close()
        {
            isOpen = false;
        }

        public override void OnActivated()
        {
            index = 0;
            ScreenReaderManager.SpeakInterrupt(
                "Accessibility settings. Up and Down to move, Enter to toggle, Escape to close.");
            AnnounceCurrent(queued: true);
            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            ScreenReaderManager.SpeakInterrupt("Settings closed");
            base.OnDeactivated();
        }

        public override bool HandleInput()
        {
            // Fully modal while open: take ownership of all input so the game and lower
            // states stay inert underneath the menu.
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            var settings = ModConfig.Settings;
            if (settings == null || settings.Count == 0)
            {
                // Nothing to show (config not initialized) — close gracefully.
                if (Input.GetKeyDown(KeyCode.Escape))
                    Close();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1, settings.Count);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1, settings.Count);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                index = 0;
                AnnounceCurrent();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                index = settings.Count - 1;
                AnnounceCurrent();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backslash))
            {
                AnnounceCurrent();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                ToggleCurrent(settings);
                return true;
            }

            // Modal: swallow everything else so nothing leaks to the game underneath.
            return true;
        }

        private void Move(int delta, int count)
        {
            int newIndex = index + delta;
            bool wrapped = false;
            if (newIndex < 0) { newIndex = count - 1; wrapped = true; }
            else if (newIndex >= count) { newIndex = 0; wrapped = true; }

            if (wrapped) MenuCue.PlayWrap();
            index = newIndex;
            AnnounceCurrent();
        }

        private void ToggleCurrent(System.Collections.Generic.List<ModConfig.Setting> settings)
        {
            if (index < 0 || index >= settings.Count) return;
            ModConfig.Setting setting = settings[index];
            setting.Toggle();
            // Announce the new state, e.g. "Distance units, tiles".
            ScreenReaderManager.SpeakInterrupt(setting.Describe());
            ModLog.Debug($"[SettingsMenuState] Toggled {setting.Label} -> {setting.ValueText}");
        }

        private void AnnounceCurrent(bool queued = false)
        {
            var settings = ModConfig.Settings;
            if (settings == null || index < 0 || index >= settings.Count) return;
            string text = settings[index].Describe();
            if (queued)
                ScreenReaderManager.Speak(text);
            else
                ScreenReaderManager.SpeakInterrupt(text);
        }
    }
}
