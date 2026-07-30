using MelonLoader;
using System;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Where the mod routes its output through Tolk. Speech is the default; the
    /// other modes send text to a connected Braille display (Tolk drives whatever
    /// the active screen reader — NVDA/JAWS — has attached). Chosen in the
    /// accessibility settings menu; see <see cref="ModConfig.OutputMode"/>.
    /// </summary>
    public enum OutputMode
    {
        /// <summary>Speech only (Tolk_Speak). Braille display is not written by the mod.</summary>
        Speech = 0,
        /// <summary>Both speech and Braille (Tolk_Output).</summary>
        SpeechAndBraille = 1,
        /// <summary>Braille display only (Tolk_Braille), no speech.</summary>
        BrailleOnly = 2
    }

    /// <summary>
    /// Manages screen reader initialization and text-to-speech output
    /// </summary>
    public static class ScreenReaderManager
    {
        private static Tolk.Tolk screenReader;
        private static bool isLoaded = false;

        /// <summary>
        /// Initializes the screen reader library
        /// </summary>
        /// <returns>True if initialization was successful</returns>
        public static bool Initialize()
        {
            try
            {
                screenReader = new Tolk.Tolk();
                screenReader.Load();
                isLoaded = true;

                string detectedReader = screenReader.DetectScreenReader();
                if (detectedReader != null)
                {
                    MelonLogger.Msg($"Screen reader detected: {detectedReader}");
                }
                else
                {
                    MelonLogger.Msg("No screen reader detected (Tolk loaded, will use SAPI if available)");
                }

                // Report Braille availability so the log shows whether a display is
                // connected when a user has selected a Braille output mode.
                try
                {
                    MelonLogger.Msg($"Braille display: {(screenReader.HasBraille() ? "connected" : "not detected")}");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"Braille capability check failed: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to initialize Tolk: {ex.Message}");
                MelonLogger.Error("Screen reader support will be disabled. Make sure Tolk.dll is in the game directory.");
                isLoaded = false;
                return false;
            }
        }

        /// <summary>
        /// Shuts down the screen reader library
        /// </summary>
        public static void Shutdown()
        {
            if (screenReader != null && screenReader.IsLoaded())
            {
                MelonLogger.Msg("Unloading Tolk screen reader...");
                screenReader.Unload();
                isLoaded = false;
            }
        }

        /// <summary>
        /// Queues text for the screen reader without interrupting current speech.
        /// Use this for sequential announcements, automatic notifications, and follow-up context.
        /// This is the default and recommended method for most announcements.
        /// </summary>
        public static void Speak(string text)
        {
            text = UITextExtractor.CleanText(text);
            if (string.IsNullOrEmpty(text)) return;
            AudioAwareAnnouncementManager.Instance.QueueAnnouncement(text, false);
        }

        /// <summary>
        /// Speaks text immediately, interrupting any current speech.
        /// Bypasses the audio-aware queue entirely for instant feedback.
        /// Use this for direct user actions (navigation, key presses) where old speech is stale
        /// and the user expects immediate feedback on what they just did.
        /// </summary>
        public static void SpeakInterrupt(string text)
        {
            text = UITextExtractor.CleanText(text);
            if (string.IsNullOrEmpty(text)) return;
            // User-initiated actions always speak immediately - clear stale queue and speak directly
            AudioAwareAnnouncementManager.Instance.ClearQueue();
            SpeakDirect(text, true);
        }

        /// <summary>
        /// Sends text directly to the screen reader, bypassing the audio-aware queue.
        /// Use this only when you need to speak immediately regardless of voiceover state
        /// (e.g., for critical system messages or debugging).
        /// </summary>
        public static void SpeakDirect(string text, bool interrupt = false)
        {
            if (screenReader != null && isLoaded)
            {
                text = UITextExtractor.CleanText(text);
                if (string.IsNullOrEmpty(text)) return;
                Output(text, interrupt);
            }
        }

        /// <summary>
        /// Routes a cleaned string to Tolk according to the configured output mode:
        /// speech (default), speech + Braille, or Braille only. Every spoken string
        /// funnels through here, so this is the single point that honors Braille mode.
        /// Falls back to speech if a Braille call fails (e.g. no display connected).
        /// </summary>
        private static void Output(string text, bool interrupt)
        {
            try
            {
                switch (ModConfig.OutputMode)
                {
                    case OutputMode.BrailleOnly:
                        screenReader.Braille(text);
                        break;
                    case OutputMode.SpeechAndBraille:
                        screenReader.Output(text, interrupt);
                        break;
                    default:
                        screenReader.Speak(text, interrupt: interrupt);
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Output failed ({ModConfig.OutputMode}), falling back to speech: {ex.Message}");
                screenReader.Speak(text, interrupt: interrupt);
            }
        }

        /// <summary>
        /// Checks if the screen reader is loaded and available
        /// </summary>
        public static bool IsLoaded => isLoaded && screenReader != null && screenReader.IsLoaded();
    }
}
