using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Core.Format {
    public static class Ccs {
        const double TickRate = 2.0;
        const int OctaveOffset = -1;
        const string ProlongedSoundMark = "ー";

        sealed class GroupInfo {
            public string Name { get; init; } = string.Empty;
            public bool IsMuted { get; init; }
            public bool IsSolo { get; init; }
        }

        public static UProject Load(string file) {
            var doc = XDocument.Load(file, LoadOptions.None);
            if (doc.Root == null || !string.Equals(doc.Root.Name.LocalName, "Scenario", StringComparison.Ordinal)) {
                throw new FileFormatException("Unrecognizable CCS file format.");
            }

            var project = new UProject {
                FilePath = file,
            };
            Ustx.AddDefaultExpressions(project);
            project.tracks.Clear();
            project.parts.Clear();
            project.tempos = new List<UTempo> { new UTempo(0, 120) };
            project.timeSignatures = new List<UTimeSignature> { new UTimeSignature(0, 4, 4) };

            var scene = doc.Root.Element("Sequence")?.Element("Scene");
            if (scene == null) {
                throw new FileFormatException("CCS file does not contain Sequence/Scene.");
            }

            var groups = BuildGroupMap(scene);
            var allTempos = new List<UTempo>();
            var allTimeSignatures = new List<UTimeSignature>();

            foreach (var unit in scene.Element("Units")?.Elements("Unit") ?? Enumerable.Empty<XElement>()) {
                var category = Attr(unit, "Category");
                if (!string.Equals(category, "SingerSong", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                var song = unit.Element("Song");
                if (song == null) {
                    continue;
                }
                var groupId = Attr(unit, "Group");
                groups.TryGetValue(groupId ?? string.Empty, out var group);
                var unitName = Attr(unit, "Name");
                var trackName = !string.IsNullOrWhiteSpace(unitName)
                    ? unitName
                    : (!string.IsNullOrWhiteSpace(group?.Name) ? group!.Name : $"Track{project.tracks.Count + 1}");

                var track = new UTrack(project) {
                    TrackNo = project.tracks.Count,
                    TrackName = trackName,
                    Mute = group?.IsMuted ?? false,
                    Solo = group?.IsSolo ?? false,
                };
                var part = ParsePart(song, trackName, project, out var tempos, out var timeSignatures);
                if (part == null) {
                    continue;
                }
                part.trackNo = track.TrackNo;
                part.AfterLoad(project, track);
                project.tracks.Add(track);
                project.parts.Add(part);
                allTempos.AddRange(tempos);
                allTimeSignatures.AddRange(timeSignatures);
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
            Log.Information($"Loaded CCS file: {file}, tracks={project.tracks.Count}, parts={project.parts.Count}");
            return project;
        }

        static Dictionary<string, GroupInfo> BuildGroupMap(XElement scene) {
            var map = new Dictionary<string, GroupInfo>();
            foreach (var group in scene.Element("Groups")?.Elements("Group") ?? Enumerable.Empty<XElement>()) {
                var id = Attr(group, "Id");
                if (string.IsNullOrWhiteSpace(id)) {
                    continue;
                }
                map[id] = new GroupInfo {
                    Name = Attr(group, "Name") ?? string.Empty,
                    IsMuted = AttrBool(group, "IsMuted", false),
                    IsSolo = AttrBool(group, "IsSolo", false),
                };
            }
            return map;
        }

        static UVoicePart? ParsePart(
            XElement song,
            string name,
            UProject project,
            out List<UTempo> tempos,
            out List<UTimeSignature> timeSignatures) {
            tempos = new List<UTempo>();
            timeSignatures = new List<UTimeSignature> { new UTimeSignature(0, 4, 4) };

            int prevTick = 0;
            foreach (var timeNode in song.Element("Beat")?.Elements("Time") ?? Enumerable.Empty<XElement>()) {
                int tick = (int)(AttrInt(timeNode, "Clock", 0) / TickRate);
                int numerator = AttrInt(timeNode, "Beats", 4);
                int denominator = AttrInt(timeNode, "BeatType", 4);
                int ticksInMeasure = Math.Max(1, 480 * 4 * timeSignatures[^1].beatPerBar / timeSignatures[^1].beatUnit);
                int tickDiff = tick - prevTick;
                int measureDiff = tickDiff / ticksInMeasure;
                timeSignatures.Add(new UTimeSignature(
                    timeSignatures[^1].barPosition + measureDiff,
                    numerator,
                    denominator));
                prevTick = tick;
            }

            foreach (var tempoNode in song.Element("Tempo")?.Elements("Sound") ?? Enumerable.Empty<XElement>()) {
                int tick = (int)(AttrInt(tempoNode, "Clock", 0) / TickRate);
                double bpm = AttrDouble(tempoNode, "Tempo", 120);
                tempos.Add(new UTempo(tick, bpm));
            }

            var part = new UVoicePart {
                name = name,
                position = 0,
            };
            int partEnd = 0;
            foreach (var noteNode in song.Element("Score")?.Elements("Note") ?? Enumerable.Empty<XElement>()) {
                int pitchStep = AttrInt(noteNode, "PitchStep", 0);
                int pitchOctave = AttrInt(noteNode, "PitchOctave", 4) - OctaveOffset;
                int tone = pitchStep + pitchOctave * 12;
                int start = (int)(AttrInt(noteNode, "Clock", 0) / TickRate);
                int duration = Math.Max(1, (int)(AttrInt(noteNode, "Duration", 0) / TickRate));
                var note = project.CreateNote(tone, start, duration);
                var lyric = Attr(noteNode, "Lyric") ?? string.Empty;
                note.lyric = lyric == ProlongedSoundMark ? "-" : lyric;
                var phonetic = Attr(noteNode, "Phonetic");
                if (!string.IsNullOrWhiteSpace(phonetic)) {
                    note.lyric = $"{note.lyric}[{phonetic!.Replace(",", " ")}]";
                }
                part.notes.Add(note);
                partEnd = Math.Max(partEnd, start + duration);
            }
            if (part.notes.Count == 0) {
                return null;
            }
            part.Duration = partEnd;
            return part;
        }

        static string? Attr(XElement element, string name) {
            return element.Attribute(name)?.Value;
        }

        static int AttrInt(XElement element, string name, int defaultValue) {
            var s = Attr(element, name);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        static double AttrDouble(XElement element, string name, double defaultValue) {
            var s = Attr(element, name);
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
        }

        static bool AttrBool(XElement element, string name, bool defaultValue) {
            var s = Attr(element, name);
            return bool.TryParse(s, out var value) ? value : defaultValue;
        }
    }
}
