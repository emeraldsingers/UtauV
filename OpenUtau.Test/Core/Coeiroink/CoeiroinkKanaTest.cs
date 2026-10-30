using System.Linq;
using Xunit;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkKanaTest {
        static string Parse(string lyric) {
            Assert.True(CoeiroinkKana.TryParse(lyric, out var moras), lyric);
            return string.Join(" | ", moras.Select(m => m.ToString()));
        }

        [Theory]
        [InlineData("か", "k a")]
        [InlineData("カ", "k a")]
        [InlineData("し", "sh i")]
        [InlineData("ちゃ", "ch a")]
        [InlineData("きょ", "ky o")]
        [InlineData("ふぁ", "f a")]
        [InlineData("てぃ", "t i")]
        [InlineData("ゔぁ", "v a")]
        [InlineData("を", "o")]
        [InlineData("ん", "N")]
        [InlineData("っ", "cl")]
        [InlineData("あ", "a")]
        public void ParsesSingleKana(string lyric, string expected) {
            Assert.Equal(expected, Parse(lyric));
        }

        [Fact]
        public void ParsesWordsAndLongVowels() {
            Assert.Equal("k o | N | n i | ch i | w a", Parse("こんにちわ"));
            Assert.Equal("k a | a", Parse("かー"));
        }

        [Theory]
        [InlineData("ka", "k a")]
        [InlineData("sha", "sh a")]
        [InlineData("n", "N")]
        [InlineData("k a", "k a")]
        [InlineData("ky o", "ky o")]
        [InlineData("N", "N")]
        public void ParsesRomajiAndTokens(string lyric, string expected) {
            Assert.Equal(expected, Parse(lyric));
        }

        [Theory]
        [InlineData("hello")]
        [InlineData("k")]
        [InlineData("")]
        public void RejectsUnknownText(string lyric) {
            Assert.False(CoeiroinkKana.TryParse(lyric, out _));
        }

        [Fact]
        public void RecognizesRestsAndExtensions() {
            Assert.True(CoeiroinkKana.IsRest("R"));
            Assert.True(CoeiroinkKana.IsRest("sp"));
            Assert.True(CoeiroinkKana.IsExtension("+"));
            Assert.True(CoeiroinkKana.IsExtension("ー"));
            Assert.False(CoeiroinkKana.IsRest("か"));
        }
    }
}
