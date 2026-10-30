using System;
using System.IO;
using System.Linq;
using System.Threading;
using OpenUtau.Classic;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Core.Coeiroink {
    [Collection(RenderSingletonCollection.Name)]
    public class CoeiroinkRendererTest {
        const string BankVariable = "COEIROINK_TEST_BANK";
        const string OutputVariable = "COEIROINK_TEST_OUT";

        static UProject NewProject(USinger singer) {
            var project = new UProject();
            project.RegisterExpression(new UExpressionDescriptor("engine", "eng", 0, 100, 0) { options = new[] { "" } });
            project.RegisterExpression(new UExpressionDescriptor("volume", "vol", 0, 100, 100));
            project.RegisterExpression(new UExpressionDescriptor("velocity", "vel", 0, 100, 100));
            project.RegisterExpression(new UExpressionDescriptor("modulation", "mod", 0, 100, 0));
            project.RegisterExpression(new UExpressionDescriptor("direct", "dir", 0, 100, 0));
            project.RegisterExpression(new UExpressionDescriptor("shift", "shft", 0, 100, 0));
            project.RegisterExpression(new UExpressionDescriptor("attack", "atk", 0, 100, 100));
            project.RegisterExpression(new UExpressionDescriptor("decay", "dec", 0, 100, 100));
            project.tracks.Clear();
            var track = new UTrack("Lead") { TrackNo = 0 };
            track.Singer = singer;
            track.RendererSettings.renderer = Renderers.COEIROINK;
            track.RendererSettings.Validate(track);
            project.tracks.Add(track);
            return project;
        }

        [Fact]
        public void RendersSungPhraseAtNotePitch() {
            var bank = Environment.GetEnvironmentVariable(BankVariable);
            if (string.IsNullOrEmpty(bank) || !Directory.Exists(bank)) {
                return;
            }
            ThreadGuard.SetUiThread(Thread.CurrentThread);
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var loader = new VoicebankLoader(Path.GetDirectoryName(bank)!);
            var characterFile = loader.FindCharacterFiles().First(f => Path.GetDirectoryName(f) == bank);
            var singer = ClassicSingerLoader.LoadSinger(Path.GetDirectoryName(bank)!, characterFile);
            singer.EnsureLoaded();
            Assert.Empty(singer.Errors);

            var project = NewProject(singer);
            var track = project.tracks[0];
            var part = new UVoicePart { name = "A", trackNo = 0, position = 0, duration = 1920 };
            project.parts.Add(part);
            project.timeAxis.BuildSegments(project);
            var lyrics = new[] { "こ", "ん", "に", "ち", "は" };
            var tones = new[] { 60, 62, 64, 62, 60 };
            for (int i = 0; i < lyrics.Length; i++) {
                var note = UNote.Create();
                note.position = i * 360;
                note.duration = 360;
                note.tone = tones[i];
                note.lyric = lyrics[i];
                note.ExtendedDuration = 360;
                part.notes.Add(note);
                if (i > 0) {
                    var previous = part.notes.ElementAt(i - 1);
                    previous.Next = note;
                    note.Prev = previous;
                }
                var phoneme = new UPhoneme { position = note.position, phoneme = lyrics[i], Parent = note };
                part.phonemes.Add(phoneme);
                phoneme.Validate(new ValidateOptions(), project, track, part, note);
                Assert.False(phoneme.Error, phoneme.ErrorException?.ToString());
            }
            var phrases = RenderPhrase.FromPart(project, track, part);
            Assert.Single(phrases);

            var renderer = new CoeiroinkRenderer();
            var result = renderer.Render(phrases[0], new Progress(1), 0, new CancellationTokenSource(), false).Result;
            Assert.NotNull(result.samples);
            Assert.True(result.samples.Length > 44100 * 0.8);
            Assert.True(result.samples.Max(Math.Abs) > 0.05f);

            var output = Environment.GetEnvironmentVariable(OutputVariable);
            if (!string.IsNullOrEmpty(output)) {
                Format.Wave.WriteMono16Wav(output, result.samples);
            }

            var f0 = Worldline.F0(result.samples, 44100, 5, 1);
            double startMs = result.positionMs - result.leadingMs;
            var measured = new System.Collections.Generic.List<string>();
            for (int i = 0; i < lyrics.Length; i++) {
                double centerMs = project.timeAxis.TickPosToMsPos(i * 360 + 220);
                int frame = (int)((centerMs - startMs) / 5);
                var window = f0.Skip(frame - 4).Take(9).Where(v => v > 0).ToArray();
                Assert.NotEmpty(window);
                double expected = MusicMath.ToneToFreq(tones[i]);
                double cents = 1200 * Math.Log2(window.Average() / expected);
                measured.Add($"{lyrics[i]}:{cents:F0}");
            }
            Assert.True(measured.All(m => Math.Abs(double.Parse(m.Split(':')[1])) <= 100), string.Join(" ", measured)); Assert.All(measured, m => Assert.InRange(double.Parse(m.Split(':')[1]), -100, 100));
        }
    }
}
