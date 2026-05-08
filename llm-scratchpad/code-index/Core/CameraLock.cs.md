File: Core/CameraLock.cs — locks camera Y rotation so Up = North; Harmony-patches CameraControllerSpline.Update to enforce it.

namespace Wasteland2AccessibilityMod.Core  (line 5)

static class CameraLock  (line 13)
    // Locks camera rotation so that Up = North always; patches CameraControllerSpline.Update to enforce fixed Y rotation.

    public static bool IsLocked { get; set; }  (line 18)
    private static float lockedYRotation  (line 21)
    private static bool initialized  (line 22)

    public static void Initialize()  (line 24)
    // Toggles lock on/off; if locking, captures current rotation and announces via screen reader.
    public static void Toggle()  (line 33)
    // Reads the camera controller's current eulerAngles.y and stores it as the locked rotation.
    public static void CaptureCurrentRotation()  (line 54)
    // Resets lockedYRotation to 0 and immediately forces the camera transform to face North.
    public static void ResetToNorth()  (line 69)

    public static float LockedYRotation { get; }  (line 88)
    public static bool IsInitialized { get; }  (line 90)


[HarmonyPatch(typeof(CameraControllerSpline), "Update")]
class CameraControllerSpline_RotationLock  (line 99)
    // POSTFIX on CameraControllerSpline.Update; enforces locked Y rotation and zeroes out cameraMove rotation components (z/w) to prevent game state accumulation.

    public static void Postfix(CameraControllerSpline __instance)  (line 102)
        // note: also writes to InputManager.cameraMove to zero rotation input so the game doesn't re-accumulate rotational state.
