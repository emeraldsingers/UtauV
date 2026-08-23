using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using K4os.Hash.xxHash;
using Serilog;

namespace OpenUtau.Core.Util {
    public static class VoicebankFingerprint {
        static readonly HashSet<string> skippedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".wav", ".frq",
        };

        public static string Compute(string? location) {
            try {
                if (string.IsNullOrEmpty(location)) {
                    return null;
                }
                var entries = new List<(string path, long length, DateTime mtimeUtc)>();
                if (File.Exists(location)) {
                    AddEntry(entries, location, Path.GetFileName(location));
                } else if (Directory.Exists(location)) {
                    foreach (var file in Directory.EnumerateFiles(location, "*", SearchOption.AllDirectories)) {
                        if (skippedExtensions.Contains(Path.GetExtension(file))) {
                            continue;
                        }
                        AddEntry(entries, file, Path.GetRelativePath(location, file));
                    }
                } else {
                    return null;
                }

                entries.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
                using var stream = new MemoryStream();
                using (var writer = new BinaryWriter(stream)) {
                    writer.Write(entries.Count);
                    foreach (var entry in entries) {
                        writer.Write(entry.path);
                        writer.Write(entry.length);
                        writer.Write(entry.mtimeUtc.Ticks);
                    }
                }
                return XXH64.DigestOf(stream.ToArray()).ToString("x16");
            } catch (Exception e) {
                Log.Error(e, $"Failed to compute voicebank fingerprint for {location}");
                return null;
            }
        }

        static void AddEntry(List<(string path, long length, DateTime mtimeUtc)> entries, string file, string relativePath) {
            var info = new FileInfo(file);
            entries.Add((relativePath.Replace('\\', '/'), info.Length, info.LastWriteTimeUtc));
        }
    }
}
