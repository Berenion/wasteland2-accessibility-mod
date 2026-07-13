using System.Collections.Generic;
using UnityEngine;
using MelonLoader;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Controls how much fog-hidden content the scanner and tile cursor surface.
    /// A player-facing accessibility aid (not a fidelity choice): some users want
    /// to navigate the whole map regardless of what the party has actually seen.
    /// </summary>
    public enum ScannerRevealMode
    {
        /// <summary>Normal fog-of-war fidelity: only what a sighted player could see.</summary>
        Normal = 0,
        /// <summary>Reveal every loaded interactable, fogged or not, with real names.</summary>
        RevealAll = 1,
        /// <summary>Reveal fogged interactables but mask undiscovered ones as "unrevealed".</summary>
        RevealUnnamed = 2
    }

    /// <summary>
    /// Shared fog of war visibility check used across the mod to avoid
    /// announcing objects that sighted players cannot see.
    /// </summary>
    public static class FOWHelper
    {
        /// <summary>
        /// Active reveal mode for the scanner / tile cursor. Set from ModConfig on
        /// load and when the accessibility settings menu cycles it. Only the
        /// mode-aware gate (<see cref="PassesScannerGate"/>) and name masking read
        /// this — the strict primitives (IsVisibleToSighted, IsPerceptionGated,
        /// IsDiscoveredNormally) stay true to real fog state regardless.
        /// </summary>
        public static ScannerRevealMode RevealMode { get; set; } = ScannerRevealMode.Normal;

        /// <summary>Spoken value for the settings menu, e.g. "off" / "reveal all".</summary>
        public static string RevealModeText(ScannerRevealMode mode)
        {
            switch (mode)
            {
                case ScannerRevealMode.RevealAll: return "reveal all";
                case ScannerRevealMode.RevealUnnamed: return "unrevealed names";
                default: return "off";
            }
        }

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
        /// Mirrors the game's own click gate, InputManager.CheckInstigateDrama
        /// (InputManager.cs:2338): a target is interactable when it has a FOWRenderers
        /// and that renderer reports visible, OR it has no FOWRenderers and its tile is
        /// explored.
        ///   1. Mobs (anything with a Mob component — NPCs, enemies) carry a FOWRenderers
        ///      added at runtime. The game gates their clicks on FOWRenderers.isVisible,
        ///      NOT FOWSystem.IsVisible(position); those differ for an NPC that is rendered
        ///      and clickable at a gate/doorway while its tile isn't lit in the FOW buffer
        ///      (e.g. door-guard NPCs, pre-recruit companions). So we trust
        ///      FOWRenderers.isVisible while the GameObject is active (LateUpdate keeps it
        ///      fresh). The one case that field lies is SetActive(false) NPCs (SleepManager.
        ///      cs:134, level-chunk culling): LateUpdate never runs, so mIsVisible stays at
        ///      its stale, often default-true value. For those we fall back to a live
        ///      FOWSystem.IsVisible query — an inactive object isn't clickable anyway.
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
            {
                FOWRenderers fow = go.GetComponent<FOWRenderers>();
                if (fow != null && go.activeInHierarchy)
                    return fow.isVisible;
                return FOWSystem.instance.IsVisible(go.transform.position);
            }

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
            ModLog.Debug("[FOWHelper] LoadMap detected — waiting for FOW to converge");
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
                        ModLog.Debug($"[ActivationTrack] Recording inactive teleporter: {nexus.name} at {nexus.transform.position}");
                    seenInactive.Add(nexus);
                }
                else if (seenInactive.Contains(nexus))
                {
                    ModLog.Debug($"[ActivationTrack] Teleporter activated (inactive→active): {nexus.name} at {nexus.transform.position}");
                    recentlyActivated.Add(nexus);
                    seenInactive.Remove(nexus);
                }
                else if (isNew && IsNearParty(nexus.transform.position, NewTeleporterDetectionRange))
                {
                    // Teleporter just appeared in the interactables list near the party.
                    // This handles doors that were never in the list while inactive
                    // (SetActive(false) prevented them from registering).
                    ModLog.Debug($"[ActivationTrack] New teleporter near party: {nexus.name} at {nexus.transform.position}");
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
            ModLog.Debug($"[FOWHelper] Marking nexus as recently activated: {nexus.name}");
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
        ///    activated teleporters (perception-revealed ShortcutDoors) bypass,
        ///    as do normal pokable "door" teleporters the party can already see
        ///    and walk up to (cave entrances, building doors).
        /// </summary>
        public static bool IsPerceptionGated(InteractableNexus nexus)
        {
            if (nexus == null) return false;

            // Mobs (NPCs, enemies) are never perception-gated. The character is standing
            // in plain sight and must stay selectable so the player can talk to / recruit /
            // fight them. Some NPCs (Angela Deth, door-guard grenadiers, etc.) carry a
            // SkillObject_Examine with a difficulty — that is a bonus lore descriptor, NOT
            // a hidden-until-perceived reward. The game's own click gate
            // (InputManager.CheckInstigateDrama) blocks interaction only on skob.hidden,
            // never on !perceived, and mob.isHidden is already handled upstream by
            // nexus.isVisible. The perception-reward hiding below is meant for inanimate
            // hidden stashes only; applied to mobs it wrongly makes recruitable/quest NPCs
            // unselectable.
            if (nexus.isMob)
                return false;

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

                    // A teleporter the party can already see and walk up to — a
                    // normal pokable "door" like the Snarly Cave entrances or any
                    // building door — is something a sighted player sees and uses,
                    // so reveal it regardless of where it leads. IsVisibleToSighted
                    // (checked before this method at every call site) has already
                    // confirmed the door's own position is in explored fog. The
                    // destination filter below only guards blocked/passive shortcut
                    // teleporters whose far end is still unexplored.
                    if (!teleporter.blockPoke && !teleporter.bInstigateBlocked)
                        return false;

                    Vector3 destPos = teleporter.targetTransform.position;
                    float dist = Vector3.Distance(nexus.transform.position, destPos);
                    if (dist > 100f && !FOWSystem.instance.IsExplored(destPos))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The single visibility gate every scanner / tile-cursor list should use.
        /// In Normal mode it's exactly the strict triad (game-visible, sighted-FOW,
        /// not perception-gated) callers previously inlined. In a reveal mode it
        /// drops the FOW / perception filters and shows every loaded interactable,
        /// but still respects scripted invisibility (nexus.isHidden / mob.isHidden)
        /// — those aren't fog, and forcing them visible would surface objects the
        /// game deliberately un-rendered (story reveals, dead triggers).
        ///
        /// Note the hard limit: SleepManager SetActive(false)s NPCs far from the
        /// party, so reveal mode cannot surface distance-culled or un-spawned mobs;
        /// it reveals fog-hidden but loaded content only.
        /// </summary>
        public static bool PassesScannerGate(InteractableNexus nexus)
        {
            if (nexus == null || nexus.gameObject == null) return false;
            if (RevealMode == ScannerRevealMode.Normal)
                return IsDiscoveredNormally(nexus);
            // Mobs (characters, enemies, corpses that still link a Mob) keep their
            // normal fog filter for now — reveal mode only surfaces inanimate
            // interactables (containers, doors, examines, exits, loot, misc). This
            // also keeps the live-mob tile path (FindMobsOnTile) and the scanner
            // Characters category consistent, and avoids masking live mob names.
            if (nexus.isMob)
                return IsDiscoveredNormally(nexus);
            return IsRevealVisible(nexus);
        }

        /// <summary>
        /// True if the party would genuinely see/notice this interactable under
        /// normal fog-of-war rules. This is the strict triad — independent of
        /// <see cref="RevealMode"/> — used both by Normal-mode gating and by name
        /// masking (a reveal-mode item that fails this reads as "unrevealed").
        /// </summary>
        public static bool IsDiscoveredNormally(InteractableNexus nexus)
        {
            if (nexus == null || nexus.gameObject == null) return false;
            if (!nexus.isVisible) return false;
            if (!IsVisibleToSighted(nexus.gameObject)) return false;
            if (IsPerceptionGated(nexus)) return false;
            return true;
        }

        /// <summary>
        /// Reveal-mode visibility for a non-mob interactable: mirrors
        /// InteractableNexus.isVisible minus its FOWRenderers clause, so fogged-
        /// but-loaded objects show while scripted-invisible ones stay hidden.
        /// (Mobs never reach here — PassesScannerGate routes them to the strict
        /// path — so no mob.isHidden check is needed.)
        /// </summary>
        private static bool IsRevealVisible(InteractableNexus nexus)
        {
            return !nexus.isHidden;
        }
    }
}
