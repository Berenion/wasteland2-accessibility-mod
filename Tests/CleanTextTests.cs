using Wasteland2AccessibilityMod;
using Xunit;

namespace Wasteland2AccessibilityMod.Tests
{
    /// <summary>
    /// Tests for UITextExtractor.CleanText — the single chokepoint every spoken string
    /// flows through. Pure (Regex/string), so it runs without Unity or MelonLoader.
    /// </summary>
    public class CleanTextTests
    {
        // --- Null / empty passthrough ---

        [Fact]
        public void Null_ReturnsNull()
        {
            Assert.Null(UITextExtractor.CleanText(null));
        }

        [Fact]
        public void Empty_ReturnsEmpty()
        {
            Assert.Equal("", UITextExtractor.CleanText(""));
        }

        [Fact]
        public void PlainText_Unchanged()
        {
            Assert.Equal("Hello world", UITextExtractor.CleanText("Hello world"));
        }

        // --- Well-formed NGUI tags ---

        [Theory]
        [InlineData("[FFFFFF]Hello[-]", "Hello")]
        [InlineData("[b]Hi[/b]", "Hi")]
        [InlineData("[c][FFCC00]Gold[-][/c]", "Gold")]
        [InlineData("[-]", "")]
        public void WellFormedTags_AreStripped(string input, string expected)
        {
            Assert.Equal(expected, UITextExtractor.CleanText(input));
        }

        // --- Unclosed / malformed tags (the defensive fix) ---

        [Theory]
        [InlineData("[b", "")]                 // dangling opening fragment alone
        [InlineData("Hello [b", "Hello")]      // dangling fragment after text
        [InlineData("extra]", "extra")]        // orphaned closing bracket
        [InlineData("[b]Hi[/b extra]", "Hi extra")] // half-formed closing tag mid-string
        public void UnclosedTags_DoNotReachOutput(string input, string expected)
        {
            string result = UITextExtractor.CleanText(input);
            Assert.Equal(expected, result);
            Assert.DoesNotContain("[", result);
            Assert.DoesNotContain("]", result);
        }

        // --- Zone-marker prefixes (anchored to start only) ---

        [Theory]
        [InlineData("AZ_Door", "Door")]
        [InlineData("AZ1_PCPod", "PCPod")]
        [InlineData("CA3_Terminal", "Terminal")]
        [InlineData("LA_Gate", "Gate")]
        public void ZonePrefix_AtStart_IsStripped(string input, string expected)
        {
            Assert.Equal(expected, UITextExtractor.CleanText(input));
        }

        [Fact]
        public void ZonePrefix_MidSentence_IsPreserved()
        {
            // The prefix is anchored to the start, so it must not touch mid-text matches.
            Assert.Equal("see AZ_Door", UITextExtractor.CleanText("see AZ_Door"));
        }

        // --- Special symbols ---

        [Theory]
        [InlineData("<@>Name", "Name")]
        [InlineData("<@&>Topic", "Topic")]
        [InlineData("a@b", "ab")]
        public void AngleAndAtSymbols_AreStripped(string input, string expected)
        {
            Assert.Equal(expected, UITextExtractor.CleanText(input));
        }

        [Fact]
        public void Ampersand_BecomesAnd()
        {
            Assert.Equal("Salt and Pepper", UITextExtractor.CleanText("Salt & Pepper"));
        }

        // --- Whitespace normalization ---

        [Theory]
        [InlineData("a\nb", "a b")]
        [InlineData("a\tb", "a b")]
        [InlineData("a\\nb", "a b")]
        [InlineData("too    many     spaces", "too many spaces")]
        [InlineData("  trim me  ", "trim me")]
        public void Whitespace_IsCollapsedAndTrimmed(string input, string expected)
        {
            Assert.Equal(expected, UITextExtractor.CleanText(input));
        }

        // --- Realistic combined input ---

        [Fact]
        public void CombinedMarkup_IsFullyCleaned()
        {
            Assert.Equal(
                "Health 10 of 20",
                UITextExtractor.CleanText("[c][FFFFFF]Health[-] 10 of 20\n"));
        }
    }
}
