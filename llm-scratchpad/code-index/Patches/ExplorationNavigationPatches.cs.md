File: Patches/ExplorationNavigationPatches.cs — Harmony patches for exploration-mode navigation: interactable cycling, teleporter diagnostics, and AZ10 hidden shortcut door registration.

namespace Wasteland2AccessibilityMod.Patches  (line 7)

// Announces selected interactable when the controller cycles to a new one via SelectNextInteractable.
[HarmonyPatch(typeof(InputManager), "SelectNextInteractable", new System.Type[] { typeof(bool), typeof(bool), typeof(bool) })]
class InputManager_SelectNextInteractable_Patch  (line 12)
    private static InteractableNexus lastSelectedInteractable  (line 15)

    [HarmonyPostfix]
    public static void Postfix(InputManager __instance)  (line 18)
        // note: calls NavigationManager.AnnounceInteractable with isFromCycling=true.

// Diagnostic: logs Event_ASI_Poked call details for InteractableTeleporter.
[HarmonyPatch(typeof(InteractableTeleporter), "Event_ASI_Poked")]
class InteractableTeleporter_Poked_Patch  (line 46)
    [HarmonyPrefix]
    public static void Prefix(InteractableTeleporter __instance)  (line 48)

// Diagnostic: logs Activate call details including followRules, mustActivate, and teleport state.
[HarmonyPatch(typeof(InteractableTeleporter), "Activate")]
class InteractableTeleporter_Activate_Patch  (line 56)
    [HarmonyPrefix]
    public static void Prefix(InteractableTeleporter __instance)  (line 58)

// Diagnostic: logs DoTeleport start including target transform and position.
[HarmonyPatch(typeof(InteractableTeleporter), "DoTeleport")]
class InteractableTeleporter_DoTeleport_Patch  (line 66)
    [HarmonyPrefix]
    public static void Prefix(InteractableTeleporter __instance)  (line 68)

// Registers activated shortcut doors with FOWHelper and logs full nexus/FOW/teleporter state for both doors.
[HarmonyPatch(typeof(AZ10_HiddenShortcut), "Event_ASI_examine")]
class AZ10_HiddenShortcut_Examine_Patch  (line 76)
    [HarmonyPostfix]
    public static void Postfix(AZ10_HiddenShortcut __instance)  (line 78)
        // note: calls FOWHelper.MarkAsRecentlyActivated on active doors so IsPerceptionGated bypasses destination filter.

    private static void RegisterDoorIfActive(GameObject door)  (line 124)
    private static void LogDoorState(string label, GameObject door)  (line 132)
        // note: logs active, pos, nexus visibility/hidden, FOW visible/explored, teleporter presence, and inInteractablesList.
