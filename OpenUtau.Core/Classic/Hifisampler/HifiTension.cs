// Ported from hifisampler's util/audio.py (https://github.com/openhachimi/hifisampler),
// licensed under the Apache License 2.0; see LICENSE in this folder.
// Modified for OpenUtau: translated to C#, reading OpenUtau's resampler arguments.

using System;
using System.Numerics;

namespace OpenUtau.Classic.Hifisampler {
    /// <summary>
    /// util/audio.py pre_emphasis_base_tension: a log-amplitude tilt across the spectrum
    /// (clamped to +-2 nepers), keeping the phase, renormalized to the input peak.
    /// </summary>
    internal static class HifiTension {
        public static float[] Apply(float[] wave, double b, int sampleRate, int nfft, int hop, int winSize) {
            return Apply(wave, _ => b, sampleRate, nfft, hop, winSize);
        }

        /// <param name="bAt">
        /// The tilt b at a sample position, for a tension curve: each STFT frame takes the
        /// value at its center, and the output gain the value at each sample.
        /// </param>
        public static float[] Apply(float[] wave, Func<double, double> bAt, int sampleRate, int nfft, int hop, int winSize) {
            int originalLength = wave.Length;
            int padLength = (hop - originalLength % hop) % hop;
            var x = new double[originalLength + padLength];
            for (int i = 0; i < originalLength; i++) {
                x[i] = wave[i];
            }
            var window = HifiStft.HannWindow(winSize);
            var spec = HifiStft.Spectrum(x, nfft, hop, window);

            int fftBin = nfft / 2 + 1;
            double x0 = fftBin / ((sampleRate / 2.0) / 1500);
            for (int m = 0; m < spec.Length; m++) {
                // Centered frames: frame m is centered on sample m * hop.
                double b = bAt(m * hop);
                var frame = spec[m];
                for (int k = 0; k < fftBin; k++) {
                    double tilt = Math.Clamp((-b / x0) * k + b, -2, 2);
                    double amp = frame[k].Magnitude;
                    double phase = Math.Atan2(frame[k].Imaginary, frame[k].Real);
                    amp = Math.Exp(Math.Log(Math.Max(amp, 1e-9)) + tilt);
                    frame[k] = new Complex(amp * Math.Cos(phase), amp * Math.Sin(phase));
                }
            }
            var filtered = HifiStft.Inverse(spec, nfft, hop, window);

            double originalMax = HifiArray.MaxAbs(x);
            double filteredMax = HifiArray.MaxAbs(filtered);
            var result = new float[originalLength];
            if (filteredMax == 0) {
                return result;  // silent input; Python would divide 0 by 0 here
            }
            double norm = originalMax / filteredMax;
            for (int i = 0; i < originalLength && i < filtered.Length; i++) {
                result[i] = (float)(filtered[i] * norm * (Math.Clamp(bAt(i) / -15, 0, 0.33) + 1));
            }
            return result;
        }
    }
}
