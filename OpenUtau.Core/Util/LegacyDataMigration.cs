using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace OpenUtau.Core.Util {
    public readonly record struct LegacyDataProgress(int Done, int Total, string Current);

    public class LegacyDataMigration {
        static readonly string[] entries = {
            "Singers", "Content", "Resamplers", "Wavtools", "Dependencies", "Plugins",
            "Dictionaries", "Templates", "Themes", "SingerThemes", "Backups", "Logs", "notepresets.json",
        };

        static readonly string[] rootEntries = { "prefs.json", "note-defaults.json" };

        readonly string dataPath;
        readonly string rootPath;
        readonly string[] sources;

        public LegacyDataMigration(string dataPath, IEnumerable<string> sources, string? rootPath = null) {
            this.dataPath = Path.TrimEndingDirectorySeparator(dataPath);
            this.rootPath = Path.TrimEndingDirectorySeparator(rootPath ?? Directory.GetParent(this.dataPath)!.FullName);
            this.sources = sources
                .Select(Path.TrimEndingDirectorySeparator)
                .Where(s => !string.Equals(s, this.dataPath, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static LegacyDataMigration ForCurrent() {
            return new LegacyDataMigration(PathManager.Inst.DataPath, PathManager.Inst.LegacyDataSources, PathManager.Inst.RootPath);
        }

        public bool IsNeeded() {
            try {
                return sources.Any(source => entries.Any(entry => {
                    string path = Path.Combine(source, entry);
                    return File.Exists(path) || Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
                }) || IsRootEntryPending(source));
            } catch (Exception e) {
                Log.Error(e, "Failed to probe legacy data.");
                return false;
            }
        }

        record Unit(string From, string To, string Label);

        bool IsRootEntryPending(string source) {
            return !SamePath(source, rootPath) && rootEntries.Any(entry =>
                File.Exists(Path.Combine(source, entry)) && !File.Exists(Path.Combine(rootPath, entry)));
        }

        static bool SamePath(string a, string b) {
            return string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b),
                StringComparison.OrdinalIgnoreCase);
        }

        List<Unit> CollectUnits() {
            var units = new List<Unit>();
            foreach (var source in sources) {
                if (!SamePath(source, rootPath)) {
                    foreach (var entry in rootEntries) {
                        string from = Path.Combine(source, entry);
                        if (File.Exists(from) && !File.Exists(Path.Combine(rootPath, entry))) {
                            units.Add(new Unit(from, Path.Combine(rootPath, entry), entry));
                        }
                    }
                }
                foreach (var entry in entries) {
                    string from = Path.Combine(source, entry);
                    string to = Path.Combine(dataPath, entry);
                    if (File.Exists(from)) {
                        units.Add(new Unit(from, to, entry));
                    } else if (Directory.Exists(from)) {
                        foreach (var child in Directory.EnumerateFileSystemEntries(from)) {
                            string name = Path.GetFileName(child);
                            units.Add(new Unit(child, Path.Combine(to, name), Path.Combine(entry, name)));
                        }
                        if (!Directory.EnumerateFileSystemEntries(from).Any()) {
                            TryDeleteEmpty(from);
                        }
                    }
                }
            }
            return units;
        }

        public void Run(IProgress<LegacyDataProgress>? progress = null) {
            var units = CollectUnits();
            Directory.CreateDirectory(dataPath);
            for (int i = 0; i < units.Count; i++) {
                var unit = units[i];
                progress?.Report(new LegacyDataProgress(i, units.Count, unit.Label));
                try {
                    MoveUnit(unit);
                } catch (Exception e) {
                    Log.Error(e, $"Failed to migrate {unit.From}");
                }
            }
            foreach (var source in sources) {
                foreach (var entry in entries) {
                    TryDeleteEmpty(Path.Combine(source, entry));
                }
            }
            progress?.Report(new LegacyDataProgress(units.Count, units.Count, string.Empty));
        }

        static void MoveUnit(Unit unit) {
            if (File.Exists(unit.To) || Directory.Exists(unit.To)) {
                Log.Warning($"Skipped {unit.From}: {unit.To} already exists.");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(unit.To)!);
            if (File.Exists(unit.From)) {
                File.Move(unit.From, unit.To);
                return;
            }
            try {
                Directory.Move(unit.From, unit.To);
            } catch (IOException e) {
                Log.Warning(e, $"Rename failed for {unit.From}, copying instead.");
                CopyDirectory(unit.From, unit.To);
                Directory.Delete(unit.From, true);
            }
        }

        static void CopyDirectory(string from, string to) {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.EnumerateFiles(from)) {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.EnumerateDirectories(from)) {
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
            }
        }

        static void TryDeleteEmpty(string path) {
            try {
                if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) {
                    Directory.Delete(path);
                }
            } catch (Exception) {
            }
        }
    }
}
