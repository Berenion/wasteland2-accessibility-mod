using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Helper class to detect and wait for voiceover audio to complete
    /// </summary>
    public static class VoiceoverHelper
    {
        private static FieldInfo bubbleTextInfosField = null;
        private static bool fieldInitialized = false;

        /// <summary>
        /// Checks if any voiceover audio is currently playing or will start soon in conversations
        /// </summary>
        public static bool IsVoiceoverPlaying()
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
                        MelonLogger.Warning("Could not find bubbleTextInfos field in BubbleTextManager");
                        return false;
                    }
                }

                // Get the list of active bubble texts
                if (bubbleTextInfosField != null)
                {
                    var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;
                    if (bubbleTextInfos == null || bubbleTextInfos.Count == 0)
                    {
                        return false;
                    }

                    // Check each bubble text for audio that is ACTUALLY playing right now
                    foreach (var btInfo in bubbleTextInfos)
                    {
                        if (btInfo == null) continue;

                        Type btInfoType = btInfo.GetType();

                        // Check if audio is ACTUALLY playing via audioRef
                        FieldInfo audioRefField = btInfoType.GetField("audioRef");
                        if (audioRefField != null)
                        {
                            var audioRef = audioRefField.GetValue(btInfo);
                            if (audioRef != null)
                            {
                                MethodInfo getMethod = audioRef.GetType().GetMethod("Get");
                                if (getMethod != null)
                                {
                                    var audioObject = getMethod.Invoke(audioRef, null);
                                    if (audioObject != null)
                                    {
                                        MethodInfo isPlayingMethod = audioObject.GetType().GetMethod("IsPlaying");
                                        if (isPlayingMethod != null)
                                        {
                                            bool isPlaying = (bool)isPlayingMethod.Invoke(audioObject, null);
                                            if (isPlaying)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        // Note: We intentionally do NOT check for pending audio (timeStarted == -1).
                        // Pending bubble texts may wait many seconds behind other bubbles from the
                        // same owner, and blocking TTS for that long is wrong.
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking voiceover status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if there is any voiced conversation audio that is pending (not yet started)
        /// or actively playing in BubbleTextManager. This catches audio that hasn't been
        /// initialized yet due to BubbleTextManager's updateBlocked mechanism.
        /// Use this to prevent TTS from speaking over voiced NPC dialogue.
        /// </summary>
        public static bool HasPendingOrActiveVoicedAudio()
        {
            try
            {
                var btmInstance = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
                if (btmInstance == null) return false;

                if (!fieldInitialized)
                {
                    Type btmType = typeof(BubbleTextManager);
                    bubbleTextInfosField = btmType.GetField("bubbleTextInfos",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInitialized = true;
                }

                if (bubbleTextInfosField == null) return false;

                var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;
                if (bubbleTextInfos == null || bubbleTextInfos.Count == 0) return false;

                foreach (var btInfo in bubbleTextInfos)
                {
                    if (btInfo == null) continue;

                    Type btInfoType = btInfo.GetType();

                    // Check textKind - only care about AudioConversation (voiced NPC dialogue)
                    FieldInfo textKindField = btInfoType.GetField("textKind");
                    if (textKindField == null) continue;
                    object textKindValue = textKindField.GetValue(btInfo);
                    if (textKindValue == null) continue;
                    string textKindName = textKindValue.ToString();

                    if (textKindName != "AudioConversation") continue;

                    // Check if this bubble text has an audio name set (meaning it will play audio)
                    FieldInfo audioNameField = btInfoType.GetField("audioName");
                    if (audioNameField != null)
                    {
                        string audioName = audioNameField.GetValue(btInfo) as string;
                        if (!string.IsNullOrEmpty(audioName) && audioName.Length > 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking pending voiced audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if BubbleTextManager has ANY active conversation-type bubble texts.
        /// This includes DescConversation, AudioConversation, DescPercConversation, etc.
        /// Use this to prevent ConversationState from activating while NPC text is still
        /// being displayed (with or without audio).
        /// </summary>
        public static bool HasActiveConversationBubbles()
        {
            try
            {
                var btmInstance = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
                if (btmInstance == null) return false;

                if (!fieldInitialized)
                {
                    Type btmType = typeof(BubbleTextManager);
                    bubbleTextInfosField = btmType.GetField("bubbleTextInfos",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInitialized = true;
                }

                if (bubbleTextInfosField == null) return false;

                var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;
                if (bubbleTextInfos == null || bubbleTextInfos.Count == 0) return false;

                foreach (var btInfo in bubbleTextInfos)
                {
                    if (btInfo == null) continue;

                    Type btInfoType = btInfo.GetType();

                    FieldInfo textKindField = btInfoType.GetField("textKind");
                    if (textKindField == null) continue;
                    object textKindValue = textKindField.GetValue(btInfo);
                    if (textKindValue == null) continue;
                    string textKindName = textKindValue.ToString();

                    // Any conversation-type bubble text means NPC text is still displaying
                    if (textKindName == "Conversation" ||
                        textKindName == "AudioConversation" ||
                        textKindName == "DescConversation" ||
                        textKindName == "DescPercConversation" ||
                        textKindName == "AsciiArtConversation" ||
                        textKindName == "Epilogue")
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking conversation bubbles: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if BubbleTextManager has any active description-type bubble texts
        /// (DescConversation, DescPercConversation). When present alongside AudioConversation,
        /// it means the current AddText text is the description (read immediately),
        /// not the subtitle for upcoming voiceover (wait).
        /// </summary>
        public static bool HasActiveDescriptionBubbles()
        {
            try
            {
                var btmInstance = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
                if (btmInstance == null) return false;

                if (!fieldInitialized)
                {
                    Type btmType = typeof(BubbleTextManager);
                    bubbleTextInfosField = btmType.GetField("bubbleTextInfos",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    fieldInitialized = true;
                }

                if (bubbleTextInfosField == null) return false;

                var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;
                if (bubbleTextInfos == null || bubbleTextInfos.Count == 0) return false;

                foreach (var btInfo in bubbleTextInfos)
                {
                    if (btInfo == null) continue;

                    Type btInfoType = btInfo.GetType();
                    FieldInfo textKindField = btInfoType.GetField("textKind");
                    if (textKindField == null) continue;
                    object textKindValue = textKindField.GetValue(btInfo);
                    if (textKindValue == null) continue;
                    string textKindName = textKindValue.ToString();

                    if (textKindName == "DescConversation" || textKindName == "DescPercConversation")
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking description bubbles: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the remaining time for currently playing voiceover audio
        /// </summary>
        public static float GetVoiceoverRemainingTime()
        {
            try
            {
                var btmInstance = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
                if (btmInstance == null || bubbleTextInfosField == null)
                {
                    return 0f;
                }

                var bubbleTextInfos = bubbleTextInfosField.GetValue(btmInstance) as System.Collections.IList;
                if (bubbleTextInfos == null || bubbleTextInfos.Count == 0)
                {
                    return 0f;
                }

                float maxRemainingTime = 0f;

                foreach (var btInfo in bubbleTextInfos)
                {
                    if (btInfo == null) continue;

                    Type btInfoType = btInfo.GetType();

                    // Get AudioClipTime and AudioTimeElapsed methods
                    MethodInfo clipTimeMethod = btInfoType.GetMethod("AudioClipTime");
                    MethodInfo timeElapsedMethod = btInfoType.GetMethod("AudioTimeElapsed");

                    if (clipTimeMethod != null && timeElapsedMethod != null)
                    {
                        float clipTime = (float)clipTimeMethod.Invoke(btInfo, null);
                        float elapsed = (float)timeElapsedMethod.Invoke(btInfo, null);
                        float remaining = clipTime - elapsed;

                        if (remaining > maxRemainingTime)
                        {
                            maxRemainingTime = remaining;
                        }
                    }
                }

                return maxRemainingTime;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting voiceover remaining time: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Speaks text with automatic delay if voiceover is playing
        /// </summary>
        public static void SpeakWithVoiceoverDelay(string text, bool interrupt = false, float additionalDelay = 0f)
        {
            float voiceoverRemaining = GetVoiceoverRemainingTime();
            float totalDelay = voiceoverRemaining + additionalDelay;

            if (totalDelay > 0.1f)
            {
                // Start a coroutine to delay the announcement
                MelonCoroutines.Start(DelayedSpeak(text, interrupt, totalDelay));
            }
            else if (interrupt)
            {
                ScreenReaderManager.SpeakInterrupt(text);
            }
            else
            {
                ScreenReaderManager.Speak(text);
            }
        }

        private static IEnumerator DelayedSpeak(string text, bool interrupt, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (interrupt)
                ScreenReaderManager.SpeakInterrupt(text);
            else
                ScreenReaderManager.Speak(text);
        }
    }
}
