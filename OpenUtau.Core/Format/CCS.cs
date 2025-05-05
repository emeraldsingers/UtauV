using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Format {
    public static class CCS {
        private const int TICK_RATE = 2; 
        private const int RESOLUTION = 480;
        private const int FIXED_MEASURE_PREFIX = 1;
        private const int OCTAVE_OFFSET = -1; 

        public static UProject Load(string file) {
            var doc = new XmlDocument();
            doc.Load(file);

            var scene = doc.SelectSingleNode("//Scene") ?? throw new FileFormatException("Scene not found in CCS file.");
            var units = scene.SelectNodes(".//Unit[@Category='SingerSong']");
            var groups = scene.SelectNodes(".//Group[@Category='SingerSong']");

            var project = new UProject { resolution = RESOLUTION };
            Ustx.AddDefaultExpressions(project);

            UTimeSignature[] timeSignatures = null;
            foreach (XmlElement unit in units) {
                var timeNodes = unit.SelectNodes(".//Song/Beat/Time");
                if (timeNodes.Count > 0) {
                    timeSignatures = ParseTimeSignatures(timeNodes);
                    break;
                }
            }
            timeSignatures ??= new[] { new UTimeSignature(0, 4, 4) }; 
            project.timeSignatures = timeSignatures.ToList();

            // Parse tempos
            UTempo[] tempos = null;
            foreach (XmlElement unit in units) {
                var tempoNodes = unit.SelectNodes(".//Song/Tempo/Sound");
                if (tempoNodes.Count > 0) {
                    tempos = ParseTempos(tempoNodes);
                    break;
                }
            }
            tempos ??= new[] { new UTempo(0, 120) }; 
            project.tempos = tempos.ToList();

            var tickCounter = new TickCounter(RESOLUTION);
            tickCounter.GoToMeasure(FIXED_MEASURE_PREFIX, timeSignatures);
            long tickPrefix = tickCounter.Tick;

            int trackNo = 0;
            foreach (XmlElement unit in units) {
                var groupId = unit.GetAttribute("Group");
                var group = groups.Cast<XmlElement>().FirstOrDefault(g => g.GetAttribute("Id") == groupId);
                var trackName = group?.GetAttribute("Name") ?? $"Track {trackNo + 1}";

                var track = new UTrack(project) { TrackNo = trackNo, TrackName = trackName };
                var part = new UVoicePart { trackNo = trackNo, position = 0 };

                var noteNodes = unit.SelectNodes(".//Song/Score/Note");
                foreach (XmlElement noteNode in noteNodes) {
                    long clock = long.Parse(noteNode.GetAttribute("Clock"));
                    int tickOn = (int)(clock * (RESOLUTION / TICK_RATE) - tickPrefix);
                    int duration = (int)(long.Parse(noteNode.GetAttribute("Duration")) * (RESOLUTION / TICK_RATE));
                    int pitchStep = int.Parse(noteNode.GetAttribute("PitchStep"));
                    int pitchOctave = int.Parse(noteNode.GetAttribute("PitchOctave")) + OCTAVE_OFFSET;
                    int key = pitchStep + pitchOctave * 12;
                    string lyric = noteNode.GetAttribute("Lyric");

                    var note = project.CreateNote(key, tickOn, duration);
                    note.lyric = lyric;
                    part.notes.Add(note);
                }

                if (part.notes.Any()) {
                    part.Duration = part.notes.Max(n => n.End);
                    project.parts.Add(part);
                    project.tracks.Add(track);
                }
                trackNo++;
            }

            project.timeSignatures = project.timeSignatures
                .Select(ts => new UTimeSignature(ts.barPosition - FIXED_MEASURE_PREFIX, ts.beatPerBar, ts.beatUnit))
                .Where(ts => ts.barPosition >= 0)
                .ToList();
            if (project.timeSignatures.Any()) project.timeSignatures[0].barPosition = 0;

            project.tempos = project.tempos
                .Select(t => new UTempo((int)(t.position - tickPrefix), t.bpm))
                .Where(t => t.position >= 0)
                .ToList();
            if (project.tempos.Any()) project.tempos[0].position = 0;

            project.ValidateFull();
            return project;
        }

        private static UTimeSignature[] ParseTimeSignatures(XmlNodeList timeNodes) {
            var timeList = timeNodes.Cast<XmlElement>()
                .Select(n => new {
                    Clock = long.Parse(n.GetAttribute("Clock")),
                    Numerator = int.Parse(n.GetAttribute("Beats")),
                    Denominator = int.Parse(n.GetAttribute("BeatType"))
                })
                .OrderBy(t => t.Clock)
                .ToList();

            var tickCounter = new TickCounter(RESOLUTION);
            var signatures = new List<UTimeSignature>();
            foreach (var t in timeList) {
                tickCounter.GoToTick(t.Clock * (RESOLUTION / TICK_RATE), t.Numerator, t.Denominator);
                signatures.Add(new UTimeSignature(tickCounter.Measure, t.Numerator, t.Denominator));
            }
            return signatures.ToArray();
        }

        private static UTempo[] ParseTempos(XmlNodeList tempoNodes) {
            return tempoNodes.Cast<XmlElement>()
                .Select(n => new UTempo(
                    (int)(long.Parse(n.GetAttribute("Clock")) * (RESOLUTION / TICK_RATE)),
                    double.Parse(n.GetAttribute("Tempo"))))
                .OrderBy(t => t.position)
                .ToArray();
        }

        private class TickCounter {
            public long Tick { get; private set; }
            public int Measure { get; private set; }
            private int ticksPerBeat;
            private int beatsPerMeasure;
            private readonly int resolution;

            public TickCounter(int resolution) {
                this.resolution = resolution;
                Tick = 0;
                Measure = 0;
                beatsPerMeasure = 4;
                ticksPerBeat = resolution / 4; 
            }

            public void GoToTick(long targetTick, int numerator, int denominator) {
                while (Tick < targetTick) {
                    long nextMeasureTick = Tick + ticksPerBeat * beatsPerMeasure;
                    if (nextMeasureTick <= targetTick) { Tick = nextMeasureTick; Measure++; } else { Tick = targetTick; break; }
                }
                beatsPerMeasure = numerator;
                ticksPerBeat = resolution / denominator;
            }

            public void GoToMeasure(int targetMeasure, UTimeSignature[] timeSignatures) {
                while (Measure < targetMeasure) {
                    var ts = timeSignatures.LastOrDefault(t => t.barPosition <= Measure) ?? new UTimeSignature(0, 4, 4);
                    beatsPerMeasure = ts.beatPerBar;
                    ticksPerBeat = resolution / ts.beatUnit;
                    Tick += ticksPerBeat * beatsPerMeasure;
                    Measure++;
                }
            }
        }
    }
}
