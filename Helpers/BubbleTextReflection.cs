using System.Collections;
using System.Reflection;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Helpers
{
    /// <summary>
    /// Shared reflection cache for <c>BubbleTextManager.bubbleTextInfos</c>
    /// (private <c>List&lt;BubbleTextInfo&gt;</c>).
    ///
    /// Multiple callers (<c>AudioAwareAnnouncementManager</c>, <c>VoiceoverHelper</c>)
    /// iterate this list with their own textKind / audio-state filters; this
    /// helper unifies the FieldInfo lookup so we don't repeat the lazy-init
    /// pattern in every method.
    /// </summary>
    public static class BubbleTextReflection
    {
        private static FieldInfo bubbleTextInfosField;
        private static bool fieldInitialized;

        /// <summary>
        /// Returns the BubbleTextManager's bubbleTextInfos list, or null if the
        /// manager singleton isn't loaded or the field couldn't be resolved.
        /// Iteration order matches the manager's internal list.
        /// </summary>
        public static IList GetBubbleTextInfos()
        {
            if (!MonoBehaviourSingleton<BubbleTextManager>.HasInstance()) return null;
            var btm = MonoBehaviourSingleton<BubbleTextManager>.GetInstance();
            if (btm == null) return null;

            if (!fieldInitialized)
            {
                bubbleTextInfosField = typeof(BubbleTextManager).GetField(
                    "bubbleTextInfos",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                fieldInitialized = true;
                if (bubbleTextInfosField == null)
                    MelonLogger.Warning("[BubbleTextReflection] Could not find BubbleTextManager.bubbleTextInfos");
            }

            if (bubbleTextInfosField == null) return null;
            return bubbleTextInfosField.GetValue(btm) as IList;
        }
    }
}
