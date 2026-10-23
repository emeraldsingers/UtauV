using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau;
using OpenUtau.App.ViewModels;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Format;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test {
    public class HarmGenTest {
        [Fact]
        public void DetectKeysPrefersCmajorForDiatonicPhrase() {
            var notes = new List<(int Tone, int Duration)> {
                (60, 960), (64, 480), (67, 960), (62, 480),
                (65, 480), (69, 480), (71, 480), (60, 960),
            };

            var result = HarmonyAnalysis.DetectKeys(notes, 1).Single();

            Assert.Equal(0, result.KeyIndex);
            Assert.Equal("major", result.Mode);
            Assert.InRange(result.Confidence, 0.0, 1.0);
        }

        [Fact]
        public void ChordSequenceUsesContextInsteadOfRootEnumerationOrder() {
            var part = new UVoicePart { position = 0, notes = new SortedSet<UNote>() };
            var notes = new List<NoteEvent> {
                CreateEvent(part, 60, 0),
                CreateEvent(part, 64, 0),
                CreateEvent(part, 67, 0),
                CreateEvent(part, 67, 480),
                CreateEvent(part, 71, 480),
                CreateEvent(part, 74, 480),
            };

            var windows = HarmGenEdit.EstimateChords(
                notes,
                keyIndex: 0,
                mode: "major",
                windowTicks: 480,
                allowSus: true,
                allowSevenths: true,
                confidenceThreshold: 0.45,
                strictDiatonic: true,
                ticksPerBeat: 480,
                beatsPerBar: 4,
                beatUnit: 4,
                strongBeatWeight: 1,
                weakBeatWeight: 0.5);

            Assert.Equal(2, windows.Count);
            Assert.Equal(0, windows[0].Chord.Root);
            Assert.Equal("maj", windows[0].Chord.Quality);
            Assert.Equal(7, windows[1].Chord.Root);
            Assert.Equal("maj", windows[1].Chord.Quality);
        }

        [Fact]
        public void CompoundPreferredIntervalsAreNotReducedToUnison() {
            var style = StyleProfiles.GetProfile("Cinematic");

            Assert.NotNull(style);
            Assert.Equal(0, HarmGenEdit.IntervalCost(12, style!));
            Assert.Equal(0, HarmGenEdit.IntervalCost(19, style!));
        }

        [Fact]
        public void ManualMinorAllowsMajorDominant() {
            var part = new UVoicePart { position = 0, notes = new SortedSet<UNote>() };
            var notes = new List<NoteEvent> {
                CreateEvent(part, 64, 0),
                CreateEvent(part, 68, 0),
                CreateEvent(part, 71, 0),
            };

            var chord = HarmGenEdit.EstimateChords(
                notes, 9, "minor", 480, true, true, 0.45, true,
                480, 4, 4, 1, 0.5).Single().Chord;

            Assert.Equal(4, chord.Root);
            Assert.Equal("maj", chord.Quality);
        }

        [Fact]
        public void SingleMelodyNoteHasLowChordConfidence() {
            var part = new UVoicePart { position = 0, notes = new SortedSet<UNote>() };
            var notes = new List<NoteEvent> { CreateEvent(part, 64, 0) };

            var window = HarmGenEdit.EstimateChords(
                notes, 0, "major", 480, true, true, 0.45, true,
                480, 4, 4, 1, 0.5).Single();

            Assert.True(window.LowConfidence);
            Assert.InRange(window.Confidence, 0, 0.45);
        }

        [Fact]
        public void SustainedChoirCreatesLongLyricAPadNotes() {
            var part = new UVoicePart { position = 0, notes = new SortedSet<UNote>() };
            var target = new List<NoteEvent> {
                CreateEvent(part, 60, 0),
                CreateEvent(part, 64, 480),
            };
            var chords = new List<ChordWindow> {
                new ChordWindow(0, 480, new ChordInfo(0, "maj", new List<int> { 0, 4, 7 }), 1, false),
                new ChordWindow(480, 960, new ChordInfo(0, "maj", new List<int> { 0, 4, 7 }), 1, false),
            };
            var style = StyleProfiles.GetProfile("Choir")!;
            var config = new HarmonyConfig(
                4, style, 0, "major", true, new List<int> { 0, 2, 4, 5, 7, 9, 11 },
                480, 4, 4, 480, true, true, 0.45, true, 0, 0, 4, false,
                "", 0, 0, 0, true,
                new List<(int, int)> { (-19, -10), (-9, -3), (3, 9), (10, 19) }, 1, 0.5);

            var result = HarmGenEdit.GenerateSustainedHarmonies(target, chords, config, "");

            Assert.Equal(4, result.Count);
            Assert.All(result, voice => {
                Assert.Single(voice);
                Assert.Equal(960, voice[0].duration);
                Assert.All(voice, note => Assert.Equal("a", note.lyric));
            });
        }

        private static NoteEvent CreateEvent(UVoicePart part, int tone, int position) {
            var note = UNote.Create();
            note.tone = tone;
            note.position = position;
            note.duration = 480;
            part.notes.Add(note);
            return new NoteEvent(
                tone,
                position,
                note.duration,
                part.position + position,
                note.lyric,
                part,
                note,
                part.notes.Count - 1,
                0);
        }
    }

    /// <summary>
    /// Runs the whole edit on a project: two new tracks, one part each, notes that
    /// stay inside their voice ranges and on the scale of the chosen key.
    /// </summary>
    [Collection(RenderSingletonCollection.Name)]
    public class HarmGenEndToEndTest : IDisposable {
        readonly UProject previousProject;
        readonly List<UCommand> executed = new List<UCommand>();

        public HarmGenEndToEndTest() {
            previousProject = DocManager.Inst.TakeProjectForTest(BuildProject());
            DocManager.Inst.CommandSink = cmd => {
                cmd.Execute();
                executed.Add(cmd);
            };
        }

        public void Dispose() {
            DocManager.Inst.CommandSink = null!;
            DocManager.Inst.TakeProjectForTest(previousProject);
        }

        [Fact]
        public void RunAddsOneTrackPerVoiceWithNotesOnTheScale() {
            var project = DocManager.Inst.Project;
            var part = project.parts.OfType<UVoicePart>().Single();
            var config = new HarmGenConfig {
                TrackIndexes = new List<int> { 0 },
                NumVoices = 2,
                StyleName = "Pop",
                UseManualKey = true,
                ManualKeyIndex = 0,
                ManualMode = "major",
                UseDefaultRanges = true,
                RangesRelative = true,
                AvoidCrossing = true,
            };

            new HarmGenEdit(config).Run(project, part, new List<UNote>(), DocManager.Inst);

            Assert.Equal(3, project.tracks.Count);
            var scale = new HashSet<int> { 0, 2, 4, 5, 7, 9, 11 };
            for (int voice = 0; voice < 2; voice++) {
                var track = project.tracks[voice + 1];
                Assert.EndsWith($"Harmony {voice + 1}", track.TrackName);
                Assert.Equal(voice + 1, track.TrackNo);
                var harmonyPart = project.parts.OfType<UVoicePart>().Single(p => p.trackNo == track.TrackNo);
                Assert.NotEmpty(harmonyPart.notes);
                Assert.All(harmonyPart.notes, note => {
                    Assert.Contains(((note.tone % 12) + 12) % 12, scale);
                    Assert.InRange(note.tone, part.notes.Min(n => n.tone) - 24, part.notes.Max(n => n.tone) + 24);
                });
            }
            Assert.Equal(-5.0, project.tracks[1].Volume);
        }

        [Fact]
        public void RunHonoursTheSelectionInsteadOfTheWholePart() {
            var project = DocManager.Inst.Project;
            var part = project.parts.OfType<UVoicePart>().Single();
            var notes = part.notes.ToList();
            var config = new HarmGenConfig {
                TrackIndexes = new List<int> { 0 },
                NumVoices = 1,
                UseManualKey = true,
                ManualKeyIndex = 0,
                ManualMode = "major",
                UseDefaultRanges = true,
                RangesRelative = true,
            };

            new HarmGenEdit(config).Run(project, part, new List<UNote> { notes[1] }, DocManager.Inst);

            var harmonyPart = project.parts.OfType<UVoicePart>().Single(p => p.trackNo == 1);
            Assert.Single(harmonyPart.notes);
        }

        [Fact]
        public void RunIgnoresTrackIndexesOutsideTheProject() {
            var project = DocManager.Inst.Project;
            var part = project.parts.OfType<UVoicePart>().Single();
            var config = new HarmGenConfig {
                TrackIndexes = new List<int> { 5 },
                NumVoices = 2,
                UseManualKey = true,
            };

            new HarmGenEdit(config).Run(project, part, new List<UNote>(), DocManager.Inst);

            Assert.Single(project.tracks);
        }

        [Fact]
        public void RunGeneratesEightVoicesWhenAsked() {
            var project = DocManager.Inst.Project;
            var part = project.parts.OfType<UVoicePart>().Single();
            var config = new HarmGenConfig {
                TrackIndexes = new List<int> { 0 },
                NumVoices = 8,
                StyleName = "Pop",
                UseManualKey = true,
                ManualKeyIndex = 0,
                ManualMode = "major",
                UseDefaultRanges = true,
                RangesRelative = true,
            };

            new HarmGenEdit(config).Run(project, part, new List<UNote>(), DocManager.Inst);

            Assert.Equal(9, project.tracks.Count);
            var names = project.tracks.Skip(1).Select(track => track.TrackName).ToList();
            Assert.Equal(8, names.Distinct().Count());
            Assert.All(names, name => Assert.StartsWith("Lead - ", name));
        }

        static UProject BuildProject() {
            var project = new UProject();
            Ustx.AddDefaultExpressions(project);
            project.RegisterExpression(new UExpressionDescriptor("velocity", "vel", 0, 100, 100));
            project.RegisterExpression(new UExpressionDescriptor("volume", "vol", 0, 100, 100));
            var track = project.tracks[0];
            track.TrackName = "Lead";
            track.Singer = new HarmGenTestSinger();

            var part = new UVoicePart { trackNo = 0, position = 0 };
            int[] tones = { 60, 64, 67, 62, 65, 69, 71, 72 };
            for (int i = 0; i < tones.Length; i++) {
                var note = UNote.Create();
                note.position = i * 480;
                note.duration = 480;
                note.tone = tones[i];
                note.lyric = "a";
                note.ExtendedDuration = 480;
                note.pitch.snapFirst = false;
                part.notes.Add(note);
            }
            part.Duration = tones.Length * 480;
            project.parts.Add(part);
            return project;
        }

        class HarmGenTestSinger : USinger {
            public HarmGenTestSinger() {
                found = true;
                loaded = true;
            }
            public override string Id => "harmgen-test-singer";
            public override USingerType SingerType => USingerType.Classic;
            public override IList<USubbank> Subbanks => new List<USubbank> { new USubbank(new Subbank()) };
            public override IList<UOto> Otos => new List<UOto>();
        }
    }

    [Collection(RenderSingletonCollection.Name)]
    public class HarmGenViewModelTest {
        [Fact]
        public void VoiceRangesFollowTheVoiceCount() {
            var viewModel = new HarmGenViewModel(new UProject(), 0);

            Assert.Equal(2, viewModel.VoiceRanges.Count);

            viewModel.NumVoices = 8;

            Assert.Equal(8, viewModel.VoiceCount);
            Assert.Equal(8, viewModel.VoiceRanges.Count);
            Assert.Equal(8, viewModel.VoiceRanges.Select(range => range.Name).Distinct().Count());

            viewModel.NumVoices = 1;

            Assert.Single(viewModel.VoiceRanges);
        }

        [Fact]
        public void BuildConfigCarriesOneRangePerVoice() {
            var viewModel = new HarmGenViewModel(new UProject(), 0);
            viewModel.NumVoices = 5;

            var config = viewModel.BuildConfig();

            Assert.Equal(5, config.NumVoices);
            Assert.Equal(5, config.VoiceRanges.Count);
            Assert.Equal(viewModel.VoiceRanges.Select(range => (range.Min, range.Max)), config.VoiceRanges);
            Assert.False(config.SustainedChoir);
        }

        [Fact]
        public void CustomRangesSurviveAVoiceCountChange() {
            var viewModel = new HarmGenViewModel(new UProject(), 0);
            viewModel.NumVoices = 3;
            viewModel.UseDefaultRanges = false;
            viewModel.VoiceRanges[0].Min = -5;
            viewModel.VoiceRanges[0].Max = 5;

            viewModel.NumVoices = 6;

            Assert.Equal(6, viewModel.VoiceRanges.Count);
            Assert.Equal(-5, viewModel.VoiceRanges[0].Min);
            Assert.Equal(5, viewModel.VoiceRanges[0].Max);
            Assert.All(viewModel.VoiceRanges.Skip(3), range => Assert.NotEqual(0, range.Max));
        }
    }
}
