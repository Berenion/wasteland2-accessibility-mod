using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Locks camera rotation so that Up = North always.
    /// When locked, prevents camera rotation from Q/E keys, right-stick, etc.
    /// Patches CameraControllerSpline.Update() to enforce fixed rotation after
    /// the game's rotation logic runs.
    /// </summary>
    public static class CameraLock
    {
        /// <summary>
        /// Whether camera rotation is currently locked.
        /// </summary>
        public static bool IsLocked { get; set; } = true;

        // The Y-axis rotation to use when locked (0 = North facing, camera looks south toward player)
        private static float lockedYRotation = 0f;
        private static bool initialized = false;

        public static void Initialize()
        {
            initialized = true;
            MelonLogger.Msg($"[CameraLock] Initialized - Locked: {IsLocked}");
        }

        /// <summary>
        /// Toggle camera lock on/off. Announces state change via screen reader.
        /// </summary>
        public static void Toggle()
        {
            IsLocked = !IsLocked;

            if (IsLocked)
            {
                CaptureCurrentRotation();
                ScreenReaderManager.SpeakInterrupt("Camera locked");
            }
            else
            {
                ScreenReaderManager.SpeakInterrupt("Camera unlocked");
            }

            MelonLogger.Msg($"[CameraLock] Toggled - Locked: {IsLocked}");
        }

        /// <summary>
        /// Capture the current camera Y rotation as the locked rotation.
        /// Called when locking to preserve current facing direction.
        /// </summary>
        public static void CaptureCurrentRotation()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var game = MonoBehaviourSingleton<Game>.GetInstance();
            if (game.cameraController != null)
            {
                lockedYRotation = game.cameraController.transform.eulerAngles.y;
                MelonLogger.Msg($"[CameraLock] Captured rotation: {lockedYRotation}");
            }
        }

        /// <summary>
        /// Reset camera to face North (Y rotation = 0).
        /// </summary>
        public static void ResetToNorth()
        {
            lockedYRotation = 0f;

            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

            var game = MonoBehaviourSingleton<Game>.GetInstance();
            if (game.cameraController != null)
            {
                Vector3 euler = game.cameraController.transform.eulerAngles;
                game.cameraController.transform.eulerAngles = new Vector3(euler.x, 0f, euler.z);
            }

            MelonLogger.Msg("[CameraLock] Reset to North");
        }

        /// <summary>
        /// Get the locked Y rotation value.
        /// </summary>
        public static float LockedYRotation => lockedYRotation;

        public static bool IsInitialized => initialized;
    }

    /// <summary>
    /// POSTFIX on CameraControllerSpline.Update() to enforce camera lock.
    /// Runs after the game's camera logic (including rotation at lines 423-446).
    /// Forces the camera transform rotation back to the locked value.
    /// </summary>
    [HarmonyPatch(typeof(CameraControllerSpline), "Update")]
    public class CameraControllerSpline_RotationLock
    {
        [HarmonyPostfix]
        public static void Postfix(CameraControllerSpline __instance)
        {
            if (!CameraLock.IsLocked || !CameraLock.IsInitialized) return;

            // Force rotation to locked Y value, preserving X and Z
            Vector3 currentEuler = __instance.transform.eulerAngles;
            if (Mathf.Abs(currentEuler.y - CameraLock.LockedYRotation) > 0.01f)
            {
                __instance.transform.eulerAngles = new Vector3(
                    currentEuler.x,
                    CameraLock.LockedYRotation,
                    currentEuler.z
                );
            }

            // Also zero out rotation input to prevent the game from accumulating rotation state
            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            if (inputManager != null)
            {
                Vector4 cm = inputManager.cameraMove;
                if (cm.z != 0f || cm.w != 0f)
                {
                    inputManager.cameraMove = new Vector4(cm.x, cm.y, 0f, 0f);
                }
            }
        }
    }
}
