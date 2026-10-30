using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkModel : IDisposable {
        public const float FlowNoiseScale = 0.333f;
        public const float DurationNoiseScale = 0.333f;

        static readonly ConcurrentDictionary<string, CoeiroinkModel> cache = new ConcurrentDictionary<string, CoeiroinkModel>();

        public readonly CoeiroinkModelConfig Config;
        public readonly Dictionary<string, long> TokenIds;
        readonly InferenceSession durationSession;
        readonly InferenceSession synthSession;
        readonly object runLock = new object();

        CoeiroinkModel(CoeiroinkStyle style) {
            if (!style.IsExported) {
                throw new FileNotFoundException(
                    $"ONNX models for the COEIROINK style \"{style.Name}\" were not found in {style.ModelDir}. " +
                    $"Export them with the tool from {CoeiroinkConfig.ExporterUrl}");
            }
            Config = CoeiroinkModelConfig.Load(style.ConfigPath);
            TokenIds = Config.TokenIds();
            using var scope = Onnx.EnterDmlScope();
            durationSession = Onnx.getInferenceSession(style.DurationModelPath);
            synthSession = Onnx.getInferenceSession(style.SynthModelPath);
        }

        public static CoeiroinkModel Get(CoeiroinkStyle style) {
            return cache.GetOrAdd(style.ModelDir, _ => new CoeiroinkModel(style));
        }

        public static void Evict(string modelDir) {
            if (cache.TryRemove(modelDir, out var model)) {
                model.Dispose();
            }
        }

        public bool TryEncode(IEnumerable<string> tokens, out long[] ids) {
            var list = new List<long>();
            foreach (var token in tokens) {
                if (!TokenIds.TryGetValue(token, out var id)) {
                    ids = Array.Empty<long>();
                    return false;
                }
                list.Add(id);
            }
            ids = list.ToArray();
            return true;
        }

        public float[] PredictDurations(long[] ids, int seed) {
            var random = new Random(seed);
            var noise = new DenseTensor<float>(new[] { 1, 2, ids.Length });
            for (int i = 0; i < noise.Length; i++) {
                noise.SetValue(i, (float)(NextGaussian(random) * DurationNoiseScale));
            }
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("text", new DenseTensor<long>(ids, new[] { 1, ids.Length })),
                NamedOnnxValue.CreateFromTensor("noise", noise),
            };
            lock (runLock) {
                using var scope = Onnx.EnterDmlScope();
                using var results = durationSession.Run(inputs);
                return results.First().AsTensor<float>().ToArray();
            }
        }

        public float[] Synthesize(long[] ids, int[] frames, int seed, float noiseScale = FlowNoiseScale) {
            int totalFrames = frames.Sum();
            var random = new Random(seed);
            var noise = new DenseTensor<float>(new[] { 1, Config.HiddenChannels, totalFrames });
            for (int i = 0; i < noise.Length; i++) {
                noise.SetValue(i, (float)NextGaussian(random));
            }
            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor("text", new DenseTensor<long>(ids, new[] { 1, ids.Length })),
                NamedOnnxValue.CreateFromTensor("duration", new DenseTensor<float>(frames.Select(f => (float)f).ToArray(), new[] { ids.Length })),
                NamedOnnxValue.CreateFromTensor("noise", noise),
                NamedOnnxValue.CreateFromTensor("noise_scale", new DenseTensor<float>(new[] { noiseScale }, Array.Empty<int>())),
            };
            lock (runLock) {
                using var scope = Onnx.EnterDmlScope();
                using var results = synthSession.Run(inputs);
                return results.First().AsTensor<float>().ToArray();
            }
        }

        static double NextGaussian(Random random) {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        public void Dispose() {
            durationSession.Dispose();
            synthSession.Dispose();
        }
    }
}
