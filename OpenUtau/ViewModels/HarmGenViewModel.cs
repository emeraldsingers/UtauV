using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;
using ReactiveUI;

namespace OpenUtau.App.ViewModels {
    public partial class HarmGenViewModel : ViewModelBase {
        public class TrackItem : ViewModelBase {
            bool isChecked;
            public bool IsChecked {
                get => isChecked;
                set => this.RaiseAndSetIfChanged(ref isChecked, value);
            }
            public int Index { get; }
            public string Name { get; }
            public TrackItem(int index, string name) {
                Index = index;
                Name = name;
            }
        }

        static readonly string[] KeyNames = {
            "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B"
        };

        readonly List<TrackItem> trackItems = new List<TrackItem>();
        readonly List<PhonemizerFactory?> phonemizerOptions = new List<PhonemizerFactory?>();

        public HarmGenViewModel(UProject project, int defaultTrackNo) {
            this.Project = project;

            for (int i = 0; i < project.tracks.Count; i++) {
                trackItems.Add(new TrackItem(i, $"{i + 1}: {project.tracks[i].TrackName}"));
            }
            Tracks = trackItems;
            foreach (var item in trackItems) {
                item.PropertyChanged += (_, e) => {
                    if (e.PropertyName == nameof(TrackItem.IsChecked)) {
                        this.RaisePropertyChanged(nameof(SelectedTrackCount));
                        this.RaisePropertyChanged(nameof(CanGenerate));
                        this.RaisePropertyChanged(nameof(Summary));
                        this.RaisePropertyChanged(nameof(DetectedKeyText));
                    }
                };
            }
            if (defaultTrackNo >= 0 && defaultTrackNo < trackItems.Count) {
                trackItems[defaultTrackNo].IsChecked = true;
            }

            phonemizerOptions.Add(null);
            phonemizerOptions.AddRange(PhonemizerFactory.GetAll());
            PhonemizerOptions = phonemizerOptions
                .Select(p => p == null
                    ? ThemeManager.GetString("dialogs.harmgen.phonemizer.track")
                    : p.ToString())
                .ToArray();

            StyleOptions = new[] {
                ThemeManager.GetString("dialogs.harmgen.style.pop"),
                ThemeManager.GetString("dialogs.harmgen.style.choir"),
                ThemeManager.GetString("dialogs.harmgen.style.cinematic"),
                ThemeManager.GetString("dialogs.harmgen.style.randomized"),
            };
            WindowOptions = new[] {
                ThemeManager.GetString("dialogs.harmgen.window.coarse"),
                ThemeManager.GetString("dialogs.harmgen.window.medium"),
                ThemeManager.GetString("dialogs.harmgen.window.fine"),
            };
            KeyOptions = KeyNames;
            ModeOptions = new[] {
                ThemeManager.GetString("dialogs.harmgen.key.mode.major"),
                ThemeManager.GetString("dialogs.harmgen.key.mode.minor"),
            };

            styleIndex = 0;
            windowIndex = 1;
            RefreshDefaultRanges();
        }

        public UProject Project { get; }
        public static string[] StyleNames => new[] { "Pop", "Choir", "Cinematic", "Randomized Human" };

        public List<TrackItem> Tracks { get; }
        public string[] PhonemizerOptions { get; }
        public string[] StyleOptions { get; }
        public string[] WindowOptions { get; }
        public string[] KeyOptions { get; }
        public string[] ModeOptions { get; }

        public int SelectedTrackCount => trackItems.Count(item => item.IsChecked);
        public bool CanGenerate => SelectedTrackCount > 0;
        public int VoiceCount => Math.Clamp(numVoices, SustainedChoir ? 4 : 1, SustainedChoir ? 8 : 4);
        public bool RangesEditable => !useDefaultRanges;
        public string StyleName => StyleNames[Math.Clamp(styleIndex, 0, StyleNames.Length - 1)];

