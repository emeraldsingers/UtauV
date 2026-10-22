using System;
using System.Linq;
using Xunit;

namespace OpenUtau.Classic.Hifisampler {
    /// <summary>The Mb / Mv / Mt branching, with a stand-in for hnsep, so no package is needed.</summary>
    public class HifiFeaturesTest {
        static float[] Sine(int n, double amplitude) =>
            Enumerable.Range(0, n).Select(i => (float)(amplitude * Math.Sin(2 * Math.PI * 220 * i / 44100.0))).ToArray();

        static float[][] Mel(float[] x) {
            var mel = new HifiMelSpectrogram(44100, 2048, 2048, 128, 40, 16000, 128).Compute(x, 0);
            HifiMelSpectrogram.LogCompress(mel);
            return mel;
        }

        static void AssertMelEqual(float[][] expected, float[][] actual) {
            Assert.Equal(expected.Length, actual.Length);
            double maxErr = 0;
            for (int i = 0; i < expected.Length; i++) {
                for (int k = 0; k < expected[i].Length; k++) {
                    maxErr = Math.Max(maxErr, Math.Abs(expected[i][k] - actual[i][k]));
                }
            }
            Assert.True(maxErr < 1e-4, $"max error {maxErr}");
        }

        [Theory]
        [InlineData(0, 100, 0, false)]
        [InlineData(-50, 50, 0, false)]  // equal gains: plain scaling
        [InlineData(0, 50, 0, true)]
        [InlineData(-100, 100, 0, true)]
        [InlineData(0, 100, 20, true)]
        public void SeparationOnlyWhenNeeded(int mb, int mv, int mt, bool expected) {
            Assert.Equal(expected, HifiFeatures.NeedsSeparation(mb, mv, mt));
            bool called = false;
            HifiFeatures.Generate(Sine(4410, 0.3), mb, mv, mt, 0, new HifiSamplerConfig(), x => {
                called = true;
                return x.Select(v => v * 0.5f).ToArray();
            });
            Assert.Equal(expected, called);
        }

        [Fact]
        public void DefaultFlagsAnalyzeTheSource() {
            var x = Sine(44100, 0.3);
            var f = HifiFeatures.Generate(x, 0, 100, 0, 0, new HifiSamplerConfig(), _ => throw new Exception("not needed"));
            Assert.Equal(1.0, f.Scale);
            Assert.Equal(44100 / 128, f.Mel.Length);
            AssertMelEqual(Mel(x), f.Mel);
        }

        [Fact]
        public void BreathAndVoicingGains() {
            var x = Sine(44100, 0.3);
            // Stand-in: the harmonic part is half the signal, so the noise part is the other half.
            Func<float[], float[]> half = s => s.Select(v => v * 0.5f).ToArray();
            // Mb -100 drops the noise, Mv 100 keeps the harmonic: 0.5 x.
            var f = HifiFeatures.Generate(x, -100, 100, 0, 0, new HifiSamplerConfig(), half);
            AssertMelEqual(Mel(x.Select(v => v * 0.5f).ToArray()), f.Mel);
            // Mb 100 doubles the noise, Mv 50 halves the harmonic: 1.25 x, over 0.5 peak so rescaled.
            f = HifiFeatures.Generate(Sine(44100, 0.6), 100, 50, 0, 0, new HifiSamplerConfig(), half);
            Assert.Equal(0.5 / (0.6 * 1.25), f.Scale, 4);
        }

        [Fact]
        public void LoudSourceIsScaledToHalfPeak() {
            var f = HifiFeatures.Generate(Sine(44100, 0.8), 0, 100, 0, 0, new HifiSamplerConfig(), _ => throw new Exception());
            Assert.Equal(0.5 / 0.8, f.Scale, 4);
        }
    }
}
