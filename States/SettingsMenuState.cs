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

        // Sound-glossary sub-mode: a preview browser reached from the last menu item.
        private bool inGlossary = false;
        private int glossaryIndex = 0;

        public override bool IsActive => isOpen;

        public override string GetHelpText()
        {
            return "Accessibility settings. Up and Down move between settings, " +
                   "Enter or Left and Right toggle the focused setting, " +
                   "Home and End jump to first and last, backslash repeats the current setting, " +
                   "Escape or Shift+S closes. The last item is a sound glossary: open it with Enter, " +
                   "then Up and Down to hear each scanner sound and what it means, Escape to go back.";
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
            inGlossary = false;
            glossaryIndex = 0;
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

            // Sound-glossary sub-mode owns input while open.
            if (inGlossary)
                return HandleGlossaryInput();

            var settings = ModConfig.Settings;
            if (settings == null || settings.Count == 0)
            {
                // Nothing to show (config not initialized) — close gracefully.
                if (Input.GetKeyDown(KeyCode.Escape))
                    Close();
                return true;
            }

            // The list is the toggle settings plus one trailing "Sound glossary" action.
            int count = settings.Count + 1;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1, count);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1, count);
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
                index = count - 1;
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
                ActivateCurrent(settings);
                return true;
            }

            // Modal: swallow everything else so nothing leaks to the game underneath.
            return true;
        }

        /// <summary>Input handling while the sound glossary is open (previews each cue).</summary>
        private bool HandleGlossaryInput()
        {
            var list = ScannerCueSounds.Glossary;
            if (list == null || list.Count == 0)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseGlossary();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseGlossary();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                GlossaryMove(-1, list.Count);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                GlossaryMove(1, list.Count);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                glossaryIndex = 0;
                AnnounceAndPlayGlossary();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                glossaryIndex = list.Count - 1;
                AnnounceAndPlayGlossary();
                return true;
            }

            // Enter / Left / Right / backslash: replay the focused cue.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.Backslash))
            {
                if (glossaryIndex >= 0 && glossaryIndex < list.Count)
                    ScannerCueSounds.Play(list[glossaryIndex].Category);
                return true;
            }

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

        /// <summary>Toggle the focused setting, or open the glossary if it's the last item.</summary>
        private void ActivateCurrent(System.Collections.Generic.List<ModConfig.Setting> settings)
        {
            if (index == settings.Count)
            {
                OpenGlossary();
                return;
            }
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
            if (settings == null || index < 0) return;

            string text;
            if (index == settings.Count)
                text = "Sound glossary, press Enter to open";
            else if (index < settings.Count)
                text = settings[index].Describe();
            else
                return;

            if (queued)
                ScreenReaderManager.Speak(text);
            else
                ScreenReaderManager.SpeakInterrupt(text);
        }

        // --- Sound glossary sub-mode ---

        private void OpenGlossary()
        {
            inGlossary = true;
            glossaryIndex = 0;
            ScreenReaderManager.SpeakInterrupt(
                "Sound glossary. Up and Down to hear each sound, Enter to replay, Escape to go back.");
            AnnounceAndPlayGlossary();
        }

        private void CloseGlossary()
        {
            inGlossary = false;
            // Land back on the glossary item in the main list.
            ScreenReaderManager.SpeakInterrupt("Sound glossary, press Enter to open");
        }

        private void GlossaryMove(int delta, int count)
        {
            int newIndex = glossaryIndex + delta;
            bool wrapped = false;
            if (newIndex < 0) { newIndex = count - 1; wrapped = true; }
            else if (newIndex >= count) { newIndex = 0; wrapped = true; }

            if (wrapped) MenuCue.PlayWrap();
            glossaryIndex = newIndex;
            AnnounceAndPlayGlossary();
        }

        /// <summary>Play the focused cue and read its name and meaning.</summary>
        private void AnnounceAndPlayGlossary()
        {
            var list = ScannerCueSounds.Glossary;
            if (list == null || glossaryIndex < 0 || glossaryIndex >= list.Count) return;
            var entry = list[glossaryIndex];
            ScannerCueSounds.Play(entry.Category);
            ScreenReaderManager.SpeakInterrupt($"{entry.Name}. {entry.Description}");
        }
    }
}
