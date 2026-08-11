using OpenUtau.Core.DiffSinger;
using Xunit;

namespace OpenUtau.Core {
    public class DiffSingerRendererTest {
        [Fact]
        public void RenderCacheFileNameIncludesVarianceSteps() {
            var lowStepName = DiffSingerRenderer.GetRenderCacheFileName(0x1234, 0.5, 20, 10);
            var highStepName = DiffSingerRenderer.GetRenderCacheFileName(0x1234, 0.5, 20, 100);

            Assert.NotEqual(lowStepName, highStepName);
            Assert.Contains("-vsteps10.wav", lowStepName);
            Assert.Contains("-vsteps100.wav", highStepName);
        }
    }
}
