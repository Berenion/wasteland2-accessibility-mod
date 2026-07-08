using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Owns the GameOverScreen shown when the party is wiped. The game presents two
    /// options — Continue (return to the main menu) and Load — but they live inside a
    /// container that only fades in ~2.5 seconds after the screen appears, and the screen
    /// never sets UICamera.selectedObject. As a result GenericMenuState announced the
    /// "Game Over" title but none of the options. This state announces the death text
    /// immediately, then the options once they fade in, and lets the user choose with the
    /// arrow keys and Enter.
    ///
    /// Each option is wired directly to its on-screen button's own click delegate, so the
    /// spoken label and the action it performs always come from the same button (no
    /// hard-coded label/action mapping to drift out of sync with the prefab).
    ///
    /// Priority 62 - above GenericMenuState (55) and MainMenuState (60). GenericMenuState
    /// also yields on GameOverScreen so only this state drives it.
    /// </summary>
    public class GameOverState : AccessibilityStateBase
    {
        public override string Name => "GameOver";
        public override int Priority => 62;

        public override string GetHelpText()
        {
            return "Game over. Up and Down choose between the options, Enter selects.";
        }

        private GameOverScreen cachedScreen;
        private readonly List<Option> options = new List<Option>();
        private int index = 0;
        private bool announcedDeath = false;
        private bool announcedOptions = false;

        private class Option
        {
            public string Label;
            public Action Activate;
        }

        public override bool IsActive
        {
            get
            {
                var screen = SceneQueryCache.Find<GameOverScreen>();
                return screen != null && screen.gameObject.activeInHierarchy;
            }
        }

        public override void OnActivated()
        {
            cachedScreen = UnityEngine.Object.FindObjectOfType<GameOverScreen>();
            index = 0;
            announcedDeath = false;
            announcedOptions = false;
            options.Clear();
            base.OnActivated();
        }

        public override void OnDeactivated()
        {
            cachedScreen = null;
            options.Clear();
            announcedDeath = false;
            announcedOptions = false;
            base.OnDeactivated();
        }

        public override bool HandleInput()
        {
            if (cachedScreen == null || !cachedScreen.gameObject.activeInHierarchy)
            {
                cachedScreen = UnityEngine.Object.FindObjectOfType<GameOverScreen>();
                if (cachedScreen == null) return false;
            }

            // Own the screen: stop the game (and lower states) from acting on these keys.
            InputSuppressor.ShouldSuppressGameInput = true;
            InputSuppressor.ShouldSuppressUINavigation = true;
            InputSuppressor.ShouldSuppressButtonEvents = true;

            // Announce the death text right away — it's ready before the options fade in.
            if (!announcedDeath)
            {
                announcedDeath = true;
                string death = cachedScreen.deathLabel != null
                    ? UITextExtractor.CleanText(cachedScreen.deathLabel.text)
                    : "";
                ScreenReaderManager.SpeakInterrupt(
                    string.IsNullOrEmpty(death) ? "Game over" : "Game over. " + death);
            }

            // The option buttons live inside containerObject, which only becomes active
            // once the screen finishes fading in. Keep trying until they resolve.
            if (!announcedOptions)
            {
                BuildOptions();
                if (options.Count > 0)
                {
                    announcedOptions = true;
                    AnnounceOptions();
                }
                // else: still fading in — hold the screen and try again next frame.
            }

            if (options.Count == 0)
                return true; // own the screen while it initializes

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (index == options.Count - 1 && options.Count > 1) MenuCue.PlayWrap();
                index = (index + 1) % options.Count;
                AnnounceOption();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                index--;
                if (index < 0) { index = options.Count - 1; if (options.Count > 1) MenuCue.PlayWrap(); }
                AnnounceOption();
                return true;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                var opt = options[index];
                ScreenReaderManager.SpeakInterrupt(opt.Label + " selected");
                try { opt.Activate?.Invoke(); }
                catch (Exception ex) { MelonLogger.Error($"[GameOverState] Option '{opt.Label}' failed: {ex.Message}"); }
                return true;
            }

            return true;
        }

        // Reads the option buttons from the (now-active) container. The screen has exactly
        // two actions — Continue (return to the main menu) and Load — but the container
        // holds duplicate/decorative UIButtons, and the real buttons call OnContinueClicked
        // /OnLoadClicked (which take a GameObject arg) rather than a void NGUI onClick
        // delegate. So we can't execute onClick (it's empty) and can't trust the raw button
        // count. Instead: take each labelled button, classify it by the "load" keyword, keep
        // the first of each kind (dedupe), and wire it to the screen's public method.
        // Returns with an empty list while the container is still hidden.
        private void BuildOptions()
        {
            options.Clear();
            if (cachedScreen == null || cachedScreen.containerObject == null) return;
            if (!cachedScreen.containerObject.activeInHierarchy) return;

            var scr = cachedScreen;
            bool haveContinue = false;
            bool haveLoad = false;

            foreach (var button in scr.containerObject.GetComponentsInChildren<UIButton>())
            {
                if (button == null || !button.gameObject.activeInHierarchy) continue;

                var label = button.GetComponentInChildren<UILabel>();
                string text = label != null ? UITextExtractor.CleanText(label.text) : null;
                if (string.IsNullOrEmpty(text)) continue; // skip unlabelled/decorative buttons

                if (text.IndexOf("load", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (haveLoad) continue;
                    haveLoad = true;
                    options.Add(new Option { Label = text, Activate = () => scr.OnLoadClicked(null) });
                }
                else
                {
                    if (haveContinue) continue;
                    haveContinue = true;
                    options.Add(new Option { Label = text, Activate = () => scr.OnContinueClicked(null) });
                }
            }
        }

        private void AnnounceOptions()
        {
            if (options.Count == 0) return;
            index = 0;
            string countPart = options.Count > 1 ? $", 1 of {options.Count}" : "";
            ScreenReaderManager.SpeakInterrupt(
                $"{options[0].Label}{countPart}. Up and Down to choose, Enter to select.");
        }

        private void AnnounceOption()
        {
            if (index < 0 || index >= options.Count) return;
            string countPart = options.Count > 1 ? $", {index + 1} of {options.Count}" : "";
            ScreenReaderManager.SpeakInterrupt(options[index].Label + countPart);
        }
    }
}