        int numVoices = 2;
        public int NumVoices {
            get => numVoices;
            set {
                this.RaiseAndSetIfChanged(ref numVoices, value);
                RefreshDefaultRanges();
            }
        }

        int styleIndex;
        public int StyleIndex {
            get => styleIndex;
            set {
                this.RaiseAndSetIfChanged(ref styleIndex, value);
                RefreshDefaultRanges();
            }
        }

        int windowIndex;
        public int WindowIndex {
            get => windowIndex;
            set => this.RaiseAndSetIfChanged(ref windowIndex, value);
        }

        bool sustainedChoir;
        public bool SustainedChoir {
            get => sustainedChoir;
            set {
                this.RaiseAndSetIfChanged(ref sustainedChoir, value);
                if (value && numVoices < 4) {
                    NumVoices = 4;
                }
                RefreshDefaultRanges();
            }
        }

        int phonemizerIndex;
        public int PhonemizerIndex {
            get => phonemizerIndex;
            set => this.RaiseAndSetIfChanged(ref phonemizerIndex, value);
        }

        bool useManualKey;
        public bool UseManualKey {
            get => useManualKey;
            set => this.RaiseAndSetIfChanged(ref useManualKey, value);
        }

        int keyIndex;
        public int KeyIndex {
            get => keyIndex;
            set => this.RaiseAndSetIfChanged(ref keyIndex, value);
        }

        int modeIndex;
        public int ModeIndex {
            get => modeIndex;
            set => this.RaiseAndSetIfChanged(ref modeIndex, value);
        }

        bool allowSus = true;
        public bool AllowSus {
            get => allowSus;
            set => this.RaiseAndSetIfChanged(ref allowSus, value);
        }

        bool allowSevenths = true;
        public bool AllowSevenths {
            get => allowSevenths;
            set => this.RaiseAndSetIfChanged(ref allowSevenths, value);
        }

        bool avoidCrossing = true;
        public bool AvoidCrossing {
            get => avoidCrossing;
            set => this.RaiseAndSetIfChanged(ref avoidCrossing, value);
        }

        bool rangesRelative = true;
        public bool RangesRelative {
            get => rangesRelative;
            set {
                this.RaiseAndSetIfChanged(ref rangesRelative, value);
                if (!value && useDefaultRanges) {
                    UseDefaultRanges = false;
                }
            }
        }

        bool useDefaultRanges = true;
        public bool UseDefaultRanges {
            get => useDefaultRanges;
            set {
                this.RaiseAndSetIfChanged(ref useDefaultRanges, value);
                this.RaisePropertyChanged(nameof(RangesEditable));
                RefreshDefaultRanges();
            }
        }

        int range1Min;
        public int Range1Min {
            get => range1Min;
            set => this.RaiseAndSetIfChanged(ref range1Min, value);
        }

        int range1Max;
        public int Range1Max {
            get => range1Max;
            set => this.RaiseAndSetIfChanged(ref range1Max, value);
        }

        int range2Min;
        public int Range2Min {
            get => range2Min;
            set => this.RaiseAndSetIfChanged(ref range2Min, value);
        }

        int range2Max;
        public int Range2Max {
            get => range2Max;
            set => this.RaiseAndSetIfChanged(ref range2Max, value);
        }

        int range3Min;
        public int Range3Min {
            get => range3Min;
            set => this.RaiseAndSetIfChanged(ref range3Min, value);
        }

        int range3Max;
        public int Range3Max {
            get => range3Max;
            set => this.RaiseAndSetIfChanged(ref range3Max, value);
        }

        int range4Min;
        public int Range4Min {
            get => range4Min;
            set => this.RaiseAndSetIfChanged(ref range4Min, value);
        }

        int range4Max;
        public int Range4Max {
            get => range4Max;
            set => this.RaiseAndSetIfChanged(ref range4Max, value);
        }

