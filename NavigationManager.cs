using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Categories for filtering interactable objects
    /// </summary>
    public enum InteractableCategory
    {
        All,        // All interactables
        Party,      // Party members (PCs)
        Characters, // NPCs (friendly and hostile)
        Containers, // Lootable containers, lockers, etc.
        Objects,    // Doors, switches, computers, etc.
        Exits,      // Map exits and area transitions (SceneLoad)
        Examine,    // Examinable/perception objects (descriptions, clues)
        Loot,       // Ground items and loot piles
        Misc        // Everything else (teleporters, dig spots, etc.)
    }

    public static class NavigationManager
    {
        private static List<InteractableNexus> filteredInteractables = new List<InteractableNexus>();
        private static int currentIndex = -1;
        private static string lastAnnouncement = "";
        private static InteractableNexus lastAnnouncedInteractable = null;

        // Separate tracking for keyboard-selected interactable (via cycling)
        // This is NOT overwritten by proximity announcements
        private static InteractableNexus selectedInteractable = null;

        /// <summary>
        /// The currently selected interactable from cycling (PageUp/PageDown).
        /// Used by MapCursorState to jump the cursor to this object's position.
        /// </summary>
        public static InteractableNexus SelectedInteractable => selectedInteractable;

        // Category filtering
        private static InteractableCategory currentCategory = InteractableCategory.All;
        private static readonly InteractableCategory[] categoryOrder =
        {
            InteractableCategory.All,
            InteractableCategory.Party,
            InteractableCategory.Characters,
            InteractableCategory.Containers,
            InteractableCategory.Objects,
            InteractableCategory.Exits,
            InteractableCategory.Examine,
            InteractableCategory.Loot,
            InteractableCategory.Misc
        };

        public static InteractableCategory CurrentCategory => currentCategory;

        /// <summary>
        /// Cycles to the next category (Page Down)
        /// </summary>
        public static void NextCategory()
        {
            if (!FOWHelper.IsFOWReady())
            {
                ScreenReaderManager.SpeakInterrupt("Still loading, try again");
                return;
            }

            int currentIdx = System.Array.IndexOf(categoryOrder, currentCategory);
            currentIdx = (currentIdx + 1) % categoryOrder.Length;
            currentCategory = categoryOrder[currentIdx];

            // Reset selection when changing category
            currentIndex = -1;
            selectedInteractable = null;

            // Update list and announce
            UpdateFilteredList();
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = filteredInteractables.Count;
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            MelonLogger.Msg($"Category changed to: {categoryName} ({count} items)");
        }

        /// <summary>
        /// Cycles to the previous category (Page Up)
        /// </summary>
        public static void PreviousCategory()
        {
            if (!FOWHelper.IsFOWReady())
            {
                ScreenReaderManager.SpeakInterrupt("Still loading, try again");
                return;
            }

            int currentIdx = System.Array.IndexOf(categoryOrder, currentCategory);
            currentIdx--;
            if (currentIdx < 0) currentIdx = categoryOrder.Length - 1;
            currentCategory = categoryOrder[currentIdx];

            // Reset selection when changing category
            currentIndex = -1;
            selectedInteractable = null;

            // Update list and announce
            UpdateFilteredList();
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = filteredInteractables.Count;
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            MelonLogger.Msg($"Category changed to: {categoryName} ({count} items)");
        }

        private static string GetCategoryDisplayName(InteractableCategory category)
        {
            switch (category)
            {
                case InteractableCategory.All: return "All";
                case InteractableCategory.Party: return "Party";
                case InteractableCategory.Characters: return "Characters";
                case InteractableCategory.Containers: return "Containers";
                case InteractableCategory.Objects: return "Objects";
                case InteractableCategory.Exits: return "Exits";
                case InteractableCategory.Examine: return "Examine";
                case InteractableCategory.Loot: return "Loot";
                case InteractableCategory.Misc: return "Miscellaneous";
                default: return "Unknown";
            }
        }

        public static void CycleNext()
        {
            if (!FOWHelper.IsFOWReady())
            {
                ScreenReaderManager.SpeakInterrupt("Still loading, try again");
                return;
            }

            UpdateFilteredList();

            if (filteredInteractables.Count == 0)
            {
                string categoryName = GetCategoryDisplayName(currentCategory);
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()} nearby");
                currentIndex = -1;
                return;
            }

            // Increment index with wrap-around
            currentIndex = (currentIndex + 1) % filteredInteractables.Count;

            SelectAndAnnounce(currentIndex);
        }

        public static void CyclePrevious()
        {
            if (!FOWHelper.IsFOWReady())
            {
                ScreenReaderManager.SpeakInterrupt("Still loading, try again");
                return;
            }

            UpdateFilteredList();

            if (filteredInteractables.Count == 0)
            {
                string categoryName = GetCategoryDisplayName(currentCategory);
                ScreenReaderManager.SpeakInterrupt($"No {categoryName.ToLower()} nearby");
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
                ScreenReaderManager.SpeakInterrupt("No interactable selected");
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
                ScreenReaderManager.SpeakInterrupt("Previously selected interactable is no longer available");
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
            string distanceStr = TileCoordinateSystem.GetDistanceText(player.transform.position, nexus.InstigatePoint);
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
            ScreenReaderManager.SpeakInterrupt(lastAnnouncement);
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
            FOWHelper.UpdateActivationTracking();

            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            if (inputManager == null) return;

            PC player = inputManager.GetFirstSelectedPlayer();
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            // Party category: PCs aren't in InteractableNexus.interactables when conscious,
            // so we build the list directly from Game.party and Game.partyFollowers
            if (currentCategory == InteractableCategory.Party)
            {
                UpdatePartyList(playerPos);
                return;
            }

            // For "All" category, also include party members since they're not in the interactables list
            if (currentCategory == InteractableCategory.All)
            {
                UpdatePartyList(playerPos);
                // Continue below to also add regular interactables
            }

            // Diagnostic: check if ShortcutDoor nexus is in the list at all
            foreach (var dbgNexus in InteractableNexus.interactables)
            {
                if (dbgNexus != null && dbgNexus.transform != null)
                {
                    Vector3 dp = dbgNexus.transform.position;
                    if (dp.x > 88f && dp.x < 93f && dp.z > 111f && dp.z < 116f)
                        MelonLogger.Msg($"  [NavDiag] Found nexus near ShortcutDoor: {dbgNexus.name} at {dp}, isVisible={dbgNexus.isVisible}, isHidden={dbgNexus.isHidden}, active={dbgNexus.gameObject.activeInHierarchy}");
                }
            }

            foreach (var nexus in InteractableNexus.interactables)
            {
                // Step-by-step trace for ShortcutDoor
                bool traceThis = false;
                if (nexus != null && nexus.transform != null)
                {
                    Vector3 tp = nexus.transform.position;
                    traceThis = (tp.x > 88f && tp.x < 93f && tp.z > 111f && tp.z < 116f);
                }
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name} at {nexus.transform.position}: step=start");

                // Apply visibility filters
                if (nexus == null) { if (traceThis) MelonLogger.Msg($"  [NavTrace] SKIPPED: nexus==null"); continue; }
                if (!nexus.isVisible)
                {
                    // Log rejections near ShortcutDoor position to diagnose disappearance
                    if (nexus.transform != null)
                    {
                        Vector3 p = nexus.transform.position;
                        if ((p.x > 85f && p.x < 95f && p.z > 108f && p.z < 118f) ||
                            nexus.drama is InteractableTeleporter)
                        {
                            bool hasTp = nexus.GetComponent<InteractableTeleporter>() != null;
                            MelonLogger.Msg($"  [NavReject] {nexus.name} at {p}: failed isVisible (isHidden={nexus.isHidden}, active={nexus.gameObject.activeInHierarchy}, dramaType={(nexus.drama != null ? nexus.drama.GetType().Name : "null")}, hasTpComponent={hasTp})");
                        }
                    }
                    continue;
                }
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: passed isVisible");
                if (nexus.transform == null) { if (traceThis) MelonLogger.Msg($"  [NavTrace] SKIPPED: transform==null"); continue; }
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: checking IsVisibleThroughFOW");
                if (!FOWHelper.IsVisibleThroughFOW(nexus.transform.position))
                {
                    if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: FAILED IsVisibleThroughFOW");
                    // Log teleporter rejections for diagnostics
                    if (nexus.drama is InteractableTeleporter || traceThis)
                        MelonLogger.Msg($"  [NavReject] {nexus.name}: failed IsVisibleThroughFOW at {nexus.transform.position}");
                    continue;
                }
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: checking IsPerceptionGated, drama={(nexus.drama != null ? nexus.drama.GetType().Name : "null")}");
                if (FOWHelper.IsPerceptionGated(nexus))
                {
                    if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: FAILED IsPerceptionGated");
                    MelonLogger.Msg($"  [NavReject] {nexus.name}: failed IsPerceptionGated");
                    // Log teleporter details
                    var tp = nexus.drama as InteractableTeleporter;
                    if (tp != null && tp.targetTransform != null)
                    {
                        float dist = Vector3.Distance(nexus.transform.position, tp.targetTransform.position);
                        bool explored = FOWSystem.instance != null ? FOWSystem.instance.IsExplored(tp.targetTransform.position) : true;
                        MelonLogger.Msg($"  [NavReject]   teleporter dest={tp.targetTransform.position}, dist={dist:F1}, destExplored={explored}");
                    }
                    continue;
                }

                // Apply category filter
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: checking MatchesCategory({currentCategory})");
                if (!MatchesCategory(nexus, currentCategory)) { if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: FAILED MatchesCategory"); continue; }
                if (traceThis) MelonLogger.Msg($"  [NavTrace] {nexus.name}: PASSED ALL FILTERS - added to list");

                filteredInteractables.Add(nexus);

                // Diagnostic: log properties that may help filter perception-gated objects
                string objName = nexus.name ?? "null";
                string dramaType = nexus.drama != null ? nexus.drama.GetType().Name : "null";
                bool instigateBlocked = nexus.drama != null && nexus.drama.bInstigateBlocked;
                SkillObject_Examine skob = nexus.skobExamine;
                if (skob == null && nexus.gameObject != null)
                    skob = nexus.gameObject.GetComponent<SkillObject_Examine>();
                string skobInfo = skob != null
                    ? $"skob(diff={skob.difficulty}, perceived={skob.perceived}, hidden={skob.hidden})"
                    : "skob=null";
                var teleporter = nexus.drama as InteractableTeleporter;
                string tpInfo = teleporter != null && teleporter.targetTransform != null
                    ? $"tp={teleporter.targetTransform.position}"
                    : "";
                MelonLogger.Msg($"  [NavFilter] {objName}: type={dramaType}, blocked={instigateBlocked}, {skobInfo} {tpInfo}");
            }

            // Sort by distance (nearest first)
            filteredInteractables = filteredInteractables
                .OrderBy(n => Vector3.Distance(n.InstigatePoint, playerPos))
                .ToList();

            MelonLogger.Msg($"Filtered interactables: {filteredInteractables.Count} found (category: {currentCategory})");
        }

        private static void UpdatePartyList(Vector3 playerPos)
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return;
            var game = MonoBehaviourSingleton<Game>.GetInstance();

            // Add party PCs
            if (game.party != null)
            {
                foreach (var pc in game.party)
                {
                    if (pc == null || pc.gameObject == null) continue;
                    if (!pc.gameObject.activeInHierarchy) continue;

                    var nexus = pc.gameObject.GetComponent<InteractableNexus>();
                    if (nexus == null)
                    {
                        // Try to find it on a child or via Drama
                        var drama = pc.gameObject.GetComponent<Drama>();
                        if (drama != null)
                        {
                            nexus = drama.GetComponent<InteractableNexus>();
                        }
                    }
                    if (nexus != null)
                    {
                        filteredInteractables.Add(nexus);
                    }
                }
            }

            // Add party followers (CNPCs like Angela Deth)
            if (game.partyFollowers != null)
            {
                foreach (var follower in game.partyFollowers)
                {
                    if (follower == null || follower.gameObject == null) continue;
                    if (!follower.gameObject.activeInHierarchy) continue;

                    var nexus = follower.gameObject.GetComponent<InteractableNexus>();
                    if (nexus == null)
                    {
                        var drama = follower.gameObject.GetComponent<Drama>();
                        if (drama != null)
                        {
                            nexus = drama.GetComponent<InteractableNexus>();
                        }
                    }
                    if (nexus != null)
                    {
                        filteredInteractables.Add(nexus);
                    }
                }
            }

            // Sort by distance
            filteredInteractables = filteredInteractables
                .OrderBy(n => Vector3.Distance(n.transform.position, playerPos))
                .ToList();

            MelonLogger.Msg($"Filtered party members: {filteredInteractables.Count} found");
        }

        private static bool MatchesCategory(InteractableNexus nexus, InteractableCategory category)
        {
            if (category == InteractableCategory.All) return true;

            // Get the Drama component for type checking
            Drama drama = nexus.drama;

            switch (category)
            {
                case InteractableCategory.Party:
                    // Party members (PCs)
                    if (drama != null)
                    {
                        Mob mob = drama.GetMob();
                        if (mob != null && mob is PC) return true;
                    }
                    return false;

                case InteractableCategory.Characters:
                    // NPCs (not PCs) - characters you can talk to or fight
                    if (drama != null)
                    {
                        Mob mob = drama.GetMob();
                        if (mob != null && mob is NPC) return true;
                    }
                    return false;

                case InteractableCategory.Containers:
                    // Categorize as Container if it's an InteractableInventoryObject that
                    // is either pokeable (unlocked) or locked (needs safecrack/pickLock/etc).
                    InteractableInventoryObject invObj = drama as InteractableInventoryObject;
                    if (invObj == null)
                        invObj = nexus.GetComponent<InteractableInventoryObject>();
                    if (invObj == null) return false;
                    var cInteractions = invObj.GetAllowedInteractions();
                    if (cInteractions == null) return false;
                    // Unlocked container with loot
                    if (cInteractions.ContainsKey("Poked") && cInteractions["Poked"] == 1)
                        return true;
                    // Locked container — has skill interactions (value 1 = available,
                    // -1 = not prodded but present). Either means it's interactable.
                    foreach (var kvp in cInteractions)
                    {
                        if (kvp.Key == "Poked") continue;
                        if (kvp.Value != 0) return true;
                    }
                    return false;

                case InteractableCategory.Objects:
                    // Doors, switches, computers, etc. - InteractableObjects that aren't containers or exits
                    if (drama != null)
                    {
                        // Exclude SceneLoad (exits)
                        if (drama is SceneLoad) return false;

                        // Must be an InteractableObject but NOT a lootable container and NOT a character
                        if (drama is InteractableObject && !MatchesCategory(nexus, InteractableCategory.Containers))
                        {
                            Mob mob = drama.GetMob();
                            if (mob == null) return true; // No mob = object
                        }
                    }
                    // Check component directly
                    var io = nexus.GetComponent<InteractableObject>();
                    if (io != null && !MatchesCategory(nexus, InteractableCategory.Containers))
                    {
                        if (drama == null || drama.GetMob() == null) return true;
                    }
                    return false;

                case InteractableCategory.Exits:
                    // Map exits and area transitions
                    if (drama != null && drama is SceneLoad) return true;
                    // Also check component directly
                    if (nexus.GetComponent<SceneLoad>() != null) return true;
                    // Check for WorldMapExitEncounter
                    if (drama != null && drama.GetType().Name.Contains("WorldMapExit")) return true;
                    // Check object name patterns for exits
                    string exitName = nexus.name.ToLower();
                    if (exitName.Contains("exit") || exitName.Contains("sceneload") ||
                        exitName.Contains("worldmap") || exitName.Contains("leave"))
                        return true;
                    return false;

                case InteractableCategory.Examine:
                    // Examinable objects - perception checks, descriptions, clues
                    // These typically only have a "Descriptor" or "perception" interaction
                    if (drama != null)
                    {
                        // Check if it's primarily an examine/descriptor object
                        var interactions = drama.GetAllowedInteractions();
                        if (interactions != null)
                        {
                            bool hasDescriptor = interactions.ContainsKey("Descriptor") && interactions["Descriptor"] == 1;
                            bool hasPerception = interactions.ContainsKey("perception") && interactions["perception"] == 1;

                            // If it has descriptor/perception but isn't a container, NPC, or usable object
                            if (hasDescriptor || hasPerception)
                            {
                                // Make sure it's not already covered by other categories
                                bool isLootableContainer = MatchesCategory(nexus, InteractableCategory.Containers);
                                if (!isLootableContainer &&
                                    !(drama is InteractableObject) &&
                                    drama.GetMob() == null)
                                {
                                    return true;
                                }
                                // Also include InteractableObjects that only have descriptor interaction
                                // or locked containers where Poked is unavailable
                                if (drama is InteractableObject &&
                                    (!interactions.ContainsKey("Poked") || interactions["Poked"] != 1))
                                {
                                    return true;
                                }
                            }
                        }
                        // Also check by type name for common examine patterns
                        string typeName = drama.GetType().Name.ToLower();
                        if (typeName.Contains("examine") || typeName.Contains("descriptor") ||
                            typeName.Contains("clue") || typeName.Contains("perception"))
                            return true;
                    }
                    // Objects with SkillObject_Examine that aren't lootable containers
                    if (nexus.skobExamine != null && !MatchesCategory(nexus, InteractableCategory.Containers))
                        return true;
                    // Check object name patterns
                    string examName = nexus.name.ToLower();
                    if (examName.Contains("examine") || examName.Contains("descriptor") ||
                        examName.Contains("clue") || examName.Contains("perception"))
                        return true;
                    return false;

                case InteractableCategory.Loot:
                    // Ground items - typically have "Loot" or "Item" in name, or are GroundItemDrama
                    string lootName = nexus.name.ToLower();
                    if (lootName.Contains("loot") || lootName.Contains("grounditem") || lootName.Contains("drop"))
                        return true;
                    // Check if it's a ground item drama
                    if (drama != null && drama.GetType().Name.Contains("GroundItem"))
                        return true;
                    return false;

                case InteractableCategory.Misc:
                    // Miscellaneous - anything that doesn't fit other categories
                    // This includes: teleporters, dig spots, shrines, special objects, etc.
                    // Check if it matches any OTHER specific category - if so, exclude it
                    if (MatchesCategory(nexus, InteractableCategory.Party)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Characters)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Containers)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Objects)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Exits)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Examine)) return false;
                    if (MatchesCategory(nexus, InteractableCategory.Loot)) return false;
                    // Doesn't match any specific category - it's misc
                    return true;

                default:
                    return true;
            }
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
                    string mobName = mob.template.displayName;
                    if (mob.isDead) mobName += ", dead";
                    return mobName;
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
            // Strip internal zone-marker prefix (AZ_, AZ1_, CA_, LA_, ...) before
            // converting underscores to spaces.
            name = System.Text.RegularExpressions.Regex.Replace(name, @"^(AZ|CA|LA)\d?_", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, @"_\d+$", "");
            name = name.Replace("_", " ");

            return name.Trim();
        }
    }
}
