using System;
using System.Linq;
using NWaves.Transforms;

namespace OpenUtau.Core.SignalChain {
    public class CrossSynthDSP {

        const int   SAMPLE_RATE = 44100;
        const float EPS         = 1e-8f;
        const bool  NWAVES_INVERSE_NORMALIZES = false;

        public static float[] StftBlend(
            float[]  a, float[]  b,
            float[]  frameRatios,
            double[] anchorA = null,
            double[] anchorB = null) {

            if (a == null && b != null) return (float[])b.Clone();
            if (b == null && a != null) return (float[])a.Clone();
            if (a == null && b == null) return new float[0];
            int length = Math.Max(a.Length, b.Length);
            if (a.Length < length) Array.Resize(ref a, length);
            if (b.Length < length) Array.Resize(ref b, length);
            if (frameRatios == null || frameRatios.All(r => r < 0.01f)) return (float[])a.Clone();
            if (frameRatios.All(r => r > 0.99f)) return (float[])b.Clone();

            const int fftSize = 2048;
            const int hopSize = 512;
            const int half    = fftSize / 2 + 1;
            float invN = NWAVES_INVERSE_NORMALIZES ? 1f : 1f / fftSize;
            var fft  = new Fft(fftSize);
            var win  = MakeHann(fftSize);
            float[] omega = new float[half];
            for (int k = 0; k < half; k++)
                omega[k] = 2f * MathF.PI * k / fftSize;

            float[] phsAccA  = new float[half];
            float[] phsAccB  = new float[half];
            float[] prevPhsA = new float[half];
            float[] prevPhsB = new float[half];
            bool firstFrame  = true;
            float prevEnergyA = 0f;
            float prevEnergyB = 0f;
            const float TRANSIENT_THRESHOLD = 4.0f;

            float[] output = new float[length];
            float[] wsum   = new float[length];

            float[] reA = new float[fftSize];
            float[] imA = new float[fftSize];
            float[] reB = new float[fftSize];
            float[] imB = new float[fftSize];

            bool hasAnchors  = anchorA != null && anchorB != null && anchorA.Length >= 2;
            int totalFrames  = (length - fftSize) / hopSize + 1;

            for (int f = 0; f < totalFrames; f++) {
                int posA = f * hopSize;

                int posB = posA;
                if (hasAnchors) {
                    double tA_ms = posA * 1000.0 / SAMPLE_RATE;
                    double tB_ms;
                    if      (tA_ms <= anchorA[0])                  tB_ms = anchorB[0];
                    else if (tA_ms >= anchorA[anchorA.Length - 1]) tB_ms = anchorB[anchorB.Length - 1];
                    else                                           tB_ms = PiecewiseLerp(anchorA, anchorB, tA_ms);
                    posB = (int)Math.Max(0, Math.Min(length - fftSize, tB_ms * SAMPLE_RATE / 1000.0));
                }

                float ratio = frameRatios != null && f < frameRatios.Length
                    ? frameRatios[f]
                    : (frameRatios?.Length > 0 ? frameRatios[frameRatios.Length - 1] : 0f);

                for (int i = 0; i < fftSize; i++) { reA[i] = a[posA + i] * win[i]; imA[i] = 0f; }
                fft.Direct(reA, imA);

                for (int i = 0; i < fftSize; i++) { reB[i] = b[posB + i] * win[i]; imB[i] = 0f; }
                fft.Direct(reB, imB);

                float energyA = 0f, energyB = 0f;
                for (int k = 0; k < half; k++) {
                    energyA += reA[k]*reA[k] + imA[k]*imA[k];
                    energyB += reB[k]*reB[k] + imB[k]*imB[k];
                }
                bool silenceToSignalA = !firstFrame && prevEnergyA <= EPS && energyA > EPS;
                bool transientA = !firstFrame && prevEnergyA > EPS && energyA / prevEnergyA > TRANSIENT_THRESHOLD;
                bool silenceToSignalB = !firstFrame && prevEnergyB <= EPS && energyB > EPS;
                bool transientB = !firstFrame && prevEnergyB > EPS && energyB / prevEnergyB > TRANSIENT_THRESHOLD;
                bool resetA = firstFrame || silenceToSignalA || transientA;
                bool resetB = firstFrame || silenceToSignalB || transientB;
                prevEnergyA = energyA;
                prevEnergyB = energyB;
                const float BYPASS_RATIO = 0.005f;
                bool bypass = ratio < BYPASS_RATIO;

                for (int k = 0; k < half; k++) {
                    float magA = MathF.Sqrt(reA[k]*reA[k] + imA[k]*imA[k]);
                    float magB = MathF.Sqrt(reB[k]*reB[k] + imB[k]*imB[k]);

                    float rawPhsA = MathF.Atan2(imA[k], reA[k]);
                    float rawPhsB = MathF.Atan2(imB[k], reB[k]);

                    if (resetA) {
                        phsAccA[k] = rawPhsA;
                    } else {
                        float dpA = WrapPi(rawPhsA - prevPhsA[k] - omega[k] * hopSize);
                        phsAccA[k] += omega[k] * hopSize + dpA;
                    }
                    if (resetB) {
                        phsAccB[k] = rawPhsB;
                    } else {
                        float dpB = WrapPi(rawPhsB - prevPhsB[k] - omega[k] * hopSize);
                        phsAccB[k] += omega[k] * hopSize + dpB;
                    }

                    prevPhsA[k] = rawPhsA;
                    prevPhsB[k] = rawPhsB;

                    if (bypass) continue;
                    const float SILENCE_GATE_FLOOR = 0.001f;
                    float silenceGateA = MathF.Min(1f, magA / SILENCE_GATE_FLOOR);
                    float linBlend = magA * (1f - ratio) + magB * ratio;
                    float geoBlend = MathF.Exp(
                        MathF.Log(magA + EPS) * (1f - ratio) +
                        MathF.Log(magB + EPS) * ratio);
                    float minMag = MathF.Min(magA, magB);
                    float silenceWeight = MathF.Min(1f, minMag / 0.01f);
                    float magOut = (linBlend + silenceWeight * (geoBlend - linBlend)) * silenceGateA;
                    float pxr = (1f - ratio) * MathF.Cos(phsAccA[k]) + ratio * MathF.Cos(phsAccB[k]);
                    float pxi = (1f - ratio) * MathF.Sin(phsAccA[k]) + ratio * MathF.Sin(phsAccB[k]);
                    float phsOut = pxr * pxr + pxi * pxi > 1e-12f ? MathF.Atan2(pxi, pxr) : phsAccA[k];
                    reA[k] = magOut * MathF.Cos(phsOut);
                    imA[k] = (k == 0 || k == half - 1) ? 0f : magOut * MathF.Sin(phsOut);

                    if (k > 0 && k < fftSize - k) {
                        reA[fftSize - k] =  reA[k];
                        imA[fftSize - k] = -imA[k];
                    }
                }

                firstFrame = false;
                if (bypass) {
                    for (int i = 0; i < fftSize; i++) {
                        float w = win[i];
                        output[posA + i] += a[posA + i] * w * w;
                        wsum  [posA + i] += w * w;
                    }
                } else {
                    fft.Inverse(reA, imA);
                    for (int i = 0; i < fftSize; i++) {
                        float w = win[i];
                        output[posA + i] += reA[i] * invN * w;
                        wsum  [posA + i] += w * w;
                    }
                }

            }
            const float WSUM_FLOOR = 0.02f;
            for (int i = 0; i < length; i++) {
                output[i] /= MathF.Max(wsum[i], WSUM_FLOOR);
            }

            return output;
        }

