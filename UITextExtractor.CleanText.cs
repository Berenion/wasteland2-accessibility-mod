using System.Text.RegularExpressions;

namespace Wasteland2AccessibilityMod
{
    /// <summary>
    /// Pure text-cleaning half of <see cref="UITextExtractor"/>. Kept free of any Unity
    /// dependency (only Regex/string) so it can be unit-tested standalone — CleanText is the
    /// single chokepoint every spoken string flows through, so it's the highest-value thing
    /// to lock down with tests. The GameObject-based extraction lives in UITextExtractor.cs.
    /// </summary>
    public static partial class UITextExtractor
    {
        /// <summary>
        /// Removes NGUI formatting codes and special symbols from text.
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
