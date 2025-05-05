using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using OpenUtau.Core.Ustx;
using System.Collections.Generic;
using System.Diagnostics;
using Serilog;
namespace OpenUtau.Core.Format {
    public static class SVP {
        private const long TICK_RATE = 1470000L;
        private const double SVP_VIBRATO_DEFAULT_START_SEC = 0.25;
        private const double SVP_VIBRATO_DEFAULT_EASE_IN_SEC = 0.2;
        private const double SVP_VIBRATO_DEFAULT_EASE_OUT_SEC = 0.2;
        private const double SVP_VIBRATO_DEFAULT_DEPTH_SEMITONE = 1.0;
        private const double SVP_VIBRATO_DEFAULT_FREQUENCY_HZ = 5.5;
        private const double SVP_VIBRATO_DEFAULT_PHASE_RAD = 0.0;

        public static UProject Load(string file) {
            UProject project = new UProject();
            Ustx.AddDefaultExpressions(project);
            project.FilePath = file;
            Log.Information($"Loading SVP file: {file}");

            var json = File.ReadAllText(file, Encoding.UTF8);
            var svpData = JsonConvert.DeserializeObject<SvpFile>(json);

            if (svpData == null) {
                throw new FileFormatException("Invalid SVP file format or empty.");
            }

            if (svpData.time?.meter != null)
                project.timeSignatures = svpData.time.meter
                    .Select(meter => new UTimeSignature(meter.index, meter.numerator, meter.denominator))
                    .ToList();
            else project.timeSignatures = new List<UTimeSignature> { new UTimeSignature(0, 4, 4) };

            project.tempos = svpData.time?.tempo
                .Select(tempo => new UTempo((int)(tempo.position / TICK_RATE), tempo.bpm))
                .ToList() ?? new List<UTempo> { new UTempo(0, 120) };
            Log.Information($"  Loaded {project.tempos.Count} tempos.");

            if (svpData.tracks != null) {
                Log.Information($"  Loading {svpData.tracks.Count} tracks...");
                foreach (var svpTrack in svpData.tracks) {
                    Log.Information($"    Processing track: {svpTrack.name}");
                    if (svpTrack.mainGroup?.notes != null && svpTrack.mainGroup.notes.Any()) {
                        Log.Information($"      Parsing notes and curves for track: {svpTrack.name} group: {svpTrack.mainGroup.name}");
                        var track = new UTrack(project);
                        track.TrackNo = project.tracks.Count;

                        var part = ParsePart(svpTrack, svpTrack.mainGroup, project);

                        if (part != null) {
                            part.trackNo = track.TrackNo;
                            part.AfterLoad(project, track);
                            project.tracks.Add(track);
                            project.parts.Add(part);
                            Log.Information($"       Parsed part for track {svpTrack.name}, duration {part.Duration}");
                        }

                    }
                }
            }

            project.ValidateFull();
            Log.Information($"   SVP file loaded sucessfully.");
            return project;
        }

        static UVoicePart ParsePart(SvpTrack svpTrack, SvpGroup svpGroup, UProject project) {
            var part = new UVoicePart();
            part.name = svpTrack.name ?? "Unnamed Track";
            
            int trackPosition = 0;
            if (svpTrack.mainRef?.blickOffset != null) {
                trackPosition = (int)(svpTrack.mainRef.blickOffset / TICK_RATE);
            }
            part.position = trackPosition;

            if (svpGroup.notes == null || svpGroup.notes.Count == 0) {
                Log.Information($"      No notes in this track.");
                return null;
            }

            var firstNote = svpGroup.notes.FirstOrDefault();
            int partStart = firstNote != null ? (int)Math.Round((double)(firstNote.onset / TICK_RATE)) : 0;
            int partEnd = 0;

            foreach (var svpNote in svpGroup.notes) {
                var noteStart = (int)Math.Round((double)(svpNote.onset / TICK_RATE));
                var noteDuration = (int)Math.Round((double)(svpNote.duration / TICK_RATE));

                var note = project.CreateNote(
                   svpNote.pitch,
                  noteStart,
                  noteDuration
                 );
                note.lyric = svpNote.lyrics ?? string.Empty;
                part.notes.Add(note);

                partStart = Math.Min(partStart, noteStart);
                partEnd = Math.Max(partEnd, noteStart + noteDuration);
            }

            if (part.notes.Count > 0) {

                part.Duration = partEnd - partStart;
            } else {
                return null;
            }
            ProcessCurves(project, part, svpTrack, svpGroup);
            return part;
        }

