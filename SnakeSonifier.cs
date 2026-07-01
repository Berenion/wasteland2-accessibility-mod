using System;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Procedural-tone audio for the Ranger Citadel Snake Easter egg (see
    /// <see cref="States.ComputerGameState"/>). The Snake "screen" is a raw pixel
    /// texture with no readable text, so the game is sonified instead: a panned,
    /// pitched beep beacon points at the food, and distinct cues mark eating and
    /// imminent collisions.
    ///
    /// There is no tone generator elsewhere in the mod (<see cref="MenuCue"/> only
    /// triggers the game's own audio events), so this builds short sine clips at load
    /// and plays them through a dedicated AudioSource on a persistent GameObject.
    ///
    /// Sonification scheme:
    ///   - pan (left/right) encodes the food's horizontal offset from the snake head,
    ///   - pitch (high/low) encodes its vertical offset (food above = higher),
    ///   - the caller re-triggers the beacon faster as the food gets closer.
    /// All audio is best-effort: any failure logs once and no-ops.
    /// </summary>
    public static class SnakeSonifier
    {
        private const int SampleRate = 44100;

        private static GameObject host;
        private static AudioSource source;
        private static AudioClip beaconClip;   // soft sine, the food beacon
        private static AudioClip eatClip;      // bright high blip, food eaten
        private static AudioClip dangerClip;   // low buzz, lethal cell ahead
        private static bool initFailed;

        private static bool EnsureInit()
        {
            if (source != null) return true;
            if (initFailed) return false;

            try
            {
                host = new GameObject("Wasteland2AccessibilitySnakeAudio");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.hideFlags = HideFlags.HideAndDontSave;

                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f; // 2D so panStereo applies cleanly
                source.bypassEffects = true;
                source.bypassListenerEffects = true;
                source.ignoreListenerVolume = false;

                beaconClip = MakeTone("snakeBeacon", 523f, 0.09f, 0.004f);
                eatClip = MakeTone("snakeEat", 988f, 0.13f, 0.006f);
                dangerClip = MakeTone("snakeDanger", 165f, 0.16f, 0.006f);
                return true;
            }
            catch (Exception ex)
            {
                initFailed = true;
                MelonLogger.Warning($"[SnakeSonifier] Audio init failed, sonification disabled: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds a short mono sine clip with a brief linear attack/release envelope so
        /// the beeps don't click. <paramref name="ramp"/> is the fade length in seconds.
        /// </summary>
        private static AudioClip MakeTone(string name, float freq, float seconds, float ramp)
        {
            int samples = Mathf.Max(1, (int)(SampleRate * seconds));
            int rampSamples = Mathf.Max(1, (int)(SampleRate * ramp));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float env = 1f;
                if (i < rampSamples) env = (float)i / rampSamples;
                else if (i > samples - rampSamples) env = (float)(samples - i) / rampSamples;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate) * env;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Plays one beacon beep for the food. <paramref name="pan"/> is -1 (left) to
        /// 1 (right); <paramref name="pitch"/> shifts the tone (above 1 = higher = food
        /// is above the head); <paramref name="volume"/> is 0..1 (louder = closer).
        /// </summary>
        public static void Beacon(float pan, float pitch, float volume)
        {
            if (!EnsureInit()) return;
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.pitch = Mathf.Clamp(pitch, 0.4f, 3f);
            source.PlayOneShot(beaconClip, Mathf.Clamp01(volume));
        }

        /// <summary>Bright centered blip when the snake eats food.</summary>
        public static void Eat()
        {
            if (!EnsureInit()) return;
            source.panStereo = 0f;
            source.pitch = 1f;
            source.PlayOneShot(eatClip, 0.8f);
        }

        /// <summary>
        /// Low buzz when the cell directly ahead would kill the snake (wall, body, or
        /// obstacle). Panned toward the danger's side for a little extra cueing.
        /// </summary>
        public static void Danger(float pan)
        {
            if (!EnsureInit()) return;
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.pitch = 1f;
            source.PlayOneShot(dangerClip, 0.7f);
        }

        /// <summary>Stops any in-flight cue (called when leaving the Snake screen).</summary>
        public static void Stop()
        {
            if (source != null) source.Stop();
        }
    }
}
