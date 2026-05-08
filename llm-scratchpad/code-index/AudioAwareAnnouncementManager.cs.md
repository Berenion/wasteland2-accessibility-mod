File: AudioAwareAnnouncementManager.cs — queues screen reader announcements while voiceover audio is playing to prevent speech overlap

namespace Wasteland2AccessibilityMod  (line 7)

class AudioAwareAnnouncementManager  (line 13)
    // Singleton. Defers TTS until voiceover finishes; speaks immediately when no audio is active.

    private static AudioAwareAnnouncementManager _instance  (line 15)
    public static AudioAwareAnnouncementManager Instance  (line 16)  [get-only singleton property]

    class QueuedAnnouncement  (line 28)  [private nested]
        public string Text  (line 30)
        public bool Interrupt  (line 31)
        public float QueueTime  (line 32)
        public bool HasWaitedForAudio  (line 33)

    private Queue<QueuedAnnouncement> announcementQueue  (line 36)
    private float lastAudioStopTime  (line 37)
    private bool wasPlayingLastFrame  (line 38)
    private bool wasInConversationLastFrame  (line 39)
    private float conversationStartTime  (line 40)
    private float conversationEndTime  (line 41)
    private const float DELAY_AFTER_AUDIO_STOPS = 0.4f  (line 42)
    private const float DELAY_AFTER_CONVERSATION_ENDS = 0.2f  (line 43)
    private const float CONVERSATION_GRACE_PERIOD = 0.5f  (line 44)
    private static FieldInfo bubbleTextInfosField  (line 47)
    private static bool fieldInitialized  (line 48)

    public void Initialize()  (line 50)
    public void Update()  (line 55)
        // note: must be called every frame from OnUpdate; tracks Drama.isConversationOn/isCutsceneOn and drives queue processing

    // Checks if voiceover/dialogue audio is currently playing or will play soon
    private bool IsVoiceAudioPlaying()  (line 125)
        // note: uses reflection to access BubbleTextManager.bubbleTextInfos; caches FieldInfo after first call; filters out Bark/Radio/Label textKinds; checks audioRef.Get().IsPlaying(); respects CONVERSATION_GRACE_PERIOD

    // Queue an announcement or speak it immediately if no audio is playing
    public void QueueAnnouncement(string text, bool interrupt = false)  (line 244)
        // note: skips IsNullOrWhiteSpace check manually (no .NET 3.5 IsNullOrWhiteSpace); deduplicates queue by text equality; calls SpeakDirect when no voiceover

    // Process the announcement queue (speaks one announcement)
    private void ProcessQueue()  (line 292)

    // Process all queued announcements immediately (used when conversation ends)
    private void ProcessAllQueuedAnnouncements()  (line 318)

    // Get the current size of the announcement queue
    public int GetQueueSize()  (line 329)

    // Clear all queued announcements
    public void ClearQueue()  (line 336)

    // Check if voice audio is currently playing
    public bool IsDialogueAudioPlaying()  (line 346)