        static void ProcessCurves(UProject project, UVoicePart part, SvpTrack svpTrack, SvpGroup svpGroup) {
            var tempos = project.tempos.Select(tempo => new CoreTempo(tempo.position, tempo.bpm)).ToList();
            if (svpGroup.parameters?.pitchDelta != null) {
                var points = ProcessPitchCurve(svpGroup.parameters.pitchDelta.points, svpTrack, svpGroup, tempos);
                ProcessCurvePoints(project, part, Ustx.PITD, points, (double val) => (int)Math.Round(val));
            }
        }
        private static List<Pair<long, double>> ProcessPitchCurve(double[]? rawPoints, SvpTrack track, SvpGroup group, List<CoreTempo> tempos) {
            Log.Information("        Starting ProcessPitchCurve");
            var points = rawPoints?.
                Select((value, index) => new { value, index })
                .GroupBy(x => x.index / 2)
                .Select(group => group.Select(item => item.value).ToList())
                .Select(chunk => new Pair<long, double>((long)chunk[0], chunk[1] / 100)).ToList()
                ?? new List<Pair<long, double>>();

            var refData = track.mainRef;
            var notesWithVibrato = group.notes?.Select(note => new SvpNoteWithVibrato(
                  noteStartTick: (long)(note.onset + (refData?.blickOffset ?? 0)),
                  noteLengthTick: (long)note.duration,
                 vibratoStart: note.attributes != null && note.attributes.TryGetValue("tF0VbrStart", out var tF0VbrStart) ? (double?)tF0VbrStart : null,
                  easeInLength: note.attributes != null && note.attributes.TryGetValue("tF0VbrLeft", out var tF0VbrLeft) ? (double?)tF0VbrLeft : null,
                   easeOutLength: note.attributes != null && note.attributes.TryGetValue("tF0VbrRight", out var tF0VbrRight) ? (double?)tF0VbrRight : null,
                    depth: note.attributes != null && note.attributes.TryGetValue("dF0Vbr", out var dF0Vbr) ? (double?)dF0Vbr : null,
                     phase: note.attributes != null && note.attributes.TryGetValue("pF0Vbr", out var pF0Vbr) ? (double?)pF0Vbr : null,
                     frequency: note.attributes != null && note.attributes.TryGetValue("fF0Vbr", out var fF0Vbr) ? (double?)fF0Vbr : null))?.ToList() ?? new List<SvpNoteWithVibrato>();
            var vibratoEnvPoints = group.parameters?.vibratoEnv?.points?.Select((value, index) => new { value, index })?
                                   .GroupBy(indexedValue => indexedValue.index / 2)
                                  .Select(group => group.Select(item => item.value).ToList())
                                    .Select(item => new Pair<long, double>((long)item[0], item[1]))?.ToList()
                                   ?? new List<Pair<long, double>>();
                                   
            if (refData?.blickOffset != null) {
                for (int i = 0; i < points.Count; ++i) {
                    points[i] = new Pair<long, double>((long)(points[i].First + refData.blickOffset), points[i].Second);
                }
                for (int i = 0; i < vibratoEnvPoints.Count; ++i) {
                    vibratoEnvPoints[i] = new Pair<long, double>((long)(vibratoEnvPoints[i].First + refData.blickOffset), vibratoEnvPoints[i].Second);
                }
            }
            
            var vibratoDefaultParameters = refData?.voice == null ? null : new SvpDefaultVibratoParameters(
               vibratoStart: (double?)refData.voice.vocalModeParams?.GetValueOrDefault("tF0VbrStart"),
               easeInLength: (double?)refData.voice.vocalModeParams?.GetValueOrDefault("tF0VbrLeft"),
                 easeOutLength: (double?)refData.voice.vocalModeParams?.GetValueOrDefault("tF0VbrRight"),
                    depth: (double?)refData.voice.vocalModeParams?.GetValueOrDefault("dF0Vbr"),
                    frequency: (double?)refData.voice.vocalModeParams?.GetValueOrDefault("fF0Vbr")
                );

            points = processSvpInputPitchData(
                     points.Select(item => new Pair<long, double>(item.First, item.Second)).ToList(),
                     group.parameters?.pitchDelta?.mode,
                   notesWithVibrato,
                   tempos,
                 vibratoEnvPoints.Select(item => new Pair<long, double>(item.First, item.Second)).ToList(),
                 group.parameters?.vibratoEnv?.mode,
                  vibratoDefaultParameters
                );

            for (int i = 0; i < points.Count; ++i) {
                points[i] = new Pair<long, double>((long)(points[i].First / TICK_RATE), points[i].Second * 100);
            }
            
            Log.Information("        Finished ProcessPitchCurve");
            return points;
        }

