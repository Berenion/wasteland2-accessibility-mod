File: Patches/ConversationPatches.cs — Harmony patches for the conversation/dialogue system: tracks voiceover presence, reads NPC dialogue and response options, and announces conversation lifecycle events.

namespace Wasteland2AccessibilityMod.Patches  (line 9)

// Tracks whether the most recently printed bubble text has a valid voiceover audio file.
[HarmonyPatch(typeof(BubbleTextManager), "Print", new Type[] { typeof(BubbleTextKind), typeof(GameObject), typeof(string), typeof(string), typeof(GameObject), typeof(float), typeof(BubbleTextManager.NotifyBubbleText), typeof(string), typeof(bool), typeof(Texture2D), typeof(Texture2D), typeof(bool) })]
class BubbleTextManager_Print_Patch  (line 27)
    // True if most recently printed bubble has voiceover audio; reset each Print call.
    public static bool LastPrintHadAudio { get; private set; }  (line 32)
    // BubbleTextKind of the most recently printed bubble.
    public static BubbleTextKind LastPrintTextKind { get; private set; }  (line 37)

    [HarmonyPostfix]
    public static void Postfix(BubbleTextKind textKind, string audioName)  (line 40)
        // note: verifies audio ID via AudioManager.IsValidAudioID; treats placeholder "__" as unvoiced.

// Reads NPC dialogue text aloud; waits for voiceover to finish before speaking if audio is present.
[HarmonyPatch(typeof(ConversationHUD), "AddText", new Type[] { typeof(string), typeof(float), typeof(ConversationHUD.NotifyDone), typeof(bool), typeof(bool) })]
class ConversationHUD_AddText_Patch  (line 83)
    private static string lastAnnouncedText  (line 90)
    private static float lastAnnouncedTime  (line 91)

    [HarmonyPostfix]
    public static void Postfix(string p_value, bool isAppend, bool rangerSay)  (line 94)
        // note: skips append and rangerSay lines; deduplicates within 0.5 s; defers via coroutine when VO is active.

    // Polls until voiced audio finishes, then speaks the text.
    private static IEnumerator SpeakAfterVoiceoverFinishes(string text)  (line 167)

// Announces available response options when buttons are added; suppressed during active ConversationState navigation.
[HarmonyPatch(typeof(ConversationHUD), "AddButton")]
class ConversationHUD_AddButton_Patch  (line 193)
    private static HashSet<string> announcedButtons  (line 196)
    private static float lastButtonAnnouncementTime  (line 197)
    private static List<string> currentButtons  (line 198)

    [HarmonyPostfix]
    public static void Postfix(KeywordInfo keywordInfo)  (line 201)
        // note: skips when Drama.isConversationOn (ConversationState handles navigation); adds skill requirement and Goodbye context.

// Logs button removal for debugging.
[HarmonyPatch(typeof(ConversationHUD), "RemoveButton")]
class ConversationHUD_RemoveButton_Patch  (line 296)
    [HarmonyPostfix]
    public static void Postfix(string keywordLabel)  (line 299)

// Announces the selected response text (full sayRangerText) when a topic is pressed; suppressed when ConversationState is managing navigation.
[HarmonyPatch(typeof(ConversationHUD), "OnTopicPressed")]
class ConversationHUD_OnTopicPressed_Patch  (line 317)
    [HarmonyPrefix]
    public static void Prefix(ConversationHUD __instance, UnityEngine.GameObject button)  (line 320)
        // note: uses reflection to read buttonList.sayRangerText for the full response text.

// Announces the hovered response option (full sayRangerText with skill info); suppressed when ConversationState is managing navigation.
[HarmonyPatch(typeof(ConversationHUD), "OnTopicMouseOver")]
class ConversationHUD_OnTopicMouseOver_Patch  (line 432)
    private static string lastHoveredButton  (line 434)
    private static float lastHoverTime  (line 435)

    [HarmonyPostfix]
    public static void Postfix(ConversationHUD __instance, UnityEngine.GameObject button)  (line 438)
        // note: deduplicates within 0.3 s; includes skill level and "ends conversation" context.

// Logs when conversation option list is cleared.
[HarmonyPatch(typeof(ConversationHUD), "Clear")]
class ConversationHUD_Clear_Patch  (line 596)
    [HarmonyPostfix]
    public static void Postfix()  (line 599)

// Announces conversation start with NPC name; defers via VoiceoverHelper to avoid clashing with opening VO.
[HarmonyPatch(typeof(ConversationHUD), "OnConversationStart")]
class ConversationHUD_OnConversationStart_Patch  (line 616)
    [HarmonyPostfix]
    public static void Postfix(ConversationHUD __instance)  (line 619)

// Announces conversation end.
[HarmonyPatch(typeof(ConversationHUD), "OnConversationEnd")]
class ConversationHUD_OnConversationEnd_Patch  (line 650)
    [HarmonyPostfix]
    public static void Postfix()  (line 653)
