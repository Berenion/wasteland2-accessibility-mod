File: UITextExtractor.cs — extracts and cleans UI text from NGUI GameObjects; strips formatting codes for screen reader output

namespace Wasteland2AccessibilityMod  (line 5)

// Handles extraction and cleaning of UI text from game objects
class UITextExtractor  (line 9)  [public static]

    // Checks if a GameObject is an interactive UI element (not just decorative)
    public static bool IsInteractiveElement(GameObject go)  (line 14)
        // note: returns false for "background"/plain "label" names; returns false for UISprite-only objects with no interactive components; checks UIButton/Toggle/Slider/Input/PopupList/ButtonKeys/UILabel with interactive children

    // Extracts text from a UI GameObject with context-aware type detection
    public static string ExtractUIText(GameObject go)  (line 78)
        // note: tries UILabel on object, then children; appends type suffix ("Slider", "Checked"/"Unchecked", "Text Field", "Dropdown"); no suffix for UIButton (contextually obvious); falls back to go.name

    // Removes NGUI formatting codes and special symbols from text
    public static string CleanText(string text)  (line 136)
        // note: strips zone-marker prefixes (AZ_/CA_/LA_) at string start; removes [FFFFFF]/[-]/[b] etc.; removes <@&> angle-bracket codes; replaces & with "and"; collapses whitespace; called by all Speak* methods
