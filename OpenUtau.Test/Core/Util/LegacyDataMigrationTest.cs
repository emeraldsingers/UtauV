using System.IO;
using OpenUtau.Core.Util;
using Xunit;

namespace OpenUtau.Core.Util {
    public class LegacyDataMigrationTest {
        static string NewRoot() {
            string root = Path.Combine(Path.GetTempPath(), "utauv-migration-" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            return root;
        }

        [Fact]
        public void MovesEntriesFromBinToData() {
            string root = NewRoot();
            try {
                string bin = Path.Combine(root, "bin");
                string data = Path.Combine(root, "Data");
                Directory.CreateDirectory(Path.Combine(bin, "Singers", "Voice", "sub"));
                File.WriteAllText(Path.Combine(bin, "Singers", "Voice", "sub", "a.wav"), "a");
                Directory.CreateDirectory(Path.Combine(bin, "Resamplers"));
                File.WriteAllText(Path.Combine(bin, "Resamplers", "r.exe"), "r");
                File.WriteAllText(Path.Combine(bin, "UtauV.exe"), "exe");

                var migration = new LegacyDataMigration(data, new[] { bin, root });
                Assert.True(migration.IsNeeded());
                migration.Run();

                Assert.True(File.Exists(Path.Combine(data, "Singers", "Voice", "sub", "a.wav")));
                Assert.True(File.Exists(Path.Combine(data, "Resamplers", "r.exe")));
                Assert.True(File.Exists(Path.Combine(bin, "UtauV.exe")));
                Assert.False(Directory.Exists(Path.Combine(bin, "Singers")));
                Assert.False(migration.IsNeeded());
            } finally {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void KeepsExistingTargets() {
            string root = NewRoot();
            try {
                string bin = Path.Combine(root, "bin");
                string data = Path.Combine(root, "Data");
                Directory.CreateDirectory(Path.Combine(bin, "Singers", "Voice"));
                File.WriteAllText(Path.Combine(bin, "Singers", "Voice", "old.txt"), "old");
                Directory.CreateDirectory(Path.Combine(data, "Singers", "Voice"));
                File.WriteAllText(Path.Combine(data, "Singers", "Voice", "new.txt"), "new");

                new LegacyDataMigration(data, new[] { bin }).Run();

                Assert.True(File.Exists(Path.Combine(data, "Singers", "Voice", "new.txt")));
                Assert.False(File.Exists(Path.Combine(data, "Singers", "Voice", "old.txt")));
                Assert.True(File.Exists(Path.Combine(bin, "Singers", "Voice", "old.txt")));
            } finally {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void CollectsFromNestedBinChain() {
            string root = NewRoot();
            try {
                string bin1 = Path.Combine(root, "bin");
                string bin2 = Path.Combine(bin1, "bin");
                string data = Path.Combine(root, "Data");
                Directory.CreateDirectory(Path.Combine(bin1, "Singers", "A"));
                File.WriteAllText(Path.Combine(bin1, "Singers", "A", "a.txt"), "a");
                Directory.CreateDirectory(Path.Combine(bin2, "Singers", "B"));
                File.WriteAllText(Path.Combine(bin2, "Singers", "B", "b.txt"), "b");
                Directory.CreateDirectory(Path.Combine(bin2, "Logs"));
                File.WriteAllText(Path.Combine(bin2, "Logs", "log20200101.txt"), "l");
                File.WriteAllText(Path.Combine(bin1, "prefs.json"), "{}");

                new LegacyDataMigration(data, new[] { bin2, bin1, root }, root).Run();

                Assert.True(File.Exists(Path.Combine(data, "Singers", "A", "a.txt")));
                Assert.True(File.Exists(Path.Combine(data, "Singers", "B", "b.txt")));
                Assert.True(File.Exists(Path.Combine(data, "Logs", "log20200101.txt")));
                Assert.True(File.Exists(Path.Combine(root, "prefs.json")));
            } finally {
                Directory.Delete(root, true);
            }
        }
    }
}
