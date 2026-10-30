using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkStyle {
        public string Name = string.Empty;
        public int Id;
        public string ModelDir = string.Empty;
        public string Icon = string.Empty;

        public string DurationModelPath => Path.Combine(ModelDir, CoeiroinkConfig.DurationModelFile);
        public string SynthModelPath => Path.Combine(ModelDir, CoeiroinkConfig.SynthModelFile);
        public string ConfigPath => Path.Combine(ModelDir, "config.yaml");
        public bool IsExported => File.Exists(DurationModelPath) && File.Exists(SynthModelPath);
    }

    public class CoeiroinkConfig {
        public const string ExporterUrl = "https://github.com/emeraldsingers/coeiroink-onnx-exporter";
        public const string MetasFile = "metas.json";
        public const string DurationModelFile = "duration.onnx";
        public const string SynthModelFile = "synth.onnx";

        public string SpeakerName = string.Empty;
        public string SpeakerUuid = string.Empty;
        public string Version = string.Empty;
        public List<CoeiroinkStyle> Styles = new List<CoeiroinkStyle>();

        public static bool IsCoeiroinkFolder(string dir) {
            return File.Exists(Path.Combine(dir, MetasFile)) && Directory.Exists(Path.Combine(dir, "model"));
        }

        public static CoeiroinkConfig Load(string dir) {
            var config = new CoeiroinkConfig();
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, MetasFile)));
            var root = doc.RootElement;
            config.SpeakerName = root.TryGetProperty("speakerName", out var name) ? name.GetString() ?? string.Empty : string.Empty;
            config.SpeakerUuid = root.TryGetProperty("speakerUuid", out var uuid) ? uuid.GetString() ?? string.Empty : string.Empty;
            config.Version = root.TryGetProperty("version", out var version) ? version.GetString() ?? string.Empty : string.Empty;
            if (root.TryGetProperty("styles", out var styles)) {
                foreach (var style in styles.EnumerateArray()) {
                    int id = style.GetProperty("styleId").GetInt32();
                    config.Styles.Add(new CoeiroinkStyle {
                        Name = style.GetProperty("styleName").GetString() ?? id.ToString(),
                        Id = id,
                        ModelDir = Path.Combine(dir, "model", id.ToString()),
                        Icon = Path.Combine(dir, "icons", $"{id}.png"),
                    });
                }
            }
            return config;
        }
    }

    public class CoeiroinkModelConfig {
        public string[] Tokens = Array.Empty<string>();
        public int SampleRate = 44100;
        public int HopLength = 512;
        public int HiddenChannels = 192;

        public static CoeiroinkModelConfig Load(string path) {
            var lines = File.ReadAllLines(path);
            var config = new CoeiroinkModelConfig();
            var tokens = new List<string>();
            bool inTokens = false;
            foreach (var line in lines) {
                if (inTokens) {
                    var match = Regex.Match(line, @"^-\s*(.+?)\s*$");
                    if (match.Success) {
                        tokens.Add(Unquote(match.Groups[1].Value));
                        continue;
                    }
                    inTokens = false;
                }
                if (line.StartsWith("token_list:")) {
                    inTokens = true;
                    continue;
                }
                config.SampleRate = ReadInt(line, @"^\s+sampling_rate:\s*(\d+)", config.SampleRate);
                config.HopLength = ReadInt(line, @"^\s+hop_length:\s*(\d+)", config.HopLength);
                config.HiddenChannels = ReadInt(line, @"^\s+hidden_channels:\s*(\d+)", config.HiddenChannels);
            }
            config.Tokens = tokens.ToArray();
            if (config.Tokens.Length == 0) {
                throw new InvalidDataException($"token_list was not found in {path}");
            }
            return config;
        }

        static int ReadInt(string line, string pattern, int fallback) {
            var match = Regex.Match(line, pattern);
            return match.Success ? int.Parse(match.Groups[1].Value) : fallback;
        }

        static string Unquote(string value) {
            if (value.Length >= 2 && (value[0] == '\'' && value[^1] == '\'' || value[0] == '"' && value[^1] == '"')) {
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            }
            return value;
        }

        public double FrameMs => HopLength * 1000.0 / SampleRate;

        public Dictionary<string, long> TokenIds() {
            var map = new Dictionary<string, long>();
            for (int i = 0; i < Tokens.Length; i++) {
                map.TryAdd(Tokens[i], i);
            }
            return map;
        }
    }
}
