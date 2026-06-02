using System.Linq;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core {
    public class RenderInvalidationTest {
        [Fact]
        public void NoteCommandReportsNoteRangeInProjectTicks() {
            var part = new UVoicePart { position = 100, Duration = 1000 };
            var note = new UNote { position = 200, duration = 300 };
            var command = new AddNoteCommand(part, note);

            var invalidation = Assert.Single(command.GetRenderInvalidations());

            Assert.Same(part, invalidation.part);
            Assert.Equal(300, invalidation.startTick);
            Assert.Equal(600, invalidation.endTick);
        }

        [Fact]
        public void SetCurveCommandReportsEditedRangeInProjectTicks() {
            var project = new UProject();
            var part = new UVoicePart { position = 100, Duration = 1000 };
            var command = new SetCurveCommand(
                project, part, Format.Ustx.PITD, x: 50, y: 0, lastX: 100, lastY: 0);

            var invalidation = Assert.Single(command.GetRenderInvalidations());

            Assert.Same(part, invalidation.part);
            Assert.Equal(150, invalidation.startTick);
            Assert.Equal(201, invalidation.endTick);
        }

        [Fact]
        public void MergedSetCurveCommandKeepsMergedEditRange() {
            var project = new UProject();
            var part = new UVoicePart { position = 100, Duration = 1000 };
            var command = new MergedSetCurveCommand(
                project,
                part,
                Format.Ustx.PITD,
                oldXs: new[] { 0, 400 },
                oldYs: new[] { 0, 0 },
                newXs: new[] { 0, 400 },
                newYs: new[] { 0, 0 },
                startTick: 50,
                endTick: 101);

            var invalidation = Assert.Single(command.GetRenderInvalidations());

            Assert.Equal(150, invalidation.startTick);
            Assert.Equal(201, invalidation.endTick);
        }
    }
}
