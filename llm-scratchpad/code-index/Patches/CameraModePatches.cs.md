File: Patches/CameraModePatches.cs — Harmony patch to announce camera mode changes when InputManager.ChangeCameraMode is called.

namespace Wasteland2AccessibilityMod.Patches  (line 4)

// Announces camera mode changes; stores pre-change mode in Prefix and compares in Postfix.
[HarmonyPatch(typeof(InputManager), "ChangeCameraMode")]
class InputManager_ChangeCameraMode_Patch  (line 11)
    private static GamepadCameraMode oldMode  (line 13)

    [HarmonyPrefix]
    public static void Prefix(InputManager __instance)  (line 16)
        // note: captures current camera mode before the change occurs.

    [HarmonyPostfix]
    public static void Postfix(GamepadCameraMode newMode)  (line 25)
        // note: announces new mode only if it differs from oldMode.

    private static string GetCameraModeName(GamepadCameraMode mode)  (line 37)
        // note: maps enum value to human-readable string (Rotate and Zoom, Pan, Locked).
