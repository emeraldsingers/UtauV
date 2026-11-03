using System.Linq;
using Xunit;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkMelWarpTest {
        static float[][] Ramp(int frames) {
            return Enumerable.Range(0, frames).Select(i => new[] { (float)i, (float)(i * 2) }).ToArray();
        }

        [Fact]
        public void EqualLengthsKeepEveryFrame() {
            var indexes = CoeiroinkMelWarp.SourceIndexes(6, 6, true);
            Assert.Equal(new double[] { 0, 1, 2, 3, 4, 5 }, indexes);
        }

        [Fact]
        public void SustainKeepsEdgesAndSpreadsTheMiddle() {
            var indexes = CoeiroinkMelWarp.SourceIndexes(10, 30, true);
            Assert.Equal(30, indexes.Length);
            Assert.Equal(new double[] { 0, 1, 2 }, indexes.Take(3).ToArray());
            Assert.Equal(new double[] { 7, 8, 9 }, indexes.Skip(27).ToArray());
            var middle = indexes.Skip(3).Take(24).ToArray();
            Assert.Equal(3, middle.First());
            Assert.Equal(6, middle.Last());
            Assert.True(middle.Zip(middle.Skip(1), (a, b) => b >= a).All(x => x));
        }

        [Fact]
        public void NonSustainedTokensStretchLinearly() {
            var indexes = CoeiroinkMelWarp.SourceIndexes(2, 5, false);
            Assert.Equal(0, indexes.First());
            Assert.Equal(1, indexes.Last());
            Assert.Equal(0.5, indexes[2], 6);
        }

        [Fact]
        public void WarpProducesTheOutputLengthAndStaysInsideTheSourceRange() {
            var mel = Ramp(14);
            var model = new[] { 2, 10, 2 };
            var output = new[] { 5, 30, 4 };
            var warped = CoeiroinkMelWarp.Warp(mel, model, output, new[] { false, true, false });
            Assert.Equal(39, warped.Length);
            Assert.Equal(mel[2][0], warped[5][0]);
            Assert.Equal(mel[11][0], warped[5 + 29][0]);
            Assert.All(warped.Skip(5).Take(30), row => Assert.InRange(row[0], 2f, 11f));
            Assert.All(warped.Skip(5).Take(30), row => Assert.Equal(row[0] * 2, row[1], 3));
        }

        [Fact]
        public void OutputPositionsMapModelFramesOntoTheOutputTimeline() {
            var positions = CoeiroinkMelWarp.OutputPositions(new[] { 2, 4 }, new[] { 2, 8 });
            Assert.Equal(6, positions.Length);
            Assert.Equal(0, positions[0], 6);
            Assert.Equal(1, positions[1], 6);
            Assert.Equal(2.5, positions[2], 6);
            Assert.Equal(8.5, positions[5], 6);
        }

        [Fact]
        public void ShortSourceFramesAreRepeatedWhenMelRunsShort() {
            var mel = Ramp(3);
            var warped = CoeiroinkMelWarp.Warp(mel, new[] { 2, 2 }, new[] { 2, 2 }, new[] { false, false });
            Assert.Equal(4, warped.Length);
            Assert.Equal(mel[2][0], warped[3][0]);
        }
    }
}
