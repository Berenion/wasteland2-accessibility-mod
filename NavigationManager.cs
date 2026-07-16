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
        Cover,      // Cover positions (Cover components: short / tall). Not interactables.
        Labels,     // Tiles the player has labelled (LocationLabels). Not interactables.
        Misc        // Everything else (teleporters, dig spots, etc.)
    }

    public static class NavigationManager
    {
        private static List<InteractableNexus> filteredInteractables = new List<InteractableNexus>();
        private static int currentIndex = -1;

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
            InteractableCategory.Cover,
            InteractableCategory.Labels,
            InteractableCategory.Misc
        };

        public static InteractableCategory CurrentCategory => currentCategory;

        // --- Point categories (Cover, Labels) ---
        // Some things worth cycling to aren't InteractableNexuses at all: cover comes from
        // Cover components, and a player label belongs to a bare tile that may hold nothing.
        // Both are just "a world position with a name", so they share one parallel path: a
        // list of points, cycled and announced like interactables but without game
        // selection/highlight (announce only — there is nothing to select or highlight).
        private struct ScanPoint
        {
            public Vector3 Position;
            public string Label;
        }
        private static readonly List<ScanPoint> points = new List<ScanPoint>();
        private static int pointIndex = -1;
        private static bool hasSelectedPoint = false;
        private static ScanPoint selectedPoint;

        /// <summary>True while the scanner is on a category backed by points rather than
        /// interactables (Cover, Labels), so callers use the SelectedPoint* API instead of
        /// SelectedInteractable.</summary>
        public static bool IsPointCategory =>
            currentCategory == InteractableCategory.Cover ||
            currentCategory == InteractableCategory.Labels;

        /// <summary>World position of the point selected via cycling, or null.
        /// Used by MapCursorState to jump the cursor there (Home).</summary>
        public static Vector3? SelectedPointPosition => hasSelectedPoint ? selectedPoint.Position : (Vector3?)null;

        /// <summary>Spoken label of the selected point (e.g. "Tall cover", or the player's
        /// own label text), or null.</summary>
        public static string SelectedPointLabel => hasSelectedPoint ? selectedPoint.Label : null;

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
            pointIndex = -1;
            hasSelectedPoint = false;

            // Update list and announce
            UpdateFilteredList();
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = CurrentCount();
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            ModLog.Debug($"Category changed to: {categoryName} ({count} items)");
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
            pointIndex = -1;
            hasSelectedPoint = false;

            // Update list and announce
            UpdateFilteredList();
            string categoryName = GetCategoryDisplayName(currentCategory);
            int count = CurrentCount();
            ScreenReaderManager.SpeakInterrupt($"{categoryName}, {count} found");

            ModLog.Debug($"Category changed to: {categoryName} ({count} items)");
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
                case InteractableCategory.Cover: return "Cover";
                case InteractableCategory.Labels: return "Labels";
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

            if (IsPointCategory) { CyclePoint(1); return; }

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

            if (IsPointCategory) { CyclePoint(-1); return; }

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

        /// <summary>Item count for the active category (point categories ride their own list).</summary>
        private static int CurrentCount()
        {
            return IsPointCategory ? points.Count : filteredInteractables.Count;
        }

        /// <summary>"No cover nearby" / "No labels in this area" — the empty-list line for
        /// the active point category.</summary>
        private static string EmptyPointMessage()
        {
            return currentCategory == InteractableCategory.Labels
                ? "No labels in this area"
                : "No cover nearby";
        }

        // Cycles through the active point category's positions (dir = +1 next, -1 previous),
        // announcing the selected one with distance and direction from the player.
        // Announce-only: no game selection or highlight, since points aren't Targetables/nexuses.
        private static void CyclePoint(int dir)
        {
            if (points.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt(EmptyPointMessage());
                pointIndex = -1;
                hasSelectedPoint = false;
                return;
            }

            pointIndex += (dir >= 0 ? 1 : -1);
            if (pointIndex < 0) pointIndex = points.Count - 1;
            else if (pointIndex >= points.Count) pointIndex = 0;

            selectedPoint = points[pointIndex];
            hasSelectedPoint = true;
            AnnouncePoint(selectedPoint);
        }

        private static void AnnouncePoint(ScanPoint point)
        {
            var inputManager = MonoBehaviourSingleton<InputManager>.GetInstance();
            PC player = inputManager != null ? inputManager.GetFirstSelectedPlayer() : null;
            if (player == null)
            {
                ScreenReaderManager.SpeakInterrupt(point.Label);
                return;
            }

            string distanceStr = TileCoordinateSystem.GetDistanceText(player.transform.position, point.Position);
            string direction = DirectionHelper.GetDirectionDescription(player.transform.position, point.Position);
            ScreenReaderManager.SpeakInterrupt($"{point.Label}, {distanceStr}, {direction}");
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

            // Any player label on the tile this sits on, so one of five identical "Door"s
            // announces as the one they named. Labels belong to the place, not the object.
            //
            // Keyed on transform.position, NOT GetNexusPosition: that returns the instigate
            // point (where the ranger stands to use it), which is frequently a different
            // tile from the object itself. The cursor's Home jump snaps to the object's own
            // tile (MapCursorState.SnapCursorToWorldTile via transform.position), so that is
            // the tile the player labels — look it up in the same place they wrote it, or
            // the label would silently never be found from the scanner.
            string label = nexus.transform != null
                ? LocationLabels.Get(nexus.transform.position)
                : null;

            // Format: "Name, [labelled X,] Distance, Direction"
            string announcement = string.IsNullOrEmpty(label)
                ? $"{name}, {distanceStr}, {direction}"
                : $"{name}, labelled {label}, {distanceStr}, {direction}";

            // Only update selectedInteractable if this came from keyboard cycling
            // Proximity announcements should NOT overwrite the cycling selection
            if (isFromCycling)
            {
                selectedInteractable = nexus;
            }

            ModLog.Debug($"Announcing: {announcement} (fromCycling: {isFromCycling})");
            ScreenReaderManager.SpeakInterrupt(announcement);
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

            // Cover and Labels ride their own list (world positions, not nexuses).
            if (IsPointCategory)
            {
                UpdatePointList(playerPos);
                return;
            }

            // For "All" category, also include party members since they're not in the interactables list
            if (currentCategory == InteractableCategory.All)
            {
                UpdatePartyList(playerPos);
                // Continue below to also add regular interactables
            }

            foreach (var nexus in InteractableNexus.interactables)
            {
                if (nexus == null) continue;
                if (nexus.transform == null) continue;
                if (!FOWHelper.PassesScannerGate(nexus)) continue;
                if (!HasInteractionSurface(nexus)) continue;
                if (IsLootedEmptyContainer(nexus)) continue;
                if (IsDefusedTrap(nexus)) continue;
                if (!MatchesCategory(nexus, currentCategory)) continue;
                filteredInteractables.Add(nexus);
            }

            // Sort by distance (nearest first)
            filteredInteractables = filteredInteractables
                .OrderBy(n => Vector3.Distance(n.InstigatePoint, playerPos))
                .ToList();

            ModLog.Debug($"Filtered interactables: {filteredInteractables.Count} found (category: {currentCategory})");
        }

        /// <summary>
        /// Builds the active point category's list, sorted nearest-first. Rebuilt on each
        /// cycle (on a key press, not per frame), so a scene scan here is fine.
        /// </summary>
        private static void UpdatePointList(Vector3 playerPos)
        {
            points.Clear();

            if (currentCategory == InteractableCategory.Labels)
                AddLabelPoints();
            else
                AddCoverPoints();

            points.Sort((a, b) =>
                Vector3.Distance(a.Position, playerPos).CompareTo(Vector3.Distance(b.Position, playerPos)));

            ModLog.Debug($"Point list: {points.Count} points found (category: {currentCategory})");
        }

        /// <summary>
        /// Every tile the player has labelled in this scene. Deliberately not fog-gated:
        /// a label is the player's own note about a place they have already been, so hiding
        /// it behind fog of war would defeat the point of writing it down.
        /// </summary>
        private static void AddLabelPoints()
        {
            foreach (var labelled in LocationLabels.GetForCurrentScene())
                points.Add(new ScanPoint { Position = labelled.Position, Label = labelled.Text });
        }

        /// <summary>
        /// Short / tall cover the player can currently see through the fog, from the Cover
        /// components in the scene.
        /// </summary>
        private static void AddCoverPoints()
        {
            var covers = UnityEngine.Object.FindObjectsOfType<Cover>();
            foreach (var cover in covers)
            {
                if (cover == null || cover.gameObject == null) continue;
                if (!cover.gameObject.activeInHierarchy) continue;
                if (cover.type == Cover.CoverType.Destroyed) continue;

                Vector3 pos = cover.transform.position;
                if (!FOWHelper.IsVisibleThroughFOW(pos)) continue;

                string label = cover.type == Cover.CoverType.Tall ? "Tall cover" : "Short cover";
                points.Add(new ScanPoint { Position = pos, Label = label });
            }
        }

        /// <summary>
        /// True if the nexus exposes something the player can actually target, ruling out
        /// invisible scripting trigger volumes (e.g. AZ4_HiddenBoyTrigger) that register an
        /// InteractableNexus — complete with an enabled Highlight — but offer no interaction.
        /// A real target trips at least one of: it's an InteractableObject (objects, containers,
        /// doors, switches), it has an examine SkillObject, it backs a mob (characters), or it
        /// reports an available interaction such as Poked (map exits / skill interactions).
        /// Verified against [TileTrace] logs: the trigger matches none of these; Harvey's Cart,
        /// RedSkorpionPoster, the WorldLoadGlobe exit, and NPCs each match at least one.
        /// </summary>
        /// <summary>
        /// True once a lootable container has been opened and emptied, so it should drop
        /// off the scanner — otherwise looted containers (the start-area diggables are the
        /// worst case) pile up with no way to tell which still hold loot. The game's own
        /// InteractableInventoryObject.empty getter reports this (the loot locker exists
        /// and Count == 0). Ranger lockers and any container that accepts items are left
        /// in: they stay useful as storage even when empty.
        /// </summary>
        private static bool IsLootedEmptyContainer(InteractableNexus nexus)
        {
            var invObj = nexus.drama as InteractableInventoryObject;
            if (invObj == null) invObj = nexus.GetComponent<InteractableInventoryObject>();
            if (invObj == null) return false;
            if (invObj.isRangerLocker || invObj.willAcceptItems) return false;
            if (!MonoBehaviourSingleton<LootLockerManager>.HasInstance()) return false;
            return invObj.empty;
        }

        /// <summary>
        /// True once a trap-only interactable (land mine, laser tripwire) has been disarmed
        /// or has already gone off, so it should drop off the scanner the same way a looted
        /// container does — a defused mine is scenery, and leaving it in makes the live ones
        /// harder to pick out.
        ///
        /// InteractableObject.isDemolitionsTrapped (state flag 5) is the game's own armed
        /// flag: DisarmedDemolitions() clears it and disables the Demolitions/BoobyTrap
        /// components, and both LandMine and IO_LaserTripwire gate their whole behaviour on
        /// it (LandMine.Event_ASI_examine reports "The land mine has been disarmed" when it
        /// is false). isExploded covers a trap that was triggered rather than defused.
        ///
        /// Deliberately limited to these two types. Other booby-trapped objects — the AZ3
        /// Interceptor's car bomb, trapped chests, the CA1 armory door — still have a
        /// container, door, or quest interaction left once the trap is gone, so they stay.
        /// </summary>
        private static bool IsDefusedTrap(InteractableNexus nexus)
        {
            var obj = nexus.drama as InteractableObject;
            if (obj == null) obj = nexus.GetComponent<InteractableObject>();
            if (obj == null) return false;
            if (!(obj is LandMine) && !(obj is IO_LaserTripwire)) return false;
            return !obj.isDemolitionsTrapped || obj.isExploded;
        }

        public static bool HasInteractionSurface(InteractableNexus nexus)
        {
            if (nexus.drama is InteractableObject) return true;
            if (nexus.skobExamine != null) return true;
            if (nexus.isMob) return true;

            var interactions = nexus.GetAllowedInteractions();
            if (interactions != null)
            {
                foreach (var kvp in interactions)
                {
                    if (kvp.Value == 1) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Every scanner-visible interactable right now — all categories plus party
        /// members — unsorted, applying the same visibility / FOW / perception /
        /// interaction-surface / looted-container / defused-trap filters the cycling
        /// scanner uses.
        /// Callers order it themselves (e.g. the tile cursor sorts by distance from the
        /// cursor rather than from the player). Ignores the current category filter on
        /// purpose: this is the "what's around me" scan, not the cycling view.
        /// </summary>
        public static List<InteractableNexus> GetAllVisibleInteractables()
        {
            var result = new List<InteractableNexus>();
            FOWHelper.UpdateActivationTracking();

            AddPartyNexuses(result);

            foreach (var nexus in InteractableNexus.interactables)
            {
                if (nexus == null) continue;
                if (nexus.transform == null) continue;
                if (!FOWHelper.PassesScannerGate(nexus)) continue;
                if (!HasInteractionSurface(nexus)) continue;
                if (IsLootedEmptyContainer(nexus)) continue;
                if (IsDefusedTrap(nexus)) continue;
                result.Add(nexus);
            }

            return result;
        }

        /// <summary>Public accessor for an interactable's spoken display name.</summary>
        public static string GetDisplayName(InteractableNexus nexus)
        {
            return nexus == null ? null : GetInteractableName(nexus);
        }

        /// <summary>
        /// The single category an interactable belongs to for cueing, chosen by priority
        /// so an item that could match several lands in one bucket. Party (PCs) is reported
        /// as-is so callers can skip it; everything that matches no named subcategory falls
        /// through to Misc (the generic bucket). Mirrors how the cycling scanner classifies.
        /// </summary>
        public static InteractableCategory GetPrimaryCategory(InteractableNexus nexus)
        {
            if (nexus == null) return InteractableCategory.Misc;
            if (MatchesCategory(nexus, InteractableCategory.Party)) return InteractableCategory.Party;
            if (MatchesCategory(nexus, InteractableCategory.Characters)) return InteractableCategory.Characters;
            if (MatchesCategory(nexus, InteractableCategory.Containers)) return InteractableCategory.Containers;
            if (MatchesCategory(nexus, InteractableCategory.Exits)) return InteractableCategory.Exits;
            if (MatchesCategory(nexus, InteractableCategory.Objects)) return InteractableCategory.Objects;
            if (MatchesCategory(nexus, InteractableCategory.Examine)) return InteractableCategory.Examine;
            if (MatchesCategory(nexus, InteractableCategory.Loot)) return InteractableCategory.Loot;
            return InteractableCategory.Misc;
        }

        /// <summary>
        /// World position used for distance / direction to an interactable. Prefers the
        /// instigate point (where the ranger stands to use it); falls back to the transform.
        /// </summary>
        public static Vector3 GetNexusPosition(InteractableNexus nexus)
        {
            if (nexus == null) return Vector3.zero;
            Vector3 p = nexus.InstigatePoint;
            if (p == Vector3.zero && nexus.transform != null) return nexus.transform.position;
            return p;
        }

        private static void UpdatePartyList(Vector3 playerPos)
        {
            AddPartyNexuses(filteredInteractables);

            // Sort by distance
            filteredInteractables = filteredInteractables
                .OrderBy(n => Vector3.Distance(n.transform.position, playerPos))
                .ToList();

            ModLog.Debug($"Filtered party members: {filteredInteractables.Count} found");
        }

        /// <summary>
        /// Appends the active party PCs and party followers (as their InteractableNexus)
        /// to <paramref name="target"/>. They aren't in InteractableNexus.interactables
        /// while conscious, so both the cycling list and the radius scan pull them here.
        /// </summary>
        private static void AddPartyNexuses(List<InteractableNexus> target)
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
                        target.Add(nexus);
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
                        target.Add(nexus);
                    }
                }
            }
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

                case InteractableCategory.Cover:
                case InteractableCategory.Labels:
                    // Point categories: backed by world positions, never by a nexus. Handled
                    // entirely by UpdatePointList, so no interactable belongs to them — and
                    // they are deliberately absent from the Misc exclusions below, since a
                    // labelled tile shouldn't drop its object out of Misc.
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
            // "Unrevealed names" mode: the item is shown so the player can navigate
            // toward it, but its identity stays hidden until genuinely discovered.
            if (FOWHelper.RevealMode == ScannerRevealMode.RevealUnnamed &&
                !FOWHelper.IsDiscoveredNormally(nexus))
                return "Unrevealed";

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

                // Orphaned PickupItem (post-reload corpse with no mob link). Game
                // strips the NPC reference unless the corpse was marked persistent,
                // so we name it by its loot instead. items.displayName resets to
                // the default "<@>Contents" on reload, so don't bother with it.
                var pickup = nexus.drama as PickupItem;
                if (pickup != null)
                {
                    string lootName = BuildPickupLootName(pickup);
                    if (!string.IsNullOrEmpty(lootName)) return lootName;
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

        internal static string BuildPickupLootName(PickupItem pickup)
        {
            if (pickup == null || pickup.items == null) return null;

            int distinctShown = 0;
            int extraStacks = 0;
            var parts = new List<string>();
            foreach (ItemInstance item in pickup.items)
            {
                if (item == null || item.template == null) continue;
                string raw = item.template.displayName;
                if (string.IsNullOrEmpty(raw)) continue;
                string clean = UITextExtractor.CleanText(raw);
                if (string.IsNullOrEmpty(clean)) continue;

                if (distinctShown < 3)
                {
                    parts.Add(item.quantity > 1 ? $"{item.quantity} {clean}" : clean);
                    distinctShown++;
                }
                else
                {
                    extraStacks++;
                }
            }

            if (parts.Count == 0)
            {
                return pickup.items.Count > 0 ? "Loot pile" : "Empty loot pile";
            }

            string body = string.Join(", ", parts.ToArray());
            if (extraStacks > 0) body += $", +{extraStacks} more";
            return "Loot: " + body;
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
