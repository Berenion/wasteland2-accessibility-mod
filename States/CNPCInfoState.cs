using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using Wasteland2AccessibilityMod.Core;
using Wasteland2AccessibilityMod.Helpers;

namespace Wasteland2AccessibilityMod.States
{
    /// <summary>
    /// Keyboard navigation and screen reader support for <see cref="CNPCInfoMenu"/> — the
    /// "[NAME] would like to join your party" screen, which doubles as the party-roster
    /// screen when the party is full and someone has to go.
    ///
    /// The screen is a prospective-companion panel plus one <see cref="CHA_CNPCEntry"/> per
    /// current follower, each carrying its own Dismiss button. It DOES wire a real
    /// UIButtonKeys navigation graph (CNPCInfoMenu.cs:66-77), so GenericMenuState could
    /// move around it — but every follower's control is just a button labelled "Dismiss".
    /// The name lives in the entry's sibling nameLabel (CHA_CNPCEntry.cs:39), so the screen
    /// read back as "Dismiss, Dismiss, Dismiss" with no way to tell who was who, and none
    /// of the stats, biography or rogue chance the screen exists to present.
    ///
    /// This state replaces that with a named list: the prospective companion first, then
    /// each follower. Enter accepts on the prospective entry and dismisses on a follower;
    /// both routes end in the game's own "Are you sure?" ModalMessageMenu, which DialogState
    /// (priority 70) already reads and which outranks this state while it is up.
    ///
    /// Priority 57 — above GenericMenuState (55) so it claims this screen, below KeypadState
    /// (58) and DialogState (70).
    /// </summary>
    public class CNPCInfoState : AccessibilityStateBase
    {
        public override string Name => "CNPCInfo";
        public override int Priority => 57;

        public override string GetHelpText()
        {
            return "Companion join screen. Up and Down move through the recruit and your current followers. " +
                   "Enter accepts the recruit, or dismisses the selected follower after a confirmation. " +
                   "I reads the selected character's biography and stats, Tab repeats the current line, " +
                   "C summarises party space, Escape declines and closes.";
        }

        // One row of the screen: either the recruit being offered or a current follower.
        private class Row
        {
            public bool IsProspect;
            public PC Companion;        // the CNPC behind the row (dummy instance for the prospect)
            public CHA_CNPCEntry Entry; // the UI widget, used to trigger vanilla's dismiss flow
            public string Name;
        }

        private readonly List<Row> rows = new List<Row>();
        private int index = -1;
        private bool isDirty = true;
        private string lastAnnounced;

        // The menu instance whose intro has already been spoken. Distinguishes a genuinely
        // new screen from returning after the confirmation modal closed.
        private CNPCInfoMenu introducedMenu = null;

        // Info browser — Up/Down through the selected character's detail lines, Escape closes.
        private bool isInfoBrowsing = false;
        private readonly List<string> infoLines = new List<string>();
        private int infoIndex = -1;

        // CHA_CNPCEntry.currentPC and CNPCInfoMenu.currentNPC are private; both are the
        // authoritative link from a UI row back to the character it represents.
        private static bool reflectionCached = false;
        private static FieldInfo entryCurrentPCField;
        private static FieldInfo menuCurrentNPCField;

