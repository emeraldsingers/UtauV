using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenUtau.Core.Ustx;
using YamlDotNet.Serialization;

namespace OpenUtau.Core.Editing {

    public class AutoPitchConfig {
        [JsonPropertyName("noiseScale")]
        public float NoiseScale { get; set; } = 0.1f;

        [JsonPropertyName("use21point")]
        public bool Use21Point { get; set; } = false;
    }

    public class AutoPitchEdit : BatchEdit {
        public string Name => "AutoPitch";

        private InferenceSession _session;
        private Dictionary<string, int> _phonemeToIdx;
        private readonly string[] _shapeMap = new string[] { "io", "i", "o", "l", "li", "lo" };
        private bool _isInitialized = false;
        private float _noiseScale;
        private bool _use21Points;

        public AutoPitchEdit() {
            LoadConfig();
        }

        private void LoadConfig() {
            string configPath = Path.Combine(PathManager.Inst.DependencyPath, "autopitch", "config.json");
            string configDir = Path.GetDirectoryName(configPath);

            try {
                if (!Directory.Exists(configDir)) {
                    Directory.CreateDirectory(configDir);
                }

                if (!File.Exists(configPath)) {
                    var defaultConfig = new AutoPitchConfig();
                    _noiseScale = defaultConfig.NoiseScale;
                    _use21Points = defaultConfig.Use21Point;
                    string jsonOutput = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(configPath, jsonOutput);
                } else {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AutoPitchConfig>(json, new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true
                    });

                    _noiseScale = config?.NoiseScale ?? 0.1f;
                    _use21Points = config?.Use21Point ?? false;
                }
            } catch (Exception e) {
                Console.WriteLine($"Error loading/creating AutoPitch config: {e.Message}");
                _noiseScale = 0.1f;
                _use21Points = false;
            }
            Console.WriteLine($"AutoPitch Config Loaded: noiseScale={_noiseScale}, use21point={_use21Points}");
        }

        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            if (!_isInitialized) {
                InitializeModel();
            }
            if (!_isInitialized) {
                return;
            }

            var notes = selectedNotes.Count > 0 ? selectedNotes.ToList() : part.notes.ToList();
            if (notes.Count == 0)
                return;

            var (noteFeatures, phonemeIndices) = ExtractFeatures(notes);

