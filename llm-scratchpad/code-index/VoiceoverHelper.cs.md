File: VoiceoverHelper.cs — reflection-based helpers for inspecting BubbleTextManager voiceover state (playing, pending, conversation bubbles)

namespace Wasteland2AccessibilityMod  (line 8)

// Helper class to detect and wait for voiceover audio to complete
class VoiceoverHelper  (line 13)  [public static]

    private static FieldInfo bubbleTextInfosField  (line 15)
    private static bool fieldInitialized  (line 16)

    // Checks if any voiceover audio is currently playing or will start soon in conversations
    public static bool IsVoiceoverPlaying()  (line 21)
        // note: uses reflection to get BubbleTextManager.bubbleTextInfos (cached); checks audioRef.Get().IsPlaying() per bubble; does NOT check pending (timeStarted==-1) audio

    // Checks for AudioConversation bubbles that are pending or active; validates audio file via AudioManager.IsValidAudioID
    public static bool HasPendingOrActiveVoicedAudio()  (line 111)
        // note: only cares about textKind=="AudioConversation"; validates audioName against AudioManager to avoid false positives from placeholder names

    // Checks for any active conversation-type bubble texts (with or without audio); use to gate ConversationState activation
    public static bool HasActiveConversationBubbles()  (line 178)
        // note: matches textKinds: Conversation, AudioConversation, DescConversation, DescPercConversation, AsciiArtConversation, Epilogue

    // Checks for DescConversation or DescPercConversation bubbles; used to distinguish description text from voiceover subtitles
    public static bool HasActiveDescriptionBubbles()  (line 237)

    // Gets the remaining time (seconds) for the longest currently-playing voiceover clip
    public static float GetVoiceoverRemainingTime()  (line 283)
        // note: calls AudioClipTime() and AudioTimeElapsed() via reflection per bubble; returns 0 if none playing

    // Speaks text with automatic delay if voiceover is playing; starts a coroutine for delayed delivery
    public static void SpeakWithVoiceoverDelay(string text, bool interrupt = false, float additionalDelay = 0f)  (line 339)
        // note: uses MelonCoroutines.Start; add additionalDelay on top of GetVoiceoverRemainingTime()

    private static IEnumerator DelayedSpeak(string text, bool interrupt, float delay)  (line 359)
