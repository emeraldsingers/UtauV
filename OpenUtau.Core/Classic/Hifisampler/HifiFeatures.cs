// Ported from hifisampler's backend/resampler.py (https://github.com/openhachimi/hifisampler),
// licensed under the Apache License 2.0; see LICENSE in this folder.
// Modified for OpenUtau: translated to C#, reading OpenUtau's resampler arguments.

using System;

namespace OpenUtau.Classic.Hifisampler {
    /// <summary>
    /// Resampler.generate_features: breath / voicing / tension on the source, peak-scaled to
    /// at most 0.5, then the log mel at the analysis hop. Breathiness, voicing and tension
    /// come from OpenUtau's Mb / Mv / Mt flags (not hifisampler's Hb / Hv / Ht):
    /// noise gain 1 + Mb / 100, harmonic gain Mv / 100, tension Mt.
    /// </summary>
    internal sealed class HifiFeatures {
        /// <summary>Log mel, [frame][mel], at <see cref="HifiSamplerConfig.OriginHopSize"/>.</summary>
        public readonly float[][] Mel;
        /// <summary>The gain applied before analysis, undone on the rendered audio.</summary>
        public readonly double Scale;

        public HifiFeatures(float[][] mel, double scale) {
            Mel = mel;
            Scale = scale;
        }

        /// <summary>Whether the flags need the harmonic / noise split, as _needs_hnsep_separation.</summary>
        public static bool NeedsSeparation(int mb, int mv, int mt) {
            return mt != 0 || 100 + mb != mv;
        }

        /// <param name="harmonic">The harmonic part of a signal (hnsep), only called when needed.</param>
        public static HifiFeatures Generate(float[] wave, int mb, int mv, int mt, int g,
                HifiSamplerConfig config, Func<float[], float[]> harmonic) {
            var x = (float[])wave.Clone();
            if (NeedsSeparation(mb, mv, mt)) {
                var h = harmonic(wave);
                double breath = Math.Clamp(100 + mb, 0, 500) / 100.0;
                double voicing = Math.Clamp(mv, 0, 150) / 100.0;
                var voiced = new float[h.Length];
                for (int i = 0; i < h.Length; i++) {
                    voiced[i] = (float)(voicing * h[i]);
                }
                if (mt != 0) {
                    double tension = Math.Clamp(mt, -100, 100);
                    voiced = HifiTension.Apply(voiced, -tension / 50, config.SampleRate, config.NFft, config.HopSize, config.WinSize);
                }
                for (int i = 0; i < x.Length; i++) {
                    x[i] = (float)(breath * (wave[i] - h[i]) + voiced[i]);
                }
            } else if (mb != 0 || mv != 100) {
                // Equal gains: scale the whole signal instead of separating.
                double gain = Math.Clamp(100 + mb, 0, 500) / 100.0;
                for (int i = 0; i < x.Length; i++) {
                    x[i] = (float)(x[i] * gain);
                }
            }

            double scale = 1.0;
            float waveMax = (float)HifiArray.MaxAbs(x);
            if (waveMax >= 0.5f) {
                float s = 0.5f / waveMax;
                for (int i = 0; i < x.Length; i++) {
                    x[i] *= s;
                }
                scale = s;
            }

            double gender = Math.Clamp(g, -600, 600);
            var analyzer = new HifiMelSpectrogram(config.SampleRate, config.NFft, config.WinSize,
                config.OriginHopSize, config.MelFmin, config.MelFmax, config.NMels);
            var mel = analyzer.Compute(x, gender / 100);
            HifiMelSpectrogram.LogCompress(mel);
            return new HifiFeatures(mel, scale);
        }
    }
}