        static void ProcessCurvePoints(UProject project, UVoicePart part, string curveAbbr, List<Pair<long, double>>? points, Func<double, int> valueMapper) {
            Log.Information($"          Starting ProcessCurvePoints for {curveAbbr}");
            if (points == null || points.Count == 0) {
                Log.Information($"          No points, skipping ProcessCurvePoints for {curveAbbr}");
                return;
            }
            var curve = part.curves.Find(c => c.abbr == curveAbbr);
            if (curve == null) {
                if (project.expressions.TryGetValue(curveAbbr, out var desc)) {
                    curve = new UCurve(desc);
                    part.curves.Add(curve);
                }
            }
            if (curve == null) {
                Log.Information($"          Cannot process curve with abbr {curveAbbr}");
                return;
            }
            
            for (int i = 0; i < points.Count; ++i) {
                var tick = (int)points[i].First;
                int value = valueMapper(points[i].Second);
                
                curve.Set(tick, value, tick, 0);
                if (i + 1 >= points.Count) {
                    curve.Set(part.Duration + part.position, value, tick, 0);
                }
            }
            Log.Information($"          Finished ProcessCurvePoints for {curveAbbr}");
        }
        private static List<Pair<long, double>> processSvpInputPitchData(
                    List<Pair<long, double>> points,
                   string? mode,
                    List<SvpNoteWithVibrato> notesWithVibrato,
                   List<CoreTempo> tempos,
                    List<Pair<long, double>> vibratoEnvPoints,
                   string? vibratoEnvMode,
                   SvpDefaultVibratoParameters? vibratoDefaultParameters) {
            Log.Information($"        Starting processSvpInputPitchData, points count {points.Count}");

            var transformedPoints = points.Merge();
            Log.Information("           processSvpInputPitchData: Merged points.");
            transformedPoints = transformedPoints.Interpolate(mode);
            Log.Information("            processSvpInputPitchData: Interpolated points.");

            Log.Information("            processSvpInputPitchData: Appended vibrato points.");
            transformedPoints = transformedPoints.RemoveRedundantPoints();
            Log.Information($"        Finished processSvpInputPitchData, resulting points: {transformedPoints.Count}");

            return transformedPoints;
        }

