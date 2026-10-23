using System.Threading;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Core {
    [Collection(RenderSingletonCollection.Name)]
    public class SingerManagerTest {
        public SingerManagerTest() {
            ThreadGuard.SetUiThread(Thread.CurrentThread);
        }

        class TestSinger : USinger {
            public int FreeCount { get; private set; }
            public TestSinger(string id) {
                this.id = id;
                found = true;
            }
            public override string Id => id;
            public override bool HoldsMemory => true;
            public override void FreeMemory() {
                FreeCount++;
            }
            private readonly string id;
        }

        static UProject ProjectWithSingers(params USinger[] singers) {
            var project = new UProject();
            project.tracks.Clear();
            foreach (var singer in singers) {
                project.tracks.Add(new UTrack("Track") { Singer = singer });
            }
            return project;
        }

        [Fact]
        public void SingerLoadedAfterLastReleaseIsFreedWhenItLeavesUse() {
            var first = new TestSinger("first");
            var second = new TestSinger("second");
            var manager = SingerManager.Inst;

            var project = ProjectWithSingers(first);
            manager.MarkSingerUsed(first);
            manager.ReleaseSingersNotInUse(project);
            Assert.Equal(0, first.FreeCount);

            project.tracks[0].Singer = second;
            manager.MarkSingerUsed(second);
            manager.ReleaseSingersNotInUse(project);

            Assert.Equal(1, first.FreeCount);
            Assert.Equal(0, second.FreeCount);
        }

        [Fact]
        public void SingerNeverClaimedByATrackIsNotFreed() {
            var displayed = new TestSinger("displayed");
            var manager = SingerManager.Inst;

            var project = ProjectWithSingers(new TestSinger("used"));
            manager.MarkSingerUsed(new TestSinger("stale"));
            manager.ReleaseSingersNotInUse(project);

            Assert.Equal(0, displayed.FreeCount);
        }
    }
}
