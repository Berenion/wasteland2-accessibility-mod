using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Quick environment scanning feature that announces categorized lists
    /// of nearby objects (enemies, NPCs, items, exits, interactables).
    /// Priority 80 - highest priority to ensure scan key is always captured.
    /// </summary>
    public class ScannerState : IAccessibilityState
    {
        public string Name => "Scanner";
        public int Priority => 80;

        // Scan settings
        private const float SHORT_RANGE = 10f;
        private const float MEDIUM_RANGE = 25f;
        private const float LONG_RANGE = 50f;

        // Current scan range
        private float currentScanRange = MEDIUM_RANGE;
        private int rangeIndex = 1; // 0=short, 1=medium, 2=long

        // Scan state
        private bool scanInProgress = false;
        private int currentCategory = 0;
        private List<ScanResult> lastScanResults = new List<ScanResult>();

        // Categories
        private static readonly string[] CATEGORY_NAMES = {
            "Enemies",
            "NPCs",
            "Loot",
            "Exits",
            "Interactables"
        };

        private class ScanResult
        {
            public string Name;
            public string Category;
            public float Distance;
            public string Direction;
            public Vector3 Position;
            public object Source; // Original object reference
        }

        public bool IsActive
        {
            get
            {
                // Scanner is always listening for its hotkey during gameplay
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;
                if (MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive()) return false;
                if (Drama.isConversationOn) return false;

                // Active during scan navigation
                return scanInProgress;
            }
        }

        public bool HandleInput()
        {
            // S key triggers a new scan (works even when not "active")
            if (Input.GetKeyDown(KeyCode.S) && !scanInProgress)
            {
                // Only scan if not in menus/conversation
                if (!MonoBehaviourSingleton<GUIManager>.HasInstance()) return false;
                if (MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive()) return false;
                if (Drama.isConversationOn) return false;

                PerformScan();
                return true;
            }

            // If scan is in progress, handle navigation
            if (scanInProgress)
            {
                // Up/Down to cycle categories
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    CycleCategoryBackward();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    CycleCategoryForward();
                    InputSuppressor.ShouldSuppressUINavigation = true;
                    return true;
                }

                // Enter to select/navigate to closest in category
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SelectClosestInCategory();
                    return true;
                }

                // R to change scan range
                if (Input.GetKeyDown(KeyCode.R))
                {
                    CycleScanRange();
                    return true;
                }

                // Escape or S again to exit scan mode
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.S))
                {
                    ExitScanMode();
                    return true;
                }

                // Suppress other input during scan
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[ScannerState] Activated");
        }

        public void OnDeactivated()
        {
            scanInProgress = false;
            lastScanResults.Clear();
            MelonLogger.Msg("[ScannerState] Deactivated");
        }

        private void PerformScan()
        {
            scanInProgress = true;
            currentCategory = 0;
            lastScanResults.Clear();

            PC pc = GetPartyLeader();
            if (pc == null)
            {
                ScreenReaderManager.SpeakInterrupt("No party member selected");
                scanInProgress = false;
                return;
            }

            Vector3 scanOrigin = pc.transform.position;

            // Scan for all object types
            ScanForEnemies(scanOrigin);
            ScanForNPCs(scanOrigin);
            ScanForLoot(scanOrigin);
            ScanForExits(scanOrigin);
            ScanForInteractables(scanOrigin);

            // Announce summary
            AnnounceScanSummary();
        }

        private bool IsVisibleThroughFOW(Vector3 position)
        {
            return FOWHelper.IsVisibleThroughFOW(position);
        }

        private void ScanForEnemies(Vector3 origin)
        {
            Mob[] allMobs = UnityEngine.Object.FindObjectsOfType<Mob>();

            foreach (var mob in allMobs)
            {
                if (mob == null || mob.gameObject == null) continue;
                if (!mob.gameObject.activeInHierarchy) continue;
                if (mob is PC) continue; // Skip party members
                if (!IsVisibleThroughFOW(mob.transform.position)) continue;

                float distance = Vector3.Distance(origin, mob.transform.position);
                if (distance > currentScanRange) continue;

                // Check if hostile
                bool isHostile = false;
                string mobName = "Unknown";

                if (mob is NPC npc)
                {
                    if (npc.npcTemplate != null)
                    {
                        isHostile = npc.npcTemplate.faction == Faction.Bad;
                        mobName = UITextExtractor.CleanText(Language.Localize(npc.npcTemplate.displayName, false, false, string.Empty));
                    }
                }
                else
                {
                    // Non-NPC mobs are typically enemies
                    isHostile = true;
                    mobName = mob.gameObject.name;
                }

                if (isHostile)
                {
                    lastScanResults.Add(new ScanResult
                    {
                        Name = mobName,
                        Category = "Enemies",
                        Distance = distance,
                        Direction = DirectionHelper.GetDirectionDescription(origin, mob.transform.position),
                        Position = mob.transform.position,
                        Source = mob
                    });
                }
            }
        }

        private void ScanForNPCs(Vector3 origin)
        {
            Mob[] allMobs = UnityEngine.Object.FindObjectsOfType<Mob>();

            foreach (var mob in allMobs)
            {
                if (mob == null || mob.gameObject == null) continue;
                if (!mob.gameObject.activeInHierarchy) continue;
                if (mob is PC) continue;
                if (!IsVisibleThroughFOW(mob.transform.position)) continue;

                float distance = Vector3.Distance(origin, mob.transform.position);
                if (distance > currentScanRange) continue;

                if (mob is NPC npc && npc.npcTemplate != null)
                {
                    // Only friendly NPCs
                    if (npc.npcTemplate.faction != Faction.Bad)
                    {
                        string npcName = UITextExtractor.CleanText(Language.Localize(npc.npcTemplate.displayName, false, false, string.Empty));

                        lastScanResults.Add(new ScanResult
                        {
                            Name = npcName,
                            Category = "NPCs",
                            Distance = distance,
                            Direction = DirectionHelper.GetDirectionDescription(origin, mob.transform.position),
                            Position = mob.transform.position,
                            Source = npc
                        });
                    }
                }
            }
        }

        private void ScanForLoot(Vector3 origin)
        {
            // Find world containers via InteractableNexus that have InteractableInventoryObject
            foreach (var nexus in InteractableNexus.interactables)
            {
                if (nexus == null || !nexus.isVisible) continue;
                if (nexus.isPC) continue;
                if (!IsVisibleThroughFOW(nexus.transform.position)) continue;

                // Check if this nexus is a lootable container (Poked must be available)
                InteractableInventoryObject invObj = nexus.drama as InteractableInventoryObject;
                if (invObj == null)
                    invObj = nexus.GetComponent<InteractableInventoryObject>();
                if (invObj == null) continue;
                var interactions = invObj.GetAllowedInteractions();
                if (interactions == null || !interactions.ContainsKey("Poked") || interactions["Poked"] != 1)
                    continue;

                float distance = Vector3.Distance(origin, nexus.transform.position);
                if (distance > currentScanRange) continue;

                string containerName = nexus.gameObject.name;
                // Try to get a better name from Drama
                if (nexus.drama != null && !string.IsNullOrEmpty(nexus.drama.name))
                {
                    containerName = UITextExtractor.CleanText(nexus.drama.name);
                }
                else
                {
                    containerName = containerName.Replace("_", " ").Replace("(Clone)", "").Trim();
                    if (string.IsNullOrEmpty(containerName)) containerName = "Container";
                }

                lastScanResults.Add(new ScanResult
                {
                    Name = containerName,
                    Category = "Loot",
                    Distance = distance,
                    Direction = DirectionHelper.GetDirectionDescription(origin, nexus.transform.position),
                    Position = nexus.transform.position,
                    Source = nexus
                });
            }
        }

        private void ScanForExits(Vector3 origin)
        {
            // Find scene load triggers (exits/doors)
            SceneLoad[] exits = UnityEngine.Object.FindObjectsOfType<SceneLoad>();

            foreach (var exit in exits)
            {
                if (exit == null || exit.gameObject == null) continue;
                if (!exit.gameObject.activeInHierarchy) continue;
                if (!IsVisibleThroughFOW(exit.transform.position)) continue;

                float distance = Vector3.Distance(origin, exit.transform.position);
                if (distance > currentScanRange) continue;

                string exitName = "Exit";
                // Try to get destination name if available
                if (!string.IsNullOrEmpty(exit.sceneName))
                {
                    exitName = $"Exit to {exit.sceneName}";
                }

                lastScanResults.Add(new ScanResult
                {
                    Name = exitName,
                    Category = "Exits",
                    Distance = distance,
                    Direction = DirectionHelper.GetDirectionDescription(origin, exit.transform.position),
                    Position = exit.transform.position,
                    Source = exit
                });
            }
        }

        private void ScanForInteractables(Vector3 origin)
        {
            foreach (var interactable in InteractableNexus.interactables)
            {
                if (interactable == null || !interactable.isVisible) continue;
                if (interactable.isPC) continue;
                if (!IsVisibleThroughFOW(interactable.transform.position)) continue;

                float distance = Vector3.Distance(origin, interactable.transform.position);
                if (distance > currentScanRange) continue;

                // Skip if already categorized as enemy, NPC, loot, or exit
                if (IsAlreadyCategorized(interactable)) continue;

                string name = GetInteractableName(interactable);

                lastScanResults.Add(new ScanResult
                {
                    Name = name,
                    Category = "Interactables",
                    Distance = distance,
                    Direction = DirectionHelper.GetDirectionDescription(origin, interactable.transform.position),
                    Position = interactable.transform.position,
                    Source = interactable
                });
            }
        }

        private bool IsAlreadyCategorized(InteractableNexus interactable)
        {
            // Check if this interactable's position matches something already found
            foreach (var result in lastScanResults)
            {
                if (Vector3.Distance(result.Position, interactable.transform.position) < 1f)
                {
                    return true;
                }
            }
            return false;
        }

        private string GetInteractableName(InteractableNexus interactable)
        {
            if (interactable == null) return "Unknown";

            // Try to get name from Drama
            if (interactable.drama != null)
            {
                string dramaName = interactable.drama.name;
                if (!string.IsNullOrEmpty(dramaName))
                {
                    return UITextExtractor.CleanText(dramaName);
                }
            }

            // Try SkillObject_Examine
            if (interactable.skobExamine != null)
            {
                return "Examinable object";
            }

            // Fallback to GameObject name
            string goName = interactable.gameObject.name;
            if (!string.IsNullOrEmpty(goName))
            {
                goName = goName.Replace("_", " ").Replace("(Clone)", "").Trim();
                return goName;
            }

            return "Object";
        }

        private void AnnounceScanSummary()
        {
            // Count by category
            Dictionary<string, int> categoryCounts = new Dictionary<string, int>();
            foreach (var cat in CATEGORY_NAMES)
            {
                categoryCounts[cat] = 0;
            }

            foreach (var result in lastScanResults)
            {
                if (categoryCounts.ContainsKey(result.Category))
                {
                    categoryCounts[result.Category]++;
                }
            }

            // Build summary
            List<string> summaryParts = new List<string>();
            summaryParts.Add($"Scan complete, range {currentScanRange:F0} meters");

            int totalFound = lastScanResults.Count;
            if (totalFound == 0)
            {
                summaryParts.Add("nothing found");
            }
            else
            {
                foreach (var cat in CATEGORY_NAMES)
                {
                    int count = categoryCounts[cat];
                    if (count > 0)
                    {
                        summaryParts.Add($"{count} {cat.ToLower()}");
                    }
                }
                summaryParts.Add("Use arrows to browse, Enter to select, R for range, S to exit");
            }

            ScreenReaderManager.SpeakInterrupt(string.Join(", ", summaryParts.ToArray()));

            // If we found anything, announce first category
            if (totalFound > 0)
            {
                currentCategory = FindFirstNonEmptyCategory();
            }
            else
            {
                scanInProgress = false;
            }
        }

        private int FindFirstNonEmptyCategory()
        {
            for (int i = 0; i < CATEGORY_NAMES.Length; i++)
            {
                if (GetResultsForCategory(CATEGORY_NAMES[i]).Count > 0)
                {
                    return i;
                }
            }
            return 0;
        }

        private List<ScanResult> GetResultsForCategory(string category)
        {
            List<ScanResult> results = new List<ScanResult>();
            foreach (var result in lastScanResults)
            {
                if (result.Category == category)
                {
                    results.Add(result);
                }
            }
            // Sort by distance
            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results;
        }

        private void CycleCategoryForward()
        {
            int startCategory = currentCategory;
            do
            {
                currentCategory = (currentCategory + 1) % CATEGORY_NAMES.Length;
            }
            while (GetResultsForCategory(CATEGORY_NAMES[currentCategory]).Count == 0 && currentCategory != startCategory);

            AnnounceCurrentCategory();
        }

        private void CycleCategoryBackward()
        {
            int startCategory = currentCategory;
            do
            {
                currentCategory--;
                if (currentCategory < 0) currentCategory = CATEGORY_NAMES.Length - 1;
            }
            while (GetResultsForCategory(CATEGORY_NAMES[currentCategory]).Count == 0 && currentCategory != startCategory);

            AnnounceCurrentCategory();
        }

        private void AnnounceCurrentCategory()
        {
            string categoryName = CATEGORY_NAMES[currentCategory];
            var results = GetResultsForCategory(categoryName);

            if (results.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()}");
                return;
            }

            List<string> announcements = new List<string>();
            announcements.Add($"{categoryName}, {results.Count} found");

            // Announce up to first 5 items
            int maxAnnounce = Math.Min(5, results.Count);
            for (int i = 0; i < maxAnnounce; i++)
            {
                var result = results[i];
                announcements.Add($"{result.Name}, {result.Distance:F0} meters {result.Direction}");
            }

            if (results.Count > 5)
            {
                announcements.Add($"and {results.Count - 5} more");
            }

            ScreenReaderManager.SpeakInterrupt(string.Join(". ", announcements.ToArray()));
        }

        private void SelectClosestInCategory()
        {
            string categoryName = CATEGORY_NAMES[currentCategory];
            var results = GetResultsForCategory(categoryName);

            if (results.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()} to select");
                return;
            }

            var closest = results[0]; // Already sorted by distance

            // Resolve the InteractableNexus for selection
            InteractableNexus nexus = null;
            if (closest.Source is InteractableNexus directNexus)
            {
                nexus = directNexus;
            }
            else if (closest.Source is MonoBehaviour mb && mb != null)
            {
                // Try to find InteractableNexus on the same GameObject (for Mob, SceneLoad, etc.)
                nexus = mb.GetComponent<InteractableNexus>();
            }

            if (nexus != null && MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                MonoBehaviourSingleton<InputManager>.GetInstance().selectedInteractable = nexus;
            }

            // Snap camera to the object
            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var cameraController = MonoBehaviourSingleton<Game>.GetInstance().cameraController;
                if (cameraController != null)
                {
                    cameraController.Snap(closest.Position, instant: false);
                }
            }

            ScreenReaderManager.SpeakInterrupt($"Selected: {closest.Name}, {closest.Distance:F0} meters {closest.Direction}");

            // Exit scan mode after selection
            scanInProgress = false;
        }

        private void CycleScanRange()
        {
            rangeIndex = (rangeIndex + 1) % 3;
            switch (rangeIndex)
            {
                case 0:
                    currentScanRange = SHORT_RANGE;
                    break;
                case 1:
                    currentScanRange = MEDIUM_RANGE;
                    break;
                case 2:
                    currentScanRange = LONG_RANGE;
                    break;
            }

            // Auto-rescan at new range so the list is never stale
            PerformScan();
        }

        private void ExitScanMode()
        {
            scanInProgress = false;
            lastScanResults.Clear();
            ScreenReaderManager.SpeakInterrupt("Scan mode off");
        }

        private PC GetPartyLeader()
        {
            if (MonoBehaviourSingleton<InputManager>.HasInstance())
            {
                var pc = MonoBehaviourSingleton<InputManager>.GetInstance().GetFirstSelectedPlayer();
                if (pc != null) return pc;
            }

            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null && party.Count > 0)
                {
                    return party[0];
                }
            }

            return null;
        }
    }
}
