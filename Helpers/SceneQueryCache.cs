using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Per-frame memoization of expensive scene queries (FindObjectOfType /
    /// FindObjectsOfType). In Unity 5.2 these are uncached linear scans of every
    /// loaded object. The input router polls every state's IsActive each frame, and
    /// while a menu is open several states query the same types — so a single menu
    /// frame triggers a dozen-plus redundant scans. That drops the menu frame rate
    /// enough to visibly lag keyboard feedback (input is polled once per rendered
    /// frame), which is what makes the pause and save/load screens feel sluggish.
    ///
    /// The cache is keyed on Time.frameCount and reset automatically when the frame
    /// advances. Within a single frame the scene graph is not mutated between these
    /// read-only IsActive checks, so a cached result is identical to a fresh
    /// FindObjectOfType call. Time.frameCount keeps advancing while paused
    /// (timeScale = 0), so menus invalidate the cache correctly too.
    /// </summary>
    public static class SceneQueryCache
    {
        private static int cachedFrame = -1;
        private static readonly Dictionary<Type, UnityEngine.Object> singletons =
            new Dictionary<Type, UnityEngine.Object>();
        private static GUIScreen[] guiScreens;

        private static void EnsureCurrentFrame()
        {
            int frame = Time.frameCount;
            if (frame != cachedFrame)
            {
                cachedFrame = frame;
                singletons.Clear();
                guiScreens = null;
            }
        }

        /// <summary>
        /// Frame-cached equivalent of UnityEngine.Object.FindObjectOfType&lt;T&gt;().
        /// Repeated calls for the same type within one frame return the same instance
        /// (including a cached null when none exists). A result that gets destroyed
        /// later in the frame still compares == null via Unity's overloaded operator,
        /// so callers' "!= null && activeInHierarchy" guards stay correct.
        /// </summary>
        public static T Find<T>() where T : UnityEngine.Object
        {
            EnsureCurrentFrame();
            Type key = typeof(T);
            UnityEngine.Object cached;
            if (!singletons.TryGetValue(key, out cached))
            {
                cached = UnityEngine.Object.FindObjectOfType<T>();
                singletons[key] = cached;
            }
            return cached as T;
        }

        /// <summary>
        /// Frame-cached equivalent of UnityEngine.Object.FindObjectsOfType&lt;GUIScreen&gt;().
        /// Callers must treat the returned array as read-only.
        /// </summary>
        public static GUIScreen[] GUIScreens()
        {
            EnsureCurrentFrame();
            if (guiScreens == null)
                guiScreens = UnityEngine.Object.FindObjectsOfType<GUIScreen>();
            return guiScreens;
        }
    }
}
