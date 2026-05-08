File: Patches/DescriptionPatches.cs — Patches HUD_Controller.QueueTextDescription to read examine/description text aloud and maintain a combat log buffer.

namespace Wasteland2AccessibilityMod.Patches  (line 5)

// Reads queued description/combat/radio/system text aloud; skips conversation lines handled by ConversationPatches.
[HarmonyPatch(typeof(HUD_Controller), "QueueTextDescription")]
class HUD_Controller_QueueTextDescription_Patch  (line 11)
    private static readonly List<string> combatLog  (line 13)
    private const int MAX_LOG_ENTRIES  (line 14)

    // Public accessor for the combat log list (read by CombatMovementPatches and FloatingTextPatches).
    public static List<string> CombatLog => combatLog  (line 16)

    public static void ClearLog()  (line 18)

    [HarmonyPostfix]
    public static void Postfix(string newText, HUD_Controller.TextType textType, bool hasAudio)  (line 24)
        // note: skips Conversation type while Drama.isConversationOn; adds type prefix (Success, Failed, Combat, Radio, System).

    private static string GetTextTypePrefix(HUD_Controller.TextType textType)  (line 65)
