using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkTimelineTest {
        const double FrameMs = 512 * 1000.0 / 44100;

        static CoeiroinkSyllable Syllable(string text, double startMs, double endMs, int tone) {
            CoeiroinkKana.TryParse(text, out var moras);
            return new CoeiroinkSyllable { Mora = moras[0], StartMs = startMs, EndMs = endMs, Tone = tone };
        }

        static List<CoeiroinkSyllable> Melody(params (string text, int tone)[] notes) {
            var list = new List<CoeiroinkSyllable>();
            for (int i = 0; i < notes.Length; i++) {
                list.Add(Syllable(notes[i].text, 400 + i * 500, 400 + (i + 1) * 500, notes[i].tone));
            }
            return list;
        }

        [Fact]
        public void PlanWrapsMorasInStartAndEnd() {
            var plan = CoeiroinkTimeline.BuildPlan(Melody(("か", 60), ("あ", 60)));
            Assert.Equal("^ k a a $", string.Join(" ", plan.Tokens));
        }

        [Fact]
        public void RisingMelodyPutsFallAfterPeak() {
            var plan = CoeiroinkTimeline.BuildPlan(Melody(("か", 60), ("さ", 67), ("た", 67), ("な", 60)));
            Assert.Equal("^ k a s a t a ] n a $", string.Join(" ", plan.Tokens));
        }

        [Fact]
        public void HighFirstMoraPutsFallAfterIt() {
            var plan = CoeiroinkTimeline.BuildPlan(Melody(("か", 67), ("さ", 60), ("た", 60)));
            Assert.Equal("^ k a ] s a t a $", string.Join(" ", plan.Tokens));
        }

        [Fact]
        public void LongGapInsertsPauseToken() {
            var syllables = new List<CoeiroinkSyllable> {
                Syllable("か", 400, 800, 60),
                Syllable("さ", 1600, 2000, 60),
            };
            var plan = CoeiroinkTimeline.BuildPlan(syllables);
            Assert.Equal("^ k a _ s a $", string.Join(" ", plan.Tokens));
        }

        [Fact]
        public void ForcedPauseSilencesAShortGap() {
            var first = Syllable("か", 400, 800, 60);
            var second = Syllable("さ", 800, 1200, 60);
            second.PauseBefore = true;
            var plan = CoeiroinkTimeline.BuildPlan(new List<CoeiroinkSyllable> { first, second });
            Assert.Equal("^ k a _ s a $", string.Join(" ", plan.Tokens));
        }

        [Fact]
        public void ShortRestOverThresholdInsertsPause() {
            var plan = CoeiroinkTimeline.BuildPlan(new List<CoeiroinkSyllable> {
                Syllable("か", 400, 700, 60),
                Syllable("さ", 800, 1200, 60),
            });
            Assert.Contains(CoeiroinkTimeline.PauseToken, plan.Tokens);
        }

        [Fact]
        public void LongPhrasesAreSplitIntoAccentPhrases() {
            var notes = Enumerable.Range(0, 10).Select(_ => ("あ", 60)).ToArray();
            var plan = CoeiroinkTimeline.BuildPlan(Melody(notes));
            Assert.Contains(CoeiroinkTimeline.BoundaryToken, plan.Tokens);
        }

        [Fact]
        public void VowelOnsetsLandOnTheNoteStarts() {
            var syllables = Melody(("か", 60), ("さ", 62), ("た", 64));
            var plan = CoeiroinkTimeline.BuildPlan(syllables);
            var predicted = plan.Tokens.Select(_ => 4f).ToArray();
            double startMs = syllables[0].StartMs - 160;
            var frames = CoeiroinkTimeline.Allocate(plan, syllables, predicted, startMs, FrameMs);
            Assert.All(frames, f => Assert.True(f >= 1));

            int vowelIndex = 0;
            int cursor = 0;
            for (int i = 0; i < plan.Tokens.Count; i++) {
                if (plan.Roles[i] == CoeiroinkTokenRole.Vowel) {
                    double onsetMs = startMs + cursor * FrameMs;
                    Assert.InRange(onsetMs - syllables[vowelIndex].StartMs, -FrameMs, FrameMs);
                    vowelIndex++;
                }
                cursor += frames[i];
            }
            Assert.Equal(3, vowelIndex);
        }

        [Fact]
        public void CarrierFollowsThePhraseAfterATailPause() {
            var syllables = Melody(("か", 60), ("あ", 60));
            var plan = CoeiroinkTimeline.BuildPlan(syllables, true);
            Assert.Equal("^ k a a _ a r i g a t o u $", string.Join(" ", plan.Tokens));
            Assert.Equal(5, plan.PhraseTokenCount);
            Assert.Equal(CoeiroinkTokenRole.Tail, plan.Roles[4]);
            Assert.All(plan.Roles.Skip(5).Take(8), r => Assert.Equal(CoeiroinkTokenRole.Carrier, r));
        }

        [Fact]
        public void PlanWithoutCarrierCountsEveryToken() {
            var plan = CoeiroinkTimeline.BuildPlan(Melody(("か", 60)));
            Assert.Equal(plan.Tokens.Count, plan.PhraseTokenCount);
        }

        [Fact]
        public void CarrierTokensGetPredictedFramesAndTheTailGetsTailFrames() {
            var syllables = Melody(("か", 60), ("あ", 60));
            var plan = CoeiroinkTimeline.BuildPlan(syllables, true);
            var predicted = Enumerable.Repeat(3.2f, plan.Tokens.Count).ToArray();
            var frames = CoeiroinkTimeline.Allocate(plan, syllables, predicted, 240, FrameMs);
            Assert.Equal(CoeiroinkTimeline.TailFrames, frames[plan.PhraseTokenCount - 1]);
            Assert.All(frames.Skip(plan.PhraseTokenCount), f => Assert.Equal(4, f));
        }

        [Fact]
        public void ModelFramesCapVowelsAndLeadingSilence() {
            var syllables = Melody(("か", 60), ("あ", 60));
            var plan = CoeiroinkTimeline.BuildPlan(syllables, true);
            var predicted = Enumerable.Repeat(2f, plan.Tokens.Count).ToArray();
            var frames = CoeiroinkTimeline.Allocate(plan, syllables, predicted, 240, FrameMs);
            var model = CoeiroinkTimeline.ModelFrames(plan, frames);
            for (int i = 0; i < frames.Length; i++) {
                if (plan.Roles[i] == CoeiroinkTokenRole.Vowel) {
                    Assert.Equal(System.Math.Min(frames[i], CoeiroinkTimeline.MaxVowelFrames), model[i]);
                } else if (plan.Roles[i] == CoeiroinkTokenRole.Start) {
                    Assert.True(model[i] <= CoeiroinkTimeline.StartModelFrames);
                } else if (plan.Roles[i] == CoeiroinkTokenRole.Consonant || plan.Roles[i] == CoeiroinkTokenRole.Carrier) {
                    Assert.Equal(frames[i], model[i]);
                }
            }
            Assert.Contains(frames.Where((f, i) => plan.Roles[i] == CoeiroinkTokenRole.Vowel), f => f > CoeiroinkTimeline.MaxVowelFrames);
        }

        [Fact]
        public void SustainMaskMarksOnlySungVowels() {
            var plan = CoeiroinkTimeline.BuildPlan(Melody(("か", 60), ("っ", 60), ("ん", 60)), true);
            var mask = CoeiroinkTimeline.SustainMask(plan);
            for (int i = 0; i < mask.Length; i++) {
                Assert.Equal(plan.Roles[i] == CoeiroinkTokenRole.Vowel && plan.Tokens[i] != "cl", mask[i]);
            }
        }
    }
}
