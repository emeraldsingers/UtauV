using System.Collections.Generic;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Test.Core.USTx {
    public class UTrackVoiceColorTest {
        class TestSinger : USinger {
            readonly List<USubbank> subbanks = new List<USubbank>();

            public TestSinger(params string[] colors) {
                found = true;
                loaded = true;
                foreach (var color in colors) {
                    subbanks.Add(new USubbank(new Subbank() { Color = color }));
                }
            }
            public override string Id => "test";
            public override string Name => "test";
            public override USingerType SingerType => USingerType.Classic;
            public override IList<USubbank> Subbanks => subbanks;
        }

        [Fact]
        public void SingerWithoutColorsDoesNotBreakVoiceColorValidation() {
            var project = new UProject();
            OpenUtau.Core.Format.Ustx.AddDefaultExpressions(project);
            var track = new UTrack("Track") { Singer = new TestSinger() };

            track.Validate(default, project);

            Assert.NotNull(track.VoiceColorExp);
            Assert.Empty(track.VoiceColorExp.options);
        }

        [Fact]
        public void VoiceColorDefaultIsClampedToSingerColors() {
            var project = new UProject();
            OpenUtau.Core.Format.Ustx.AddDefaultExpressions(project);
            project.expressions[OpenUtau.Core.Format.Ustx.CLR].CustomDefaultValue = 5;
            var track = new UTrack("Track") { Singer = new TestSinger("power", "whisper") };

            track.Validate(default, project);

            Assert.Equal(new[] { "power", "whisper" }, track.VoiceColorExp.options);
            Assert.Equal(1, track.VoiceColorExp.max);
            Assert.Equal(1, track.VoiceColorExp.CustomDefaultValue);
        }
    }
}
