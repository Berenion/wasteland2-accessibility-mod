using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace Wasteland2AccessibilityMod.Core
{
    /// <summary>
    /// Watches party movement during exploration and on the world map and announces when
    /// the party (or an individual ungrouped member) finishes an ordered move and comes to
    /// rest — so a player who can't see the map knows the move has completed.
    ///
    /// There is no usable game-side "movement ended" event here: Mob.FinishedMoving /
    /// EventInfo_MobMoveEnded fire only from the combat AIAction_* system, never from the
    /// free navmesh movement used in exploration or on the world map. So this polls the
    /// navMeshAgent path state each frame (the same signal the manual Backspace stop reads)
    /// and fires on a moving -> stopped transition.
    ///
    /// Ticked every frame from MelonMod.OnUpdate, independent of which input state owns
    /// input, so it keeps watching even while a menu or the scanner has focus. The
    /// transition is debounced so a redirect (which briefly clears the path) or a between-
    /// corner recompute doesn't read as a stop.
    /// </summary>
    public static class PartyStopMonitor
    {
        /// <summary>
        /// How long (unscaled seconds) a unit must stay pathless after moving before the
        /// stop is announced. Covers the world-map redirect gap (StopAndClear clears the
        /// path for a few frames before StartMoving's coroutine sets the new one) and any
        /// single-frame path recompute. Long enough to swallow those, short enough that a
        /// real arrival is announced promptly.
        /// </summary>
        private const float StopDebounceSeconds = 0.35f;

        // --- Exploration per-PC state (keyed by PC instance id) ---
        // A PC is in wasMoving once it starts an ordered move; it leaves on announce or on
        // context reset. Idle members that never moved are never added, so they don't gate
        // the grouped "whole party stopped" announcement.
        private static readonly HashSet<int> wasMoving = new HashSet<int>();
        private static readonly Dictionary<int, float> stopPendingSince = new Dictionary<int, float>();

        // --- World-map (single party icon) state ---
        private static bool worldMapWasMoving;
        private static float worldMapStopPendingSince = -1f;

        // Global suppression window: set by the manual (Backspace) stop handlers so their
        // own "Party stopped" confirmation isn't duplicated by the poll-detected stop.
        private static float suppressUntil = -1f;

        // Scratch lists reused each tick to avoid per-frame allocation.
        private static readonly List<PC> stoppedThisFrame = new List<PC>();
        private static readonly List<int> staleIds = new List<int>();

        /// <summary>
        /// Silence auto stop announcements for a short window. Called by the manual
        /// (Backspace) stop handlers, which speak their own confirmation.
        /// </summary>
        public static void SuppressNextStop()
        {
            suppressUntil = Time.unscaledTime + 0.6f;
        }

        private static bool Suppressed()
        {
            return Time.unscaledTime < suppressUntil;
        }

        public static void Tick()
        {
            if (!ModConfig.AnnouncePartyStopped) { Reset(); return; }
            if (!MonoBehaviourSingleton<Game>.HasInstance()) { Reset(); return; }

            Game game = MonoBehaviourSingleton<Game>.GetInstance();

            // World map: the party is a single icon (WorldMapParty), no separate members.
            if (game.state == GameState.WorldMap)
            {
                ResetExploration();
                // A world-map drama (radio call, ambush) can pause or clear the path for
                // reasons other than arrival — don't read those as a completed move.
                if (Drama.isConversationOn || Drama.isCutsceneOn)
                    ResetWorldMap();
                else
                    TickWorldMap();
                return;
            }

            ResetWorldMap();

            // Exploration / random encounter only, and never during combat or drama —
            // agents stop for those reasons, which isn't an ordered move completing.
            bool inCombat = MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                            MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat;
            if ((game.state != GameState.Gameplay && game.state != GameState.RandomEncounter) ||
                inCombat || Drama.isConversationOn || Drama.isCutsceneOn)
            {
                ResetExploration();
                return;
            }

            TickExploration(game);
        }

        private static void TickWorldMap()
        {
            WorldMapParty party = WorldMapParty.instance;
            if (party == null) { ResetWorldMap(); return; }

            bool movingNow = party.HasPath();
            if (movingNow)
            {
                worldMapWasMoving = true;
                worldMapStopPendingSince = -1f;
                return;
            }

            if (!worldMapWasMoving) return;

            float now = Time.unscaledTime;
            if (worldMapStopPendingSince < 0f)
            {
                worldMapStopPendingSince = now;
            }
            else if (now - worldMapStopPendingSince >= StopDebounceSeconds)
            {
                worldMapWasMoving = false;
                worldMapStopPendingSince = -1f;
                if (!Suppressed())
                {
                    ScreenReaderManager.Speak("Party stopped");
                    ModLog.Debug("[PartyStopMonitor] World-map party stopped");
                }
            }
        }

        private static void TickExploration(Game game)
        {
            var party = game.party;
            if (party == null) { ResetExploration(); return; }

            bool grouped = MonoBehaviourSingleton<InputManager>.HasInstance() &&
                           MonoBehaviourSingleton<InputManager>.GetInstance().isPartyGrouped;

            float now = Time.unscaledTime;
            stoppedThisFrame.Clear();

            for (int i = 0; i < party.Count; i++)
            {
                PC pc = party[i];
                if (pc == null) continue;

                int id = pc.GetInstanceID();
                if (IsMoving(pc))
                {
                    wasMoving.Add(id);
                    stopPendingSince.Remove(id);
                }
                else if (wasMoving.Contains(id))
                {
                    if (!stopPendingSince.ContainsKey(id))
                    {
                        stopPendingSince[id] = now;
                    }
                    else if (now - stopPendingSince[id] >= StopDebounceSeconds)
                    {
                        stoppedThisFrame.Add(pc);
                        wasMoving.Remove(id);
                        stopPendingSince.Remove(id);
                    }
                }
            }

            if (stoppedThisFrame.Count > 0 && !Suppressed())
            {
                if (grouped)
                {
                    // Grouped party moves as a unit — announce once, when the last member
                    // has come to rest (nothing left moving or pending a debounced stop).
                    if (wasMoving.Count == 0)
                    {
                        ScreenReaderManager.Speak("Party stopped");
                        ModLog.Debug("[PartyStopMonitor] Grouped party stopped");
                    }
                }
                else
                {
                    // Ungrouped: members move independently — name each one that stopped.
                    for (int i = 0; i < stoppedThisFrame.Count; i++)
                    {
                        string name = GetName(stoppedThisFrame[i]);
                        ScreenReaderManager.Speak($"{name} stopped");
                        ModLog.Debug($"[PartyStopMonitor] Member stopped: {name}");
                    }
                }
            }

            PruneStaleMembers(party);
        }

        private static bool IsMoving(PC pc)
        {
            NavMeshAgent agent = pc.navMeshAgent;
            return agent != null && agent.enabled && (agent.hasPath || agent.pathPending);
        }

        private static string GetName(PC pc)
        {
            try
            {
                return UITextExtractor.CleanText(
                    Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[PartyStopMonitor] Could not resolve PC name: {ex.Message}");
                return "Ranger";
            }
        }

        /// <summary>
        /// Drop tracking for ids no longer in the party (a member left / died) so the sets
        /// don't retain stale handles across roster changes.
        /// </summary>
        private static void PruneStaleMembers(System.Collections.Generic.List<PC> party)
        {
            if (wasMoving.Count == 0 && stopPendingSince.Count == 0) return;

            staleIds.Clear();
            foreach (int id in wasMoving)
            {
                bool present = false;
                for (int i = 0; i < party.Count; i++)
                {
                    if (party[i] != null && party[i].GetInstanceID() == id) { present = true; break; }
                }
                if (!present) staleIds.Add(id);
            }
            for (int i = 0; i < staleIds.Count; i++)
            {
                wasMoving.Remove(staleIds[i]);
                stopPendingSince.Remove(staleIds[i]);
            }
        }

        private static void Reset()
        {
            ResetExploration();
            ResetWorldMap();
        }

        private static void ResetExploration()
        {
            wasMoving.Clear();
            stopPendingSince.Clear();
        }

        private static void ResetWorldMap()
        {
            worldMapWasMoving = false;
            worldMapStopPendingSince = -1f;
        }
    }
}
