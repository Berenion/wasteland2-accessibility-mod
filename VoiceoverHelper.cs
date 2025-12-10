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

                    // Check each bubble text for active audio or audio that will play
                    foreach (var btInfo in bubbleTextInfos)
                    {
                        if (btInfo == null) continue;

                        Type btInfoType = btInfo.GetType();

                        // Check if this is a subtitle to audio (has voiceover)
                        FieldInfo isSubtitleField = btInfoType.GetField("isSubtitleToAudio");
                        if (isSubtitleField != null)
                        {
                            bool isSubtitle = (bool)isSubtitleField.GetValue(btInfo);
                            if (isSubtitle)
                            {
                                // This text has associated audio
                                return true;
                            }
                        }

                        // Also check if audio is currently playing
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
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
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
            else
            {
                // Speak immediately
                ScreenReaderManager.Speak(text, interrupt);
            }
        }

        private static IEnumerator DelayedSpeak(string text, bool interrupt, float delay)
        {
            yield return new WaitForSeconds(delay);
            ScreenReaderManager.Speak(text, interrupt);
        }
    }
}
