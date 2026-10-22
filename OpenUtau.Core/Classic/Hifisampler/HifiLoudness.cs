// Ported from hifisampler's util/audio.py (https://github.com/openhachimi/hifisampler),
// licensed under the Apache License 2.0; see LICENSE in this folder.
// Modified for OpenUtau: translated to C#, reading OpenUtau's resampler arguments.
// The meter follows pyloudnorm (MIT).

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Classic.Hifisampler {
    /// <summary>
    /// pyloudnorm Meter(rate, block_size).integrated_loudness for mono: ITU-R BS.1770-4
    /// K-weighting, gated mean square over overlapping blocks.
    /// </summary>
    internal static class HifiLoudnessMeter {
        public static double IntegratedLoudness(double[] data, int rate, double blockSize = 0.400) {
            var x = (double[])data.Clone();
            // K-weighting: high shelf (G 4 dB, Q 1/sqrt 2, 1500 Hz), then high pass (Q 0.5, 38 Hz).
            ApplyRbj(x, rate, 4.0, 1 / Math.Sqrt(2), 1500.0, highShelf: true);
            ApplyRbj(x, rate, 0.0, 0.5, 38.0, highShelf: false);

            double tg = blockSize;
            const double gammaA = -70.0;
            const double overlap = 0.75;
            const double step = 1.0 - overlap;
            double t = x.Length / (double)rate;
            int numBlocks = (int)Math.Round((t - tg) / (tg * step), MidpointRounding.ToEven) + 1;
            var z = new double[Math.Max(numBlocks, 0)];
            for (int j = 0; j < z.Length; j++) {
                int l = (int)(tg * (j * step) * rate);
                int u = (int)(tg * (j * step + 1) * rate);
                double sum = 0;
                for (int i = l; i < u && i < x.Length; i++) {
                    sum += x[i] * x[i];
                }
                z[j] = 1.0 / (tg * rate) * sum;
            }
            var loudness = z.Select(zj => -0.691 + 10.0 * Math.Log10(zj)).ToArray();

            double MeanOf(IEnumerable<int> blocks) {
                var list = blocks.ToList();
                return list.Count == 0 ? double.NaN : list.Average(j => z[j]);
            }
            var absGated = Enumerable.Range(0, z.Length).Where(j => loudness[j] >= gammaA);
            double gammaR = -0.691 + 10.0 * Math.Log10(MeanOf(absGated)) - 10.0;
            var relGated = Enumerable.Range(0, z.Length).Where(j => loudness[j] > gammaR && loudness[j] >= gammaA);
            double zAvg = MeanOf(relGated);
            if (double.IsNaN(zAvg)) {
                zAvg = 0;  // np.nan_to_num
            }
            return -0.691 + 10.0 * Math.Log10(zAvg);
        }

        /// <summary>pyloudnorm IIRfilter coefficients (RBJ cookbook), applied with lfilter.</summary>
        static void ApplyRbj(double[] x, int rate, double g, double q, double fc, bool highShelf) {
            double a = Math.Pow(10, g / 40.0);
            double w0 = 2.0 * Math.PI * (fc / rate);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double cos = Math.Cos(w0);
            double b0, b1, b2, a0, a1, a2;
            if (highShelf) {
                double sqrtA = Math.Sqrt(a);
                b0 = a * ((a + 1) + (a - 1) * cos + 2 * sqrtA * alpha);
                b1 = -2 * a * ((a - 1) + (a + 1) * cos);
                b2 = a * ((a + 1) + (a - 1) * cos - 2 * sqrtA * alpha);
                a0 = (a + 1) - (a - 1) * cos + 2 * sqrtA * alpha;
                a1 = 2 * ((a - 1) - (a + 1) * cos);
                a2 = (a + 1) - (a - 1) * cos - 2 * sqrtA * alpha;
            } else {
                b0 = (1 + cos) / 2;
                b1 = -(1 + cos);
                b2 = (1 + cos) / 2;
                a0 = 1 + alpha;
                a1 = -2 * cos;
                a2 = 1 - alpha;
            }
            HifiIir.Biquad(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0, x);
        }
    }

    /// <summary>
    /// util/audio.py loudness_norm: optionally trim silence, normalize toward the target
    /// loudness by strength percent, and paste back at the original position with a fade-out
    /// and a crossfade into the untouched tail.
    /// </summary>
    internal static class HifiLoudness {
        public static float[] Normalize(float[] input, int rate, bool trimSilence, double silenceThreshold,
                double loudness = -16.0, double blockSize = 0.400, double strength = 100) {
            int originalLength = input.Length;
            if (originalLength == 0) {
                return input;
            }
            var original = new double[originalLength];
            for (int i = 0; i < originalLength; i++) {
                original[i] = input[i];
            }
            var audio = original;

            int frameLength = (int)(rate * 0.02);
            int hopLength = (int)(rate * 0.01);
            var voicedFrames = new List<int>();
            if (trimSilence) {
                for (int i = 0, frame = 0; i < originalLength - frameLength; i += hopLength, frame++) {
                    if (RmsDb(original, i, frameLength) > silenceThreshold) {
                        voicedFrames.Add(frame);
                    }
                }
                if (voicedFrames.Count > 0) {
                    int paddingFrames = (int)(rate * 0.1) / hopLength;
                    int startSample = Math.Max(0, voicedFrames[0] * hopLength);
                    int endSample = Math.Min(originalLength, (voicedFrames[^1] + 1 + paddingFrames) * hopLength + frameLength);
                    audio = original[startSample..endSample];
                }
            }

            int blockSamples = (int)(rate * blockSize);
            if (audio.Length < blockSamples) {
                audio = HifiArray.ReflectPad(audio, 0, blockSamples - audio.Length);
            }

            double measured = HifiLoudnessMeter.IntegratedLoudness(audio, rate, blockSize);
            // A silent render measures -inf, where Python's gain becomes NaN; leave it as is.
            if (double.IsFinite(measured)) {
                double target = measured + (loudness - measured) * strength / 100;
                double gain = Math.Pow(10.0, (target - measured) / 20.0);
                for (int i = 0; i < audio.Length; i++) {
                    audio[i] *= gain;
                }
            }

            if (trimSilence) {
                if (voicedFrames.Count > 0) {
                    var output = new double[originalLength];
                    int startSample = Math.Max(0, voicedFrames[0] * hopLength);
                    int availableLength = Math.Min(audio.Length, originalLength - startSample);
                    int fadeLength = Math.Min((int)(rate * 0.2), availableLength / 4);
                    for (int i = 0; i < availableLength; i++) {
                        double fade = 1.0;
                        int fadeIndex = i - (availableLength - fadeLength);
                        if (fadeLength > 0 && fadeIndex >= 0) {
                            fade = Linspace(1.0, 0.0, fadeLength, fadeIndex);
                        }
                        output[startSample + i] = audio[i] * fade;
                    }
                    if (startSample + availableLength < originalLength) {
                        int remainLength = originalLength - (startSample + availableLength);
                        int crossfadeLength = Math.Min(fadeLength, remainLength);
                        if (crossfadeLength > 0) {
                            int crossfadeStart = startSample + availableLength;
                            for (int i = 0; i < remainLength; i++) {
                                double fade = i < crossfadeLength ? Linspace(0.0, 1.0, crossfadeLength, i) : 1.0;
                                output[crossfadeStart + i] = original[crossfadeStart + i] * fade;
                            }
                        }
                    }
                    audio = output;
                } else {
                    audio = audio[..Math.Min(audio.Length, originalLength)];
                }
            }

            if (originalLength < blockSamples) {
                audio = audio[..Math.Min(audio.Length, originalLength)];
            }
            var result = new float[audio.Length];
            for (int i = 0; i < audio.Length; i++) {
                result[i] = (float)audio[i];
            }
            return result;
        }

        static double RmsDb(double[] x, int start, int length) {
            double sum = 0;
            for (int i = start; i < start + length; i++) {
                sum += x[i] * x[i];
            }
            double rms = Math.Sqrt(sum / length);
            return rms < 1e-10 ? double.NegativeInfinity : 20 * Math.Log10(rms);
        }

        /// <summary>The i-th of n values of np.linspace(start, stop, n).</summary>
        static double Linspace(double start, double stop, int n, int i) {
            if (n == 1) {
                return start;
            }
            return i == n - 1 ? stop : start + i * ((stop - start) / (n - 1));
        }
    }
}
