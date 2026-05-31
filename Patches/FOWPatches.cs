using HarmonyLib;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Marks FOW as dirty whenever the game restores FOW state from save data.
    /// Game.cs:980 calls FOWSystem.LoadMap inside a coroutine that runs AFTER the
    /// Unity sceneLoaded event, so hooking OnSceneWasLoaded on the mod side races
    /// the grace period. Hooking LoadMap directly catches the real stomp point:
    /// LoadMap writes r=255 into mBuffer1 for every explored cell (FOWSystem.cs:851),
    /// which makes IsVisible(pos) return true everywhere until UpdateBuffer runs.
    /// </summary>
    [HarmonyPatch(typeof(FOWSystem), "LoadMap")]
    public class FOWSystem_LoadMap_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            FOWHelper.NotifyFOWMapLoaded();
        }
    }
}
