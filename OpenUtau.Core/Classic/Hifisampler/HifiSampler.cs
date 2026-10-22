// Ported from hifisampler's backend/resampler.py (https://github.com/openhachimi/hifisampler),
// licensed under the Apache License 2.0; see LICENSE in this folder.
// Modified for OpenUtau: translated to C#, reading OpenUtau's resampler arguments.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Analysis;
using OpenUtau.Core.DiffSinger;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.Util;

namespace OpenUtau.Classic.Hifisampler {
    /// <summary>The consonant reaches the cutoff, so there is nothing to stretch or loop.</summary>
    public class ConsonantExceedsCutoffError : SynthRequestError { }

    /// <summary>
    /// hifisampler (openhachimi/hifisampler backend/resampler.py) for one note: the source's
    /// log mel is stretched to the requested length and voiced by pc-nsf-hifigan at the note's
    /// pitch. Kept in the Python's order so the two can be read side by side.
    /// </summary>
    public static class HifiSampler {
        public static float[] Resample(ResamplerItem item) {
            // item.modulation is unused, as in hifisampler.
            return Resample(item.inputFile, item.tone, item.velocity, item.flags, item.offset,
                item.consonant, item.cutoff, item.durRequired, item.volume, item.tempo, item.pitches,
                new HifiSamplerConfig());
        }

        /// <summary>The UTAU resampler arguments; pitches are cents relative to tone, every 5 ticks.</summary>
        public static float[] Resample(string inputFile, int tone, int velocity,
                IEnumerable<Tuple<string, int?, string>> flagList, double offset, double consonant,
                double cutoff, double length, int volume, double tempo, int[] pitches, HifiSamplerConfig config) {
            var vocoder = HifiVocoder.Instance;
            config.Validate(vocoder.Config);

            float[] wave;
            using (var waveStream = Wave.OpenFile(inputFile)) {
                // GetSamples resamples to 44.1 kHz, as WorldlineResampler reads its input.
                wave = Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1, 0));
            }
            if (wave.Length == 0) {
                throw new Exception($"Empty samples in {inputFile}.");
            }

            var flags = new HifiFlags(flagList);
            var features = HifiFeatures.Generate(wave,
                flags.Get("Mb") ?? 0, flags.Get("Mv") ?? 100, flags.Get("Mt") ?? 0, flags.Get("g") ?? 0,
                config, x => Hnsep.Instance.Harmonic(x));

            var timing = new HifiNoteTiming(config, features.Mel.Length, velocity, offset,
                consonant, cutoff, length, flags.Has("He"));
            var melRender = timing.RenderMel(features.Mel);

            var t = new double[melRender.Length];
            for (int i = 0; i < t.Length; i++) {
                t[i] = i * timing.Thop;
            }
            var pitchRender = HifiNotePitch.Render(pitches, tone, flags.Get("t") ?? 0,
                tempo, timing.NewStart, t);
            var f0 = pitchRender.Select(p => 440 * Math.Pow(2, (p - 69) / 12)).ToArray();

            var wavCon = vocoder.Synthesize(melRender, f0);
            int cutStart = Math.Min(wavCon.Length, (int)(timing.NewStart * config.SampleRate));
            int cutEnd = Math.Clamp((int)(timing.NewEnd * config.SampleRate), cutStart, wavCon.Length);
            var render = wavCon[cutStart..cutEnd];

