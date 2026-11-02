using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Serilog;

namespace OpenUtau.Core.Util {
    public static class LayoutRepair {
        const string Disposable = "UtauV.exe";

        record Plan(string Root, string Deep, string Relative, string[] Parts);

        static Plan? Detect() {
            if (!OS.IsWindows() || PathManager.Inst.LegacyDataSources.Length == 0) {
                return null;
            }
            string root = Path.TrimEndingDirectorySeparator(PathManager.Inst.RootPath);
            string binRoot = Path.Combine(root, "bin");
            string deep = Path.TrimEndingDirectorySeparator(PathManager.Inst.BinaryPath);
            if (string.Equals(deep, binRoot, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }
            string prefix = binRoot + Path.DirectorySeparatorChar;
            if (!deep.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return null;
            }
            string relative = deep.Substring(prefix.Length);
            var parts = relative.Split(Path.DirectorySeparatorChar);
            if (parts.Any(part => !string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase))) {
                return null;
            }
            if (Directory.Exists(Path.Combine(root, "bin.old"))) {
                Log.Warning("Layout repair skipped: bin.old already exists.");
                return null;
            }
            return new Plan(root, deep, relative, parts);
        }

        public static bool IsNeeded() {
            try {
                return Detect() != null;
            } catch (Exception e) {
                Log.Error(e, "Failed to check layout.");
                return false;
            }
        }

        public static bool Schedule() {
            try {
                var plan = Detect();
                if (plan == null) {
                    return false;
                }
                bool cleanup = OnlyDisposableLeft(Path.Combine(plan.Root, "bin"), plan.Parts);
                string script = BuildScript(plan.Root, plan.Relative, cleanup);
                string scriptPath = Path.Combine(Path.GetTempPath(), $"utauv-layout-{Guid.NewGuid():N}.cmd");
                File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
                Process.Start(new ProcessStartInfo("cmd.exe") {
                    ArgumentList = { "/c", scriptPath },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                Log.Information($"Scheduled layout repair of {plan.Deep}, cleanup={cleanup}.");
                return true;
            } catch (Exception e) {
                Log.Error(e, "Failed to schedule layout repair.");
                return false;
            }
        }

        static bool OnlyDisposableLeft(string binRoot, string[] parts) {
            string dir = binRoot;
            foreach (var part in parts) {
                string next = Path.Combine(dir, part);
                foreach (var entry in Directory.EnumerateFileSystemEntries(dir)) {
                    if (string.Equals(entry, next, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    string name = Path.GetFileName(entry);
                    if (File.Exists(entry)) {
                        if (!string.Equals(name, Disposable, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(name, "installed.txt", StringComparison.OrdinalIgnoreCase)) {
                            return false;
                        }
                    } else if (!string.Equals(name, "Cache", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(name, "Logs", StringComparison.OrdinalIgnoreCase)
                        && Directory.EnumerateFileSystemEntries(entry).Any()) {
                        return false;
                    }
                }
                dir = next;
            }
            return true;
        }

        static string BuildScript(string root, string relative, bool cleanup) {
            using var process = Process.GetCurrentProcess();
            string imageName = Path.GetFileName(process.MainModule!.FileName);
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");
            sb.AppendLine(":wait");
            sb.AppendLine($"tasklist /FI \"PID eq {process.Id}\" /FI \"IMAGENAME eq {imageName}\" /NH | find /I \"{imageName}\" >nul");
            sb.AppendLine("if not errorlevel 1 (ping 127.0.0.1 -n 2 >nul & goto wait)");
            sb.AppendLine($"move \"{root}\\bin\" \"{root}\\bin.old\" >nul");
            sb.AppendLine("if errorlevel 1 goto end");
            sb.AppendLine($"move \"{root}\\bin.old\\{relative}\" \"{root}\\bin\" >nul");
            sb.AppendLine($"if errorlevel 1 move \"{root}\\bin.old\" \"{root}\\bin\" >nul");
            if (cleanup) {
                sb.AppendLine($"if exist \"{root}\\bin.old\" rmdir /s /q \"{root}\\bin.old\"");
            }
            sb.AppendLine(":end");
            sb.AppendLine("del \"%~f0\"");
            return sb.ToString();
        }
    }
}
