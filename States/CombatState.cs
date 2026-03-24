using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Accessibility state for combat. Provides initiative tracker and will be
    /// the foundation for all combat accessibility features.
    /// Priority 45 - above MapCursorState(30) but below menu states(50+).
    /// </summary>
    public class CombatState : IAccessibilityState
    {
        public string Name => "Combat";
        public int Priority => 45;

        // Reflection cache for private CombatManager.curActor
        private static FieldInfo curActorField;

        // Initiative browsing state
        private bool browsingInitiative = false;
        private int initiativeIndex = 0;
        private List<InitiativeEntry> initiativeList = new List<InitiativeEntry>();

        private class InitiativeEntry
        {
            public string Name;
            public bool IsHostile;
            public bool IsCurrentActor;
            public Mob Mob; // null if entry is a bomb or mob couldn't be resolved
            public string Details;
        }

        public bool IsActive
        {
            get
            {
                if (!MonoBehaviourSingleton<CombatManager>.HasInstance()) return false;
                if (!MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat) return false;

                // Don't intercept input if a menu/dialog is open over combat
                if (MonoBehaviourSingleton<GUIManager>.HasInstance() &&
                    MonoBehaviourSingleton<GUIManager>.GetInstance().IsAnyMenuActive())
                    return false;
                // Note: Do NOT check Drama.isConversationOn here — combat barks set it
                // true during combat, which would disable us. ConversationState (priority 50)
                // handles real conversations above us already.

                return true;
            }
        }

        public bool HandleInput()
        {
            // T key: open/refresh initiative tracker (turn order)
            // T is mapped to "Center On Character" in cInput, so we must suppress game input
            if (Input.GetKeyDown(KeyCode.T))
            {
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (browsingInitiative)
                {
                    ExitInitiativeBrowse();
                }
                else
                {
                    OpenInitiativeTracker();
                }
                return true;
            }

            // While browsing initiative list
            if (browsingInitiative)
            {
                // Always suppress all input while browsing
                InputSuppressor.ShouldSuppressUINavigation = true;
                InputSuppressor.ShouldSuppressGameInput = true;
                InputSuppressor.ShouldSuppressButtonEvents = true;

                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    CycleInitiativeForward();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    CycleInitiativeBackward();
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    ExitInitiativeBrowse();
                    // Prevent the "Back" event from bleeding into the next frame and opening the pause menu
                    EventManager.ignoreNextBack = true;
                    return true;
                }

                return true;
            }

            return false;
        }

        public void OnActivated()
        {
            MelonLogger.Msg("[CombatState] Activated - combat started");
        }

        public void OnDeactivated()
        {
            browsingInitiative = false;
            initiativeList.Clear();
            MelonLogger.Msg("[CombatState] Deactivated - combat ended");
        }

        // --- Helpers ---

        private Mob GetCurrentActor()
        {
            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return null;

            if (curActorField == null)
            {
                curActorField = typeof(CombatManager).GetField("curActor",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            return curActorField?.GetValue(cm) as Mob;
        }

        // --- Initiative Tracker ---

        private void OpenInitiativeTracker()
        {
            BuildInitiativeList();

            if (initiativeList.Count == 0)
            {
                ScreenReaderManager.SpeakInterrupt("No initiative data available");
                return;
            }

            browsingInitiative = true;
            initiativeIndex = 0;

            // Announce summary then first entry
            string summary = $"Initiative order, {initiativeList.Count} combatants. ";
            summary += FormatEntry(initiativeList[0]);

            ScreenReaderManager.SpeakInterrupt(summary);
        }

        private void BuildInitiativeList()
        {
            initiativeList.Clear();

            var cm = MonoBehaviourSingleton<CombatManager>.GetInstance();
            if (cm == null) return;

            Mob currentActor = GetCurrentActor();

            // Read from displayQueue - the game's own predicted turn order
            var displayQueue = cm.displayQueue;
            if (displayQueue == null || displayQueue.Count == 0) return;

            for (int i = 0; i < displayQueue.Count; i++)
            {
                var actor = displayQueue[i];
                if (actor == null) continue;

                var entry = new InitiativeEntry
                {
                    Name = GetDisplayName(actor.name),
                    IsHostile = actor.isHostile,
                    IsCurrentActor = false,
                    Mob = null,
                    Details = ""
                };

                // Try to resolve the Mob from the gameObject
                if (actor.gameObject != null)
                {
                    var mob = actor.gameObject.GetComponent<Mob>();
                    if (mob != null)
                    {
                        entry.Mob = mob;
                        entry.IsCurrentActor = (mob == currentActor);
                        entry.Details = BuildMobDetails(mob);
                    }
                }

                // If this is the first entry and name is "Bomb", mark it differently
                if (actor.name == "Bomb")
                {
                    entry.Name = "Bomb";
                    entry.Details = "explosive";
                }

                initiativeList.Add(entry);
            }
        }

        private string GetDisplayName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return "Unknown";
            return UITextExtractor.CleanText(Language.Localize(rawName, false, false, string.Empty));
        }

        private string BuildMobDetails(Mob mob)
        {
            var parts = new List<string>();

            // HP info
            float maxHP = mob.stats.GetMaxHP();
            if (maxHP > 0)
            {
                float hpPercent = (mob.curHP / maxHP) * 100f;
                parts.Add($"{mob.curHP:F0} of {maxHP:F0} HP, {hpPercent:F0}%");
            }

            // AP info (only meaningful for current actor)
            if (GetCurrentActor() == mob)
            {
                parts.Add($"{mob.combatActionPointsRemaining} AP remaining");
            }

            // State info
            if (mob.mobState == Mob.MobState.UNCONSCIOUS)
                parts.Add("unconscious");
            if (mob.inCover)
                parts.Add(mob.coverType == Cover.CoverType.Tall ? "in tall cover" : "in short cover");
            if (mob.isCrouching)
                parts.Add("crouching");
            if (mob.isHidden)
                parts.Add("hidden");

            return string.Join(", ", parts.ToArray());
        }

        private string FormatEntry(InitiativeEntry entry)
        {
            var parts = new List<string>();

            // Position in order
            int position = initiativeList.IndexOf(entry) + 1;

            // Current actor marker
            if (entry.IsCurrentActor)
                parts.Add($"{position}. {entry.Name}, current turn");
            else
                parts.Add($"{position}. {entry.Name}");

            // Faction
            if (entry.Name != "Bomb")
                parts.Add(entry.IsHostile ? "hostile" : "friendly");

            // Details
            if (!string.IsNullOrEmpty(entry.Details))
                parts.Add(entry.Details);

            return string.Join(", ", parts.ToArray());
        }

        private void CycleInitiativeForward()
        {
            if (initiativeList.Count == 0) return;

            initiativeIndex = (initiativeIndex + 1) % initiativeList.Count;
            ScreenReaderManager.SpeakInterrupt(FormatEntry(initiativeList[initiativeIndex]));
        }

        private void CycleInitiativeBackward()
        {
            if (initiativeList.Count == 0) return;

            initiativeIndex--;
            if (initiativeIndex < 0) initiativeIndex = initiativeList.Count - 1;
            ScreenReaderManager.SpeakInterrupt(FormatEntry(initiativeList[initiativeIndex]));
        }

        private void ExitInitiativeBrowse()
        {
            browsingInitiative = false;
            initiativeList.Clear();
            ScreenReaderManager.SpeakInterrupt("Initiative closed");
        }
    }
}
