File: States/ConversationState.cs — keyboard navigation for NPC conversation options; handles both advance-mode (Enter to skip voiceover) and input-mode (Up/Down/Enter over dialogue choices) (priority 50).

namespace Wasteland2AccessibilityMod.States  (line 9)

class ConversationState : IAccessibilityState  (line 16)

    // --- Interface properties ---
    public string Name => "Conversation"  (line 18)
    public int Priority => 50  (line 19)

    // --- Static flag ---
    // When true, passive ConversationPatches skip button-add and hover announcements.
    public static bool IsManagingNavigation { get; private set; }  (line 25)

    // --- Fields ---
    private int selectedIndex = -1  (line 27)
    private readonly List<ConversationOption> currentOptions = new List<ConversationOption>()  (line 28)
    private int lastKnownButtonCount = 0  (line 29)
    private static int announcementGeneration = 0  (line 32)
        // note: incremented on every manual navigation move to cancel pending coroutine announcements.
    private static FieldInfo buttonListField  (line 35)
        // note: reflection cache for ConversationHUD.buttonList (private field).
    private static bool fieldsCached = false  (line 36)

    // --- Nested type ---
    private class ConversationOption  (line 38)
        public string KeywordLabel  (line 40)
        public string DisplayText  (line 41)
        public string FullResponseText  (line 42)
        public string SkillInfo  (line 43)
        public bool IsGoodbye  (line 44)
        public bool IsUnavailable  (line 45)
        public GameObject ButtonObject  (line 46)

    // --- Private properties ---
    // True when Drama.isConversationOn and DramaGUI.waitState == ForAdvance.
    private bool IsInAdvanceMode { get; }  (line 52)
        // note: intentionally does NOT check Drama.isCutsceneOn — many conversations set cutsceneStart()
        //       to freeze party movement while still expecting normal conversation input.

    // True when Drama.isConversationOn, waitState == ForInput, clickToContinue is hidden, no active description bubbles, and button count > 0.
    private bool IsInInputMode { get; }  (line 69)
        // note: 1-frame race condition guard via VoiceoverHelper.HasActiveDescriptionBubbles().

    // --- Interface property ---
    // Inactive when VendorScreen is open; otherwise active when IsInAdvanceMode or IsInInputMode.
    public bool IsActive { get; }  (line 91)
        // note: yields to ShopState when vendor screen is open even though conversation panel stays active behind the shop UI.

    // --- Interface methods ---
    // Advance mode: Enter skips current dialogue. Input mode: Up/Down navigate options, Enter selects, Home/End jump to ends.
    public bool HandleInput()  (line 106)
        // note: in advance mode, any key suppresses UI navigation. In input mode, detects button-count changes
        //       to auto-select first option when new options appear without a navigation event.

    // Sets IsManagingNavigation=true, selects index 0, refreshes options, queues first-option announcement (or logs advance-mode activation).
    public void OnActivated()  (line 199)

    // Clears IsManagingNavigation, resets selectedIndex to -1, clears option list.
    public void OnDeactivated()  (line 225)

    // --- Private helpers ---
    // Lazily reflects and caches ConversationHUD.buttonList field on first call.
    private void EnsureFieldsCached()  (line 235)

    // Returns count of buttons in ConversationHUD.buttonList via reflection; returns 0 on failure.
    private int GetButtonCount()  (line 250)

    // Reflects ConversationHUD.buttonList and rebuilds currentOptions; extracts DisplayText, FullResponseText, and SkillInfo from each ButtonInfo.
    private void RefreshOptions()  (line 262)
        // note: reads ConversationHUD.ButtonInfo.keywordLabel, gobButton (for UILabel), sayRangerText, and keywordInfo.
        //       Sets IsUnavailable=true when player skill < required skill. Clamps selectedIndex on exit.

    // Calls BubbleTextManager.FlushCurrentBark() to skip voiceover/text; speaks "Skipped".
    private void SkipCurrentDialogue()  (line 355)

    // Queues announcement without interrupt; starts SpeakOptionAfterVoiceover coroutine if voiceover is playing.
    private void AnnounceCurrentOptionQueued()  (line 371)
        // note: used for auto-focus when options first appear. Checks VoiceoverHelper.IsVoiceoverPlaying()
        //       and HasPendingOrActiveVoicedAudio() before deciding whether to use coroutine or direct Tolk queue.

    // Announces current option with interrupt (cuts off current speech); increments announcementGeneration to cancel pending coroutine.
    private void AnnounceCurrentOption()  (line 392)
        // note: used for explicit user navigation (Up/Down/Home/End).

    // Coroutine: polls up to 30 s for voiceover to finish, then queues the announcement; aborts if generation changes.
    private static IEnumerator SpeakOptionAfterVoiceover(string text, int generation)  (line 403)
        // note: polls every 0.2 s; waits an additional 0.3 s after voiceover ends before speaking.
        //       Generation check at every yield point prevents stale announcements when user navigates manually.

    // Assembles announcement: prefers FullResponseText over DisplayText, appends SkillInfo, "ends conversation" for Goodbye, and "N of total".
    private string BuildOptionAnnouncement(ConversationOption opt)  (line 422)

    // Calls ConversationHUD.OnTopicPressed(buttonObject); blocks unavailable options with spoken feedback.
    private void SelectCurrentOption()  (line 449)
        // note: resets selectedIndex=0 and lastKnownButtonCount=0 after a successful selection.