        int densityMin;
        public int DensityMin {
            get => densityMin;
            set => this.RaiseAndSetIfChanged(ref densityMin, value);
        }

        int melismaDuration;
        public int MelismaDuration {
            get => melismaDuration;
            set => this.RaiseAndSetIfChanged(ref melismaDuration, value);
        }

        int melismaRun = 4;
        public int MelismaRun {
            get => melismaRun;
            set => this.RaiseAndSetIfChanged(ref melismaRun, value);
        }

        int humanizeTiming;
        public int HumanizeTiming {
            get => humanizeTiming;
            set => this.RaiseAndSetIfChanged(ref humanizeTiming, value);
        }

        int humanizeVelocity;
        public int HumanizeVelocity {
            get => humanizeVelocity;
            set => this.RaiseAndSetIfChanged(ref humanizeVelocity, value);
        }

        int seed;
        public int Seed {
            get => seed;
            set => this.RaiseAndSetIfChanged(ref seed, value);
        }

        double harmonyVolume = -5.0;
        public double HarmonyVolume {
            get => harmonyVolume;
            set => this.RaiseAndSetIfChanged(ref harmonyVolume, value);
        }

        bool blankLyrics;
        public bool BlankLyrics {
            get => blankLyrics;
            set => this.RaiseAndSetIfChanged(ref blankLyrics, value);
        }

        string lyricReplacement = string.Empty;
        public string LyricReplacement {
            get => lyricReplacement;
            set => this.RaiseAndSetIfChanged(ref lyricReplacement, value);
        }

        public string Summary {
            get {
                if (SelectedTrackCount == 0) {
                    return ThemeManager.GetString("dialogs.harmgen.summary.none");
                }
                return string.Format(
                    ThemeManager.GetString("dialogs.harmgen.summary"),
                    SelectedTrackCount,
                    VoiceCount,
                    SelectedTrackCount * VoiceCount);
            }
        }

        public string DetectedKeyText {
            get {
                if (useManualKey) {
                    return string.Empty;
                }
                var selected = trackItems.Where(item => item.IsChecked).Select(item => item.Index).ToHashSet();
                var notes = Project.parts
                    .OfType<UVoicePart>()
                    .Where(part => selected.Contains(part.trackNo))
                    .SelectMany(part => part.notes)
                    .ToList();
                if (notes.Count == 0) {
                    return ThemeManager.GetString("dialogs.harmgen.key.detected.none");
                }
                var ranked = HarmonyAnalysis.DetectKeys(notes.Select(note => (note.tone, note.duration)), 3);
                if (ranked.Count == 0) {
                    return ThemeManager.GetString("dialogs.harmgen.key.detected.none");
                }
                var best = ranked[0];
                string key = KeyNames[Math.Clamp(best.KeyIndex, 0, KeyNames.Length - 1)];
                string mode = best.Mode == "minor" ? ModeOptions[1] : ModeOptions[0];
                var alternatives = ranked
                    .Skip(1)
                    .Select(estimate => $"{KeyNames[Math.Clamp(estimate.KeyIndex, 0, KeyNames.Length - 1)]} " +
                        $"{(estimate.Mode == "minor" ? ModeOptions[1] : ModeOptions[0])} ({estimate.Confidence:P0})");
                string alternativeText = alternatives.Any() ? $" · {string.Join(" / ", alternatives)}" : string.Empty;
                return $"{ThemeManager.GetString("dialogs.harmgen.key.detected")} {key} {mode} ({best.Confidence:P0}){alternativeText}";
            }
        }

        public void SelectAllTracks() {
            foreach (var item in trackItems) {
                item.IsChecked = true;
            }
        }

        public void SelectNoTracks() {
            foreach (var item in trackItems) {
                item.IsChecked = false;
            }
        }

