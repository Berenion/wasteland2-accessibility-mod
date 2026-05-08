File: ScreenReaderManager.cs — initializes Tolk and exposes the three-tier speech pipeline (Speak / SpeakInterrupt / SpeakDirect)

namespace Wasteland2AccessibilityMod  (line 4)

// Manages screen reader initialization and text-to-speech output
class ScreenReaderManager  (line 9)  [public static]

    private static Tolk.Tolk screenReader  (line 11)
    private static bool isLoaded  (line 12)

    // Initializes the screen reader library; returns true if successful
    public static bool Initialize()  (line 18)
        // note: loads Tolk, calls DetectScreenReader, logs result; sets isLoaded=false on exception

    // Shuts down the screen reader library
    public static void Shutdown()  (line 50)

    // Queues text without interrupting; routes through AudioAwareAnnouncementManager (interrupt=false)
    public static void Speak(string text)  (line 65)
        // note: calls UITextExtractor.CleanText before queuing; use for sequential/follow-up announcements

    // Speaks immediately with interrupt=true; clears the audio-aware queue first
    public static void SpeakInterrupt(string text)  (line 78)
        // note: calls ClearQueue() then SpeakDirect(text, true); use for direct user-action feedback where stale speech is stale

    // Bypasses the audio-aware queue; speaks directly to Tolk with configurable interrupt flag
    public static void SpeakDirect(string text, bool interrupt = false)  (line 92)
        // note: still calls CleanText; intended for critical system messages or called internally by AudioAwareAnnouncementManager

    // Checks if the screen reader is loaded and available
    public static bool IsLoaded => isLoaded && screenReader != null && screenReader.IsLoaded()  (line 105)
