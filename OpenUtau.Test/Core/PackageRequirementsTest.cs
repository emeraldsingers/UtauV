using System;
using OpenUtau.Classic.Hifisampler;
using OpenUtau.Core.Analysis;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using Xunit;

namespace OpenUtau.Core {
    public class PackageRequirementsTest {
        [Fact]
        public void EnginesNeedTheirPackages() {
            Assert.Equal(new[] { Hnsep.PackageId },
                PackageRequirements.For(new URenderSettings { renderer = Renderers.WORLDLINE_R11 }));
            Assert.Equal(new[] { HifiVocoder.PackageId },
                PackageRequirements.For(new URenderSettings { renderer = Renderers.WORLDLINE_R2 }));
            Assert.Equal(new[] { HifiVocoder.PackageId, Hnsep.PackageId },
                PackageRequirements.For(new URenderSettings { renderer = Renderers.CLASSIC, resampler = "hifisampler" }));
            Assert.Empty(PackageRequirements.For(new URenderSettings { renderer = Renderers.CLASSIC, resampler = "worldline" }));
            Assert.Empty(PackageRequirements.For(new URenderSettings { renderer = Renderers.WORLDLINE_R }));
        }

        [Fact]
        public void EnginePackagesAreInstallable() {
            Assert.Contains(Hnsep.PackageId, PackageRequirements.Installable);
            Assert.Contains(HifiVocoder.PackageId, PackageRequirements.Installable);
        }

        [Fact]
        public void CollectFindsMissingPackagesAnywhere() {
            var e = new AggregateException(
                new MissingPackageException("a"),
                new AggregateException(new InvalidOperationException("other", new MissingPackageException("b"))),
                // Rewrapped by a message that keeps only the translatable message and replaces.
                new MessageCustomizableException("Failed", "<translate:errors.failed.render>", new MissingPackageException("c", "a")),
                new MessageCustomizableException("Failed", "<translate:errors.failed.render>", new Exception("x", new MissingPackageException("d"))));
            Assert.Equal(new[] { "a", "b", "c", "d" }, MissingPackageException.Collect(e));
            Assert.Empty(MissingPackageException.Collect(new InvalidOperationException("other")));
            Assert.Empty(MissingPackageException.Collect(null));
        }
    }
}
