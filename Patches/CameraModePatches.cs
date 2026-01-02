using HarmonyLib;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patch to announce camera mode changes
    /// Patches: InputManager.ChangeCameraMode(GamepadCameraMode newMode)
    /// </summary>
    [HarmonyPatch(typeof(InputManager), "ChangeCameraMode")]
    public class InputManager_ChangeCameraMode_Patch
    {
        private static GamepadCameraMode oldMode;

        [HarmonyPrefix]
        public static void Prefix(InputManager __instance)
        {
            if (__instance == null) return;

            // Store the current mode before it changes
            oldMode = __instance.GetGamepadCameraMode();
        }

        [HarmonyPostfix]
        public static void Postfix(GamepadCameraMode newMode)
        {
            // Only announce if the mode actually changed
            if (oldMode == newMode) return;

            string modeName = GetCameraModeName(newMode);
            string announcement = $"Camera mode: {modeName}";

            MelonLogger.Msg($"Camera mode changed from {oldMode} to {newMode}");
            ScreenReaderManager.Speak(announcement, interrupt: true);
        }

        private static string GetCameraModeName(GamepadCameraMode mode)
        {
            switch (mode)
            {
                case GamepadCameraMode.rotateAndZoom:
                    return "Rotate and Zoom";
                case GamepadCameraMode.pan:
                    return "Pan";
                case GamepadCameraMode.locked:
                    return "Locked";
                default:
                    return "Unknown";
            }
        }
    }
}
