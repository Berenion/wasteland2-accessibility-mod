using System.Reflection;
using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Short, non-speech audio cues for menu navigation. Right now this is just the
    /// "wrap" cue: a soft tick played when arrowing past the first/last item loops
    /// back to the other end, so the player can tell they've cycled the whole list
    /// without counting items.
    ///
    /// Plays through the game's own AudioManager (the Clockstone audio toolkit) rather
    /// than bundling a clip. We try a short list of generic UI triggers and play the
    /// first one IsValidAudioID accepts, since which bank is loaded varies by screen;
    /// if none are valid (or the AudioManager isn't up yet) it silently no-ops.
    /// </summary>
    public static class MenuCue
    {
        // Generic UI tick triggers, tried in order. Validity is context-dependent
        // (the controller for a given trigger may not be loaded on every screen), so
        // we re-check each call and play the first that's currently valid.
        private static readonly string[] WrapTriggers =
        {
            "CharacterMenu_Tab_Hovered",
            "ButtonOptions",
            "HotkeyButton_Hover",
            "CharacterCreation_Randomize_Hovered",
            "PopupMenu_Open",
        };

        // AudioManager.Play(string) returns AudioObject, which lives in
        // Assembly-CSharp-firstpass — an assembly the mod doesn't reference. Invoking it
        // by reflection keeps that return type out of our compile-time surface so we don't
        // have to add (and ship) another reference just to discard the result.
        private static MethodInfo playMethod;

        public static void PlayWrap()
        {
            try
            {
                if (!MonoBehaviourSingleton<AudioManager>.HasInstance()) return;

                if (playMethod == null)
                    playMethod = typeof(AudioManager).GetMethod("Play", new[] { typeof(string) });
                if (playMethod == null) return;

                foreach (string trigger in WrapTriggers)
                {
                    if (AudioManager.IsValidAudioID(trigger))
                    {
                        playMethod.Invoke(null, new object[] { trigger });
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[MenuCue] PlayWrap failed: {ex.Message}");
            }
        }
    }
}
