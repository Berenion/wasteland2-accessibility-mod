using System;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Prototype "wall echolocation" for layout perception. When enabled, the map
    /// review cursor (MapCursorState) walks the navigation grid in the four cardinal
    /// directions and hands this class the number of open tiles before a wall in each
    /// direction. We drive one looping tone per direction:
    ///   - volume rises as the wall gets closer
    ///   - pitch rises as the wall gets closer (within a per-direction register)
    ///   - stereo pan separates east (right) from west (left)
    /// North/south share centre pan, so they are separated by register instead:
    /// north sits in a higher register, south in a lower one.
    ///
    /// Detection uses grid neighbour connectivity (a wall is where a tile has no
    /// walkable neighbour in that direction) rather than physics raycasts, so it
    /// finds every wall the game's pathfinding knows about and never pops between
    /// tiles. Updates happen per cursor move, not every frame.
    /// </summary>
    public static class WallSonification
    {
        /// <summary>Probe depth in tiles. A wall farther than this is "not heard".</summary>
        public const int MaxTiles = 20;

        // Stereo pan per direction (-1 left .. +1 right). Index order N, E, S, W.
        // N/S centred — they are told apart by register, not pan.
        private static readonly float[] Pans = { 0f, 0.9f, 0f, -0.9f };

        // Per-direction pitch register multiplier. East/West share a register and
        // are told apart by pan; North/South are told apart by register. Kept gentle
        // so the register shift between directions isn't jarring.
        private static readonly float[] Register = { 1.25f, 1.0f, 0.8f, 1.0f };

        private const float MaxVolume = 0.5f;
        private const float NearPitchFactor = 1.6f; // pitch factor when wall is adjacent
        private const float FarPitchFactor = 0.85f; // pitch factor at MaxTiles

        private static bool enabled;
        private static bool audioReady;
        private static GameObject root;
        private static AudioSource[] sources;
        private static AudioClip toneClip;

        public static bool Enabled => enabled;

        /// <summary>Flip the feature on/off and announce the new state.</summary>
        public static void Toggle()
        {
            if (enabled) Disable();
            else Enable();
        }

        public static void Enable()
        {
            if (enabled) return;
            EnsureAudio();
            enabled = true;
            ScreenReaderManager.SpeakInterrupt("Wall tones on");
            MelonLogger.Msg("[WallSonification] Enabled");
        }

        public static void Disable()
        {
            enabled = false;
            SilenceAll();
            ScreenReaderManager.SpeakInterrupt("Wall tones off");
            MelonLogger.Msg("[WallSonification] Disabled");
        }

        /// <summary>
        /// Update each looping tone from per-direction wall distances, measured in
        /// open tiles before the wall. Index order N, E, S, W. A value of
        /// <see cref="MaxTiles"/> or more means "no wall within range" (silent).
        /// No-op when disabled.
        /// </summary>
        public static void UpdateFromTileDistances(float[] openTiles)
        {
            if (!enabled) return;
            if (!audioReady) return;
            if (openTiles == null || openTiles.Length < 4) return;

            for (int i = 0; i < 4; i++)
            {
                AudioSource src = sources[i];
                if (src == null) continue;

                float tiles = openTiles[i];
                if (tiles >= MaxTiles)
                {
                    // No wall within range in this direction — go silent.
                    if (src.isPlaying) src.Stop();
                    src.volume = 0f;
                    continue;
                }

                float closeness = Mathf.Clamp01(1f - tiles / MaxTiles);
                src.volume = MaxVolume * closeness;
                src.pitch = Register[i] * Mathf.Lerp(FarPitchFactor, NearPitchFactor, closeness);
                src.panStereo = Pans[i];
                if (!src.isPlaying) src.Play();
            }
        }

        private static void SilenceAll()
        {
            if (sources == null) return;
            foreach (var src in sources)
            {
                if (src != null && src.isPlaying) src.Stop();
            }
        }

        private static void EnsureAudio()
        {
            if (audioReady) return;

            if (toneClip == null)
                toneClip = GenerateSineClip(440f, 1f);

            root = new GameObject("WallSonificationRoot");
            UnityEngine.Object.DontDestroyOnLoad(root);

            sources = new AudioSource[4];
            for (int i = 0; i < 4; i++)
            {
                AudioSource src = root.AddComponent<AudioSource>();
                src.clip = toneClip;
                src.loop = true;
                src.playOnAwake = false;
                src.spatialBlend = 0f;   // 2D — we set pan manually, ignore the camera
                src.volume = 0f;
                src.panStereo = Pans[i];
                sources[i] = src;
            }

            audioReady = true;
            MelonLogger.Msg("[WallSonification] Audio initialized");
        }

        /// <summary>
        /// Builds a seamless looping sine AudioClip. At 44100 Hz a 1-second clip of a
        /// 440 Hz tone holds exactly 440 whole cycles, so the loop point is click-free.
        /// </summary>
        private static AudioClip GenerateSineClip(float frequencyHz, float seconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * seconds);
            float[] data = new float[sampleCount];

            double phase = 0.0;
            double increment = 2.0 * Math.PI * frequencyHz / sampleRate;
            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = (float)Math.Sin(phase) * 0.6f;
                phase += increment;
            }

            AudioClip clip = AudioClip.Create("WallTone", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
