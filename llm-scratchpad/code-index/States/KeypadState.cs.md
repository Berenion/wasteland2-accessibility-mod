File: States/KeypadState.cs — digit-entry keyboard handling for KeypadMenu (safe/passcode popup); priority 58 sits above GenericMenuState (55) but below DialogState (70) so the "Incorrect Passcode" modal takes over cleanly.

namespace Wasteland2AccessibilityMod.States  (line 3)

class KeypadState : IAccessibilityState  (line 13)

    // --- Interface properties ---
    public string Name => "Keypad"  (line 15)
    public int Priority => 58  (line 16)

    // --- Static flag ---
    // Set to true while this state is active so the OnGUI suppressor patch can block the keypad's native key handling.
    public static bool Active = false  (line 20)
        // note: read by a Harmony patch (not inside this file) to suppress duplicate key entry from the game's own OnGUI.

    // --- Fields ---
    private KeypadMenu cachedMenu  (line 22)
    private static readonly FieldInfo currentValueField =  (line 23)
        typeof(KeypadMenu).GetField("currentValue", BindingFlags.NonPublic | BindingFlags.Instance)
        // note: static reflection cache for KeypadMenu.currentValue (private string field).
    private static readonly MethodInfo addToValueMethod =  (line 25)
        typeof(KeypadMenu).GetMethod("AddToValue", BindingFlags.NonPublic | BindingFlags.Instance)
        // note: static reflection cache for KeypadMenu.AddToValue(int) (private method).

    // --- Interface property ---
    // Active when a KeypadMenu exists and is active in hierarchy.
    public bool IsActive { get; }  (line 29)

    // --- Interface methods ---
    // Handles digits 0-9 (top-row and numpad), Backspace, C (clear), Enter (submit), Escape (cancel); suppresses all other input paths.
    public bool HandleInput()  (line 37)
        // note: suppresses ShouldSuppressGameInput, ShouldSuppressUINavigation, and ShouldSuppressButtonEvents on every call.
        //       Re-resolves cachedMenu if null or inactive before processing keys.
        //       Digit loop uses KeyCode.Alpha0 + d and KeyCode.Keypad0 + d for both key rows.

    // Sets Active=true, caches KeypadMenu, speaks full instructions string.
    public void OnActivated()  (line 95)
        // note: hardcoded instruction string "Enter passcode. Type digits, Backspace to delete, C to clear, Enter to submit, Escape to cancel."

    // Sets Active=false, nulls cachedMenu.
    public void OnDeactivated()  (line 103)

    // --- Private helpers ---
    // Checks 8-digit length limit via currentValueField reflection, then invokes addToValueMethod; speaks digit.
    private void EnterDigit(int digit)  (line 109)
        // note: hardcoded max length 8. Speaks "Maximum length reached" if at cap.
        //       Uses addToValueMethod.Invoke() rather than direct call (private method).

    // Reads currentValueField, removes last character, updates displayLabel, speaks last remaining digit or "Empty".
    private void Backspace()  (line 130)
        // note: sets displayLabel.text to "0" when the field becomes empty.
