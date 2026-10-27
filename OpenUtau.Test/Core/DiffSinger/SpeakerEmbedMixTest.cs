using System.Linq;
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
        public void ExtrapolationIsRelativeToEachFramesDefault() {
            var result = Mix(new float[] { 0, 2, 0, 0, 2, 0 }, 0, 2);

            Assert.Equal(new float[] { -1, 2, 0, 6 }, result.Take(4));
            Assert.Equal(new float[] { 0, 2, -1, 0 }, result.Skip(4));
        }
    }
}
