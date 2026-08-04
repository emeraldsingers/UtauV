using OpenUtau.Core.DiffSinger;
using Xunit;

namespace OpenUtau.Core {
    public class DiffSingerVariancePatchTest {
        [Fact]
        public void BuildChangedFrameMaskMarksOnlyChangedFrames() {
            var mask = DiffSingerVariancePatch.BuildChangedFrameMask(
                new[] { 1f, 1f, 1f, 2f },
                new[] { 1f, 2f, 1f, 2f },
                1e-4f);

            Assert.Equal(new[] { false, true, false, false }, mask);
        }

        [Fact]
        public void BuildChangedFrameMaskGroupsSpeakerEmbeddingByFrame() {
            var mask = DiffSingerVariancePatch.BuildChangedFrameMask(
                new[] { 1f, 2f, 3f, 4f, 5f, 6f },
                new[] { 1f, 2f, 3f, 40f, 5f, 6f },
                3,
                1e-4f);

            Assert.Equal(new[] { false, true, false }, mask);
        }

        [Fact]
        public void BuildChangedFrameMaskMarksAllFramesForIncompatibleEmbeddingShape() {
            var mask = DiffSingerVariancePatch.BuildChangedFrameMask(
                new[] { 1f, 2f, 3f, 4f },
                new[] { 1f, 2f, 3f },
                2,
                1e-4f);

            Assert.Equal(new[] { true, true }, mask);
        }

        [Fact]
        public void ExpandToChannelsUsesSharedFrameMask() {
            var mask = DiffSingerVariancePatch.ExpandToChannels(
                new[] { false, true, false }, 3);

            Assert.Equal(
                new[] { false, false, false, true, true, true, false, false, false },
                mask);
        }

        [Fact]
        public void HardComposePreservesUnmaskedFramesExactly() {
            var previous = Result(
                new[] { 1f, 2f, 3f, 4f },
                new[] { 5f, 6f, 7f, 8f });
            var predicted = Result(
                new[] { 10f, 20f, 30f, 40f },
                new[] { 50f, 60f, 70f, 80f });
            var mask = DiffSingerVariancePatch.ExpandToChannels(
                new[] { false, true, false, true }, 2);

            var result = DiffSingerVariancePatch.HardCompose(previous, predicted, mask, 2);

            Assert.Equal(new[] { 1f, 20f, 3f, 40f }, result.energy);
            Assert.Equal(new[] { 5f, 60f, 7f, 80f }, result.breathiness);
        }

        [Fact]
        public void HardComposeDoesNotLeakModelChangesOutsideMask() {
            var previous = Result(new[] { 1f, 2f, 3f });
            var predicted = Result(new[] { 100f, 200f, 300f });
            var mask = DiffSingerVariancePatch.ExpandToChannels(
                new[] { false, true, false }, 1);

            var result = DiffSingerVariancePatch.HardCompose(previous, predicted, mask, 1);

            Assert.Equal(new[] { 1f, 200f, 3f }, result.energy);
        }

        [Fact]
        public void HardComposeFallsBackToPredictedForIncompatibleMetadata() {
            var previous = Result(new[] { 1f, 2f, 3f }, frameMs: 50);
            var predicted = Result(new[] { 10f, 20f, 30f }, frameMs: 60);
            var mask = new[] { true, false, true };

            var result = DiffSingerVariancePatch.HardCompose(previous, predicted, mask, 1);

            Assert.Equal(predicted.energy, result.energy);
        }

        [Fact]
        public void IsMetadataCompatibleRejectsFrameLayoutChanges() {
            var previous = Result(new[] { 1f, 2f, 3f });
            var changed = Result(new[] { 1f, 2f, 3f, 4f });

            Assert.False(DiffSingerVariancePatch.IsMetadataCompatible(previous, changed));
        }

        static VarianceResult Result(
            float[] energy,
            float[]? breathiness = null,
            float frameMs = 50) {
            return new VarianceResult {
                energy = energy,
                breathiness = breathiness,
                frameMs = frameMs,
                headFrames = 1,
                tailFrames = 1,
                totalFrames = energy.Length,
            };
        }
    }
}
