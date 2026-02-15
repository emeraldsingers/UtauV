using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Core.Format {
    public static class Tssln {
        const double TickRate = 2.0;
        const int OctaveOffset = -1;
        const string ProlongedSoundMark = "ー";

        public static UProject Load(string file) {
            var root = JuceNodeParser.ParseRoot(File.ReadAllBytes(file));
            if (!string.Equals(root.Name, "TSSolution", StringComparison.Ordinal)) {
                throw new FileFormatException("Unrecognizable TSSLN file format.");
            }
            var rootData = root.ToDictionary();

            var project = new UProject {
                FilePath = file,
            };
            Ustx.AddDefaultExpressions(project);
            project.tracks.Clear();
            project.parts.Clear();
            project.tempos = new List<UTempo> { new UTempo(0, 120) };
            project.timeSignatures = new List<UTimeSignature> { new UTimeSignature(0, 4, 4) };

            var allTempos = new List<UTempo>();
            var allTimeSignatures = new List<UTimeSignature>();

            foreach (var trackGroupObj in GetList(rootData, "Tracks")) {
                if (trackGroupObj is not Dictionary<string, object?> trackGroup) {
                    continue;
                }
                foreach (var trackObj in GetList(trackGroup, "Track")) {
                    if (trackObj is not Dictionary<string, object?> trackItem) {
                        continue;
                    }
                    if (GetInt(trackItem, "Type", -1) != 0) {
                        continue;
                    }
                    var parseResult = ParseSingingTrack(trackItem, project);
                    if (parseResult is null) {
                        continue;
                    }
                    var (track, part, tempos, timeSignatures) = parseResult.Value;
                    project.tracks.Add(track);
                    project.parts.Add(part);
                    allTempos.AddRange(tempos);
                    allTimeSignatures.AddRange(timeSignatures);
                }
            }

            if (allTempos.Count > 0) {
                project.tempos = allTempos
                    .OrderBy(t => t.position)
                    .GroupBy(t => t.position)
                    .Select(g => g.First())
                    .ToList();
            }
            if (allTimeSignatures.Count > 0) {
                project.timeSignatures = allTimeSignatures
                    .OrderBy(ts => ts.barPosition)
                    .GroupBy(ts => ts.barPosition)
                    .Select(g => g.First())
                    .ToList();
            }

            project.ValidateFull();
            Log.Information($"Loaded TSSLN file: {file}, tracks={project.tracks.Count}, parts={project.parts.Count}");
            return project;
        }

        static (UTrack track, UVoicePart part, List<UTempo> tempos, List<UTimeSignature> timeSignatures)? ParseSingingTrack(
            Dictionary<string, object?> trackItem,
            UProject project) {
            var pluginData = GetDict(trackItem, "PluginData");
            if (pluginData == null) {
                return null;
            }
            var stateInformation = GetDict(pluginData, "StateInformation");
            if (stateInformation == null) {
                return null;
            }

            var trackName = GetString(trackItem, "Name", "VoiSona Track");
            var track = new UTrack(project) {
                TrackNo = project.tracks.Count,
                TrackName = trackName,
            };
            var part = new UVoicePart {
                name = trackName,
                position = 0,
            };

            var tempos = new List<UTempo>();
            var timeSignatures = new List<UTimeSignature> {
                new UTimeSignature(0, 4, 4),
            };
            int prevTick = 0;
            int partEnd = 0;

            foreach (var songObj in GetList(stateInformation, "Song")) {
                if (songObj is not Dictionary<string, object?> song) {
                    continue;
                }
                foreach (var beatObj in GetList(song, "Beat")) {
                    if (beatObj is not Dictionary<string, object?> beat) {
                        continue;
                    }
                    foreach (var timeObj in GetList(beat, "Time")) {
                        if (timeObj is not Dictionary<string, object?> timeNode) {
                            continue;
                        }
                        int tick = (int)(GetInt(timeNode, "Clock", 0) / TickRate);
                        int numerator = GetInt(timeNode, "Beats", 4);
                        int denominator = GetInt(timeNode, "BeatType", 4);
                        int ticksInMeasure = Math.Max(1, 480 * 4 * timeSignatures[^1].beatPerBar / timeSignatures[^1].beatUnit);
                        int tickDiff = tick - prevTick;
                        int measureDiff = tickDiff / ticksInMeasure;
                        timeSignatures.Add(new UTimeSignature(
                            timeSignatures[^1].barPosition + measureDiff,
                            numerator,
                            denominator));
                        prevTick = tick;
                    }
                }
                foreach (var tempoObj in GetList(song, "Tempo")) {
                    if (tempoObj is not Dictionary<string, object?> tempoGroup) {
                        continue;
                    }
                    foreach (var soundObj in GetList(tempoGroup, "Sound")) {
                        if (soundObj is not Dictionary<string, object?> soundNode) {
                            continue;
                        }
                        int tick = (int)(GetInt(soundNode, "Clock", 0) / TickRate);
                        double bpm = GetDouble(soundNode, "Tempo", 120.0);
                        tempos.Add(new UTempo(tick, bpm));
                    }
                }
                foreach (var scoreObj in GetList(song, "Score")) {
                    if (scoreObj is not Dictionary<string, object?> score) {
                        continue;
                    }
                    foreach (var noteObj in GetList(score, "Note")) {
                        if (noteObj is not Dictionary<string, object?> noteNode) {
                            continue;
                        }
                        int pitchStep = GetInt(noteNode, "PitchStep", 0);
                        int pitchOctave = GetInt(noteNode, "PitchOctave", 4) - OctaveOffset;
                        int tone = pitchStep + pitchOctave * 12;
                        int start = (int)(GetInt(noteNode, "Clock", 0) / TickRate);
                        int duration = Math.Max(1, (int)(GetInt(noteNode, "Duration", 0) / TickRate));
                        var note = project.CreateNote(tone, start, duration);
                        var lyric = GetString(noteNode, "Lyric", string.Empty);
                        note.lyric = lyric == ProlongedSoundMark ? "-" : lyric;
                        var phoneme = GetString(noteNode, "Phoneme", string.Empty);
                        if (!string.IsNullOrWhiteSpace(phoneme)) {
                            note.lyric = $"{note.lyric}[{phoneme.Replace(",", " ")}]";
                        }
                        part.notes.Add(note);
                        partEnd = Math.Max(partEnd, start + duration);
                    }
                }
            }

            if (part.notes.Count == 0) {
                return null;
            }
            part.Duration = partEnd;
            part.trackNo = track.TrackNo;
            part.AfterLoad(project, track);
            track.AfterLoad(project);
            return (track, part, tempos, timeSignatures);
        }

        static List<object?> GetList(Dictionary<string, object?> dict, string key) {
            return dict.TryGetValue(key, out var value) && value is List<object?> list
                ? list
                : new List<object?>();
        }

        static Dictionary<string, object?>? GetDict(Dictionary<string, object?> dict, string key) {
            if (!dict.TryGetValue(key, out var value) || value is not Dictionary<string, object?> result) {
                return null;
            }
            // Binary plugin data can arrive wrapped as { "<NodeName>": { ... } }.
            if (result.Count == 1 && !result.ContainsKey("Song") && !result.ContainsKey("StateInformation")) {
                var first = result.First().Value;
                if (first is Dictionary<string, object?> inner) {
                    return inner;
                }
            }
            return result;
        }

        static string GetString(Dictionary<string, object?> dict, string key, string defaultValue) {
            return dict.TryGetValue(key, out var value) && value is string s ? s : defaultValue;
        }

        static int GetInt(Dictionary<string, object?> dict, string key, int defaultValue) {
            if (!dict.TryGetValue(key, out var value) || value == null) {
                return defaultValue;
            }
            return value switch {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                float f => (int)f,
                _ => defaultValue,
            };
        }

        static double GetDouble(Dictionary<string, object?> dict, string key, double defaultValue) {
            if (!dict.TryGetValue(key, out var value) || value == null) {
                return defaultValue;
            }
            return value switch {
                int i => i,
                long l => l,
                double d => d,
                float f => f,
                _ => defaultValue,
            };
        }
    }

    enum JuceVariantType : byte {
        Int = 1,
        BoolTrue = 2,
        BoolFalse = 3,
        Double = 4,
        String = 5,
        Int64 = 6,
        Array = 7,
        Binary = 8,
        Undefined = 9,
    }

    sealed class JuceNode {
        public string Name { get; }
        public Dictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();
        public List<JuceNode> Children { get; } = new List<JuceNode>();

        public JuceNode(string name) {
            Name = name;
        }

        public Dictionary<string, object?> ToDictionary() {
            var dict = new Dictionary<string, object?>();
            foreach (var kv in Attributes) {
                dict[kv.Key] = kv.Value;
            }
            var groups = Children
                .GroupBy(child => child.Name)
                .ToDictionary(
                    g => g.Key,
                    g => (object?)g.Select(c => (object?)c.ToDictionary()).ToList());
            foreach (var kv in groups) {
                dict[kv.Key] = kv.Value;
            }
            return dict;
        }
    }

    static class JuceNodeParser {
        public static JuceNode ParseRoot(byte[] bytes) {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            return ParseNode(reader);
        }

        static JuceNode ParseNode(BinaryReader reader) {
            var node = new JuceNode(ReadCString(reader));
            int attrCount = ReadCompressedInt(reader);
            for (int i = 0; i < attrCount; i++) {
                string attrName = ReadCString(reader);
                node.Attributes[attrName] = ReadVariant(reader);
            }
            int childCount = ReadCompressedInt(reader);
            for (int i = 0; i < childCount; i++) {
                node.Children.Add(ParseNode(reader));
            }
            return node;
        }

        static object? ReadVariant(BinaryReader reader) {
            int payloadLength = ReadCompressedInt(reader);
            if (payloadLength <= 0) {
                return null;
            }
            var payload = reader.ReadBytes(payloadLength);
            using var payloadStream = new MemoryStream(payload, writable: false);
            using var payloadReader = new BinaryReader(payloadStream, Encoding.UTF8, leaveOpen: true);
            var type = (JuceVariantType)payloadReader.ReadByte();
            return type switch {
                JuceVariantType.Int => payloadReader.ReadInt32(),
                JuceVariantType.BoolTrue => true,
                JuceVariantType.BoolFalse => false,
                JuceVariantType.Double => payloadReader.ReadDouble(),
                JuceVariantType.String => ReadCString(payloadReader),
                JuceVariantType.Int64 => payloadReader.ReadInt64(),
                JuceVariantType.Array => ReadVariantArray(payloadReader),
                JuceVariantType.Binary => ParseBinary(payloadReader.ReadBytes((int)(payloadStream.Length - payloadStream.Position))),
                _ => null,
            };
        }

        static object ParseBinary(byte[] bytes) {
            try {
                var node = ParseRoot(bytes);
                return new Dictionary<string, object?> {
                    [node.Name] = node.ToDictionary(),
                };
            } catch {
                return bytes;
            }
        }

        static List<object?> ReadVariantArray(BinaryReader reader) {
            int count = ReadCompressedInt(reader);
            var list = new List<object?>(count);
            for (int i = 0; i < count; i++) {
                list.Add(ReadVariant(reader));
            }
            return list;
        }

        static int ReadCompressedInt(BinaryReader reader) {
            int width = reader.ReadByte();
            if (width <= 0) {
                return 0;
            }
            int value = 0;
            for (int i = 0; i < width; i++) {
                value |= reader.ReadByte() << (8 * i);
            }
            return value;
        }

        static string ReadCString(BinaryReader reader) {
            var bytes = new List<byte>();
            byte b;
            while ((b = reader.ReadByte()) != 0) {
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
