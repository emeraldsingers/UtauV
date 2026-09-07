using System;
using System.Linq;
using Xunit;

namespace OpenUtau.Core.SignalChain {
    public class CrossSynthDSPTest {
        [Fact]
        public void BypassesAtRatioExtremes() {
            var a = Enumerable.Range(0, 4096).Select(i => MathF.Sin(i * 0.01f)).ToArray();
            var b = Enumerable.Range(0, 4096).Select(i => MathF.Cos(i * 0.01f)).ToArray();

            Assert.Equal(a, CrossSynthDSP.StftBlend(a, b, new[] { 0f }));
            Assert.Equal(b, CrossSynthDSP.StftBlend(a, b, new[] { 1f }));
        }

        [Fact]
        public void ProducesFiniteIntermediateBlend() {
            var a = Enumerable.Range(0, 4096).Select(i => MathF.Sin(i * 0.01f)).ToArray();
            var b = Enumerable.Range(0, 4096).Select(i => MathF.Cos(i * 0.02f)).ToArray();

            var result = CrossSynthDSP.StftBlend(a, b, new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f });

            Assert.Equal(a.Length, result.Length);
            Assert.All(result, sample => Assert.True(float.IsFinite(sample)));
        }

        [Fact]
        public void LadderBlendHandlesLargePitchJumpWithoutNonFiniteSamples() {
            const int length = 8192;
            var banks = new[] {
                Enumerable.Range(0, length).Select(i => 0.35f * MathF.Sin(i * 0.02f)).ToArray(),
                Enumerable.Range(0, length).Select(i => 0.35f * MathF.Sin(i * 0.03f)).ToArray(),
                Enumerable.Range(0, length).Select(i => 0.35f * MathF.Sin(i * 0.05f)).ToArray(),
            };
            var lower = Enumerable.Repeat(0, 8).Concat(Enumerable.Repeat(2, 8)).ToArray();
            var ratio = Enumerable.Repeat(0.2f, 8).Concat(Enumerable.Repeat(0.8f, 8)).ToArray();
            var fundamental = Enumerable.Repeat(110f, 16).ToArray();
            var result = CrossSynthDSP.StftBlendLadder(banks, lower, ratio, fundamental);
            Assert.Equal(length, result.Length);
            Assert.All(result, sample => Assert.True(float.IsFinite(sample)));
            Assert.True(result.Max(sample => MathF.Abs(sample)) < 2f);
        }

        [Fact]
        public void LadderBlendCoversPartialTailFrame() {
            const int length = 2048 + 512 + 123;
            var lower = Enumerable.Repeat(0.25f, length).ToArray();
            var upper = Enumerable.Repeat(0.25f, length).ToArray();

            var result = CrossSynthDSP.StftBlendLadder(
                new[] { lower, upper }, new[] { 0, 0, 0 }, new[] { 0.5f, 0.5f, 0.5f });

            Assert.Equal(length, result.Length);
            Assert.InRange(result[^1], 0.20f, 0.30f);
        }

        [Fact]
        public void LadderBlendDoesNotBlowUpAtPhraseStart() {
            const int sampleRate = 44100;
            const int frameCount = 16;
            int length = (frameCount - 1) * 512 + 2048;
            float[] MakeBank(float formantHz) => Enumerable.Range(0, length)
                .Select(i => {
                    float sample = 0f;
                    for (int h = 1; h * 110f < sampleRate / 2f; h++) {
                        float harmonic = h * 110f;
                        float gain = MathF.Exp(-0.5f * MathF.Pow((harmonic - formantHz) / 350f, 2)) + 0.03f;
                        sample += gain * MathF.Sin(2f * MathF.PI * harmonic * i / sampleRate);
                    }
                    return sample;
                }).ToArray();
            var lower = MakeBank(700f);
            var upper = MakeBank(1600f);
            float peak = Math.Max(lower.Select(MathF.Abs).Max(), upper.Select(MathF.Abs).Max());
            for (int i = 0; i < length; i++) {
                lower[i] *= 0.5f / peak;
                upper[i] *= 0.5f / peak;
            }

            var result = CrossSynthDSP.StftBlendLadder(
                new[] { lower, upper },
                new int[frameCount],
                Enumerable.Range(0, frameCount).Select(i => (float)i / (frameCount - 1)).ToArray(),
                Enumerable.Repeat(110f, frameCount).ToArray());

            float interiorPeak = result.Skip(2048).Take(4096).Select(MathF.Abs).Max();
            float startPeak = result.Take(512).Select(MathF.Abs).Max();
            Assert.All(result, sample => Assert.True(float.IsFinite(sample)));
            Assert.True(startPeak <= interiorPeak * 1.5f);
        }
    }
}