        public static float[] StftBlendLadder(
            float[][] banks, int[] lowerBankPerFrame, float[] ratioPerFrame,
            float[] fundamentalHzPerFrame = null) {
            if (banks == null || banks.Length == 0) return new float[0];
            int bankCount = banks.Length;
            int length = banks.Where(x => x != null).Select(x => x.Length).DefaultIfEmpty().Max();
            if (length == 0) return new float[0];
            if (bankCount == 1) return banks[0] == null ? new float[length] : (float[])banks[0].Clone();

            const int frameSize = 2048;
            const int hopSize = 512;
            const int half = frameSize / 2 + 1;
            float invN = NWAVES_INVERSE_NORMALIZES ? 1f : 1f / frameSize;
            var fft = new Fft(frameSize);
            var window = MakeHann(frameSize);
            var output = new float[length];
            var windowSum = new float[length];
            var carrierRe = new float[frameSize];
            var carrierIm = new float[frameSize];
            var lowerRe = new float[frameSize];
            var lowerIm = new float[frameSize];
            var upperRe = new float[frameSize];
            var upperIm = new float[frameSize];
            var carrierMag = new float[half];
            var lowerMag = new float[half];
            var upperMag = new float[half];
            var carrierEnv = new float[half];
            var lowerEnv = new float[half];
            var upperEnv = new float[half];
            var scratch = new float[half];

            var bankRms = banks.Select(ActiveRms).ToArray();
            var voicedRms = bankRms.Where(x => x > EPS).OrderBy(x => x).ToArray();
            float referenceRms = voicedRms.Length > 0 ? voicedRms[voicedRms.Length / 2] : 0f;
            var bankGain = bankRms.Select(x => referenceRms > EPS && x > EPS
                ? Math.Clamp(referenceRms / x, 0.5f, 2f) : 1f).ToArray();

            int carrierBank = 0;
            if (lowerBankPerFrame != null && lowerBankPerFrame.Length > 0) {
                var positions = new float[lowerBankPerFrame.Length];
                for (int i = 0; i < positions.Length; i++) {
                    int lower = Math.Clamp(lowerBankPerFrame[i], 0, bankCount - 1);
                    float ratio = ratioPerFrame != null && i < ratioPerFrame.Length
                        ? Math.Clamp(ratioPerFrame[i], 0f, 1f) : 0f;
                    positions[i] = lower + ratio;
                }
                Array.Sort(positions);
                carrierBank = Math.Clamp((int)MathF.Round(positions[positions.Length / 2]), 0, bankCount - 1);
            }

            // External resamplers can place the same phoneme a few milliseconds apart
            // when rendered through different pitch subbanks. Worldline is close enough
            // to hide this, while tips exposes it as a click when the target envelope is
            // copied at the carrier's uncorrected time. Align each bank's broad energy
            // envelope once before the STFT pass; phase still comes exclusively from the
            // carrier, so this cannot create a second pitch.
            var bankOffsets = new int[bankCount];
            for (int bank = 0; bank < bankCount; bank++) {
                if (bank != carrierBank) {
                    bankOffsets[bank] = EstimateEnvelopeOffset(banks[carrierBank], banks[bank]);
                }
            }

            int totalFrames = length <= frameSize ? 1 : (length - frameSize + hopSize - 1) / hopSize + 1;
            float smoothedGain = 0f;
            float previousTargetRms = 0f;
            for (int f = 0; f < totalFrames; f++) {
                int position = f * hopSize;
                int lower = Math.Clamp(lowerBankPerFrame != null && lowerBankPerFrame.Length > 0
                    ? lowerBankPerFrame[Math.Min(f, lowerBankPerFrame.Length - 1)] : 0, 0, bankCount - 1);
                int upper = Math.Min(lower + 1, bankCount - 1);
                float ratio = ratioPerFrame != null && ratioPerFrame.Length > 0
                    ? Math.Clamp(ratioPerFrame[Math.Min(f, ratioPerFrame.Length - 1)], 0f, 1f) : 0f;
                if (upper == lower) ratio = 0f;
                float fundamental = fundamentalHzPerFrame != null && fundamentalHzPerFrame.Length > 0
                    ? fundamentalHzPerFrame[Math.Min(f, fundamentalHzPerFrame.Length - 1)] : 250f;
                if (!float.IsFinite(fundamental) || fundamental <= 0f) fundamental = 250f;

                FillAnalysisFrame(banks[carrierBank], position, window, carrierRe, carrierIm);
                FillAnalysisFrame(banks[lower], position + bankOffsets[lower], window, lowerRe, lowerIm);
                FillAnalysisFrame(banks[upper], position + bankOffsets[upper], window, upperRe, upperIm);
                fft.Direct(carrierRe, carrierIm);
                fft.Direct(lowerRe, lowerIm);
                fft.Direct(upperRe, upperIm);
                FillMagnitude(carrierRe, carrierIm, bankGain[carrierBank], carrierMag);
                FillMagnitude(lowerRe, lowerIm, bankGain[lower], lowerMag);
                FillMagnitude(upperRe, upperIm, bankGain[upper], upperMag);
                int radius = Math.Clamp((int)Math.Round(0.5f * fundamental / (SAMPLE_RATE / (float)frameSize)), 2, 16);
                SmoothLogMagnitude(carrierMag, radius, carrierEnv, scratch);
                SmoothLogMagnitude(lowerMag, radius, lowerEnv, scratch);
                SmoothLogMagnitude(upperMag, radius, upperEnv, scratch);
                for (int k = 0; k < half; k++) {
                    float target = Lerp(lowerEnv[k], upperEnv[k], ratio);
                    float transfer = Math.Clamp(MathF.Exp(target - carrierEnv[k]), 0.125f, 8f);
                    float magnitude = carrierMag[k] * transfer;
                    float phase = MathF.Atan2(carrierIm[k], carrierRe[k]);
                    carrierRe[k] = magnitude * MathF.Cos(phase);
                    carrierIm[k] = k == 0 || k == half - 1 ? 0f : magnitude * MathF.Sin(phase);
                    if (k > 0 && k < frameSize - k) {
                        carrierRe[frameSize - k] = carrierRe[k];
                        carrierIm[frameSize - k] = -carrierIm[k];
                    }
                }
                fft.Inverse(carrierRe, carrierIm);
                float targetRms = Lerp(WindowedRms(banks[lower], position + bankOffsets[lower], window, bankGain[lower]),
                    WindowedRms(banks[upper], position + bankOffsets[upper], window, bankGain[upper]), ratio);
                float synthesizedRms = InverseFrameRms(carrierRe, invN);
                float frameGain = synthesizedRms > EPS ? Math.Clamp(targetRms / synthesizedRms, 0.5f, 2f) : 1f;
                if (f > 0 && smoothedGain > EPS && targetRms > EPS && previousTargetRms > EPS
                    && targetRms / previousTargetRms < 4f) {
                    frameGain = smoothedGain + (frameGain - smoothedGain) * 0.35f;
                }
                smoothedGain = frameGain;
                previousTargetRms = targetRms;
                for (int i = 0; i < frameSize; i++) {
                    int sample = position + i;
                    if (sample >= length) break;
                    float weight = window[i];
                    output[sample] += carrierRe[i] * invN * frameGain * weight;
                    windowSum[sample] += weight * weight;
                }
            }
            for (int i = 0; i < length; i++) output[i] /= MathF.Max(windowSum[i], 0.02f);
            return output;
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        static void FillAnalysisFrame(float[] samples, int position, float[] window, float[] real, float[] imaginary) {
            for (int i = 0; i < real.Length; i++) {
                int index = position + i;
                real[i] = samples != null && index >= 0 && index < samples.Length ? samples[index] * window[i] : 0f;
                imaginary[i] = 0f;
            }
        }

        static void FillMagnitude(float[] real, float[] imaginary, float gain, float[] magnitude) {
            for (int i = 0; i < magnitude.Length; i++) magnitude[i] = MathF.Sqrt(real[i] * real[i] + imaginary[i] * imaginary[i]) * gain;
        }

        static void SmoothLogMagnitude(float[] magnitude, int radius, float[] envelope, float[] scratch) {
            for (int i = 0; i < magnitude.Length; i++) scratch[i] = MathF.Log(magnitude[i] + EPS);
            BoxSmooth(scratch, radius, envelope);
            BoxSmooth(envelope, radius, scratch);
            Array.Copy(scratch, envelope, envelope.Length);
        }

        static void BoxSmooth(float[] input, int radius, float[] output) {
            double sum = 0;
            int start = 0, end = -1;
            for (int i = 0; i < input.Length; i++) {
                int wantedStart = Math.Max(0, i - radius), wantedEnd = Math.Min(input.Length - 1, i + radius);
                while (end < wantedEnd) sum += input[++end];
                while (start < wantedStart) sum -= input[start++];
                output[i] = (float)(sum / (end - start + 1));
            }
        }

        static float ActiveRms(float[] samples) {
            if (samples == null || samples.Length == 0) return 0f;
            double sum = 0; long count = 0;
            foreach (float sample in samples) {
                if (MathF.Abs(sample) <= 1e-4f) continue;
                sum += (double)sample * sample; count++;
            }
            return count > 0 ? (float)Math.Sqrt(sum / count) : 0f;
        }

        static float WindowedRms(float[] samples, int position, float[] window, float gain) {
            if (samples == null || samples.Length == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < window.Length; i++) {
                int index = position + i;
                float sample = index >= 0 && index < samples.Length ? samples[index] * gain * window[i] : 0f;
                sum += (double)sample * sample;
            }
            return (float)Math.Sqrt(sum / window.Length);
        }

        static int EstimateEnvelopeOffset(float[] carrier, float[] target) {
            if (carrier == null || target == null || carrier.Length < 512 || target.Length < 512) return 0;
            const int envelopeWindow = 256;
            const int envelopeHop = 128;
            const int maxOffset = SAMPLE_RATE / 1000 * 15;
            int frameCount = Math.Min(carrier.Length, target.Length) / envelopeHop - 1;
            if (frameCount < 8) return 0;
            var carrierEnvelope = new float[frameCount];
            var targetEnvelope = new float[frameCount];
            for (int frame = 0; frame < frameCount; frame++) {
                int position = frame * envelopeHop;
                carrierEnvelope[frame] = EnvelopeRms(carrier, position, envelopeWindow);
                targetEnvelope[frame] = EnvelopeRms(target, position, envelopeWindow);
            }
            for (int frame = 1; frame < frameCount; frame++) {
                carrierEnvelope[frame - 1] = carrierEnvelope[frame] - carrierEnvelope[frame - 1];
                targetEnvelope[frame - 1] = targetEnvelope[frame] - targetEnvelope[frame - 1];
            }
            carrierEnvelope[frameCount - 1] = 0f;
            targetEnvelope[frameCount - 1] = 0f;
            if (carrierEnvelope.Select(value => value * value).Sum() <= EPS
                || targetEnvelope.Select(value => value * value).Sum() <= EPS) {
                return 0;
            }
            int maxFrameOffset = Math.Min(maxOffset / envelopeHop, frameCount / 3);
            double bestScore = double.NegativeInfinity;
            int bestFrameOffset = 0;
            for (int frameOffset = -maxFrameOffset; frameOffset <= maxFrameOffset; frameOffset++) {
                int start = Math.Max(0, -frameOffset);
                int end = Math.Min(frameCount, frameCount - frameOffset);
                if (end - start < 8) continue;
                double dot = 0, carrierEnergy = 0, targetEnergy = 0;
                for (int frame = start; frame < end; frame++) {
                    float a = carrierEnvelope[frame];
                    float b = targetEnvelope[frame + frameOffset];
                    dot += a * b;
                    carrierEnergy += a * a;
                    targetEnergy += b * b;
                }
                double score = dot / Math.Sqrt(Math.Max(carrierEnergy * targetEnergy, EPS));
                if (score > bestScore) {
                    bestScore = score;
                    bestFrameOffset = frameOffset;
                }
            }
            return bestFrameOffset * envelopeHop;
        }

        static float EnvelopeRms(float[] samples, int position, int length) {
            double sum = 0;
            int count = 0;
            for (int i = 0; i < length; i++) {
                int index = position + i;
                if (index < 0 || index >= samples.Length) continue;
                sum += (double)samples[index] * samples[index];
                count++;
            }
            return count > 0 ? (float)Math.Sqrt(sum / count) : 0f;
        }

        static float InverseFrameRms(float[] frame, float inverseScale) {
            double sum = 0;
            for (int i = 0; i < frame.Length; i++) { float sample = frame[i] * inverseScale; sum += (double)sample * sample; }
            return (float)Math.Sqrt(sum / frame.Length);
        }

        static float WrapPi(float x) {
            x = (x + MathF.PI) % (2f * MathF.PI);
            if (x < 0f) x += 2f * MathF.PI;
            return x - MathF.PI;
        }

        static float[] MakeHann(int size) {
            var w = new float[size];
            for (int i = 0; i < size; i++) {
                w[i] = (float)(0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / size)));
            }
            return w;
        }

        static double PiecewiseLerp(double[] xs, double[] ys, double x) {
            for (int i = 0; i < xs.Length - 1; i++) {
                double lo = xs[i], hi = xs[i + 1];
                if (x >= lo && x <= hi) {
                    double span = hi - lo;
                    if (span < 1e-9) return ys[i];
                    return ys[i] + (x - lo) / span * (ys[i + 1] - ys[i]);
                }
            }
            return ys[ys.Length - 1];
        }
    }
}