        static private List<Pair<long, double>> Merge(this List<Pair<long, double>> points) {
            return points.GroupBy(it => it.First)
            .Select(it => new Pair<long, double>(it.Key, it.Average(pair => pair.Second)))
              .OrderBy(it => it.First).ToList();
        }
        private const long SAMPLING_INTERVAL_TICK = 4L;
        private static List<Pair<long, double>> Interpolate(this List<Pair<long, double>> points, string? mode) {
            Log.Information("          Starting Interpolate");
            switch (mode) {
                case "linear":
                    Log.Information("           Interpolate method is linear");
                    return points.InterpolateLinear(SAMPLING_INTERVAL_TICK);
                case "cosine":
                    Log.Information("           Interpolate method is cosine");
                    return points.InterpolateCosineEaseInOut(SAMPLING_INTERVAL_TICK);
                case "cubic":
                    Log.Information("           Interpolate method is cubic (cosine)");
                    return points.InterpolateCosineEaseInOut(SAMPLING_INTERVAL_TICK);
                default:
                    Log.Information("           Interpolate method is default (cosine)");
                    return points.InterpolateCosineEaseInOut(SAMPLING_INTERVAL_TICK);
            }
        }
        static private List<Pair<long, double>> InterpolateLinear(this List<Pair<long, double>> points, long interval) {
            Log.Information($"            Starting InterpolateLinear for {points.Count} points");
            if (points == null || points.Count < 2) {
                Log.Information("             No points, skipping  InterpolateLinear");
                return points;
            }
            List<Pair<long, double>> interpolatedPoints = new List<Pair<long, double>>();
            for (int i = 0; i < points.Count - 1; ++i) {
                interpolatedPoints.Add(points[i]);
                var startPoint = points[i];
                var endPoint = points[i + 1];
                if (endPoint.First - startPoint.First < 1) {
                    Log.Information("           Skip zero interpoint range InterpolateLinear");
                    continue;
                }

                for (long t = startPoint.First + 1; t < endPoint.First; ++t) {
                    if ((t - startPoint.First) % interval == 0) {
                        var value = startPoint.Second + ((double)(t - startPoint.First) / (endPoint.First - startPoint.First)) * (endPoint.Second - startPoint.Second);
                        interpolatedPoints.Add(new Pair<long, double>(t, value));
                    }
                }
            }
            interpolatedPoints.Add(points[^1]);
            Log.Information($"          Finished  InterpolateLinear , result has {interpolatedPoints.Count} points");
            return interpolatedPoints;
        }
        private static List<Pair<long, double>> InterpolateCosineEaseInOut(this List<Pair<long, double>> points, long interval) {
            Log.Information($"Starting InterpolateCosineEaseInOut for {points.Count} points");
            if (points == null || points.Count < 2) {
                Log.Information("No points, skipping InterpolateCosineEaseInOut");
                return points;
            }

            var interpolatedPoints = new List<Pair<long, double>>();
            for (int i = 0; i < points.Count - 1; ++i) {
                interpolatedPoints.Add(points[i]);
                var startPoint = points[i];
                var endPoint = points[i + 1];

                double deltaY = endPoint.Second - startPoint.Second;
                double deltaX = endPoint.First - startPoint.First;
                if (deltaX <= 0) {
                    Log.Warning("Skipping Interpolation, because deltaX <= 0");
                    continue;
                }

                long maxPoints = 500;
                long effectiveInterval = interval;
                if (deltaX > maxPoints * interval) {
                    effectiveInterval = Math.Max(1, (long)(deltaX / maxPoints));
                }

                for (long t = startPoint.First + effectiveInterval; t < endPoint.First; t += effectiveInterval) {
                    double normalizedT = (double)(t - startPoint.First) / deltaX;
                    double cosValue = Math.Cos(Math.PI * normalizedT);
                    double value = startPoint.Second + deltaY * (0.5 - 0.5 * cosValue);
                    interpolatedPoints.Add(new Pair<long, double>(t, value));
                }
            }

            interpolatedPoints.Add(points[^1]);
            Log.Information($"Finished InterpolateCosineEaseInOut, result has {interpolatedPoints.Count} points");
            return interpolatedPoints;
        }

