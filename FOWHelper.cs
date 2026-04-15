using System.Collections.Generic;
using UnityEngine;
using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Shared fog of war visibility check used across the mod to avoid
    /// announcing objects that sighted players cannot see.
    /// </summary>
    public static class FOWHelper
    {
        /// <summary>
        /// Maximum distance from a party member for detecting newly-activated
        /// teleporters that appear in the interactables list at runtime.
        /// Only used for activation tracking, NOT for general visibility.
        /// </summary>
        private const float NewTeleporterDetectionRange = 10f;

        /// <summary>
        /// Tracks teleporter nexuses that were observed as inactive (SetActive false)
        /// and later became active. These are runtime-activated doors (e.g. ShortcutDoors
        /// after a perception check) and should bypass the teleporter destination filter.
        /// </summary>
        private static HashSet<InteractableNexus> seenInactive = new HashSet<InteractableNexus>();
        private static HashSet<InteractableNexus> recentlyActivated = new HashSet<InteractableNexus>();

        /// <summary>
        /// All teleporter nexuses seen in previous tracking passes. Used to detect
        /// teleporters that are newly added to the interactables list at runtime
        /// (e.g. doors that were SetActive(false) and never in the list until activated).
        /// </summary>
        private static HashSet<InteractableNexus> knownTeleporters = new HashSet<InteractableNexus>();

        public static bool IsVisibleThroughFOW(Vector3 position)
        {
            if (FOWSystem.instance == null) return true;
            return FOWSystem.instance.IsVisible(position);
        }

        private static bool IsNearParty(Vector3 position, float range)
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;
            var game = MonoBehaviourSingleton<Game>.GetInstance();
            if (game.party == null) return false;

            float rangeSq = range * range;
            foreach (var pc in game.party)
            {
                if (pc == null || pc.transform == null) continue;
                float distSq = (pc.transform.position - position).sqrMagnitude;
                if (distSq <= rangeSq) return true;
            }
            return false;
        }

        /// <summary>
        /// Call periodically to track teleporter nexuses that transition from
        /// inactive to active. This identifies doors activated at runtime
        /// (e.g. by perception checks) vs doors that were always present.
        /// </summary>
        private static float lastTrackDiagTime = 0f;

        public static void UpdateActivationTracking()
        {
            bool doDiag = (Time.time - lastTrackDiagTime) > 5f;
            if (doDiag) lastTrackDiagTime = Time.time;

            int teleporterCount = 0;
            foreach (var nexus in InteractableNexus.interactables)
            {
                if (nexus == null || nexus.gameObject == null) continue;

                bool isTeleporter = nexus.drama is InteractableTeleporter;
                if (!isTeleporter) continue;

                teleporterCount++;
                bool active = nexus.gameObject.activeInHierarchy;
                bool isNew = !knownTeleporters.Contains(nexus);

                if (doDiag)
                    MelonLogger.Msg($"[ActivationTrack] Teleporter: {nexus.name} active={active} pos={nexus.transform.position} isNew={isNew} inSeenInactive={seenInactive.Contains(nexus)} inRecentlyActivated={recentlyActivated.Contains(nexus)}");

                if (!active)
                {
                    if (!seenInactive.Contains(nexus))
                        MelonLogger.Msg($"[ActivationTrack] Recording inactive teleporter: {nexus.name} at {nexus.transform.position}");
                    seenInactive.Add(nexus);
                }
                else if (seenInactive.Contains(nexus))
                {
                    MelonLogger.Msg($"[ActivationTrack] Teleporter activated (inactive→active): {nexus.name} at {nexus.transform.position}");
                    recentlyActivated.Add(nexus);
                    seenInactive.Remove(nexus);
                }
                else if (isNew && active && IsNearParty(nexus.transform.position, NewTeleporterDetectionRange))
                {
                    // Teleporter just appeared in the interactables list near the party.
                    // This handles doors that were never in the list while inactive
                    // (SetActive(false) prevented them from registering).
                    MelonLogger.Msg($"[ActivationTrack] New teleporter near party: {nexus.name} at {nexus.transform.position}");
                    recentlyActivated.Add(nexus);
                }

                knownTeleporters.Add(nexus);
            }

            if (doDiag)
                MelonLogger.Msg($"[ActivationTrack] Summary: {teleporterCount} teleporters, {seenInactive.Count} seenInactive, {recentlyActivated.Count} recentlyActivated");
        }

        /// <summary>
        /// Clears activation tracking state. Call on scene/map change.
        /// </summary>
        public static void ClearActivationTracking()
        {
            seenInactive.Clear();
            recentlyActivated.Clear();
            knownTeleporters.Clear();
        }

        /// <summary>
        /// Directly marks a nexus as recently activated, bypassing the teleporter
        /// destination filter in IsPerceptionGated. Use this when a Harmony patch
        /// observes a door being activated by a perception check.
        /// </summary>
        public static void MarkAsRecentlyActivated(InteractableNexus nexus)
        {
            if (nexus == null) return;
            MelonLogger.Msg($"[FOWHelper] Marking nexus as recently activated: {nexus.name}");
            recentlyActivated.Add(nexus);
            seenInactive.Remove(nexus);
        }

        /// <summary>
        /// Checks if an interactable is gated behind a Perception check that hasn't
        /// been passed yet. Objects with SkillObject_Examine that have a difficulty
        /// above None and haven't been perceived should not be shown to the player.
        /// Returns true if the object should be FILTERED OUT (hidden from the player).
        /// </summary>
        public static bool IsPerceptionGated(InteractableNexus nexus)
        {
            if (nexus == null) return false;

            // Check SkillObject_Examine on the nexus itself
            SkillObject_Examine skob = nexus.skobExamine;
            if (skob == null && nexus.gameObject != null)
                skob = nexus.gameObject.GetComponent<SkillObject_Examine>();

            if (skob != null && skob.difficulty != SkillLevelCategory.None && !skob.perceived)
                return true;

            // Filter teleporters whose destination is far away and in unexplored fog of war.
            // Skip this filter for teleporters that were runtime-activated (transitioned
            // from inactive to active), as these are doors revealed by perception checks.
            if (nexus.drama != null && FOWSystem.instance != null)
            {
                var teleporter = nexus.drama as InteractableTeleporter;
                if (teleporter != null && teleporter.targetTransform != null)
                {
                    // Runtime-activated teleporters bypass this filter
                    if (recentlyActivated.Contains(nexus))
                        return false;

                    Vector3 destPos = teleporter.targetTransform.position;
                    float dist = Vector3.Distance(nexus.transform.position, destPos);
                    if (dist > 100f && !FOWSystem.instance.IsExplored(destPos))
                        return true;
                }
            }

            return false;
        }
    }
}
