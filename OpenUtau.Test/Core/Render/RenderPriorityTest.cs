using Xunit;

namespace OpenUtau.Core.Render {
    public class RenderPriorityTest {
        [Fact]
        public void PlaybackBucketPrioritizesCurrentThenFutureThenPast() {
            Assert.Equal(0, RenderPriority.PlaybackBucket(100, 200, 150));
            Assert.Equal(1, RenderPriority.PlaybackBucket(200, 300, 150));
            Assert.Equal(2, RenderPriority.PlaybackBucket(0, 100, 150));
        }

        [Fact]
        public void OverlapsUsesHalfOpenRanges() {
            Assert.True(RenderPriority.Overlaps(10, 20, 19, 30));
            Assert.False(RenderPriority.Overlaps(10, 20, 20, 30));
            Assert.False(RenderPriority.Overlaps(10, 20, 0, 10));
        }

        [Fact]
        public void PreRenderBucketPrioritizesOverlappingPriorityPart() {
            Assert.Equal(0, RenderPriority.PreRenderBucket(
                isPriorityPart: true, overlapsPriority: true, isAfterPriorityStart: true));
            Assert.Equal(1, RenderPriority.PreRenderBucket(
                isPriorityPart: true, overlapsPriority: false, isAfterPriorityStart: true));
            Assert.Equal(2, RenderPriority.PreRenderBucket(
                isPriorityPart: false, overlapsPriority: false, isAfterPriorityStart: true));
            Assert.Equal(3, RenderPriority.PreRenderBucket(
                isPriorityPart: false, overlapsPriority: false, isAfterPriorityStart: false));
        }

        [Fact]
        public void PreRenderDistancePrioritizesPhraseContainingPriorityStart() {
            Assert.Equal(0, RenderPriority.PreRenderDistance(100, 200, 150));
            Assert.Equal(50, RenderPriority.PreRenderDistance(200, 300, 150));
            Assert.Equal(50, RenderPriority.PreRenderDistance(0, 100, 150));
        }
    }
}
