using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod
{
    public static class NavigationManager
    {
        private static List<InteractableNexus> filteredInteractables = new List<InteractableNexus>();
        private static int currentIndex = -1;
        private static string lastAnnouncement = "";
        private static InteractableNexus lastAnnouncedInteractable = null;

        // Separate tracking for keyboard-selected interactable (via [ ] cycling)
        // This is NOT overwritten by proximity announcements
        private static InteractableNexus selectedInteractable = null;

        public static void CycleNext()
        {
            UpdateFilteredList();

            if (filteredInteractables.Count == 0)
            {
                ScreenReaderManager.Speak("No interactables nearby", interrupt: true);
                currentIndex = -1;
                return;
            }

            // Increment index with wrap-around
            currentIndex = (currentIndex + 1) % filteredInteractables.Count;

            SelectAndAnnounce(currentIndex);
        }

        public static void CyclePrevious()
        {
            UpdateFilteredList();

            if (filteredInteractables.Count == 0)
            {
                ScreenReaderManager.Speak("No interactables nearby", interrupt: true);
                currentIndex = -1;
                return;
            }

            // Decrement index with wrap-around
            currentIndex--;
            if (currentIndex < 0) currentIndex = filteredInteractables.Count - 1;

            SelectAndAnnounce(currentIndex);
        }

        public static void RepeatLastAnnouncement()
        {
            // Use selectedInteractable (from cycling) NOT lastAnnouncedInteractable (from proximity)
            if (selectedInteractable == null)
            {
                ScreenReaderManager.Speak("No interactable selected", interrupt: true);
                return;
            }

            // Refresh the list to get current state
            UpdateFilteredList();

            // Try to find the selected interactable in the new list
            int newIndex = filteredInteractables.IndexOf(selectedInteractable);

            if (newIndex >= 0)
            {
                // Found it - update our index and re-announce with fresh distance/direction
                currentIndex = newIndex;
                AnnounceInteractable(selectedInteractable, isFromCycling: true);
            }
            else
            {
                // Interactable no longer available (out of range or not visible)
                ScreenReaderManager.Speak("Previously selected interactable is no longer available", interrupt: true);
                selectedInteractable = null;
                lastAnnouncedInteractable = null;
                lastAnnouncement = "";
                currentIndex = -1;
            }
        }

        /// <summary>
        /// Announces an interactable with name, distance, and direction.
        /// </summary>
        /// <param name="nexus">The interactable to announce</param>
        /// <param name="isFromCycling">If true, this came from keyboard cycling and should update selectedInteractable</param>
        public static void AnnounceInteractable(InteractableNexus nexus, bool isFromCycling = false)
        {
            if (nexus == null) return;

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            if (inputManager == null) return;

            PC player = inputManager.GetFirstSelectedPlayer();
            if (player == null) return;

            string name = GetInteractableName(nexus);
            float distance = Vector3.Distance(player.transform.position, nexus.InstigatePoint);
            string distanceStr = $"{Mathf.RoundToInt(distance)} meters";
            string direction = DirectionHelper.GetDirectionDescription(player.transform.position, nexus.InstigatePoint);

            // Format: "Name, Distance, Direction"
            lastAnnouncement = $"{name}, {distanceStr}, {direction}";
            lastAnnouncedInteractable = nexus;

            // Only update selectedInteractable if this came from keyboard cycling
            // Proximity announcements should NOT overwrite the cycling selection
            if (isFromCycling)
            {
                selectedInteractable = nexus;
            }

            MelonLogger.Msg($"Announcing: {lastAnnouncement} (fromCycling: {isFromCycling})");
            ScreenReaderManager.Speak(lastAnnouncement, interrupt: true);
        }

        private static void SelectAndAnnounce(int index)
        {
            if (index < 0 || index >= filteredInteractables.Count) return;

            InteractableNexus nexus = filteredInteractables[index];

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            if (inputManager == null) return;

            // Clear previous highlight
            if (inputManager.selectedInteractable != null &&
                inputManager.selectedInteractable.GetHighlight() != null)
            {
                inputManager.selectedInteractable.GetHighlight().CursorOut();
            }

            // Set new selection in game's InputManager
            inputManager.selectedInteractable = nexus;

            // Apply highlight
            if (nexus.GetHighlight() != null)
            {
                nexus.GetHighlight().CursorOver();
            }

            // Announce with isFromCycling=true since this came from keyboard cycling
            AnnounceInteractable(nexus, isFromCycling: true);
        }

        private static void UpdateFilteredList()
        {
            filteredInteractables.Clear();

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            if (inputManager == null) return;

            PC player = inputManager.GetFirstSelectedPlayer();
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            foreach (var nexus in InteractableNexus.interactables)
            {
                // Apply visibility filters
                if (nexus == null) continue;
                if (!nexus.isVisible) continue; // This checks FOW visibility (minimap visibility)
                if (nexus.GetHighlight() == null) continue;
                if (nexus.transform == null) continue;

                // Exclude conscious PCs (unconscious PCs can be interacted with for healing)
                if (nexus.drama != null && nexus.drama.GetMob() is PC)
                {
                    PC pc = nexus.drama.GetMob() as PC;
                    if (!pc.isUnconscious) continue; // Skip conscious PCs
                }

                filteredInteractables.Add(nexus);
            }

            // Sort by distance (nearest first)
            filteredInteractables = filteredInteractables
                .OrderBy(n => Vector3.Distance(n.InstigatePoint, playerPos))
                .ToList();

            MelonLogger.Msg($"Filtered interactables: {filteredInteractables.Count} found (all minimap visible)");
        }

        private static string GetInteractableName(InteractableNexus nexus)
        {
            // Try Drama system first
            if (nexus.drama != null)
            {
                // Check for mob name via template
                Mob mob = nexus.drama.GetMob();
                if (mob != null && mob.template != null && !string.IsNullOrEmpty(mob.template.displayName))
                {
                    return mob.template.displayName;
                }
            }

            // Try UILabel on object
            UILabel label = nexus.GetComponent<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                return UITextExtractor.CleanText(label.text);
            }

            // Fall back to cleaned GameObject name
            return CleanGameObjectName(nexus.name);
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unknown object";

            // Remove common suffixes/prefixes
            name = name.Replace("(Clone)", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"_\d+$", "");
            name = name.Replace("_", " ");

            return name.Trim();
        }
    }
}
