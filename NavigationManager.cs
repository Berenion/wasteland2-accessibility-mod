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
            if (lastAnnouncedInteractable == null)
            {
                ScreenReaderManager.Speak("No interactable selected", interrupt: true);
                return;
            }

            // Refresh the list to get current state
            InteractableNexus previouslySelected = lastAnnouncedInteractable;
            UpdateFilteredList();

            // Try to find the previously selected interactable in the new list
            int newIndex = filteredInteractables.IndexOf(previouslySelected);

            if (newIndex >= 0)
            {
                // Found it - update our index and re-announce
                currentIndex = newIndex;
                AnnounceInteractable(previouslySelected);
            }
            else
            {
                // Interactable no longer available (out of range or not visible)
                ScreenReaderManager.Speak("Previously selected interactable is no longer available", interrupt: true);
                lastAnnouncedInteractable = null;
                lastAnnouncement = "";
                currentIndex = -1;
            }
        }

        public static void AnnounceInteractable(InteractableNexus nexus)
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

            MelonLogger.Msg($"Announcing: {lastAnnouncement}");
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

            // Set new selection
            inputManager.selectedInteractable = nexus;

            // Apply highlight
            if (nexus.GetHighlight() != null)
            {
                nexus.GetHighlight().CursorOver();
            }

            // Announce
            AnnounceInteractable(nexus);
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
                // Apply same filters as game's SelectNextInteractable
                if (nexus == null) continue;
                if (!nexus.isVisible) continue;
                if (nexus.GetHighlight() == null) continue;
                if (nexus.transform == null) continue;

                // Check distance (10 units is the game's hardcoded limit)
                float distance = Vector3.Distance(nexus.InstigatePoint, playerPos);
                if (distance >= 10f) continue;

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

            MelonLogger.Msg($"Filtered interactables: {filteredInteractables.Count} found");
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
