using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Stops one physical Enter press from producing two conversation advances.
    ///
    /// The game binds Enter and KeypadEnter to the "Attack Current Target" button:
    ///
    ///     cInput.SetKey("Attack Current Target", "&lt;@keyboard&gt;Return", "&lt;@keyboard&gt;KeypadEnter");
    ///
    /// and BubbleTextManager.Update() treats the RELEASE of that button as a
    /// click-to-continue, alongside the mouse click:
    ///
    ///     bool flag = cInput.GetButtonDown("Fire1") || cInput.GetButtonDown("Controller A")
    ///              || cInput.GetButtonUp("Attack Current Target")
    ///              || cInput.GetButtonUp("Toggle Group Mode");
    ///     ...
    ///     if (wasClicked || flushState != none || ((hasClickToContinue || isCutsceneOn) && flag) || ...)
    ///         -> destroy this bubble and let the Drama emit the next line
    ///
    /// The mod's states act on the key DOWN. A normal tap releases one to three frames
    /// later, and the next dialogue line's bubble is commonly already up by then (the
    /// Drama emits it the frame after the advance), so the game silently advanced past
    /// that line too — killing its voiceover the moment it started. One press, two
    /// advances.
    ///
    /// The existing suppressors can't catch this: they gate InputManager.Update,
    /// UICamera.ProcessOthers and EventManager.Update, whereas BubbleTextManager polls
    /// cInput directly. So the paired release is swallowed at the source instead. The
    /// game does the same thing for its own handlers via InputManager.lockOutButtonUp.
    ///
    /// Only the release is swallowed, and only on the frame it happens. Nothing else in
    /// the game reads the release of "Attack Current Target": every other handler for it
    /// (combat attack, popup/tutorial/save-screen OK) runs off the button DOWN.
    /// </summary>
    public static class KeyReleaseGuard
    {
        /// <summary>The cInput button Enter / KeypadEnter are bound to.</summary>
        private const string AttackCurrentTarget = "Attack Current Target";

        // True between a state consuming an Enter key-down and that key being released.
        private static bool enterPressConsumed;

        // Frame on which the release paired with a consumed press was seen. Compared
        // against Time.frameCount rather than being cleared by InputSuppressor.Reset(),
        // so the window is unambiguously the one frame the game reads that release in
        // (the mod's OnUpdate runs before the game's Update methods).
        private static int swallowEnterFrame = -1;

        /// <summary>
        /// Record that an accessibility state consumed this frame's Enter key-down, so the
        /// matching release must not reach the game.
        /// </summary>
        public static void ConsumeEnterPress()
        {
            enterPressConsumed = true;
        }

        /// <summary>
        /// Called once per frame from InputRouter.ProcessInput(), before the game's Update.
        /// </summary>
        public static void Tick()
        {
            if (enterPressConsumed &&
                (Input.GetKeyUp(KeyCode.Return) || Input.GetKeyUp(KeyCode.KeypadEnter)))
            {
                enterPressConsumed = false;
                swallowEnterFrame = Time.frameCount;
                ModLog.Debug("[KeyReleaseGuard] Swallowing paired Enter release");
            }
        }

        /// <summary>
        /// Resolves cInput.GetButtonUp(string) by name and prefixes it. cInput lives in
        /// Assembly-CSharp-firstpass.dll, which the mod does not reference, so the target is
        /// looked up at runtime and patched manually instead of via [HarmonyPatch].
        /// </summary>
        public static void ApplyPatch(HarmonyLib.Harmony harmony)
        {
            try
            {
                Type cInputType = AccessTools.TypeByName("cInput");
                if (cInputType == null)
                {
                    MelonLogger.Warning("[Core] cInput type not found — paired key-release guard inactive");
                    return;
                }

                MethodInfo target = AccessTools.Method(cInputType, "GetButtonUp", new[] { typeof(string) });
                if (target == null)
                {
                    MelonLogger.Warning("[Core] cInput.GetButtonUp(string) not found — paired key-release guard inactive");
                    return;
                }

                MethodInfo prefix = AccessTools.Method(typeof(KeyReleaseGuard), "GetButtonUpPrefix");
                harmony.Patch(target, new HarmonyMethod(prefix));
                MelonLogger.Msg("[Core] Paired key-release guard applied to cInput.GetButtonUp");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Failed to patch cInput.GetButtonUp: {ex.Message}");
            }
        }

        /// <summary>
        /// Parameter name must match the original (cInput.GetButtonUp(string description)).
        /// </summary>
        private static bool GetButtonUpPrefix(string description, ref bool __result)
        {
            if (description == AttackCurrentTarget && swallowEnterFrame == Time.frameCount)
            {
                __result = false;
                return false; // skip the original — the game must not see this release
            }
            return true;
        }
    }
}
