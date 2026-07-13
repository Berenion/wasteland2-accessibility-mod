using System.Collections.Generic;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Watches the scanner-visible set during exploration and plays a per-category cue
    /// (see <see cref="ScannerCueSounds"/>) when a new item appears — e.g. rounding a
    /// corner reveals a container, or an NPC walks into perception. Lets the player notice
    /// things entering the scanner without cycling it.
    ///
    /// Ticked every frame from MelonMod.OnUpdate but only does real work a few times a
    /// second (SecondsBetweenScans). One cue per category per scan even if several items of
    /// that category appear at once, so a revealed room is a couple of blips, not a
    /// cacophony. The first scan after (re)entering exploration — and the post-load grace
    /// window — seed the seen-set silently so the initial reveal doesn't fire a burst.
    /// Party (PCs) and the All bucket have no cue: All is covered by each item's own
    /// subcategory, and Misc is the generic cue for items in no named subcategory.
    /// </summary>
    public static class ScannerCueMonitor
    {
        // Scan cadence — responsive enough to feel immediate, cheap enough not to sweep the
        // interactable list every frame.
        private const float SecondsBetweenScans = 0.3f;

        // How long an item must have been gone before reappearing counts as "new" again.
        // Absorbs fog-of-war flicker at a visibility edge without permanently muting a
        // genuine re-entry.
        private const float ReappearCooldownSeconds = 4f;

        // Drop tracking for an id unseen this long, to bound the dictionary.
        private const float ForgetSeconds = 30f;

        // After entering exploration, loading, or a detected stall, stay silent
        // (record-only) for this long so the initial reveal — and items that stream
        // in over the first moment — seed the seen-set without firing a burst of cues.
        private const float PrimeWindowSeconds = 2f;

        // A gap between consecutive scans larger than this means the update loop
        // stalled: the window lost focus (tab-out), the game hitched, or a load ran
        // without leaving the exploration context. On the next scan we re-arm the
        // silent prime window instead of treating every still-visible item as newly
        // appeared (which, past ReappearCooldownSeconds, blasts one cue per category).
        private const float GapReprimeSeconds = 1f;

        // id -> last realtime we saw it visible.
        private static readonly Dictionary<int, float> lastSeen = new Dictionary<int, float>();
        // Realtime until which scans stay silent (record-only). Armed on reset / load
        // / detected stall.
        private static float primeUntilTime = -1f;
        private static float lastScanTime = -999f;

        // Reused scratch to avoid per-scan allocation.
        private static readonly HashSet<int> currentIds = new HashSet<int>();
        private static readonly List<int> staleIds = new List<int>();

        public static void Tick()
        {
            if (!ModConfig.ScannerCategorySounds) { ResetState(); return; }

            if (!InExplorationContext()) { ResetState(); return; }
            // FOW not yet converged after a load: stay quiet and keep the prime
            // window armed so the first real scan re-seeds silently.
            if (!FOWHelper.IsFOWReady()) { ArmPrimeWindow(); return; }

            float now = Time.realtimeSinceStartup;
            float gap = now - lastScanTime;
            if (gap < SecondsBetweenScans) return;
            lastScanTime = now;

            // Stall detected (tab-out, hitch, or in-context load) — re-arm the
            // silent window rather than blasting a cue for every item at once.
            if (gap > GapReprimeSeconds) ArmPrimeWindow();

            var visible = NavigationManager.GetAllVisibleInteractables();

            currentIds.Clear();
            // Categories that had at least one genuinely-new item this scan.
            HashSet<InteractableCategory> toCue = null;

            // Silent while inside the prime window (post-entry / post-load / post-stall)
            // or during the bulk-inventory window: record everything as seen but don't cue.
            bool silent = now < primeUntilTime || GameLoadState.IsBulkInventoryWindow();

            for (int i = 0; i < visible.Count; i++)
            {
                var nexus = visible[i];
                if (nexus == null) continue;
                // Cues follow genuine fog-of-war discovery, not the reveal-mode
                // setting: a reveal mode adds undiscovered items to the visible set,
                // but we don't want a burst of cues (or to poison the seen-set so a
                // later real discovery is silenced). In Normal mode every item here
                // is already discovered, so this is a no-op.
                if (!FOWHelper.IsDiscoveredNormally(nexus)) continue;

                int id = nexus.GetInstanceID();
                currentIds.Add(id);

                bool isNew;
                float seenAt;
                if (!lastSeen.TryGetValue(id, out seenAt)) isNew = true;
                else isNew = (now - seenAt) >= ReappearCooldownSeconds;

                lastSeen[id] = now;

                if (silent || !isNew) continue;

                InteractableCategory cat = NavigationManager.GetPrimaryCategory(nexus);
                if (!ScannerCueSounds.HasCue(cat)) continue; // Party / anything unmapped

                if (toCue == null) toCue = new HashSet<InteractableCategory>();
                toCue.Add(cat);
            }

            PruneStale(now);

            if (toCue != null)
            {
                foreach (InteractableCategory cat in toCue)
                    ScannerCueSounds.Play(cat);
            }
        }

        private static bool InExplorationContext()
        {
            if (!MonoBehaviourSingleton<Game>.HasInstance()) return false;
            var game = MonoBehaviourSingleton<Game>.GetInstance();
            if (game.state != GameState.Gameplay && game.state != GameState.RandomEncounter) return false;
            if (Drama.isConversationOn || Drama.isCutsceneOn) return false;
            if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return false;
            if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive()) return false;
            return true;
        }

        private static void PruneStale(float now)
        {
            // Forget ids not seen recently AND not currently visible.
            staleIds.Clear();
            foreach (var kv in lastSeen)
            {
                if (!currentIds.Contains(kv.Key) && (now - kv.Value) >= ForgetSeconds)
                    staleIds.Add(kv.Key);
            }
            for (int i = 0; i < staleIds.Count; i++)
                lastSeen.Remove(staleIds[i]);
        }

        private static void ResetState()
        {
            if (lastSeen.Count > 0) lastSeen.Clear();
            // Re-arm so the next time we enter exploration the reveal seeds silently.
            ArmPrimeWindow();
        }

        /// <summary>
        /// Opens (or extends) the silent prime window from now. Scans stay record-only
        /// until it elapses, so a burst of freshly-visible items never all cue at once.
        /// </summary>
        private static void ArmPrimeWindow()
        {
            primeUntilTime = Time.realtimeSinceStartup + PrimeWindowSeconds;
        }
    }
}