        private static List<Pair<long, double>> RemoveRedundantPoints(this List<Pair<long, double>> points) {
            Log.Information("         Starting RemoveRedundantPoints, points count " + points.Count);

            if (points == null || points.Count == 0) {
                Log.Information("           RemoveRedundantPoints, no points, returning empty list.");
                return new List<Pair<long, double>>();
            }
            List<Pair<long, double>> result = new List<Pair<long, double>>();
            Pair<long, double>? lastPoint = null; 


            foreach (var point in points) {

                if (lastPoint == null || (Math.Abs(point.Second - (lastPoint?.Second ?? 0)) > 0.00001)) { 
                    result.Add(point);
                    lastPoint = point;
                }

            }

            Log.Information("          Finished RemoveRedundantPoints, resulting points " + result.Count);
            return result;
        }
    }
    public class SvpFile {
        public int version { get; set; }
        public SvpTime? time { get; set; }
        public List<object>? library { get; set; }
        public List<SvpTrack>? tracks { get; set; }
        public SvpRenderConfig? renderConfig { get; set; }
    }
    public class SvpTime {
        public List<SvpMeter>? meter { get; set; }
        public List<SvpTempo>? tempo { get; set; }
    }
    public class SvpMeter {
        public int index { get; set; }
        public int numerator { get; set; }
        public int denominator { get; set; }
    }
    public class SvpTempo {
        public double position { get; set; }
        public double bpm { get; set; }
    }
    public class SvpTrack {
        public string? name { get; set; }
        public string? dispColor { get; set; }
        public int dispOrder { get; set; }
        public bool renderEnabled { get; set; }
        public SvpMixer? mixer { get; set; }
        public SvpGroup? mainGroup { get; set; }
        public SvpMainRef? mainRef { get; set; }
        public List<object>? groups { get; set; }
    }
    public class SvpMixer {
        public double gainDecibel { get; set; }
        public double pan { get; set; }
        public bool mute { get; set; }
        public bool solo { get; set; }
        public bool display { get; set; }
    }
    public class SvpGroup {
        public string? name { get; set; }
        public string? uuid { get; set; }
        public SvpParameters? parameters { get; set; }
        public Dictionary<string, object>? vocalModes { get; set; }
        public List<SvpNote>? notes { get; set; }
    }
    public class SvpParameters {
        public SvpCurve? pitchDelta { get; set; }
        public SvpCurve? vibratoEnv { get; set; }
        public SvpCurve? loudness { get; set; }
        public SvpCurve? tension { get; set; }
        public SvpCurve? breathiness { get; set; }
        public SvpCurve? voicing { get; set; }
        public SvpCurve? gender { get; set; }
        public SvpCurve? toneShift { get; set; }
    }
    public class SvpCurve {
        public string? mode { get; set; }
        public double[]? points { get; set; }
    }
    public class SvpNote {
        public string? musicalType { get; set; }
        public double onset { get; set; }
        public double duration { get; set; }
        public string? lyrics { get; set; }
        public string? phonemes { get; set; }
        public string? accent { get; set; }
        public int pitch { get; set; }
        public int detune { get; set; }
        public bool instantMode { get; set; }
        public Dictionary<string, object>? attributes { get; set; }
        public Dictionary<string, object>? systemAttributes { get; set; }
        public SvpTakes? pitchTakes { get; set; }
        public SvpTakes? timbreTakes { get; set; }
    }
    public class SvpTakes {
        public int activeTakeId { get; set; }
        public List<SvpTake>? takes { get; set; }
    }
    public class SvpTake {
        public int id { get; set; }
        public double expr { get; set; }
        public bool liked { get; set; }
    }
    public class SvpMainRef {
        public string? groupID { get; set; }
        public int blickAbsoluteBegin { get; set; }
        public int blickAbsoluteEnd { get; set; }
        public int blickOffset { get; set; }
        public int pitchOffset { get; set; }
        public bool isInstrumental { get; set; }
        public SvpCurve? systemPitchDelta { get; set; }
        public SvpDatabase? database { get; set; }
        public string? dictionary { get; set; }
        public SvpVoice? voice { get; set; }
        public SvpTakes? pitchTakes { get; set; }
        public SvpTakes? timbreTakes { get; set; }
    }
    public class SvpDatabase {
        public string? name { get; set; }
        public string? language { get; set; }
        public string? phoneset { get; set; }
        public string? languageOverride { get; set; }
        public string? phonesetOverride { get; set; }
        public string? backendType { get; set; }
        public string? version { get; set; }
    }
    public class SvpVoice {
        public bool vocalModeInherited { get; set; }
        public string? vocalModePreset { get; set; }
        public Dictionary<string, object>? vocalModeParams { get; set; }
    }
    public class SvpRenderConfig {
        public string? destination { get; set; }
        public string? filename { get; set; }
        public int numChannels { get; set; }
        public string? aspirationFormat { get; set; }
        public int bitDepth { get; set; }
        public int sampleRate { get; set; }
        public bool exportMixDown { get; set; }
        public bool exportPitch { get; set; }
    }
    internal static class MathExtension {
        public static double Clamp(this double value, double min, double max) {
            return Math.Max(min, Math.Min(value, max));
        }
    }
    internal class CoreTempo {
        public long tickPosition { get; set; }
        public double bpm { get; set; }
        public CoreTempo(long tickPosition, double bpm) {
            this.tickPosition = tickPosition;
            this.bpm = bpm;
        }
    }
    internal class TickTimeTransformer {
        private List<CoreTempo> tempos;
        public TickTimeTransformer(List<CoreTempo> tempos) {
            this.tempos = tempos;
        }
        public double tickToSec(long tick) {
            var lastTempo = this.tempos.LastOrDefault(tempo => tempo.tickPosition <= tick) ?? new CoreTempo(0, 120);
            return TempoHelper.bpmToSecPerTick(lastTempo.bpm) * (tick - lastTempo.tickPosition);
        }
        public long secToTick(double sec) {
            var tempo = this.tempos[0];
            if (this.tempos.Count > 1) {
                double tickPosition = 0;
                for (int i = 1; i < tempos.Count; i++) {
                    var previousTempo = this.tempos[i - 1];
                    var currentTempo = this.tempos[i];
                    var sectionTime = (currentTempo.tickPosition - previousTempo.tickPosition) * TempoHelper.bpmToSecPerTick(previousTempo.bpm);
                    if (sec >= sectionTime) {
                        sec -= sectionTime;
                        tickPosition = currentTempo.tickPosition;
                    } else {
                        return (long)Math.Round(tickPosition + sec / TempoHelper.bpmToSecPerTick(previousTempo.bpm));
                    }
                }
                tickPosition = this.tempos[^1].tickPosition;
                tempo = this.tempos[^1];
                return (long)Math.Round(tickPosition + sec / TempoHelper.bpmToSecPerTick(tempo.bpm));
            } else {
                return (long)Math.Round(sec / TempoHelper.bpmToSecPerTick(tempo.bpm));
            }
        }
    }

