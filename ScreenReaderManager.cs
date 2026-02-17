using MelonLoader;
using System;

namespace Wasteland2AccessibilityMod
{
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
                screenReader.Speak(text, interrupt: interrupt);
            }
        }

        /// <summary>
        /// Checks if the screen reader is loaded and available
        /// </summary>
        public static bool IsLoaded => isLoaded && screenReader != null && screenReader.IsLoaded();
    }
}
