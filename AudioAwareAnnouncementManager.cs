using MelonLoader;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Manages screen reader announcements that are queued when voiceover audio is playing
    /// Prevents screen reader from talking over dialogue/voiceover lines
    /// </summary>
    public class AudioAwareAnnouncementManager
    {
        private static AudioAwareAnnouncementManager _instance;
        public static AudioAwareAnnouncementManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AudioAwareAnnouncementManager();
                }
                return _instance;
            }
        }

        private class QueuedAnnouncement
        {
            public string Text { get; set; }
            public bool Interrupt { get; set; }
            public float QueueTime { get; set; }
            public bool HasWaitedForAudio { get; set; }
        }

        private Queue<QueuedAnnouncement> announcementQueue = new Queue<QueuedAnnouncement>();
        private float lastAudioStopTime = 0f;
        private bool wasPlayingLastFrame = false;
        private bool wasInConversationLastFrame = false;
        private float conversationStartTime = 0f;
        private float conversationEndTime = 0f;
        private const float DELAY_AFTER_AUDIO_STOPS = 0.4f; // 400ms delay after audio stops
        private const float DELAY_AFTER_CONVERSATION_ENDS = 0.2f; // 200ms delay after conversation ends
        private const float CONVERSATION_GRACE_PERIOD = 0.5f; // 500ms grace period to detect if conversation has audio

        // Reflection fields for accessing BubbleTextManager internals
        private static FieldInfo bubbleTextInfosField = null;
        private static bool fieldInitialized = false;

        public void Initialize()
        {
            MelonLogger.Msg("[AudioAware] AudioAwareAnnouncementManager initialized");
        }

        public void Update()
        {
            // Track when conversations start and end
            bool isInConversation = Drama.isConversationOn || Drama.isCutsceneOn;

            if (isInConversation && !wasInConversationLastFrame)
            {
                // Conversation started
                conversationStartTime = Time.time;
                MelonLogger.Msg($"[AudioAware] Conversation started");
            }
            else if (!isInConversation && wasInConversationLastFrame)
            {
                // Conversation ended - mark the time
                conversationEndTime = Time.time;
                if (announcementQueue.Count > 0)
                {
                    MelonLogger.Msg($"[AudioAware] Conversation ended - will speak {announcementQueue.Count} queued announcement(s) after short delay");
                }
            }

            wasInConversationLastFrame = isInConversation;

            // Only process if there's something in the queue
            if (announcementQueue.Count == 0)
            {
                return;
            }

            // Check if audio is currently playing
            bool isAudioPlaying = IsVoiceAudioPlaying();

            // Detect when audio stops
            if (wasPlayingLastFrame && !isAudioPlaying)
            {
                lastAudioStopTime = Time.time;
                MelonLogger.Msg($"[AudioAware] Voice audio stopped");
            }

            wasPlayingLastFrame = isAudioPlaying;

            // Process queue only when audio is not playing
            if (!isAudioPlaying)
            {
                // Check if we just exited a conversation
                if (conversationEndTime > 0f)
                {
                    float timeSinceConversationEnd = Time.time - conversationEndTime;
                    if (timeSinceConversationEnd >= DELAY_AFTER_CONVERSATION_ENDS)
                    {
                        // Process all queued announcements after conversation ends
                        ProcessAllQueuedAnnouncements();
                        conversationEndTime = 0f; // Reset
                    }
                }
                else
                {
                    // Normal processing: wait a short delay after audio stops before speaking
                    float timeSinceAudioStopped = Time.time - lastAudioStopTime;
                    if (timeSinceAudioStopped >= DELAY_AFTER_AUDIO_STOPS)
                    {
                        ProcessQueue();
                    }
                }
            }
        }

        /// <summary>
        /// Checks if voiceover/dialogue audio is currently playing or will play soon
        /// </summary>
        private bool IsVoiceAudioPlaying()
        {
            try
            {
                // Get BubbleTextManager instance
                var btmInstance = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
                if (btmInstance == null)
                {
                    return false;
                }

                // Get the private bubbleTextInfos field using reflection (only once)
                if (!fieldInitialized)
                {
                    Type btmType = typeof(BubbleTextManager);
                    bubbleTextInfosField = btmType.GetField("bubbleTextInfos",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInitialized = true;

                    if (bubbleTextInfosField == null)
                    {
                        MelonLogger.Warning("[AudioAware] Could not find bubbleTextInfos field in BubbleTextManager");
                        return false;
                    }
                }

                // Get the list of active bubble texts
                if (bubbleTextInfosField != null)
                {
                    var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;

                    // If conversation just started and no bubble texts yet, queue temporarily
                    if ((Drama.isConversationOn || Drama.isCutsceneOn) &&
                        (bubbleTextInfos == null || bubbleTextInfos.Count == 0))
                    {
                        float timeSinceConversationStart = Time.time - conversationStartTime;
                        if (timeSinceConversationStart < CONVERSATION_GRACE_PERIOD)
                        {
                            // Still in grace period - queue to be safe
                            return true;
                        }
                        // Grace period expired, no bubble texts = text-only conversation
                        return false;
                    }

                    // No bubble texts outside conversation = no audio
                    if (bubbleTextInfos == null || bubbleTextInfos.Count == 0)
                    {
                        return false;
                    }

                    bool hasAnyBubbleText = false;
                    bool hasAudioBubbleText = false;

                    // Check each bubble text for active audio or audio that will play
                    foreach (var btInfo in bubbleTextInfos)
                    {
                        if (btInfo == null) continue;

                        Type btInfoType = btInfo.GetType();

                        // Get the textKind to filter out barks and radio messages
                        FieldInfo textKindField = btInfoType.GetField("textKind");
                        if (textKindField == null) continue;

                        object textKindValue = textKindField.GetValue(btInfo);
                        if (textKindValue == null) continue;

                        string textKindName = textKindValue.ToString();

                        // Only consider actual conversation dialogue, not barks or radio
                        // Ignore: Bark, BarkSpecial, AudioBark, PositionedAudioBark, Radio, RadioBark, Label
                        if (textKindName.Contains("Bark") || textKindName == "Radio" || textKindName == "RadioBark" || textKindName == "Label")
                        {
                            continue; // Skip barks, radio messages, and labels
                        }

                        hasAnyBubbleText = true;

                        // Check if this is a subtitle to audio (has voiceover)
                        FieldInfo isSubtitleField = btInfoType.GetField("isSubtitleToAudio");
                        if (isSubtitleField != null)
                        {
                            bool isSubtitle = (bool)isSubtitleField.GetValue(btInfo);
                            if (isSubtitle)
                            {
                                // This text has associated audio
                                hasAudioBubbleText = true;
                                MelonLogger.Msg($"[AudioAware] Detected voiceover dialogue (textKind: {textKindName})");
                                return true;
                            }
                        }

                        // Also check if any audio is currently playing
                        FieldInfo audioRefField = btInfoType.GetField("audioRef");
                        if (audioRefField != null)
                        {
                            var audioRef = audioRefField.GetValue(btInfo);
                            if (audioRef != null)
                            {
                                // Get the AudioObject from the PoolableReference
                                MethodInfo getMethod = audioRef.GetType().GetMethod("Get");
                                if (getMethod != null)
                                {
                                    var audioObject = getMethod.Invoke(audioRef, null);
                                    if (audioObject != null)
                                    {
                                        // Check if audio is playing
                                        MethodInfo isPlayingMethod = audioObject.GetType().GetMethod("IsPlaying");
                                        if (isPlayingMethod != null)
                                        {
                                            bool isPlaying = (bool)isPlayingMethod.Invoke(audioObject, null);
                                            if (isPlaying)
                                            {
                                                hasAudioBubbleText = true;
                                                MelonLogger.Msg($"[AudioAware] Detected playing dialogue audio (textKind: {textKindName})");
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Check if there's an audioName set (audio will play)
                        FieldInfo audioNameField = btInfoType.GetField("audioName");
                        if (audioNameField != null)
                        {
                            string audioName = audioNameField.GetValue(btInfo) as string;
                            if (!string.IsNullOrEmpty(audioName) && audioName.Length > 0)
                            {
                                hasAudioBubbleText = true;
                                MelonLogger.Msg($"[AudioAware] Detected pending dialogue audio (textKind: {textKindName}, audioName: {audioName})");
                                return true;
                            }
                        }
                    }

                    // If we're in a conversation and have bubble texts but none have audio, it's text-only
                    if (hasAnyBubbleText && !hasAudioBubbleText)
                    {
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioAware] Error checking voiceover status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Queue an announcement or speak it immediately if no audio is playing
        /// </summary>
        public void QueueAnnouncement(string text, bool interrupt = false)
        {
            // .NET 3.5 doesn't have IsNullOrWhiteSpace, so check manually
            if (string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                return;
            }

            // Check if voiceover audio is currently playing
            bool isVoiceoverPlaying = IsVoiceAudioPlaying();

            // If no voiceover is playing, speak immediately (e.g., in menus)
            if (!isVoiceoverPlaying)
            {
                // Speak immediately without queueing (use SpeakDirect to avoid circular calls)
                if (ScreenReaderManager.IsLoaded)
                {
                    ScreenReaderManager.SpeakDirect(text, interrupt);
                    MelonLogger.Msg($"[AudioAware] Speaking immediately (no voiceover): {text}");
                }
                return;
            }

            // Voiceover is playing, so queue the announcement
            // Check for duplicates already in queue
            foreach (var existing in announcementQueue)
            {
                if (existing.Text == text)
                {
                    return; // Skip duplicate
                }
            }

            var announcement = new QueuedAnnouncement
            {
                Text = text,
                Interrupt = interrupt,
                QueueTime = Time.time,
                HasWaitedForAudio = false
            };

            announcementQueue.Enqueue(announcement);
            MelonLogger.Msg($"[AudioAware] Queued (voiceover active): {text} (Queue size: {announcementQueue.Count})");
        }

        /// <summary>
        /// Process the announcement queue (speaks one announcement)
        /// </summary>
        private void ProcessQueue()
        {
            if (announcementQueue.Count == 0)
            {
                return;
            }

            // Dequeue and speak the next announcement
            var announcement = announcementQueue.Dequeue();
            MelonLogger.Msg($"[AudioAware] Speaking from queue: {announcement.Text}");

            // Speak directly using ScreenReaderManager (use SpeakDirect to avoid circular calls)
            if (ScreenReaderManager.IsLoaded)
            {
                // Use the original interrupt value from when it was queued
                ScreenReaderManager.SpeakDirect(announcement.Text, announcement.Interrupt);
            }
            else
            {
                MelonLogger.Warning($"[AudioAware] Cannot speak - Screen reader not ready");
            }
        }

        /// <summary>
        /// Process all queued announcements immediately (used when conversation ends)
        /// </summary>
        private void ProcessAllQueuedAnnouncements()
        {
            while (announcementQueue.Count > 0)
            {
                ProcessQueue();
            }
        }

        /// <summary>
        /// Get the current size of the announcement queue
        /// </summary>
        public int GetQueueSize()
        {
            return announcementQueue.Count;
        }

        /// <summary>
        /// Clear all queued announcements
        /// </summary>
        public void ClearQueue()
        {
            announcementQueue.Clear();
        }

        /// <summary>
        /// Check if voice audio is currently playing
        /// This can be used by other systems to decide whether to queue announcements
        /// </summary>
        public bool IsDialogueAudioPlaying()
        {
            return IsVoiceAudioPlaying();
        }
    }
}
