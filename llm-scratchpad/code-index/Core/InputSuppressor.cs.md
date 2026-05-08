File: Core/InputSuppressor.cs — static suppression flags and Harmony PREFIX patches that block game input pipelines when accessibility states are active.

namespace Wasteland2AccessibilityMod.Core  (line 3)

static class InputSuppressor  (line 11)
    // Frame-scoped flags set by accessibility states; reset each frame by InputRouter.ProcessInput().

    // When true, InputManager.Update() is skipped entirely.
    public static bool ShouldSuppressGameInput { get; set; }  (line 16)

    // When true, UICamera.ProcessOthers() is skipped to prevent NGUI from processing arrow/D-Pad keys.
    public static bool ShouldSuppressUINavigation { get; set; }  (line 23)

    // When true, EventManager.Update() is skipped to prevent button events dispatching to GUIScreens.
    public static bool ShouldSuppressButtonEvents { get; set; }  (line 30)

    // Reset all three suppression flags; called at start of each frame by InputRouter.ProcessInput().
    public static void Reset()  (line 36)


[HarmonyPatch(typeof(InputManager), "Update")]
class InputManager_Update_Suppressor  (line 49)
    // PREFIX on InputManager.Update(); skips original when ShouldSuppressGameInput is true; always allows through during cutscenes.

    public static bool Prefix()  (line 52)


[HarmonyPatch(typeof(UICamera), "ProcessOthers")]
class UICamera_ProcessOthers_Suppressor  (line 70)
    // PREFIX on UICamera.ProcessOthers(); skips NGUI navigation processing when ShouldSuppressUINavigation is true.

    public static bool Prefix()  (line 74)


[HarmonyPatch(typeof(EventManager), "Update")]
class EventManager_Update_Suppressor  (line 93)
    // PREFIX on EventManager.Update(); skips button-event dispatch to GUIScreens when ShouldSuppressButtonEvents is true.
    // note: EventManager.Update uses cInput.GetButtonDown to dispatch Enter/Escape etc. to GUIScreen.OnButtonDown handlers, causing double processing when mod states already handle these via Input.GetKeyDown.

    public static bool Prefix()  (line 97)


static class SaveLoadScreenSuppressor  (line 116)
    // Guards OnSaveClicked/OnLoadClicked/OnButtonDown; mod sets AllowNextAction=true immediately before requesting an action so the blocked prefix lets it through once.

    // When true, the next OnSaveClicked/OnLoadClicked call is allowed; reset after passing through.
    public static bool AllowNextAction { get; set; }  (line 123)


[HarmonyPatch(typeof(SaveLoadScreen), "OnSaveClicked")]
class SaveLoadScreen_OnSaveClicked_Suppressor  (line 126)
    public static bool Prefix()  (line 130)
        // note: checks SaveLoadScreenSuppressor.AllowNextAction first; otherwise blocks when GenericMenuState.blockUIInput is true.

[HarmonyPatch(typeof(SaveLoadScreen), "OnLoadClicked")]
class SaveLoadScreen_OnLoadClicked_Suppressor  (line 146)
    public static bool Prefix()  (line 150)

[HarmonyPatch(typeof(SaveLoadScreen), "OnButtonDown")]
class SaveLoadScreen_OnButtonDown_Suppressor  (line 166)
    public static bool Prefix(string buttonName)  (line 170)
        // Blocks "Attack Current Target" and "Controller A" when GenericMenuState.blockUIInput is true.
