using UnityEngine;
using System.Text.RegularExpressions;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Handles extraction and cleaning of UI text from game objects
    /// </summary>
    public static class UITextExtractor
    {
        /// <summary>
        /// Checks if a GameObject is an interactive UI element (not just decorative)
        /// </summary>
        public static bool IsInteractiveElement(GameObject go)
        {
            string name = go.name.ToLower();

            // Skip known non-interactive patterns first (for performance)
            if (name.Contains("background") || name == "label")
            {
                return false;
            }

            // Skip if it's only a UISprite with no interactive components
            if (go.GetComponent<UISprite>() != null &&
                go.GetComponent<UIButton>() == null &&
                go.GetComponent<UIToggle>() == null &&
                go.GetComponent<UIInput>() == null &&
                go.GetComponent<UISlider>() == null &&
                go.GetComponent<UIPopupList>() == null &&
                go.GetComponent<UIButtonKeys>() == null)
            {
                return false;
            }

            // Skip standalone sprite objects
            if (name.Contains("sprite") && !name.Contains("button") && !name.Contains("toggle"))
            {
                return false;
            }

            // Check for interactive components
            if (go.GetComponent<UIButton>() != null) return true;
            if (go.GetComponent<UIToggle>() != null) return true;
            if (go.GetComponent<UISlider>() != null) return true;
            if (go.GetComponent<UIInput>() != null) return true;
            if (go.GetComponent<UIPopupList>() != null) return true;

            // Check if it has UIButtonKeys (keyboard navigation component)
            if (go.GetComponent<UIButtonKeys>() != null) return true;

            // Check if it has a UILabel directly (might be a label-only interactive element)
            UILabel label = go.GetComponent<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                // If it has a label AND interactive children, it's likely interactive
                if (go.GetComponentInChildren<UIButton>() != null ||
                    go.GetComponentInChildren<UIToggle>() != null)
                {
                    return true;
                }
            }

            // Check for specific naming patterns that indicate interactive elements
            if (name.Contains("button") || name.Contains("toggle") || name.Contains("slider") ||
                name.Contains("input") || name.Contains("dropdown") || name.Contains("container"))
            {
                return true;
            }

            // Default: if it has a UILabel child, assume it might be interactive
            return go.GetComponentInChildren<UILabel>() != null;
        }

        /// <summary>
        /// Extracts text from a UI GameObject with context-aware type detection
        /// </summary>
        public static string ExtractUIText(GameObject go)
        {
            string labelText = "";
            string elementType = "";

            // Try to get UILabel directly on the object
            UILabel label = go.GetComponent<UILabel>();
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                labelText = CleanText(label.text);
            }
            else
            {
                // Try to get UILabel from children
                label = go.GetComponentInChildren<UILabel>();
                if (label != null && !string.IsNullOrEmpty(label.text))
                {
                    labelText = CleanText(label.text);
                }
            }

            // Context-aware type detection - only announce for non-obvious elements
            if (go.GetComponent<UISlider>() != null)
            {
                elementType = "Slider";
            }
            else if (go.GetComponent<UIToggle>() != null)
            {
                UIToggle toggle = go.GetComponent<UIToggle>();
                elementType = toggle.value ? "Checked" : "Unchecked";
            }
            else if (go.GetComponent<UIInput>() != null)
            {
                elementType = "Text Field";
            }
            else if (go.GetComponent<UIPopupList>() != null)
            {
                elementType = "Dropdown";
            }
            // Skip type for UIButton - it's obvious from context

            // Build final text
            if (string.IsNullOrEmpty(labelText))
            {
                labelText = go.name; // Fallback to GameObject name
            }

            if (!string.IsNullOrEmpty(elementType))
            {
                return $"{labelText}, {elementType}";
            }

            return labelText;
        }

        /// <summary>
        /// Removes NGUI formatting codes and special symbols from text
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // Strip internal zone-marker prefixes (AZ_, AZ1_, CA_, CA3_, LA_, ...) that
            // leak through when an object has no displayName and falls back to its
            // GameObject name. Anchored to start and requires trailing underscore so it
            // never touches mid-sentence text.
            text = Regex.Replace(text, @"^(AZ|CA|LA)\d?_", "");

            // Remove NGUI color/formatting codes like [FFFFFF], [-], [b], [/b], etc.
            text = Regex.Replace(text, @"\[/?[\w-]*\]", "");

            // Catch truncated/unclosed tags the pair-matching regex above can't:
            // a malformed "[b" with no closing bracket, or a stray "]" left behind
            // by a half-formed tag. Without this, a single odd input is spoken verbatim
            // (e.g. the player hears "[b"). This is the one chokepoint every spoken
            // string flows through, so it's worth being defensive here.
            text = Regex.Replace(text, @"\[/?[\w-]*", ""); // dangling opening fragment
            text = text.Replace("]", "");                  // orphaned closing bracket

            // Remove angle bracket formatting like <@>, <@&>, <&>, etc.
            text = Regex.Replace(text, @"<[@&]+>", "");

            // Remove other special formatting symbols
            text = text.Replace("@", "");    // Remaining @ symbols
            text = text.Replace("&", "and"); // Replace & with "and" for readability
            text = text.Replace("\\n", " "); // Newline markers
            text = text.Replace("\n", " ");  // Actual newlines
            text = text.Replace("\r", "");   // Carriage returns
            text = text.Replace("\t", " ");  // Tabs

            // Remove multiple spaces
            text = Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }
    }
}
