using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Api;
using OpenUtau.Core.Hts;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Core {
    [Collection(RenderSingletonCollection.Name)]
    public class HtsLabelTimingTest {
        class LabelRenderer : HTSLabelRenderer {
            public override USingerType SingerType => USingerType.Neutrino;

            public LabelRenderer(string dir) {
                vowels.Add("a");
                pauses.Add("pau");
                var builder = G2pDictionary.NewBuilder();
                builder.AddSymbol("a", true);
                builder.AddEntry("a", new[] { "a" });
                g2p = builder.Build();
                fullScorePath = Path.Join(dir, "full.lab");
                monoTimingPath = Path.Join(dir, "mono.lab");
            }

            public string[] Process(RenderPhrase phrase) {
                Layout(phrase);
                ProcessPart(phrase);
                return File.ReadAllLines(monoTimingPath);
            }

            public double HeadMs => headMs;

            public override bool SupportsExpression(UExpressionDescriptor descriptor) => false;
            public override void SetUp() { }
            protected override HTSPhoneme[] CustomHTSPhonemeContext(HTSPhoneme[] htsPhonemes, RenderNote notes) => htsPhonemes;
            public override Task<RenderResult> Render(RenderPhrase phrase, Progress progress, int trackNo, CancellationTokenSource cancellation, bool isPreRender, RenderPhraseEvents? renderEvents = null) => throw new NotSupportedException();
            public override UExpressionDescriptor[] GetSuggestedExpressions(USinger singer, URenderSettings renderSettings) => Array.Empty<UExpressionDescriptor>();
            public override string ToString() => "test";
            public override RenderPitchResult LoadRenderedPitch(RenderPhrase phrase) => null;
        }

        static RenderPhrase BuildPhrase(double bpm) {
            var project = new UProject();
            project.tempos[0].bpm = bpm;
            project.timeAxis.BuildSegments(project);
            foreach (var abbr in new[] { Format.Ustx.VEL, Format.Ustx.VOL, Format.Ustx.MOD, Format.Ustx.DIR, Format.Ustx.SHFT, Format.Ustx.CLR, Format.Ustx.XSYC }) {
                project.RegisterExpression(new UExpressionDescriptor(abbr, abbr, 0, 100, 0));
            }
            project.RegisterExpression(new UExpressionDescriptor("pitch deviation", Format.Ustx.PITD, -1200, 1200, 0) { type = UExpressionType.Curve });
            project.RegisterExpression(new UExpressionDescriptor("resampler", Format.Ustx.ENG, 0, 0, 0) {
                type = UExpressionType.Options,
                options = new[] { "default" },
            });
            var track = new UTrack(project);
            project.tracks.Add(track);
            var part = new UVoicePart { trackNo = 0 };
            project.parts.Add(part);
            var head = UNote.Create();
            head.position = 0;
            head.duration = 480;
            head.tone = 60;
            head.lyric = "a";
            var extension = UNote.Create();
            extension.position = 480;
            extension.duration = 480;
            extension.tone = 62;
            extension.lyric = "+";
            extension.Extends = head;
            head.Next = extension;
            extension.Prev = head;
            part.notes.Add(head);
            part.notes.Add(extension);
            var phrase = RenderPhrase.FromPart(project, track, part).First();
            var firstNote = phrase.phones[0].noteIndex;
            typeof(RenderPhrase).GetProperty(nameof(RenderPhrase.phones))!
                .SetValue(phrase, phrase.phones.Where(p => p.noteIndex == firstNote).ToArray());
            return phrase;
        }

        static long ExtensionStartTicks(string[] labels) {
            return labels.Select(l => l.Split(' ')).Where(f => f[2] == "a").Select(f => long.Parse(f[0])).Max();
        }

        [Fact]
        public void ExtensionNoteFollowsTheTempoOfEachPhrase() {
            var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try {
                var renderer = new LabelRenderer(dir);
                foreach (var bpm in new[] { 100.0, 60.0, 140.0, 100.0 }) {
                    var phrase = BuildPhrase(bpm);
                    var labels = renderer.Process(phrase);
                    double expectedMs = renderer.HeadMs + 480 * 60000.0 / bpm / 480;
                    Assert.Equal(expectedMs, ExtensionStartTicks(labels) / 10000.0, 1);
                }
            } finally {
                Directory.Delete(dir, true);
            }
        }
    }
}
