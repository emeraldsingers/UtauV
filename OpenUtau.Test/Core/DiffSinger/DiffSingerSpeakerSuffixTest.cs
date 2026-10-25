using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.DiffSinger;
using OpenUtau.Core.Pipeline;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test.Core.DiffSinger {
    /// <summary>
    /// Regression tests for the "Speaker suffix not found in dsConfig.speakers" warning. A voicebank
    /// whose subbank suffixes match the dsconfig speaker list must never produce a suffix that cannot
    /// be resolved back to a speaker.
    /// </summary>
    [Collection(RenderSingletonCollection.Name)]
    public class DiffSingerSpeakerSuffixTest : IDisposable {
        static DiffSingerSpeakerSuffixTest() {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        class TestSinger : USinger {
            readonly List<USubbank> subbanks = new List<USubbank>();
            public TestSinger(params (string color, string suffix)[] banks) {
                found = true;
                loaded = true;
                foreach (var (color, suffix) in banks) {
                    subbanks.Add(new USubbank(new Subbank() { Color = color, Suffix = suffix }));
                }
            }
            public override string Id => "test-singer";
            public override string Name => "test";
            public override USingerType SingerType => USingerType.DiffSinger;
            public override IList<USubbank> Subbanks => subbanks;
            public override bool TryGetOto(string phoneme, out UOto oto) {
                oto = UOto.OfDummy(phoneme);
                return true;
            }
            public override bool TryGetMappedOto(string phoneme, int tone, string color, out UOto oto) {
                oto = UOto.OfDummy(phoneme);
                return true;
            }
        }

        class TestRenderer : IRenderer {
            public USingerType SingerType => USingerType.DiffSinger;
            public bool SupportsRenderPitch => true;
            public bool SupportsRealCurve => true;
            public bool SupportsExpression(UExpressionDescriptor descriptor) => true;
            public RenderResult Layout(RenderPhrase phrase) => new RenderResult {
                leadingMs = phrase.leadingMs,
                positionMs = phrase.positionMs,
                estimatedLengthMs = phrase.durationMs + phrase.leadingMs,
            };
            public Task<RenderResult> Render(RenderPhrase phrase, Progress progress, int trackNo,
                    CancellationTokenSource cancellation, bool isPreRender = false,
                    RenderPhraseEvents? renderEvents = null) => throw new NotImplementedException();
            public RenderPitchResult LoadRenderedPitch(RenderPhrase phrase) => null;
            public bool ShouldMergePhrases(UProject project, UTrack track, UPhoneme prev, UPhoneme next) => false;
            public (double HeadMs, double TailMs) PhrasePadding(USinger singer, IEnumerable<UPhoneme> phonemes) => (0, 0);
            public UExpressionDescriptor[] GetSuggestedExpressions(USinger singer, URenderSettings renderSettings) =>
                Array.Empty<UExpressionDescriptor>();
        }

        // Layout of a multi-speaker DiffSinger voicebank with subbanks in character.yaml and matching
        // speakers in dsconfig.yaml, without tone_ranges.
        static readonly (string, string)[] Banks = new[] {
            ("01CORE", "embeds/01CORE"),
            ("02BELT", "embeds/02BELT"),
            ("03GLOW", "embeds/03GLOW"),
            ("04DUSK", "embeds/04DUSK"),
            ("05OPEN", "embeds/05OPEN"),
            ("06AURA", "embeds/06AURA"),
            ("07MASK", "embeds/07MASK"),
        };

        static DsConfig DsConfig(params string[] speakers) {
            return new DsConfig {
                hiddenSize = 4,
                speakers = (speakers.Length > 0 ? speakers : Banks.Select(b => b.Item2)).ToList(),
            };
        }

        static DiffSingerSpeakerEmbedManager Manager(params string[] speakers) {
            return new DiffSingerSpeakerEmbedManager(DsConfig(speakers), "unused");
        }

        (UProject Project, UTrack Track, UVoicePart Part) BuildFixture(USinger singer, int voiceColor = 0) {
            var project = new UProject();
            OpenUtau.Core.Format.Ustx.AddDefaultExpressions(project);
            var track = project.tracks[0];
            track.Singer = singer;
            track.RendererSettings.Renderer = new TestRenderer();
            if (voiceColor != 0) {
                project.expressions[OpenUtau.Core.Format.Ustx.CLR].CustomDefaultValue = voiceColor;
            }
            track.Validate(default, project);

            var part = new UVoicePart { trackNo = 0, position = 0 };
            project.parts.Add(part);

            var note = UNote.Create();
            note.position = 0;
            note.duration = 480;
            note.tone = 60;
            note.lyric = "a";
            note.ExtendedDuration = 480;
            part.notes.Add(note);

            var phoneme = new UPhoneme { position = 0, phoneme = "A", Parent = note };
            part.phonemes.Add(phoneme);
            phoneme.Validate(new ValidateOptions(), project, track, part, note);
            Assert.False(phoneme.Error);
            return (project, track, part);
        }

        static string FirstPhoneSuffix(UProject project, UTrack track, UVoicePart part) {
            long generation = ++part.phraseGeneration;
            var source = PhraseSource.FromPart(project, track, part, generation);
            Assert.NotNull(source);
            var phrase = source.BuildPhrases()[0];
            return phrase.phones[0].suffix;
        }

        [Fact]
        public void SelectedVoiceColorResolvesToItsSubbankSuffix() {
            var (project, track, part) = BuildFixture(new TestSinger(Banks), voiceColor: 6);
            Assert.Equal("07MASK", track.VoiceColorExp.options[6]);
            Assert.Equal("embeds/07MASK", FirstPhoneSuffix(project, track, part));
            Assert.Equal(6, Manager().getSpeakerIndexBySuffix(FirstPhoneSuffix(project, track, part)));
        }

        [Fact]
        public void DefaultVoiceColorResolvesToFirstSubbankSuffix() {
            var (project, track, part) = BuildFixture(new TestSinger(Banks));
            Assert.Equal("embeds/01CORE", FirstPhoneSuffix(project, track, part));
            Assert.Equal(0, Manager().getSpeakerIndexBySuffix(FirstPhoneSuffix(project, track, part)));
        }

        [Fact]
        public void VoiceColorWithoutMatchingSubbankFallsBackToFirstSubbank() {
            // Voice colors are remapped when the singer is replaced, so the color stored on the track
            // can no longer match any subbank.
            var (project, track, part) = BuildFixture(new TestSinger(Banks), voiceColor: 6);
            track.Singer = new TestSinger(("A", "embeds/A"), ("B", "embeds/B"));
            Assert.Equal("embeds/A", FirstPhoneSuffix(project, track, part));
            Assert.Equal(0, Manager("embeds/A", "embeds/B").getSpeakerIndexBySuffix(FirstPhoneSuffix(project, track, part)));
        }

        [Fact]
        public void MissingVoiceColorExpressionFallsBackToFirstSubbank() {
            var (project, track, part) = BuildFixture(new TestSinger(Banks), voiceColor: 6);
            track.VoiceColorExp = null;
            Assert.Equal("embeds/01CORE", FirstPhoneSuffix(project, track, part));
            Assert.Equal(0, Manager().getSpeakerIndexBySuffix(FirstPhoneSuffix(project, track, part)));
        }

        [Fact]
        public void SubbanksWithoutToneRangesHaveEmptyToneSets() {
            var singer = new TestSinger(Banks);
            Assert.All(singer.Subbanks, subbank => Assert.Empty(subbank.toneSet));
        }

        [Fact]
        public void SpeakerIndexMatchesExactSuffix() {
            Assert.Equal(6, Manager().getSpeakerIndexBySuffix("embeds/07MASK"));
        }

        [Fact]
        public void SpeakerIndexMatchesSuffixByLastSegment() {
            // Subdirectory-organized .emb files: the subbank suffix may only name the last segment.
            Assert.Equal(6, Manager().getSpeakerIndexBySuffix("07MASK"));
        }

        [Fact]
        public void SpeakerIndexFallsBackToFirstSpeakerForUnknownSuffix() {
            Assert.Equal(0, Manager().getSpeakerIndexBySuffix("embeds/k3k0"));
        }

        [Fact]
        public void EmptySuffixResolvesToFirstSpeaker() {
            // A voicebank with no subbanks in character.yaml has no suffix to resolve; the first
            // speaker is used, as it was before the warning was introduced.
            Assert.Equal(0, Manager("dsmain/embeds/acoustic/standard", "dsmain/embeds/acoustic/power")
                .getSpeakerIndexBySuffix(string.Empty));
            Assert.Equal(0, Manager("dsmain/embeds/acoustic/standard", "dsmain/embeds/acoustic/power")
                .getSpeakerIndexBySuffix(null));
        }

        [Fact]
        public void VoicebankWithoutSubbanksHasNoSpeakerSuffix() {
            string dir = Path.Combine(Path.GetTempPath(), $"OpenUtau.SpeakerSuffix.{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            try {
                File.WriteAllText(Path.Combine(dir, VoicebankLoader.kCharTxt), "name=no-subbanks\n");
                File.WriteAllText(Path.Combine(dir, VoicebankLoader.kDsconfigYaml),
                    "acoustic: dsmain/acoustic.onnx\nphonemes: dsmain/phonemes.txt\n"
                    + "speakers:\n- dsmain/embeds/acoustic/standard\n- dsmain/embeds/acoustic/power\n");
                Directory.CreateDirectory(Path.Combine(dir, "dsmain"));
                File.WriteAllText(Path.Combine(dir, "dsmain", "phonemes.txt"), "A\n");
                var voicebank = new Voicebank {
                    File = Path.Combine(dir, VoicebankLoader.kCharTxt),
                    BasePath = dir,
                };
                VoicebankLoader.LoadVoicebank(voicebank);
                var singer = new DiffSingerSinger(voicebank);
                var subbank = Assert.Single(singer.Subbanks);
                Assert.Empty(subbank.Suffix);
                var (project, track, part) = BuildFixture(singer);
                Assert.Equal(string.Empty, FirstPhoneSuffix(project, track, part));
                Assert.Equal(0, Manager("dsmain/embeds/acoustic/standard", "dsmain/embeds/acoustic/power")
                    .getSpeakerIndexBySuffix(FirstPhoneSuffix(project, track, part)));
            } finally {
                Directory.Delete(dir, true);
            }
        }

        public void Dispose() {
        }
    }
}
