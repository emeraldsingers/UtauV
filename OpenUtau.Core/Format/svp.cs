using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using OpenUtau.Core.Ustx;

namespace SVP.Core.Format {
    /// <summary>Note model.</summary>
    public struct SvpNote {
        public int key;
        public int tickOn;
        public int tickOff;
        public string lyric;
    }

    /// <summary>Tempo model.</summary>
    public struct SvpTempo {
        public int position;
        public double bpm;
    }

    /// <summary>Time signature model.</summary>
    public struct SvpTimeSignature {
        public int index;
        public int numerator;
        public int denominator;
    }

    /// <summary>Track model.</summary>
    public struct SvpTrack {
        public string name;
        public SvpNote[] notes;
        public bool renderEnabled;
    }

    /// <summary>Project model.</summary>
    public struct SvpProject {
        public int version;
        public SvpTimeSignature[] timeSignatures;
        public SvpTempo[] tempos;
        public SvpTrack[] tracks;
    }

    public static class SvpData {
        public static UProject Load(string file) {
            if (!File.Exists(file)) {
                throw new FileNotFoundException($"File not found: {file}");
            }

            var svpProject = JsonConvert.DeserializeObject<SvpProject>(File.ReadAllText(file, Encoding.UTF8));
            if (svpProject.Equals(default(SvpProject))) {
                throw new InvalidDataException("Failed to deserialize the SVP project.");
            }


            return ConvertToUProject(svpProject);
        }


        public static void Save(string file, SvpProject project) {
            var json = JsonConvert.SerializeObject(project, Formatting.Indented);
            File.WriteAllText(file, json, Encoding.UTF8);
        }

        public static UProject ConvertToUProject(SvpProject svpProject) {
            var uProject = new UProject {
                name = "Converted Project",
                tempos = svpProject.tempos?.Select(t => new UTempo {
                    position = t.position,
                    bpm = t.bpm
                }).ToList() ?? new List<UTempo>(), // Use empty list if null

                timeSignatures = svpProject.timeSignatures?.Select(ts => new UTimeSignature {
                    barPosition = ts.index * 1920,
                    beatPerBar = ts.numerator,
                    beatUnit = ts.denominator
                }).ToList() ?? new List<UTimeSignature>(), // Use empty list if null

            };

            foreach (var svpTrack in svpProject.tracks) {
                var uTrack = new UTrack(svpTrack.name);
                uProject.tracks.Add(uTrack);

                var voicePart = new UVoicePart {
                    name = svpTrack.name,
                    position = 0
                };

                foreach (var note in svpTrack.notes) {
                    var uNote = new UNote {
                        tone = note.key,
                        position = note.tickOn,
                        duration = note.tickOff - note.tickOn,
                        lyric = string.IsNullOrEmpty(note.lyric) ? "la" : note.lyric
                    };
                    voicePart.notes.Add(uNote);
                }

                if (voicePart.notes.Any()) {
                    voicePart.Duration = voicePart.notes.Max(n => n.position + n.duration) - voicePart.position;
                    uProject.parts.Add(voicePart);
                }
            }

            uProject.timeAxis.BuildSegments(uProject);
            return uProject;
        }
    }
}
