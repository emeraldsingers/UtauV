using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkPhonemizerTest {
        static Phonemizer.Result Process(Phonemizer phonemizer, params (string lyric, int position, int duration)[] notes) {
            var project = new UProject();
            project.timeAxis.BuildSegments(project);
            var group = notes.Select(n => new Phonemizer.Note { lyric = n.lyric, position = n.position, duration = n.duration, tone = 60 }).ToArray();
            phonemizer.SetUp(new[] { group }, project, new UTrack(project));
            phonemizer.SetTiming(project.timeAxis);
            return phonemizer.Process(group, null, null, null, null, new Phonemizer.Note[0]);
        }

        static string[] Names(Phonemizer.Result result) => result.phonemes.Select(p => p.phoneme).ToArray();

        [Fact]
        public void PlainPhonemizerKeepsOnePhonemePerMora() {
            Assert.Equal(new[] { "ち" }, Names(Process(new CoeiroinkPhonemizer(), ("ち", 480, 480))));
            Assert.Equal(new[] { "k a", "k u" }, Names(Process(new CoeiroinkPhonemizer(), ("かく", 0, 960))));
        }

        [Fact]
        public void PlusPhonemizerSplitsConsonantFromVowelInsideTheNote() {
            var result = Process(new CoeiroinkPlusPhonemizer(), ("ち", 480, 480));
            Assert.Equal(new[] { "chi", "i" }, Names(result));
            Assert.Equal(0, result.phonemes[0].position);
            Assert.InRange(result.phonemes[1].position, 1, 240);
        }

        [Fact]
        public void PlusPhonemizerKeepsPlainVowelsAndNasalsWhole() {
            Assert.Equal(new[] { "あ" }, Names(Process(new CoeiroinkPlusPhonemizer(), ("あ", 0, 480))));
            Assert.Equal(new[] { "ん" }, Names(Process(new CoeiroinkPlusPhonemizer(), ("ん", 0, 480))));
        }

        [Fact]
        public void PlusPhonemizerSplitsEveryMoraOfAWord() {
            var result = Process(new CoeiroinkPlusPhonemizer(), ("きゃく", 0, 960));
            Assert.Equal(new[] { "kya", "a", "ku", "u" }, Names(result));
        }

        [Fact]
        public void BothTurnRestsIntoPauAndSkipExtensions() {
            foreach (Phonemizer phonemizer in new Phonemizer[] { new CoeiroinkPhonemizer(), new CoeiroinkPlusPhonemizer() }) {
                foreach (var lyric in new[] { "R", "SP", "pau" }) {
                    Assert.Equal(new[] { "pau" }, Names(Process(phonemizer, (lyric, 0, 480))));
                }
                Assert.Empty(Process(phonemizer, ("+", 0, 480)).phonemes);
            }
        }
    }
}
