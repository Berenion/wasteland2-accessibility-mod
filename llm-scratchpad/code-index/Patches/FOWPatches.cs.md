File: Patches/FOWPatches.cs — Patches FOWSystem.LoadMap to notify FOWHelper when the FOW map is restored from save data, preventing false IsVisible readings during the grace period.

namespace Wasteland2AccessibilityMod.Patches  (line 3)

// Notifies FOWHelper after LoadMap writes explored cells into mBuffer1 (which temporarily makes IsVisible return true everywhere).
[HarmonyPatch(typeof(FOWSystem), "LoadMap")]
class FOWSystem_LoadMap_Patch  (line 14)
    [HarmonyPostfix]
    public static void Postfix()  (line 17)
        // note: calls FOWHelper.NotifyFOWMapLoaded() to set dirty flag consumed by exploration filtering.
