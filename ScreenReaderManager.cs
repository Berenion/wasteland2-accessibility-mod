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
        /// Sends text to the screen reader with audio-aware queueing
        /// This is the recommended method for most announcements - it will queue the announcement
        /// if voiceover is playing, or speak immediately if no voiceover is active
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, interrupts current speech. Use true for focus changes, false for informational updates like tooltips.</param>
        public static void Speak(string text, bool interrupt = true)
        {
            // Route through the audio-aware announcement manager
            AudioAwareAnnouncementManager.Instance.QueueAnnouncement(text, interrupt);
        }

        /// <summary>
        /// Sends text directly to the screen reader, bypassing the audio-aware queue
        /// Use this only when you need to speak immediately regardless of voiceover state
        /// (e.g., for critical system messages or debugging)
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, interrupts current speech</param>
        public static void SpeakDirect(string text, bool interrupt = true)
        {
            if (screenReader != null && isLoaded)
            {
                screenReader.Speak(text, interrupt: interrupt);
            }
        }

        /// <summary>
        /// Checks if the screen reader is loaded and available
        /// </summary>
        public static bool IsLoaded => isLoaded && screenReader != null && screenReader.IsLoaded();
    }
}