        private static void CacheReflection()
        {
            if (reflectionCached) return;
            reflectionCached = true;
            entryCurrentPCField = typeof(CHA_CNPCEntry).GetField("currentPC",
                BindingFlags.NonPublic | BindingFlags.Instance);
            menuCurrentNPCField = typeof(CNPCInfoMenu).GetField("currentNPC",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (entryCurrentPCField == null)
                MelonLogger.Warning("[CNPCInfoState] CHA_CNPCEntry.currentPC not found — names fall back to the label");
        }

        private static CNPCInfoMenu GetMenu()
        {
            var menu = SceneQueryCache.Find<CNPCInfoMenu>();
            return (menu != null && menu.gameObject.activeInHierarchy) ? menu : null;
        }

        public override bool IsActive
        {
            get
            {
                var menu = GetMenu();
                if (menu == null) return false;

                // Yield while the dismiss/accept confirmation is stacked on top — DialogState
                // owns ModalMessageMenu, and it also outranks this state.
                var modal = SceneQueryCache.Find<ModalMessageMenu>();
                if (modal != null && modal.gameObject.activeInHierarchy) return false;

                return true;
            }
        }

        public override void OnActivated()
        {
            CacheReflection();
            isDirty = true;
            isInfoBrowsing = false;
            lastAnnounced = null;
            base.OnActivated();

            // The dismiss/accept confirmation deactivates this state while it is up, so
            // coming back from it is NOT a fresh open. Replaying the "would like to join"
            // intro every time a follower is dismissed would be maddening; instead rebuild
            // the (now shorter) roster and report where the cursor landed.
            var menu = GetMenu();
            if (menu != null && menu == introducedMenu)
            {
                BuildRows();
                isDirty = false;
                if (index >= rows.Count) index = rows.Count - 1;
                if (index < 0 && rows.Count > 0) index = 0;

                if (rows.Count == 0)
                    ScreenReaderManager.SpeakInterrupt("No followers left");
                else
                    AnnounceCurrent(force: true);
                return;
            }

            introducedMenu = menu;
            index = -1;
            AnnounceScreenIntro();
        }

        public override void OnDeactivated()
        {
            // Keep rows/index so returning from the confirmation modal resumes in place;
            // OnActivated rebuilds them against the live UI anyway.
            infoLines.Clear();
            isInfoBrowsing = false;
            base.OnDeactivated();
        }

        public override bool HandleInput()
        {
            // The screen's own UIButtonKeys graph would otherwise consume the same arrows
            // and Enter, moving the highlight underneath us and double-firing buttons.
            InputSuppressor.ShouldSuppressGameInput = true;

            if (isDirty)
            {
                BuildRows();
                isDirty = false;
            }

            if (isInfoBrowsing)
                return HandleInfoBrowsing();

            if (Input.GetKeyDown(KeyCode.UpArrow)) { Move(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { Move(1); return true; }

            if (Input.GetKeyDown(KeyCode.Home)) { MoveTo(0); return true; }
            if (Input.GetKeyDown(KeyCode.End)) { MoveTo(rows.Count - 1); return true; }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateSelected();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.I)) { OpenInfoBrowser(); return true; }

            if (Input.GetKeyDown(KeyCode.Tab)) { AnnounceCurrent(force: true); return true; }

            if (Input.GetKeyDown(KeyCode.C)) { AnnouncePartySpace(); return true; }

            if (Input.GetKeyDown(KeyCode.Escape)) { Decline(); return true; }

            return true;
        }

        // --- Rows ---

        private void BuildRows()
        {
            rows.Clear();
            var menu = GetMenu();
            if (menu == null) return;

            // The recruit. Its CHA_CNPCEntry holds a dummy CNPC built by CreateDummyCNPC,
            // which is what carries the previewed stats; the real NPC is menu.currentNPC.
            if (menu.prospectiveCNPCEntry != null)
            {
                var prospectPC = GetEntryCompanion(menu.prospectiveCNPCEntry);
                string name = ResolveName(prospectPC);
                if (string.IsNullOrEmpty(name) || name == "Unknown")
                    name = ResolveProspectNameFromMenu(menu) ?? name;

                rows.Add(new Row
                {
                    IsProspect = true,
                    Companion = prospectPC,
                    Entry = menu.prospectiveCNPCEntry,
                    Name = name
                });
            }

            // Current followers, taken from the live entry widgets rather than Game.party so
            // a row dismissed mid-screen (CHA_CNPCEntry.SelfDestruct deactivates it) drops
            // out of the list exactly when it drops out of the UI.
            if (menu.currentCNPCContainer != null)
            {
                var entries = menu.currentCNPCContainer.GetComponentsInChildren<CHA_CNPCEntry>(false);
                foreach (var entry in entries)
                {
                    if (entry == null || !entry.gameObject.activeInHierarchy) continue;
                    if (entry == menu.prospectiveCNPCEntry) continue;

                    var companion = GetEntryCompanion(entry);
                    rows.Add(new Row
                    {
                        IsProspect = false,
                        Companion = companion,
                        Entry = entry,
                        Name = ResolveName(companion)
                    });
                }
            }

            if (index >= rows.Count) index = rows.Count - 1;
            if (index < 0 && rows.Count > 0) index = 0;
        }

        private PC GetEntryCompanion(CHA_CNPCEntry entry)
        {
            if (entry == null || entryCurrentPCField == null) return null;
            try { return entryCurrentPCField.GetValue(entry) as PC; }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] Reading entry companion failed: " + ex.Message);
                return null;
            }
        }

