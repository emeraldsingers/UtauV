using System;
using System.Linq;
using NumSharp;
using OpenUtau.Core.DiffSinger;
using Xunit;

namespace OpenUtau.Test.Core.DiffSinger {
    public class SpeakerEmbedMixTest {
        static readonly float[][] Embeds = {
            new float[] { 1, 0, 0, 2 },
            new float[] { 0, 1, 0, 4 },
            new float[] { 0, 0, 1, 8 },
        };

        static float[] Mix(float[] amounts, params int[] defaults) =>
            DiffSingerSpeakerEmbedManager.MixSpeakerEmbeds(amounts, defaults.Length, 3, defaults, Embeds, 4);

        [Fact]
        public void NoCurvesGivesTheDefaultSpeaker() {
            var result = Mix(new float[] { 0, 0, 0, 0, 0, 0 }, 1, 2);

            Assert.Equal(Embeds[1], result.Take(4));
            Assert.Equal(Embeds[2], result.Skip(4));
        }

        [Fact]
        public void FullCurveGivesTheTargetSpeaker() {
            var result = Mix(new float[] { 0, 1, 0 }, 0);

            Assert.Equal(Embeds[1], result);
        }

        [Fact]
        public void PartialCurvesMatchTheWeightedAverageWithTheDefaultFillingTheRest() {
            var result = Mix(new float[] { 0, 0.25f, 0.5f }, 0);

            for (int j = 0; j < 4; ++j) {
                float expected = 0.25f * Embeds[0][j] + 0.25f * Embeds[1][j] + 0.5f * Embeds[2][j];
                Assert.Equal(expected, result[j], 5);
            }
        }

        [Fact]
        public void AmountsAboveOneExtrapolatePastTheTarget() {
            var result = Mix(new float[] { 0, 2, 0 }, 0);

            Assert.Equal(new float[] { -1, 2, 0, 6 }, result);
        }

        [Fact]
        public void NegativeAmountsMoveAwayFromTheTarget() {
            var result = Mix(new float[] { 0, -1, 0 }, 0);

            Assert.Equal(new float[] { 2, -1, 0, 0 }, result);
        }

        [Fact]
        public void AmountOnTheDefaultSpeakerChangesNothing() {
            var result = Mix(new float[] { 3, 0, 0 }, 0);

            Assert.Equal(Embeds[0], result);
        }

        [Fact]
        public void EachFrameUsesItsOwnDefaultSpeaker() {
            var result = Mix(new float[] { 0, 1, 0, 0, 1, 0 }, 0, 2);

            Assert.Equal(Embeds[1], result.Take(4));
            Assert.Equal(Embeds[1], result.Skip(4));
        }

        [Fact]
        public void AmountsAreClampedAtThreeHundredPercentEitherWay() {
            Assert.Equal(Mix(new float[] { 0, 3, 0 }, 0), Mix(new float[] { 0, 10, 0 }, 0));
            Assert.Equal(Mix(new float[] { 0, -3, 0 }, 0), Mix(new float[] { 0, -10, 0 }, 0));
            Assert.NotEqual(Mix(new float[] { 0, 3, 0 }, 0), Mix(new float[] { 0, 2, 0 }, 0));
        }

        [Fact]
        public void MatchesTheNormalizedWeightsWhenCurvesSumToAtMostOne() {
            var rng = new Random(1);
            int frames = 40, speakers = 4, hidden = 16;
            var embeds = Enumerable.Range(0, speakers)
                .Select(_ => Enumerable.Range(0, hidden).Select(__ => (float)rng.NextDouble() * 2 - 1).ToArray())
                .ToArray();
            var defaults = Enumerable.Range(0, frames).Select(i => i % speakers).ToArray();
            var amounts = new float[frames * speakers];
            for (int f = 0; f < frames; f++) {
                float budget = (float)rng.NextDouble();
                for (int s = 1; s < speakers; s++) {
                    amounts[f * speakers + s] = budget * (float)rng.NextDouble() / (speakers - 1);
                }
            }
            var weights = np.array(amounts).reshape(frames, speakers);
            for (int f = 0; f < frames; f++) {
                var sum = weights[f, ":"].ToArray<float>().Sum();
                weights[f, defaults[f]] += 1 - sum;
            }
            var matrix = np.array(embeds.SelectMany(e => e).ToArray()).reshape(speakers, hidden);
            var expected = np.dot(weights, matrix).ToArray<float>();

            var actual = DiffSingerSpeakerEmbedManager.MixSpeakerEmbeds(amounts, frames, speakers, defaults, embeds, hidden);

            Assert.Equal(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) {
                Assert.Equal(expected[i], actual[i], 4);
            }
        }

        [Fact]
        public void ExtrapolationIsRelativeToEachFramesDefault() {
            var result = Mix(new float[] { 0, 2, 0, 0, 2, 0 }, 0, 2);

            Assert.Equal(new float[] { -1, 2, 0, 6 }, result.Take(4));
            Assert.Equal(new float[] { 0, 2, -1, 0 }, result.Skip(4));
        }
    }
}