    internal static class TempoHelper {
        public static double bpmToSecPerTick(this double bpm) {
            return 60.0 / bpm / 1470000L;
        }
    }
    public class RangeLong {
        public long First { get; set; }
        public long Last { get; set; }
        public RangeLong(long first, long last) {
            this.First = first;
            this.Last = last;
        }
        public bool Contains(long value) {
            return (value >= First && value < Last);
        }
    }
    public class Pair<T, K> {
        public T First { get; set; }
        public K Second { get; set; }
        public Pair(T first, K second) {
            this.First = first;
            this.Second = second;
        }
    }
    internal class SvpDefaultVibratoParameters {
        public double? vibratoStart { get; set; }
        public double? easeInLength { get; set; }
        public double? easeOutLength { get; set; }
        public double? depth { get; set; }
        public double? frequency { get; set; }
        public SvpDefaultVibratoParameters(
           double? vibratoStart,
            double? easeInLength,
           double? easeOutLength,
          double? depth,
          double? frequency) {
            this.vibratoStart = vibratoStart;
            this.easeInLength = easeInLength;
            this.easeOutLength = easeOutLength;
            this.depth = depth;
            this.frequency = frequency;
        }
    }
    internal class SvpNoteWithVibrato {
        public long noteStartTick { get; set; }
        public long noteLengthTick { get; set; }
        public double? vibratoStart { get; set; }
        public double? easeInLength { get; set; }
        public double? easeOutLength { get; set; }
        public double? depth { get; set; }
        public double? frequency { get; set; }
        public double? phase { get; set; }
        public SvpNoteWithVibrato(
             long noteStartTick,
          long noteLengthTick,
               double? vibratoStart,
           double? easeInLength,
           double? easeOutLength,
           double? depth,
             double? frequency,
            double? phase
              ) {
            this.noteStartTick = noteStartTick;
            this.noteLengthTick = noteLengthTick;
            this.vibratoStart = vibratoStart;
            this.easeInLength = easeInLength;
            this.easeOutLength = easeOutLength;
            this.depth = depth;
            this.frequency = frequency;
            this.phase = phase;
        }
    }
}

