using System;
using System.Linq;
using OpenUtau.Core.Render;
using Xunit;

namespace OpenUtau.Classic {
    public class WorldlineR11Test {
        [Fact]
        public void RendererIds() {
            Assert.Equal(Renderers.WORLDLINE_R11, Renderers.CreateRenderer(Renderers.WORLDLINE_R11).ToString());
            Assert.Equal(Renderers.WORLDLINE_R, Renderers.CreateRenderer(Renderers.WORLDLINE_R).ToString());
            Assert.Equal(Renderers.WORLDLINE_R2, Renderers.CreateRenderer(Renderers.WORLDLINE_R2).ToString());
            Assert.Contains(Renderers.WORLDLINE_R11, Renderers.GetSupportedRenderers(Core.Ustx.USingerType.Classic));
            // The Worldline-R variants share Worldline-R's expression graphs.
            Assert.Equal(Renderers.WORLDLINE_R, Renderers.GetExpressionGraphSlot(Renderers.WORLDLINE_R11));
            Assert.Equal(Renderers.WORLDLINE_R, Renderers.GetExpressionGraphSlot(Renderers.WORLDLINE_R2));
            Assert.Equal(Renderers.DIFFSINGER, Renderers.GetExpressionGraphSlot(Renderers.DIFFSINGER));
        }

        [Fact]
        public void ContinuousNoiseMatchesWorldNoiseLevel() {
            // Unvoiced, flat and fully aperiodic: the output is the noise half alone, and
            // WORLD's noise with a flat spectrum S has per-sample variance S.
            const int fs = 44100, hop = 220, fftSize = 2048, spSize = fftSize / 2 + 1, frames = 400;
            const double s = 1e-4;
            double[] Fill(int n, double v) => Enumerable.Repeat(v, n).ToArray();
            var sp = Fill(frames * spSize, s);
            var y = Worldline.WorldSynthesisContinuousNoise(
                new double[frames], sp, Fill(frames * spSize, s), Fill(frames * spSize, 1.0),
                Fill(frames, 1.0), fftSize, hop, fs, seed: 1,
                Fill(frames, 0.5), Fill(frames, 0.5), Fill(frames, 0.5), Fill(frames, 1.0));
            var mid = y.Skip(y.Length / 10).Take(y.Length * 8 / 10);
            double db = 10 * Math.Log10(mid.Average(v => v * v) / s);
            Assert.InRange(db, -0.5, 0.5);
            Assert.All(sp, v => Assert.Equal(s, v));
        }
    }
}