            return HifiPostProcess.Apply(render, features.Scale, flags, config, pitchRender, t,
                timing.NewStart, timing.NewEnd, volume);
        }
    }

    /// <summary>A note's flags, the first occurrence of each, as hifisampler parses them.</summary>
    internal sealed class HifiFlags {
        readonly Dictionary<string, int?> values = new Dictionary<string, int?>();

        public HifiFlags(IEnumerable<Tuple<string, int?, string>> flags) {
            foreach (var flag in flags) {
                values.TryAdd(flag.Item1, flag.Item2);
            }
        }

        public bool Has(string name) => values.ContainsKey(name);

        public int? Get(string name) => values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// The time map of Resampler.resample: velocity-scaled consonant, stretched (or looped)
    /// vowel, mel frames cut to the note plus <see cref="HifiSamplerConfig.Fill"/> on each side.
    /// Times in seconds; the "//" floor divisions of the Python are Math.Floor.
    /// </summary>
    internal sealed class HifiNoteTiming {
        public readonly double ThopOrigin;
        public readonly double Thop;
        public readonly double Vel;
        public readonly double Start;
        public readonly double End;
        public readonly double Con;
        public readonly double LengthReq;
        public readonly bool Loop;
        public readonly int ConFrame;
        public readonly int EndFrame;
        public readonly double PadLoopSize;
        public double StretchLength { get; private set; }
        public double TotalTime { get; private set; }
        public double ScalingRatio { get; private set; }
        public double StretchedNFrames { get; private set; }
        public double CutLeftMelFrames { get; private set; }
        public double CutRightMelFrames { get; private set; }
        public double NewStart { get; private set; }
        public double NewEnd { get; private set; }

        readonly int fill;
        double[] tAreaOrigin;

        public HifiNoteTiming(HifiSamplerConfig config, int melFrames, int velocity, double offset,
                double consonant, double cutoff, double length, bool loop) {
            ThopOrigin = config.OriginHopSize / (double)config.SampleRate;
            Thop = config.HopSize / (double)config.SampleRate;
            fill = config.Fill;
            Loop = loop;
            tAreaOrigin = FrameTimes(melFrames);
            TotalTime = tAreaOrigin[^1] + ThopOrigin / 2;

            Vel = Math.Pow(2, 1 - velocity / 100.0);
            Start = offset / 1000;
            End = cutoff < 0 ? Start - cutoff / 1000 : TotalTime - cutoff / 1000;
            Con = Start + consonant / 1000;
            if (End <= Start) {
                throw new CutOffBeforeOffsetError();
            }
            if (End <= Con) {
                throw new ConsonantExceedsCutoffError();
            }
            // hifisampler reads the length argument as an int.
            LengthReq = (int)length / 1000.0;
            StretchLength = End - Con;

            if (loop) {
                ConFrame = (int)Math.Floor((Con + ThopOrigin / 2) / ThopOrigin);
                EndFrame = (int)Math.Floor((End + ThopOrigin / 2) / ThopOrigin);
                PadLoopSize = Math.Floor(LengthReq / ThopOrigin) + 1;
                int loopFrames = Math.Min(EndFrame, melFrames) - ConFrame;
                if (loopFrames <= 0) {
                    throw new ConsonantExceedsCutoffError();
                }
                StretchLength = PadLoopSize * ThopOrigin;
                tAreaOrigin = FrameTimes(ConFrame + loopFrames + (int)PadLoopSize);
                TotalTime = tAreaOrigin[^1] + ThopOrigin / 2;
            }

            ScalingRatio = StretchLength < LengthReq ? LengthReq / StretchLength : 1;
            StretchedNFrames = Math.Floor((Con * Vel + (TotalTime - Con) * ScalingRatio) / Thop) + 1;

            double startLeftMelFrames = Math.Floor((Start * Vel + Thop / 2) / Thop);
            CutLeftMelFrames = startLeftMelFrames > fill ? startLeftMelFrames - fill : 0;
            double endRightMelFrames = StretchedNFrames - Math.Floor((LengthReq + Con * Vel + Thop / 2) / Thop);
            CutRightMelFrames = endRightMelFrames > fill ? endRightMelFrames - fill : 0;

            NewStart = Start * Vel - CutLeftMelFrames * Thop;
            NewEnd = (LengthReq + Con * Vel) - CutLeftMelFrames * Thop;
        }

        double[] FrameTimes(int frames) {
            var t = new double[frames];
            for (int i = 0; i < frames; i++) {
                t[i] = i * ThopOrigin + ThopOrigin / 2;
            }
            return t;
        }

        /// <summary>The analysis times (seconds) of the vocoder frames: stretch(), clipped to the source.</summary>
        public double[] SourceTimes() {
            int first = (int)CutLeftMelFrames;
            int last = (int)(StretchedNFrames - CutRightMelFrames);
            var times = new double[Math.Max(0, last - first)];
            for (int i = 0; i < times.Length; i++) {
                double t = (first + i) * Thop + Thop / 2;
                double s = t < Vel * Con ? t / Vel : Con + (t - Vel * Con) / ScalingRatio;
                times[i] = Math.Clamp(s, 0, tAreaOrigin[^1]);
            }
            return times;
        }

        /// <summary>The source mel (looped when <see cref="Loop"/>), interpolated at <see cref="SourceTimes"/>.</summary>
        public float[][] RenderMel(float[][] mel) {
            if (Loop) {
                mel = LoopMel(mel);
            }
            var times = SourceTimes();
            int bins = mel[0].Length;
            var result = new float[times.Length][];
            for (int j = 0; j < times.Length; j++) {
                double v = times[j];
                int i = HifiInterp.Interval(tAreaOrigin, v);
                double x0 = tAreaOrigin[i];
                double x1 = tAreaOrigin[i + 1];
                var lo = mel[i];
                var hi = mel[i + 1];
                var row = new float[bins];
                for (int b = 0; b < bins; b++) {
                    double slope = (hi[b] - (double)lo[b]) / (x1 - x0);
                    row[b] = (float)(slope * (v - x0) + lo[b]);
                }
                result[j] = row;
            }
            return result;
        }

        /// <summary>Frames before the consonant end, then the consonant-to-cutoff frames reflect-padded by PadLoopSize.</summary>
        float[][] LoopMel(float[][] mel) {
            int loopFrames = Math.Min(EndFrame, mel.Length) - ConFrame;
            int total = ConFrame + loopFrames + (int)PadLoopSize;
            var looped = new float[total][];
            for (int i = 0; i < ConFrame; i++) {
                looped[i] = mel[i];
            }
            for (int i = 0; i < loopFrames + (int)PadLoopSize; i++) {
                looped[ConFrame + i] = mel[ConFrame + HifiArray.ReflectIndex(i, loopFrames)];
            }
            return looped;
        }
    }

    /// <summary>
    /// The note's pitch curve: the pitchbend (cents per 5 ticks, from the note start minus the
    /// stretched preutterance) with the zero point hifisampler's decoder appends, plus the
    /// tone and the t flag, Akima-interpolated at the vocoder frames.
    /// </summary>
    internal static class HifiNotePitch {
        public static double[] Render(int[] pitches, int tone, int tFlag, double tempo, double newStart, double[] t) {
            var pitch = new double[pitches.Length + 1];
            for (int i = 0; i < pitches.Length; i++) {
                pitch[i] = pitches[i] / 100.0 + tone;
            }
            pitch[^1] = tone;
            if (tFlag != 0) {
                for (int i = 0; i < pitch.Length; i++) {
                    pitch[i] += tFlag / 100.0;
                }
            }
            var tPitch = new double[pitch.Length];
            for (int i = 0; i < tPitch.Length; i++) {
                tPitch[i] = 60.0 * i / (tempo * 96) + newStart;
            }
            var x = t.Select(v => Math.Clamp(v, newStart, Math.Max(newStart, tPitch[^1]))).ToArray();
            return HifiInterp.Akima(tPitch, pitch, x);
        }
    }

    /// <summary>The pc-nsf-hifigan package's vocoder: log mel [1, T, bins] and f0 [1, T] Hz to a waveform.</summary>
    internal sealed class HifiVocoder {
        public const string PackageId = "pc_nsf_hifigan_44.1k_hop512_128bin_2025.02";

        static readonly object loadLock = new object();
        static HifiVocoder? instance;

        public readonly DsVocoderConfig Config;
        readonly InferenceSession session;
        readonly bool isCpuRunner;
        readonly object runLock = new object();

        HifiVocoder(DsVocoderConfig config, string modelPath) {
            Config = config;
            session = Onnx.getInferenceSession(modelPath);
            isCpuRunner = Onnx.IsCpuRunner();
        }

        /// <summary>The installed package's vocoder, loaded once. Throws when the package is missing.</summary>
        public static HifiVocoder Instance {
            get {
                lock (loadLock) {
                    return instance ??= Load();
                }
            }
        }

        static HifiVocoder Load() {
            string? dir = PackageManager.Inst.GetInstalledPath(PackageId);
            string? configPath = dir == null ? null : Path.Combine(dir, "vocoder.yaml");
            if (configPath == null || !File.Exists(configPath)) {
                throw new MessageCustomizableException(
                    $"Error loading package \"{PackageId}\"",
                    "<translate:packages.errors.missing>",
                    new Exception($"Error loading package \"{PackageId}\""),
                    true,
                    new string[] { PackageId });
            }
            var config = Yaml.DefaultDeserializer.Deserialize<DsVocoderConfig>(
                File.ReadAllText(configPath, System.Text.Encoding.UTF8));
            return new HifiVocoder(config, Path.Combine(dir!, config.model));
        }

        public float[] Synthesize(float[][] mel, double[] f0) {
            int frames = mel.Length;
            if (frames == 0) {
                return new float[0];
            }
            int bins = mel[0].Length;
            var melTensor = new DenseTensor<float>(new[] { 1, frames, bins });
            var melSpan = melTensor.Buffer.Span;
            for (int i = 0; i < frames; i++) {
                mel[i].AsSpan().CopyTo(melSpan.Slice(i * bins, bins));
            }
            var f0Tensor = new DenseTensor<float>(f0.Select(v => (float)v).ToArray(), new[] { 1, frames });
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("mel", melTensor),
                NamedOnnxValue.CreateFromTensor("f0", f0Tensor),
            };
            if (isCpuRunner) {
                return Run(inputs);
            }
            lock (runLock) {
                return Run(inputs);
            }
        }

        float[] Run(List<NamedOnnxValue> inputs) {
            using var results = session.Run(inputs);
            return results.First().AsTensor<float>().ToArray();
        }
    }

    /// <summary>
    /// After the vocoder: A (pitch-slope amplitude modulation), undo the analysis scale, HG
    /// (growl), P (loudness normalization), the peak limit, and volume.
    /// </summary>
    internal static class HifiPostProcess {
        public static float[] Apply(float[] render, double scale, HifiFlags flags, HifiSamplerConfig config,
                double[] pitchRender, double[] t, double newStart, double newEnd, int volume) {
            int aFlag = flags.Get("A") ?? 0;
            if (aFlag != 0 && pitchRender.Length > 1 && t.Length > 1) {
                double a = Math.Clamp(aFlag, -100, 100);
                var derivative = HifiInterp.Gradient(pitchRender, t);
                var gain = derivative.Select(d => Math.Pow(5, 1e-4 * a * d)).ToArray();
                int n = render.Length;
                var audioTime = new double[n];
                for (int i = 0; i < n; i++) {
                    audioTime[i] = newStart + i * ((newEnd - newStart) / n);
                }
                var interpolated = HifiInterp.Linear(audioTime, t, gain);
                for (int i = 0; i < n; i++) {
                    render[i] = (float)(render[i] * interpolated[i]);
                }
            }

            for (int i = 0; i < render.Length; i++) {
                render[i] = (float)(render[i] / scale);
            }
            // Measured here, applied after growl and normalization, as hifisampler does.
            double newMax = HifiArray.MaxAbs(render);

            if (flags.Get("HG") is int hg) {
                render = HifiGrowl.Apply(render, config.SampleRate, 80.0, hg / 100.0);
            }

            if (flags.Has("P")) {
                double strength = flags.Get("P") ?? 100;
                render = HifiLoudness.Normalize(render, config.SampleRate, config.TrimSilence,
                    config.SilenceThreshold, loudness: -16.0, blockSize: 0.400, strength: strength);
            }

            if (newMax > config.PeakLimit) {
                for (int i = 0; i < render.Length; i++) {
                    render[i] = (float)(render[i] / newMax);
                }
            }

            double volumeScale = volume / 100.0;
            for (int i = 0; i < render.Length; i++) {
                render[i] = (float)(render[i] * volumeScale);
            }
            return render;
        }
    }
}
