using HarmonyLib;
using UnityEngine;
using MelonLoader;

namespace Wasteland2AccessibilityMod.Patches
{
    /// <summary>
    /// Tracks when keyboard navigation is used to suppress hover announcements temporarily.
    /// Shared between ExplorationState and passive announcement patches.
    /// </summary>
    internal static class NavigationState
    {
        public static float lastKeyboardNavigationTime = 0f;
        public const float KEYBOARD_NAVIGATION_SUPPRESS_HOVER = 1.5f;
    }

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
                    MelonLogger.Msg($"Controller selected interactable: {__instance.selectedInteractable.name}");
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

    /// <summary>
    /// Patch for Highlight.MouseOver - announces interactables when mouse hovers over them
    /// </summary>
    [HarmonyPatch(typeof(Highlight), "MouseOver")]
    public class Highlight_MouseOver_Patch
    {
        private static InteractableNexus lastHoveredInteractable = null;
        private static float lastHoverTime = 0f;
        private const float HOVER_COOLDOWN = 1.0f; // Prevent announcement spam
        private const float INTERACT_RADIUS = 5f; // Conservative radius for hover announcements (game uses 8f for detection)

        [HarmonyPostfix]
        public static void Postfix(Highlight __instance)
        {
            try
            {
                // Only announce in exploration mode
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

                Game game = MonoBehaviourSingleton<Game>.GetInstance();
                if (game.state != GameState.Gameplay && game.state != GameState.RandomEncounter) return;

                // Don't announce during conversations, cutscenes, or combat
                if (Drama.isConversationOn || Drama.isCutsceneOn) return;
                if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return;

                // Suppress hover announcements if keyboard navigation was used recently
                float timeSinceKeyboardNav = Time.time - NavigationState.lastKeyboardNavigationTime;
                if (timeSinceKeyboardNav < NavigationState.KEYBOARD_NAVIGATION_SUPPRESS_HOVER)
                {
                    return; // Don't announce hovers while user is navigating with keyboard
                }

                // Get the InteractableNexus component
                InteractableNexus nexus = __instance.GetComponent<InteractableNexus>();
                if (nexus == null || !nexus.isVisible) return;

                // Check distance - only announce if within actual interact range (8 meters)
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                if (inputManager == null) return;

                PC player = inputManager.GetFirstSelectedPlayer();
                if (player == null) return;

                float distance = Vector3.Distance(nexus.InstigatePoint, player.transform.position);
                if (distance >= INTERACT_RADIUS) return;

                // Prevent announcement spam - only announce if it's a different object or enough time has passed
                float currentTime = Time.time;
                if (nexus != lastHoveredInteractable || (currentTime - lastHoverTime) > HOVER_COOLDOWN)
                {
                    lastHoveredInteractable = nexus;
                    lastHoverTime = currentTime;

                    MelonLogger.Msg($"Mouse hover on interactable: {nexus.name} at {distance:F1} meters");
                    NavigationManager.AnnounceInteractable(nexus);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in Highlight_MouseOver_Patch: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
    }

    /// <summary>
    /// Patch for Highlight.GamepadOver - announces interactables when controller focus moves over them
    /// </summary>
    [HarmonyPatch(typeof(Highlight), "GamepadOver")]
    public class Highlight_GamepadOver_Patch
    {
        private static InteractableNexus lastGamepadInteractable = null;
        private static float lastGamepadTime = 0f;
        private const float GAMEPAD_COOLDOWN = 1.0f; // Prevent announcement spam
        private const float INTERACT_RADIUS = 5f; // Conservative radius for gamepad announcements (game uses 8f for detection)

        [HarmonyPostfix]
        public static void Postfix(Highlight __instance)
        {
            try
            {
                // Only announce in exploration mode
                if (!MonoBehaviourSingleton<Game>.HasInstance()) return;

                Game game = MonoBehaviourSingleton<Game>.GetInstance();
                if (game.state != GameState.Gameplay && game.state != GameState.RandomEncounter) return;

                // Don't announce during conversations, cutscenes, or combat
                if (Drama.isConversationOn || Drama.isCutsceneOn) return;
                if (MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return;

                // Suppress gamepad announcements if keyboard navigation was used recently
                float timeSinceKeyboardNav = Time.time - NavigationState.lastKeyboardNavigationTime;
                if (timeSinceKeyboardNav < NavigationState.KEYBOARD_NAVIGATION_SUPPRESS_HOVER)
                {
                    return; // Don't announce gamepad focus while user is navigating with keyboard
                }

                // Get the InteractableNexus component
                InteractableNexus nexus = __instance.GetComponent<InteractableNexus>();
                if (nexus == null || !nexus.isVisible) return;

                // Check distance - only announce if within actual interact range (8 meters)
                var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
                if (inputManager == null) return;

                PC player = inputManager.GetFirstSelectedPlayer();
                if (player == null) return;

                float distance = Vector3.Distance(nexus.InstigatePoint, player.transform.position);
                if (distance >= INTERACT_RADIUS) return;

                // Prevent announcement spam - only announce if it's a different object or enough time has passed
                float currentTime = Time.time;
                if (nexus != lastGamepadInteractable || (currentTime - lastGamepadTime) > GAMEPAD_COOLDOWN)
                {
                    lastGamepadInteractable = nexus;
                    lastGamepadTime = currentTime;

                    MelonLogger.Msg($"Controller focus on interactable: {nexus.name} at {distance:F1} meters");
                    NavigationManager.AnnounceInteractable(nexus);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error in Highlight_GamepadOver_Patch: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
    }
}