            int[] dimensions = new int[] { 1, notes.Count, 7 };
            var noteFeaturesTensor = new DenseTensor<float>(noteFeatures, dimensions);
            dimensions = new int[] { 1, notes.Count };
            var phonemeIndicesTensor = new DenseTensor<long>(phonemeIndices, dimensions);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("note_features", noteFeaturesTensor),
                NamedOnnxValue.CreateFromTensor("phoneme_indices", phonemeIndicesTensor)
            };

            try {
                using (var outputs = _session.Run(inputs)) {
                    var pitchParamsTensor = outputs.First(o => o.Name == "pitch_params").AsTensor<float>();
                    var pitchParams = pitchParamsTensor.ToArray();

                    int expectedParamsPerNote = _use21Points ? 21 : 15;
                    int actualParamsPerNote = pitchParamsTensor.Dimensions[2];

                    if (actualParamsPerNote != expectedParamsPerNote) {
                        Console.Error.WriteLine($"AutoPitch configuration mismatch: Config expects {expectedParamsPerNote} parameters per note (use21point={_use21Points}), but the ONNX model output {actualParamsPerNote}. Using model output size.");
                        _use21Points = (actualParamsPerNote == 21);
                    }

                    docManager.StartUndoGroup(true);
                    ApplyPitchBends(notes, pitchParams);
                    docManager.EndUndoGroup();
                }
            } catch (Exception ex) {
                Console.Error.WriteLine($"Error during AutoPitch inference or application: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void InitializeModel() {
            if (_isInitialized) return;

            string modelPath = Path.Combine(PathManager.Inst.DependencyPath, "autopitch", "autopitch.onnx");
            string vocabPath = Path.Combine(PathManager.Inst.DependencyPath, "autopitch", "phoneme_vocab.yaml");
            string configPath = Path.Combine(PathManager.Inst.DependencyPath, "autopitch", "config.json");

            try {
                if (!File.Exists(modelPath)) {
                    throw new FileNotFoundException($"AutoPitch model not found. Please ensure 'autopitch.onnx' exists in:\n{Path.GetDirectoryName(modelPath)}\nCheck the 'use21point' setting in '{configPath}' matches your model version.");
                }
                if (!File.Exists(vocabPath)) {
                    throw new FileNotFoundException($"AutoPitch vocabulary not found. Please ensure 'phoneme_vocab.yaml' exists in:\n{Path.GetDirectoryName(vocabPath)}");
                }

                var deserializer = new DeserializerBuilder().Build();
                using (var reader = new StreamReader(vocabPath)) {
                    var yaml = deserializer.Deserialize<Dictionary<string, object>>(reader);
                    if (yaml.TryGetValue("phoneme_to_idx", out object phonemeIdxObj) && phonemeIdxObj is Dictionary<object, object> dict) {
                        _phonemeToIdx = dict.ToDictionary(
                           kv => kv.Key?.ToString() ?? string.Empty,
                           kv => int.TryParse(kv.Value?.ToString(), out int val) ? val : 0
                        );
                        if (!_phonemeToIdx.ContainsKey("<UNK>")) {
                            Console.Error.WriteLine("Warning: '<UNK>' token not found in phoneme_vocab.yaml. AutoPitch might behave unexpectedly for unknown phonemes.");
                        }
                    } else {
                        throw new FormatException("Could not parse 'phoneme_to_idx' dictionary from phoneme_vocab.yaml");
                    }
                }

                _session = new InferenceSession(modelPath);
                _isInitialized = true;
            } catch (FileNotFoundException fnfEx) {
                Console.Error.WriteLine(fnfEx.Message);
            } catch (Exception ex) {
                Console.Error.WriteLine($"Failed to initialize AutoPitch: {ex.Message}\n{ex.StackTrace}");
                _isInitialized = false;
            }
        }

        private (float[], long[]) ExtractFeatures(List<UNote> notes) {
            int seqLength = notes.Count;
            float[] noteFeatures = new float[seqLength * 7];
            long[] phonemeIndices = new long[seqLength];
            Random rand = new Random();

            float noise = _noiseScale;

            for (int i = 0; i < seqLength; i++) {
                var note = notes[i];
                int baseIndex = i * 7;

                float duration = (float)note.duration / 480f;
                noteFeatures[baseIndex] = duration * (1f + (float)(rand.NextDouble() * 2 - 1) * noise);

                if (i > 0) {
                    var prevNote = notes[i - 1];
                    float toneDiff = (float)(note.tone - prevNote.tone) / 127f;
                    noteFeatures[baseIndex + 1] = toneDiff * (1f + (float)(rand.NextDouble() * 2 - 1) * noise);
                    noteFeatures[baseIndex + 5] = 1f;

                    int restLengthTicks = note.position - (prevNote.position + prevNote.duration);
                    noteFeatures[baseIndex + 3] = restLengthTicks > 0 ? 1f : 0f;
                } else {
                    noteFeatures[baseIndex + 1] = 0f;
                    noteFeatures[baseIndex + 3] = 0f;
                    noteFeatures[baseIndex + 5] = 0f;
                }

                if (i < seqLength - 1) {
                    var nextNote = notes[i + 1];
                    float toneDiff = (float)(nextNote.tone - note.tone) / 127f;
                    noteFeatures[baseIndex + 2] = toneDiff * (1f + (float)(rand.NextDouble() * 2 - 1) * noise);
                    noteFeatures[baseIndex + 6] = 1f;
                } else {
                    noteFeatures[baseIndex + 2] = 0f;
                    noteFeatures[baseIndex + 6] = 0f;
                }

                string phoneme = note.lyric ?? "<UNK>";
                phonemeIndices[i] = _phonemeToIdx.TryGetValue(phoneme, out int idx)
                                    ? idx
                                    : _phonemeToIdx.TryGetValue("<UNK>", out int unkIdx)
                                        ? unkIdx
                                        : 0;
            }

            return (noteFeatures, phonemeIndices);
        }

        private void ApplyPitchBends(List<UNote> notes, float[] pitchParams) {
            int seqLength = notes.Count;
            int numPoints = _use21Points ? 7 : 5;
            int paramsPerNote = numPoints * 3;

            for (int i = 0; i < seqLength; i++) {
                var note = notes[i];
                float[] noteParams = pitchParams.Skip(i * paramsPerNote).Take(paramsPerNote).ToArray();

                if (noteParams.Length < paramsPerNote) {
                    Console.Error.WriteLine($"Warning: Insufficient pitch parameters for note {i}. Expected {paramsPerNote}, got {noteParams.Length}. Skipping pitch bend for this note.");
                    continue;
                }

                var pitchPoints = new List<PitchPoint>();
                float lastX = 0f;

                for (int j = 0; j < numPoints; j++) {
                    int paramBaseIndex = j * 3;
                    float x = noteParams[paramBaseIndex] * 100f;
                    float y = noteParams[paramBaseIndex + 1] * 100f;
                    float shapeValue = noteParams[paramBaseIndex + 2];

                    const float epsilon = 1e-5f;
                    if (Math.Abs(x) < epsilon && Math.Abs(y) < epsilon && Math.Abs(shapeValue) < epsilon) {
                        continue;
                    }

                    int shapeIdx = Math.Min(Math.Max(0, (int)Math.Round(shapeValue * 5)), 5);
                    pitchPoints.Add(new PitchPoint(x, y, GetShapeFromIndex(shapeIdx)));
                    lastX = x;
                }

                if (pitchPoints.Count > 0) {
                    pitchPoints.Add(new PitchPoint(lastX + 15f, 0f, PitchPointShape.io));
                }

                DocManager.Inst.ExecuteCmd(new NotePitchCommand(note, pitchPoints, true));
            }
        }

        private PitchPointShape GetShapeFromIndex(int index) {
            switch (index) {
                case 0: return PitchPointShape.io;
                case 1: return PitchPointShape.i;
                case 2: return PitchPointShape.o;
                case 3: return PitchPointShape.l;
                case 4: return PitchPointShape.io;
                case 5: return PitchPointShape.io;
                default: return PitchPointShape.io;
            }
        }
    }
}