        public HarmGenConfig BuildConfig() {
            var ranges = new List<(int Min, int Max)> {
                (Range1Min, Range1Max),
                (Range2Min, Range2Max),
                (Range3Min, Range3Max),
                (Range4Min, Range4Max),
            };
            string? phonemizer = phonemizerIndex > 0 && phonemizerIndex < phonemizerOptions.Count
                ? phonemizerOptions[phonemizerIndex]?.type.FullName
                : null;
            return new HarmGenConfig {
                TrackIndexes = trackItems.Where(item => item.IsChecked).Select(item => item.Index).ToList(),
                NumVoices = VoiceCount,
                StyleName = StyleName,
                SustainedChoir = SustainedChoir,
                WindowSizeIndex = Math.Clamp(windowIndex, 0, 2),
                UseManualKey = UseManualKey,
                ManualKeyIndex = Math.Clamp(keyIndex, 0, 11),
                ManualMode = modeIndex == 1 ? "minor" : "major",
                AllowSus = AllowSus,
                AllowSevenths = AllowSevenths,
                AvoidCrossing = AvoidCrossing,
                RangesRelative = RangesRelative,
                UseDefaultRanges = UseDefaultRanges,
                VoiceRanges = ranges,
                DensityMinDuration = Math.Max(0, DensityMin),
                MelismaNoteDuration = Math.Max(0, MelismaDuration),
                MelismaRunLen = Math.Max(2, MelismaRun),
                HumanizeTiming = Math.Max(0, HumanizeTiming),
                HumanizeVelocity = Math.Max(0, HumanizeVelocity),
                Seed = Seed,
                HarmonyVolume = HarmonyVolume,
                BlankLyrics = BlankLyrics,
                LyricReplacement = LyricReplacement ?? string.Empty,
                PhonemizerFullName = phonemizer,
            };
        }

        void RefreshDefaultRanges() {
            this.RaisePropertyChanged(nameof(VoiceCount));
            this.RaisePropertyChanged(nameof(Summary));
            if (!useDefaultRanges) {
                return;
            }
            var ranges = DefaultVoiceRanges(StyleName, VoiceCount, SustainedChoir);
            SetRange(0, ranges);
            SetRange(1, ranges);
            SetRange(2, ranges);
            SetRange(3, ranges);
        }

        void SetRange(int voice, List<(int Min, int Max)> ranges) {
            int min = voice < ranges.Count ? ranges[voice].Min : 0;
            int max = voice < ranges.Count ? ranges[voice].Max : 0;
            switch (voice) {
                case 0: Range1Min = min; Range1Max = max; break;
                case 1: Range2Min = min; Range2Max = max; break;
                case 2: Range3Min = min; Range3Max = max; break;
                default: Range4Min = min; Range4Max = max; break;
            }
        }

        static List<(int Min, int Max)> DefaultVoiceRanges(string styleName, int numVoices, bool sustainedChoir) {
            if (sustainedChoir) {
                var pool = new List<(int, int)> {
                    (-24, -13), (-19, -10), (-14, -7), (-9, -3),
                    (3, 9), (7, 14), (10, 19), (15, 24),
                };
                if (numVoices <= 1) {
                    return new List<(int, int)> { pool[3] };
                }
                return Enumerable.Range(0, numVoices)
                    .Select(index => pool[(int)Math.Round(index * (pool.Count - 1.0) / (numVoices - 1))])
                    .ToList();
            }
            List<(int Min, int Max)> presets = styleName switch {
                "Cinematic" => new List<(int, int)> { (-12, -5), (7, 14), (12, 19), (-19, -12) },
                "Choir" => new List<(int, int)> { (-8, -3), (3, 8), (10, 16), (-16, -10) },
                "Randomized Human" => new List<(int, int)> { (-8, -3), (3, 9), (10, 17), (-16, -10) },
                _ => new List<(int, int)> { (-8, -3), (3, 8), (10, 16), (-16, -10) },
            };
            return presets.Take(numVoices).ToList();
        }
    }
}
