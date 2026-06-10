using HarmonyLib;
using UnityEngine;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    // NOTE: InputManager_Update_Patch has been removed.
    // All keyboard input handling is now in Core/ExplorationState.cs
    // which is routed through Core/InputRouter.cs.

    [HarmonyPatch(typeof(InputManager), "SelectNextInteractable",
        new System.Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    public class InputManager_SelectNextInteractable_Patch
    {
        private static InteractableNexus lastSelectedInteractable = null;

        [HarmonyPostfix]
        public static void Postfix(InputManager __instance)
        {
            try
            {
                // Check if an interactable was selected and it's different from the last one
                if (__instance.selectedInteractable != null &&
                    __instance.selectedInteractable != lastSelectedInteractable)
                {
                    lastSelectedInteractable = __instance.selectedInteractable;
                    ModLog.Debug($"Controller selected interactable: {__instance.selectedInteractable.name}");
                    // Controller cycling is intentional selection, so pass isFromCycling: true
                    NavigationManager.AnnounceInteractable(__instance.selectedInteractable, isFromCycling: true);
                }
                else if (__instance.selectedInteractable == null)
                {
                    lastSelectedInteractable = null;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in SelectNextInteractable_Patch: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
    }

    // Diagnostic: trace InteractableTeleporter interaction flow
    [HarmonyPatch(typeof(InteractableTeleporter), "Event_ASI_Poked")]
    public class InteractableTeleporter_Poked_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(InteractableTeleporter __instance)
        {
            ModLog.Debug($"[Teleporter] Event_ASI_Poked called on {__instance.name}, IgnorePokedEvent={__instance.IgnorePokedEvent}, targetTransform={((__instance.targetTransform != null) ? __instance.targetTransform.name : "NULL")}");
        }
    }

    [HarmonyPatch(typeof(InteractableTeleporter), "Activate")]
    public class InteractableTeleporter_Activate_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(InteractableTeleporter __instance)
        {
            ModLog.Debug($"[Teleporter] Activate called on {__instance.name}, followRules={__instance.followRules}, mustActivateBeforeTeleport={__instance.mustActivateBeforeTeleport}, isActive={__instance.isActive}, isTeleporting={InteractableTeleporter.IsTeleporting()}, targetTransform={((__instance.targetTransform != null) ? __instance.targetTransform.name : "NULL")}");
        }
    }

    [HarmonyPatch(typeof(InteractableTeleporter), "DoTeleport")]
    public class InteractableTeleporter_DoTeleport_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(InteractableTeleporter __instance)
        {
            ModLog.Debug($"[Teleporter] DoTeleport started on {__instance.name}, targetTransform={(__instance.targetTransform != null ? __instance.targetTransform.name : "NULL")}, targetPos={(__instance.targetTransform != null ? __instance.targetTransform.position.ToString() : "N/A")}");
        }
    }

    [HarmonyPatch(typeof(AZ10_HiddenShortcut), "Event_ASI_examine")]
    public class AZ10_HiddenShortcut_Examine_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(AZ10_HiddenShortcut __instance)
        {
            try
            {
                ModLog.Debug("[HiddenShortcut] Event_ASI_examine postfix fired");

                LogDoorState("ShortcutDoor", __instance.ShortcutDoor);
                LogDoorState("ShortcutDoor2", __instance.ShortcutDoor2);

                // Register activated doors so IsPerceptionGated bypasses the
                // teleporter destination filter for these runtime-activated doors.
                RegisterDoorIfActive(__instance.ShortcutDoor);
                RegisterDoorIfActive(__instance.ShortcutDoor2);

                // Log party positions for proximity reference
                if (MonoBehaviourSingleton<Game>.HasInstance())
                {
                    var game = MonoBehaviourSingleton<Game>.GetInstance();
                    if (game.party != null)
                    {
                        foreach (var pc in game.party)
                        {
                            if (pc != null && pc.transform != null)
                                ModLog.Debug($"[HiddenShortcut]   Party member {pc.name} at {pc.transform.position}");
                        }
                    }
                }

                // Check how many InteractableNexus are in the global list now
                int total = 0;
                int visible = 0;
                foreach (var nexus in InteractableNexus.interactables)
                {
                    if (nexus == null) continue;
                    total++;
                    if (nexus.isVisible) visible++;
                }
                ModLog.Debug($"[HiddenShortcut] InteractableNexus.interactables: {total} total, {visible} visible");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[HiddenShortcut] Examine patch error: {ex}");
            }
        }

        private static void RegisterDoorIfActive(GameObject door)
        {
            if (door == null || !door.activeInHierarchy) return;
            var nexus = door.GetComponent<InteractableNexus>();
            if (nexus != null)
                FOWHelper.MarkAsRecentlyActivated(nexus);
        }

        private static void LogDoorState(string label, GameObject door)
        {
            if (door == null)
            {
                ModLog.Debug($"[HiddenShortcut] {label}: NULL");
                return;
            }

            bool active = door.activeInHierarchy;
            Vector3 pos = door.transform.position;

            var nexus = door.GetComponent<InteractableNexus>();
            bool hasNexus = nexus != null;
            bool nexusVisible = hasNexus && nexus.isVisible;
            bool nexusHidden = hasNexus && nexus.isHidden;

            bool fowVisible = FOWSystem.instance != null ? FOWSystem.instance.IsVisible(pos) : true;
            bool fowExplored = FOWSystem.instance != null ? FOWSystem.instance.IsExplored(pos) : true;
            bool nearParty = FOWHelper.IsVisibleThroughFOW(pos);

            var teleporter = door.GetComponent<InteractableTeleporter>();
            bool hasTeleporter = teleporter != null;

            ModLog.Debug($"[HiddenShortcut] {label}: active={active}, pos={pos}, hasNexus={hasNexus}, nexusVisible={nexusVisible}, nexusHidden={nexusHidden}, fowVisible={fowVisible}, fowExplored={fowExplored}, passesIsVisibleThroughFOW={nearParty}, hasTeleporter={hasTeleporter}");

            // Check if nexus is in the interactables list
            if (hasNexus)
            {
                bool inList = false;
                foreach (var n in InteractableNexus.interactables)
                {
                    if (n == nexus) { inList = true; break; }
                }
                ModLog.Debug($"[HiddenShortcut] {label}: inInteractablesList={inList}");
            }
        }
    }
}
