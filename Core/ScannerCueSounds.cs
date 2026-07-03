using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Short procedural "something appeared" cues for the scanner, one recognisable motif
    /// per scanner category. Built the same way as WallSonification (generated AudioClips
    /// played through a 2D AudioSource) so no clip files ship and each category gets a
    /// distinct pitch/shape rather than a reused game UI blip.
    ///
    /// Categories map to motifs (a small note sequence, frequencies in Hz):
    ///   Characters - two falling notes  (someone is there)
    ///   Containers - single mid note
    ///   Objects    - single lower-mid note
    ///   Exits      - two rising notes, wide interval (a way out)
    ///   Examine    - single high note
    ///   Loot       - two rising notes (a small reward flourish)
    ///   Misc       - single low note (the generic "something else" cue)
    /// Party (PCs) has no cue and never reaches here.
    /// </summary>
    public static class ScannerCueSounds
    {
        // Per-note length and overall gain. Kept short and quiet so a cue is a soft blip,
        // not an alert.
        private const float PlayVolume = 0.4f;

        /// <summary>Timbre of a cue — a strong distinguishing axis on its own.</summary>
        private enum Wave { Sine, Square, Triangle }

        /// <summary>A cue: a timbre plus a short note sequence (frequencies in Hz).</summary>
        private sealed class Cue
        {
            public readonly Wave Wave;
            public readonly float NoteSeconds;
            public readonly float[] Freqs;

            public Cue(Wave wave, float noteSeconds, params float[] freqs)
            {
                Wave = wave;
                NoteSeconds = noteSeconds;
                Freqs = freqs;
            }
        }

        // Each cue is deliberately distinct along several axes at once — timbre, note
        // count/rhythm, and pitch contour — not just pitch, so they don't blur together:
        //   Characters - triangle, two falling notes (mellow "someone")
        //   Containers - square,   one low long note (a heavy thunk)
        //   Objects    - square,   two flat repeated notes (mechanical double-beep)
        //   Exits      - sine,     two falling notes, slow (a doorbell ding-dong)
        //   Examine    - sine,     high up-then-down arch (a curious blip)
        //   Loot       - sine,     two rising notes (the reward flourish)
        //   Misc       - triangle, one plain mid note (the neutral "something else")
        private static readonly Dictionary<InteractableCategory, Cue> Cues =
            new Dictionary<InteractableCategory, Cue>
            {
                { InteractableCategory.Characters, new Cue(Wave.Triangle, 0.11f, 659.25f, 440.00f) },          // E5 -> A4
                { InteractableCategory.Containers, new Cue(Wave.Square,   0.17f, 155.56f) },                   // Eb3 thunk
                { InteractableCategory.Objects,    new Cue(Wave.Square,   0.07f, 493.88f, 493.88f) },          // B4 B4
                { InteractableCategory.Exits,      new Cue(Wave.Sine,     0.15f, 698.46f, 523.25f) },          // F5 -> C5 doorbell
                { InteractableCategory.Examine,    new Cue(Wave.Sine,     0.07f, 880.00f, 1108.73f, 880.00f) },// A5 C#6 A5
                { InteractableCategory.Loot,       new Cue(Wave.Sine,     0.09f, 783.99f, 1046.50f) },         // G5 -> C6
                { InteractableCategory.Misc,       new Cue(Wave.Triangle, 0.13f, 587.33f) },                   // D5
            };

        private static readonly Dictionary<InteractableCategory, AudioClip> clips =
            new Dictionary<InteractableCategory, AudioClip>();

        /// <summary>One glossary entry: the cue's category, spoken name, and what it means.</summary>
        public sealed class CueInfo
        {
            public readonly InteractableCategory Category;
            public readonly string Name;
            public readonly string Description;

            public CueInfo(InteractableCategory category, string name, string description)
            {
                Category = category;
                Name = name;
                Description = description;
            }
        }

        /// <summary>
        /// The cues, in presentation order, for the Shift+S sound glossary. Single source
        /// of truth for what each sound means; keep in step with <see cref="Cues"/>.
        /// (Wall-scanner tones are intentionally omitted — that feature isn't ready.)
        /// </summary>
        public static readonly List<CueInfo> Glossary = new List<CueInfo>
        {
            new CueInfo(InteractableCategory.Characters, "Characters", "A person, friendly or hostile, has come into view."),
            new CueInfo(InteractableCategory.Containers, "Containers", "A lootable container or locker is in range."),
            new CueInfo(InteractableCategory.Loot, "Loot", "Dropped items or a loot pile on the ground."),
            new CueInfo(InteractableCategory.Objects, "Objects", "An interactive object such as a door, switch, or computer."),
            new CueInfo(InteractableCategory.Exits, "Exits", "A map exit or area transition."),
            new CueInfo(InteractableCategory.Examine, "Examine", "Something you can examine for a description or clue."),
            new CueInfo(InteractableCategory.Misc, "Miscellaneous", "Something that fits no other category, such as a dig spot or teleporter."),
        };

        private static GameObject root;
        private static AudioSource source;
        private static bool audioReady;

        /// <summary>True when the category has a cue (i.e. not Party / All).</summary>
        public static bool HasCue(InteractableCategory category)
        {
            return Cues.ContainsKey(category);
        }

        /// <summary>Play the cue for a category. No-op for categories without a cue.</summary>
        public static void Play(InteractableCategory category)
        {
            Cue cue;
            if (!Cues.TryGetValue(category, out cue)) return;
            try
            {
                EnsureAudio();
                if (!audioReady) return;

                AudioClip clip;
                if (!clips.TryGetValue(category, out clip) || clip == null)
                {
                    clip = GenerateCueClip(cue);
                    clips[category] = clip;
                }
                source.PlayOneShot(clip, PlayVolume);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ScannerCueSounds] Play({category}) failed: {ex.Message}");
            }
        }

        private static void EnsureAudio()
        {
            if (audioReady) return;

            root = new GameObject("ScannerCueSoundsRoot");
            UnityEngine.Object.DontDestroyOnLoad(root);

            source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // 2D
            source.volume = 1f;       // per-shot volume is passed to PlayOneShot

            audioReady = true;
            ModLog.Debug("[ScannerCueSounds] Audio initialized");
        }

        /// <summary>
        /// Builds a one-shot clip that plays the cue's notes back to back in its timbre,
        /// each with a short attack/release envelope so the blip is click-free (and so
        /// repeated same-pitch notes read as separate beeps).
        /// </summary>
        private static AudioClip GenerateCueClip(Cue cue)
        {
            const int sampleRate = 44100;
            int noteSamples = Mathf.RoundToInt(sampleRate * cue.NoteSeconds);
            int total = noteSamples * Mathf.Max(1, cue.Freqs.Length);
            float[] data = new float[total];

            // Envelope lengths (in samples) — quick fade in, longer fade out. The release
            // brings each note to zero before the next starts, separating repeated pitches.
            int attack = Mathf.Max(1, Mathf.RoundToInt(sampleRate * 0.005f));
            int release = Mathf.Max(1, Mathf.RoundToInt(sampleRate * 0.030f));

            // Square is harmonically rich (louder/harsher), so scale it back to sit level
            // with the sine/triangle cues.
            float amp = cue.Wave == Wave.Square ? 0.32f : (cue.Wave == Wave.Triangle ? 0.55f : 0.6f);

            int w = 0;
            for (int n = 0; n < cue.Freqs.Length; n++)
            {
                double phase = 0.0;
                double increment = 2.0 * Math.PI * cue.Freqs[n] / sampleRate;
                for (int i = 0; i < noteSamples; i++)
                {
                    float env = 1f;
                    if (i < attack) env = (float)i / attack;
                    else if (i > noteSamples - release) env = (float)(noteSamples - i) / release;

                    data[w++] = Sample(cue.Wave, phase) * amp * env;
                    phase += increment;
                }
            }

            AudioClip clip = AudioClip.Create("ScannerCue", total, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>One waveform sample for the given phase (radians).</summary>
        private static float Sample(Wave wave, double phase)
        {
            switch (wave)
            {
                case Wave.Square:
                    return Math.Sin(phase) >= 0.0 ? 1f : -1f;
                case Wave.Triangle:
                {
                    // Normalized phase 0..1 -> triangle in -1..1.
                    double t = (phase / (2.0 * Math.PI)) % 1.0;
                    if (t < 0) t += 1.0;
                    return (float)(4.0 * Math.Abs(t - 0.5) - 1.0);
                }
                default:
                    return (float)Math.Sin(phase);
            }
        }
    }
}
