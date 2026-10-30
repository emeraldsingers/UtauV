using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Hash.xxHash;
using NAudio.Wave;
using OpenUtau.Classic.Hifisampler;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkRenderer : IRenderer {
        public const double HeadMs = 160;
        public const double TailMs = 120;
        const int OutputSampleRate = 44100;
        const int CurveInterval = 5;
        const int CacheVersion = 5;

        static readonly HashSet<string> supportedExp = new HashSet<string>() {
            Format.Ustx.DYN,
            Format.Ustx.PITD,
            Format.Ustx.CLR,
            Format.Ustx.GENC,
        };

        static readonly object lockObj = new object();

        public USingerType SingerType => USingerType.Coeiroink;

        public bool SupportsRenderPitch => false;

        public bool SupportsExpression(UExpressionDescriptor descriptor) {
            return supportedExp.Contains(descriptor.abbr);
        }

        public (double HeadMs, double TailMs) PhrasePadding(USinger singer, IEnumerable<UPhoneme> phonemes) {
            return (HeadMs, TailMs);
        }

        public RenderResult Layout(RenderPhrase phrase) {
            return new RenderResult() {
                leadingMs = HeadMs,
                positionMs = phrase.positionMs,
                estimatedLengthMs = HeadMs + phrase.durationMs + TailMs,
            };
        }

        public Task<RenderResult> Render(RenderPhrase phrase, Progress progress, int trackNo, CancellationTokenSource cancellation, bool isPreRender, RenderPhraseEvents? renderEvents = null) {
            return Task.Run(() => {
                lock (lockObj) {
                    if (cancellation.IsCancellationRequested) {
                        return new RenderResult();
                    }
                    string progressInfo = $"Track {trackNo + 1}: {this} \"{string.Join(" ", phrase.phones.Select(p => p.phoneme))}\"";
                    progress.Complete(0, progressInfo);
                    var wavPath = Path.Join(PathManager.Inst.CachePath, $"coe-{phrase.hash:x16}-{CacheVersion}.wav");
                    phrase.AddCacheFile(wavPath);
                    var result = Layout(phrase);
                    if (!File.Exists(wavPath)) {
                        var samples = Synthesize(phrase, cancellation);
                        if (samples == null || cancellation.IsCancellationRequested) {
                            return new RenderResult();
                        }
                        Wave.WriteMono16Wav(wavPath, samples);
                    }
                    progress.Complete(phrase.phones.Length, progressInfo);
                    using (var waveStream = new WaveFileReader(wavPath)) {
                        result.samples = Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1, 0));
                    }
                    Renderers.ApplyDynamics(phrase, result);
                    return result;
                }
            });
        }

        float[]? Synthesize(RenderPhrase phrase, CancellationTokenSource cancellation) {
            var singer = phrase.singer as CoeiroinkSinger;
            if (singer == null || phrase.phones.Length == 0) {
                return null;
            }
            singer.EnsureLoaded();
            var style = singer.GetStyle(phrase.phones[0].suffix);
            if (style == null) {
                throw new MessageCustomizableException("The COEIROINK voicebank has no styles.", "metas.json has no styles.", new InvalidDataException());
            }
            CoeiroinkModel model;
            try {
                model = CoeiroinkModel.Get(style);
            } catch (FileNotFoundException e) {
                throw new MessageCustomizableException("COEIROINK ONNX models were not found. Export them with the exporter tool.", e.Message, e);
            }

            var syllables = BuildSyllables(phrase);
            if (syllables.Count == 0) {
                return new float[(int)Math.Ceiling((HeadMs + phrase.durationMs + TailMs) * OutputSampleRate / 1000)];
            }
            var plan = CoeiroinkTimeline.BuildPlan(syllables);
            if (!model.TryEncode(plan.Tokens, out var ids)) {
                var missing = plan.Tokens.Where(t => !model.TokenIds.ContainsKey(t)).Distinct();
                throw new MessageCustomizableException(
                    $"The COEIROINK model does not know the tokens: {string.Join(" ", missing)}",
                    $"Unsupported tokens: {string.Join(" ", missing)}",
                    new InvalidDataException());
            }
            int seed = Seed(ids);
            var predicted = model.PredictDurations(ids, seed);
            if (cancellation.IsCancellationRequested) {
                return null;
            }
            double startMs = phrase.positionMs - HeadMs;
            var frames = CoeiroinkTimeline.Allocate(plan, syllables, predicted, startMs, model.Config.FrameMs);
            var wave = model.Synthesize(ids, frames, seed);
            if (cancellation.IsCancellationRequested) {
                return null;
            }
            return Vocode(phrase, wave, startMs, model.Config.FrameMs, VoicedMask(plan, frames));
        }

        static List<CoeiroinkSyllable> BuildSyllables(RenderPhrase phrase) {
            var syllables = new List<CoeiroinkSyllable>();
            var phones = phrase.phones;
            string? pendingConsonant = null;
            double pendingConsonantMs = 0;
            bool pendingPause = false;
            for (int p = 0; p < phones.Length; p++) {
                var phone = phones[p];
                if (CoeiroinkKana.IsRest(phone.phoneme)) {
                    pendingPause = syllables.Count > 0;
                    pendingConsonant = null;
                    continue;
                }
                if (CoeiroinkKana.IsConsonantToken(phone.phoneme)) {
                    pendingConsonant = phone.phoneme.Trim();
                    pendingConsonantMs = phone.endMs - phone.positionMs;
                    continue;
                }
                if (!CoeiroinkKana.TryParse(phone.phoneme, out var moras)) {
                    Log.Warning($"Skipped an unrecognized COEIROINK phoneme \"{phone.phoneme}\"");
                    continue;
                }
                if (moras.Length == 1 && moras[0].Consonant != null && !moras[0].IsGeminate
                    && p + 1 < phones.Length && phones[p + 1].noteIndex == phone.noteIndex
                    && CoeiroinkKana.TryParse(phones[p + 1].phoneme, out var vowelMoras)
                    && vowelMoras.Length == 1 && vowelMoras[0].Consonant == null && vowelMoras[0].Vowel == moras[0].Vowel) {
                    var vowel = phones[p + 1];
                    syllables.Add(new CoeiroinkSyllable {
                        PauseBefore = pendingPause,
                        ConsonantMs = vowel.positionMs - phone.positionMs,
                        Mora = moras[0],
                        StartMs = vowel.positionMs,
                        EndMs = vowel.endMs,
                        Tone = vowel.tone,
                    });
                    p++;
                    pendingConsonant = null;
                    pendingPause = false;
                    continue;
                }
                double step = (phone.endMs - phone.positionMs) / moras.Length;
                for (int i = 0; i < moras.Length; i++) {
                    var mora = moras[i];
                    double? consonantMs = null;
                    if (i == 0 && pendingConsonant != null && mora.Consonant == null && !mora.IsGeminate) {
                        mora = new CoeiroinkMora(pendingConsonant, mora.Vowel);
                        consonantMs = pendingConsonantMs;
                    }
                    syllables.Add(new CoeiroinkSyllable {
                        PauseBefore = pendingPause && i == 0,
                        ConsonantMs = consonantMs,
                        Mora = mora,
                        StartMs = phone.positionMs + step * i,
                        EndMs = phone.positionMs + step * (i + 1),
                        Tone = phone.tone,
                    });
                }
                pendingConsonant = null;
                pendingPause = false;
            }
            return syllables;
        }

        static int Seed(long[] ids) {
            var bytes = new byte[ids.Length * sizeof(long)];
            Buffer.BlockCopy(ids, 0, bytes, 0, bytes.Length);
            return (int)(XXH64.DigestOf(bytes) & 0x7fffffff);
        }

        static readonly HashSet<string> voicedConsonants = new HashSet<string> {
            "m", "n", "ny", "my", "r", "ry", "y", "w", "g", "gy", "z", "j", "d", "dy", "b", "by", "v",
        };

        static bool[] VoicedMask(CoeiroinkTokenPlan plan, int[] frames) {
            var mask = new bool[frames.Sum()];
            int cursor = 0;
            for (int i = 0; i < frames.Length; i++) {
                bool voiced = plan.Roles[i] switch {
                    CoeiroinkTokenRole.Vowel => plan.Tokens[i] != CoeiroinkKana.Geminate,
                    CoeiroinkTokenRole.Consonant => voicedConsonants.Contains(plan.Tokens[i]),
                    _ => false,
                };
                Array.Fill(mask, voiced, cursor, frames[i]);
                cursor += frames[i];
            }
            return mask;
        }

        float[] Vocode(RenderPhrase phrase, float[] wave, double startMs, double frameMs, bool[] voiced) {
            var vocoder = HifiVocoder.Instance;
            var config = vocoder.Config;
            if (config.sample_rate != OutputSampleRate || Math.Abs(config.hop_size - frameMs * OutputSampleRate / 1000) > 0.5) {
                throw new MessageCustomizableException(
                    "The vocoder must be 44.1 kHz with hop size 512.",
                    $"Vocoder {config.name} is {config.sample_rate} Hz with hop {config.hop_size}.",
                    new InvalidDataException());
            }
            int frames = HifiMelSpectrogram.FrameCount(wave.Length, config.hop_size);
            var gender = SampleCurve(phrase, phrase.gender, 0, startMs + frameMs / 2, frameMs, frames, x => -0.12 * Math.Clamp(x, -100, 100));
            var analyzer = new HifiMelSpectrogram(config.sample_rate, config.fft_size, config.win_size, config.hop_size, config.mel_fmin, config.mel_fmax, config.num_mel_bins);
            var mel = analyzer.Compute(wave, gender);
            HifiMelSpectrogram.LogCompress(mel);
            var f0 = SampleCurve(phrase, phrase.pitches, 0, startMs + frameMs / 2, frameMs, frames, x => MusicMath.ToneToFreq(x * 0.01));
            for (int i = 0; i < frames; i++) {
                if (i >= voiced.Length || !voiced[i]) {
                    f0[i] = 0;
                }
            }
            var synthesized = vocoder.Synthesize(mel, f0);
            var result = new float[wave.Length];
            Array.Copy(synthesized, result, Math.Min(synthesized.Length, result.Length));
            float peak = result.Length == 0 ? 0 : result.Max(Math.Abs);
            if (peak > 0.99f) {
                float scale = 0.99f / peak;
                for (int i = 0; i < result.Length; i++) {
                    result[i] *= scale;
                }
            }
            return result;
        }

        static double[] SampleCurve(RenderPhrase phrase, float[] curve, double defaultValue, double startMs, double frameMs, int length, Func<double, double> convert) {
            var result = new double[length];
            if (curve == null || curve.Length == 0) {
                Array.Fill(result, defaultValue);
                return result;
            }
            int origin = phrase.position - phrase.leading;
            for (int i = 0; i < length; i++) {
                int ticks = phrase.timeAxis.MsPosToTickPos(startMs + i * frameMs) - origin;
                int index = Math.Clamp(ticks / CurveInterval, 0, curve.Length - 1);
                result[i] = convert(curve[index]);
            }
            return result;
        }

        public UExpressionDescriptor[] GetSuggestedExpressions(USinger singer, URenderSettings renderSettings) {
            return Array.Empty<UExpressionDescriptor>();
        }

        public RenderPitchResult LoadRenderedPitch(RenderPhrase phrase) {
            return null;
        }

        public override string ToString() => Renderers.COEIROINK;
    }
}
