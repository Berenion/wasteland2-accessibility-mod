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

        // FOW readiness tracking. Whenever Game.cs calls FOWSystem.LoadMap (save
        // restore, world-map transition, etc.), every explored cell is written
        // as Color32(255, 255, 0, 0) into mBuffer1 (FOWSystem.cs:851) — i.e.
        // both R=255 and G=255. IsVisible reads mBuffer1.r || mBuffer0.r, so
        // every explored cell reads as visible until UpdateBuffer runs.
        // UpdateBuffer is gated by Time.time, which tactical pause freezes — so
        // if the player pauses before FOW converges, the stale state persists
        // indefinitely. FOWSystem_LoadMap_Patch calls NotifyFOWMapLoaded to
        // mark the window; Tick clears it once we've had enough real time +
        // at least one unpaused frame.
        private const float LoadMapGraceSeconds = 1.0f;
        private static float lastLoadMapRealtime = float.NegativeInfinity;
        private static bool sawUnpausedFrameSinceLoadMap = true;

        public static bool IsVisibleThroughFOW(Vector3 position)
        {
            if (FOWSystem.instance == null) return true;
            return FOWSystem.instance.IsVisible(position);
        }

        /// <summary>
        /// Returns true if a sighted player could see/notice this GameObject right now.
        /// Two branches, both ignore the cached FOWRenderers.isVisible field — Unity
        /// doesn't run LateUpdate on inactive GameObjects, so any object that gets
        /// SetActive(false) (NPCs via SleepManager.cs:134, doors/scenery via level-chunk
        /// culling, etc.) keeps its last mIsVisible value, often the default `true`.
        ///   1. Mobs (anything with a Mob component): require current vision via
        ///      FOWSystem.IsVisible. Matches sighted: FOWRenderers disables their
        ///      renderers entirely outside vision.
        ///   2. Non-mobs (containers, doors, exits, examines, AZ1 Drama quest objects):
        ///      visible iff FOWSystem.IsExplored. These render through the FOWEffect
        ///      post-process at ~20% greyscale in explored fog — dim but technically
        ///      perceivable, and this is what InputManager.CheckInstigateDrama uses for
        ///      sighted click gating when FOWRenderers is absent. In unexplored territory
        ///      sighted players see nothing (FOWEffect renders at unexploredColor =
        ///      (0.05, 0.05, 0.05) — near-black).
        /// </summary>
        public static bool IsVisibleToSighted(GameObject go)
        {
            if (go == null) return false;
            if (FOWSystem.instance == null) return true;

            Mob mob = go.GetComponent<Mob>();
            if (mob != null)
                return FOWSystem.instance.IsVisible(go.transform.position);

            return FOWSystem.instance.IsExplored(go.transform.position);
        }

        /// <summary>
        /// Returns true once FOWSystem has had real time and at least one unpaused
        /// frame since the last LoadMap call. Callers that rely on IsVisibleThroughFOW
        /// should refuse to scan/filter while this is false to avoid the stale-buffer
        /// window where every explored cell reports as visible.
        /// </summary>
        public static bool IsFOWReady()
        {
            if (lastLoadMapRealtime == float.NegativeInfinity) return true;
            if (!sawUnpausedFrameSinceLoadMap) return false;
            return (Time.realtimeSinceStartup - lastLoadMapRealtime) >= LoadMapGraceSeconds;
        }

        /// <summary>
        /// Called from the FOWSystem.LoadMap Harmony postfix. Resets the readiness
        /// state so the next scan waits for FOW to converge.
        /// </summary>
        public static void NotifyFOWMapLoaded()
        {
            lastLoadMapRealtime = Time.realtimeSinceStartup;
            sawUnpausedFrameSinceLoadMap = false;
            ClearActivationTracking();
            MelonLoader.MelonLogger.Msg("[FOWHelper] LoadMap detected — waiting for FOW to converge");
        }

        /// <summary>
        /// Per-frame tick. Records whether we've seen an unpaused frame since the
        /// last LoadMap call — FOWSystem's UpdateBuffer is driven by Time.time, so
        /// it only converges while Time.timeScale > 0.
        /// </summary>
        public static void Tick()
        {
            if (!sawUnpausedFrameSinceLoadMap && Time.timeScale > 0f)
                sawUnpausedFrameSinceLoadMap = true;
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
        /// Event-driven logs only fire on transitions; no periodic dump.
        /// </summary>
        public static void UpdateActivationTracking()
        {
            foreach (var nexus in InteractableNexus.interactables)
            {
                if (nexus == null || nexus.gameObject == null) continue;
                if (!(nexus.drama is InteractableTeleporter)) continue;

                bool active = nexus.gameObject.activeInHierarchy;
                bool isNew = !knownTeleporters.Contains(nexus);

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
                else if (isNew && IsNearParty(nexus.transform.position, NewTeleporterDetectionRange))
                {
                    // Teleporter just appeared in the interactables list near the party.
                    // This handles doors that were never in the list while inactive
                    // (SetActive(false) prevented them from registering).
                    MelonLogger.Msg($"[ActivationTrack] New teleporter near party: {nexus.name} at {nexus.transform.position}");
                    recentlyActivated.Add(nexus);
                }

                knownTeleporters.Add(nexus);
            }
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
        /// Returns true if the interactable should be FILTERED OUT. Covers two
        /// independent cases:
        ///
        /// 1. Perception-REWARD items: skob has a difficulty > None and hasn't
        ///    been perceived yet. Design choice (not strict sighted-fidelity):
        ///    a sighted player can visually see and click these before the
        ///    perception challenge passes (game's mouse path gates on
        ///    skob.hidden, not perceived). But for the accessibility UX we want
        ///    these to remain "a surprise" until the party actually passes the
        ///    perception roll, matching the discovery beat a sighted player
        ///    experiences when the sparkle pops. Items with skob.hidden=true
        ///    are already handled upstream by nexus.isVisible.
        ///
        /// 2. Teleporters whose destination is far away and in unexplored fog —
        ///    avoids revealing paths the party hasn't discovered. Runtime-
        ///    activated teleporters (perception-revealed ShortcutDoors) bypass.
        /// </summary>
        public static bool IsPerceptionGated(InteractableNexus nexus)
        {
            if (nexus == null) return false;

            SkillObject_Examine skob = nexus.skobExamine;
            if (skob == null && nexus.gameObject != null)
                skob = nexus.gameObject.GetComponent<SkillObject_Examine>();

            if (skob != null && skob.difficulty != SkillLevelCategory.None && !skob.perceived)
                return true;

            if (nexus.drama != null && FOWSystem.instance != null)
            {
                var teleporter = nexus.drama as InteractableTeleporter;
                if (teleporter != null && teleporter.targetTransform != null)
                {
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