        private string ResolveName(PC pc)
        {
            if (pc == null || pc.pcTemplate == null) return "Unknown";
            try
            {
                return UITextExtractor.CleanText(
                    Language.Localize(pc.pcTemplate.displayName, false, false, string.Empty));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] Name localization failed: " + ex.Message);
                return "Unknown";
            }
        }

        private string ResolveProspectNameFromMenu(CNPCInfoMenu menu)
        {
            if (menuCurrentNPCField == null) return null;
            try
            {
                var npc = menuCurrentNPCField.GetValue(menu) as NPC;
                if (npc == null || npc.npcTemplate == null) return null;
                return UITextExtractor.CleanText(
                    Language.Localize(npc.npcTemplate.displayName, false, false, string.Empty));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] Prospect name lookup failed: " + ex.Message);
                return null;
            }
        }

        // --- Navigation ---

        private void Move(int delta)
        {
            if (rows.Count == 0) { ScreenReaderManager.SpeakInterrupt("Nothing to select"); return; }
            int next = index + delta;
            if (next < 0) next = 0;
            if (next >= rows.Count) next = rows.Count - 1;
            if (next == index) { AnnounceCurrent(force: true); return; }
            index = next;
            AnnounceCurrent();
        }

        private void MoveTo(int target)
        {
            if (rows.Count == 0) return;
            index = Mathf.Clamp(target, 0, rows.Count - 1);
            AnnounceCurrent(force: true);
        }

        private Row Current
        {
            get { return (index >= 0 && index < rows.Count) ? rows[index] : null; }
        }

        // --- Announcements ---

        private void AnnounceScreenIntro()
        {
            BuildRows();
            isDirty = false;

            var menu = GetMenu();
            string prospect = rows.Count > 0 && rows[0].IsProspect ? rows[0].Name : "Someone";
            int followers = 0;
            foreach (var row in rows) if (!row.IsProspect) followers++;

            bool hasRoom = HasRoomInParty();
            string space = hasRoom
                ? "There is room in your party"
                : "Your party is full, dismiss a follower to make room";

            ScreenReaderManager.SpeakInterrupt(
                prospect + " would like to join your party. " + space + ". " +
                followers + (followers == 1 ? " current follower" : " current followers") +
                ". Up and Down to browse, Enter to accept or dismiss, I for details, Escape to decline");

            if (rows.Count > 0)
            {
                index = 0;
                AnnounceCurrent(force: true, interrupt: false);
            }
        }

        private void AnnounceCurrent(bool force = false, bool interrupt = true)
        {
            var row = Current;
            if (row == null) return;

            string text = FormatRow(row);
            if (!force && text == lastAnnounced) return;
            lastAnnounced = text;

            if (interrupt) ScreenReaderManager.SpeakInterrupt(text);
            else ScreenReaderManager.Speak(text);
        }

        private string FormatRow(Row row)
        {
            var parts = new List<string>();
            parts.Add(row.Name);

            if (row.IsProspect)
            {
                parts.Add("wants to join");
                parts.Add(HasRoomInParty()
                    ? "Enter to accept"
                    : "party full, dismiss someone first");
            }
            else
            {
                parts.Add("current follower");

                // Rogue chance is the number this screen is really for, and vanilla buries
                // it in a hover tooltip (INV_MainPanel.cs:143 builds the same figure).
                string rogue = FormatRogueChance(row.Companion);
                if (!string.IsNullOrEmpty(rogue)) parts.Add(rogue);

                // CHA_CNPCEntry disables its own Dismiss button for a downed companion
                // (CHA_CNPCEntry.cs:69) without saying why.
                if (row.Companion != null && row.Companion.healthState >= PC.HealthState.Unconscious)
                    parts.Add("unconscious, can't be dismissed");
                else
                    parts.Add("Enter to dismiss");
            }

            parts.Add((index + 1) + " of " + rows.Count);
            return string.Join(", ", parts.ToArray());
        }

        private string FormatRogueChance(PC pc)
        {
            var cnpc = pc as CNPC;
            if (cnpc == null || cnpc.cnpcTemplate == null) return null;
            try
            {
                PC highest;
                int baseChance = cnpc.cnpcTemplate.percentToGoRogue;
                int reduction = cnpc.GetHighestRogueReduction(out highest);
                int total = Mathf.Max(baseChance - reduction, 0);
                return "rogue chance " + total + "%";
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] Rogue chance failed: " + ex.Message);
                return null;
            }
        }

        private void AnnouncePartySpace()
        {
            int size = 0;
            int cnpcs = 0;
            if (MonoBehaviourSingleton<Game>.HasInstance())
            {
                var party = MonoBehaviourSingleton<Game>.GetInstance().party;
                if (party != null)
                {
                    size = party.Count;
                    foreach (var member in party) if (member != null && member.isCNPC) cnpcs++;
                }
            }

            ScreenReaderManager.SpeakInterrupt(
                "Party has " + size + (size == 1 ? " member" : " members") + ", " +
                cnpcs + (cnpcs == 1 ? " companion" : " companions") + ". " +
                (HasRoomInParty() ? "There is room for one more" : "Party is full"));
        }

        private static bool HasRoomInParty()
        {
            try
            {
                return MonoBehaviourSingleton<Game>.HasInstance()
                       && MonoBehaviourSingleton<Game>.GetInstance().HasRoomInParty();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] HasRoomInParty failed: " + ex.Message);
                return false;
            }
        }

        // --- Actions ---

        private void ActivateSelected()
        {
            var row = Current;
            var menu = GetMenu();
            if (row == null || menu == null) return;

            if (row.IsProspect)
            {
                // Vanilla greys out Accept when the party is full (CheckAcceptButton,
                // CNPCInfoMenu.cs:118) and says nothing about why.
                if (!HasRoomInParty())
                {
                    ScreenReaderManager.SpeakInterrupt(
                        "Your party is full. Dismiss a follower first, then accept " + row.Name);
                    return;
                }

                ScreenReaderManager.SpeakInterrupt(row.Name + " joins your party");
                ModLog.Debug("[CNPCInfoState] Accepting " + row.Name);
                try { menu.OnAcceptClicked(); }
                catch (Exception ex)
                {
                    MelonLogger.Warning("[CNPCInfoState] OnAcceptClicked failed: " + ex.Message);
                    ScreenReaderManager.SpeakInterrupt("Accept failed");
                }
                return;
            }

            DismissRow(row);
        }

        private void DismissRow(Row row)
        {
            if (row.Entry == null)
            {
                ScreenReaderManager.SpeakInterrupt("Dismiss unavailable");
                return;
            }

            if (MonoBehaviourSingleton<CombatManager>.HasInstance() &&
                MonoBehaviourSingleton<CombatManager>.GetInstance().inCombat)
            {
                // OnDismissClicked would raise its own modal here; saying it directly is
                // faster and the modal still follows if the click gets that far.
                ScreenReaderManager.SpeakInterrupt("You can't dismiss a party member during combat");
                return;
            }

            if (row.Companion != null && row.Companion.healthState >= PC.HealthState.Unconscious)
            {
                ScreenReaderManager.SpeakInterrupt(
                    "You can't dismiss " + row.Name + " while they are unconscious");
                return;
            }

            ScreenReaderManager.SpeakInterrupt("Dismissing " + row.Name);
            ModLog.Debug("[CNPCInfoState] Dismiss requested for " + row.Name);

            try
            {
                // Vanilla's own flow: raises the "Are you sure?" ModalMessageMenu (whose text
                // names the ranger receiving the inventory), then RemoveCNPCFromParty and
                // SelfDestruct on confirm. DialogState reads the modal.
                row.Entry.OnDismissClicked();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] OnDismissClicked failed: " + ex.Message);
                ScreenReaderManager.SpeakInterrupt("Dismiss failed");
                return;
            }

            // The row disappears once the dismissal is confirmed, so rebuild on return.
            isDirty = true;
        }

        private void Decline()
        {
            var menu = GetMenu();
            if (menu == null) return;

            var row = rows.Count > 0 && rows[0].IsProspect ? rows[0] : null;
            ScreenReaderManager.SpeakInterrupt(
                row != null ? "Declining " + row.Name : "Closing");
            ModLog.Debug("[CNPCInfoState] Declining");

            try { menu.OnDeclineClicked(); }
            catch (Exception ex)
            {
                MelonLogger.Warning("[CNPCInfoState] OnDeclineClicked failed: " + ex.Message);
                try { menu.OnCloseClicked(); }
                catch (Exception ex2) { MelonLogger.Warning("[CNPCInfoState] OnCloseClicked failed: " + ex2.Message); }
            }
        }

        // --- Info browser ---

        private void OpenInfoBrowser()
        {
            var row = Current;
            if (row == null) return;

            infoLines.Clear();
            infoLines.Add(row.Name + (row.IsProspect ? ", prospective companion" : ", current follower"));

            if (row.Companion != null)
            {
                try
                {
                    if (row.Companion.pcTemplate != null &&
                        !string.IsNullOrEmpty(row.Companion.pcTemplate.biography))
                    {
                        string bio = UITextExtractor.CleanText(
                            Language.Localize(row.Companion.pcTemplate.biography, false, false, string.Empty));
                        if (!string.IsNullOrEmpty(bio)) infoLines.Add(bio);
                    }
                }
                catch (Exception ex) { MelonLogger.Warning("[CNPCInfoState] Biography failed: " + ex.Message); }

                string rogue = FormatRogueChance(row.Companion);
                if (!string.IsNullOrEmpty(rogue)) infoLines.Add(rogue);

                try
                {
                    infoLines.AddRange(
                        CharacterAnnouncementHelper.BuildPartyMemberInfoLines(row.Companion, false));
                }
                catch (Exception ex) { MelonLogger.Warning("[CNPCInfoState] Stat lines failed: " + ex.Message); }
            }

            if (infoLines.Count <= 1)
                infoLines.Add("No further details available");

            isInfoBrowsing = true;
            infoIndex = 0;
            ScreenReaderManager.SpeakInterrupt(
                infoLines[0] + ". Up and Down to read, Escape to go back, " + infoLines.Count + " lines");
        }

        private bool HandleInfoBrowsing()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.I))
            {
                isInfoBrowsing = false;
                infoLines.Clear();
                infoIndex = -1;
                ScreenReaderManager.SpeakInterrupt("Closed details");
                AnnounceCurrent(force: true, interrupt: false);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow)) { StepInfo(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { StepInfo(1); return true; }
            if (Input.GetKeyDown(KeyCode.Tab)) { SpeakInfoLine(); return true; }

            return true;
        }

        private void StepInfo(int delta)
        {
            if (infoLines.Count == 0) return;
            int next = Mathf.Clamp(infoIndex + delta, 0, infoLines.Count - 1);
            if (next == infoIndex)
            {
                ScreenReaderManager.SpeakInterrupt(delta < 0 ? "Top" : "End");
                return;
            }
            infoIndex = next;
            SpeakInfoLine();
        }

        private void SpeakInfoLine()
        {
            if (infoIndex < 0 || infoIndex >= infoLines.Count) return;
            ScreenReaderManager.SpeakInterrupt(
                infoLines[infoIndex] + ", " + (infoIndex + 1) + " of " + infoLines.Count);
        }
    }
}
