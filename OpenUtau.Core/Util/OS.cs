using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OpenUtau {
    public static class OS {
        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsAndroid() => OperatingSystem.IsAndroid();
        public static bool IsIOS() => OperatingSystem.IsIOS();


        public static void OpenFolder(string path) {
            if (Directory.Exists(path)) {
                Process.Start(new ProcessStartInfo {
                    FileName = GetOpener(),
                    Arguments = GetWrappedPath(path),
                });
            }
        }

        public static void GotoFile(string path) {
            if (File.Exists(path)) {
                var wrappedPath = GetWrappedPath(path);
                if (IsWindows()) {
                    Process.Start(new ProcessStartInfo {
                        FileName = GetOpener(),
                        Arguments = $"/select, {wrappedPath}",
                    });
                } else if (IsMacOS()) {
                    Process.Start(new ProcessStartInfo {
                        FileName = GetOpener(),
                        Arguments = $" -R {wrappedPath}",
                    });
                } else {
                    OpenFolder(Path.GetDirectoryName(path));
                }
            }
        }

        public static void OpenWeb(string url) {
            Process.Start(new ProcessStartInfo {
                FileName = GetOpener(),
                Arguments = url,
            });
        }

        public static bool AppExists(string path) {
            if (IsMacOS()) {
                return Directory.Exists(path) && path.EndsWith(".app");
            } else {
                return File.Exists(path);
            }
        }

        public static string GetUpdaterRid() {
            if (IsWindows()) {
                if (RuntimeInformation.ProcessArchitecture == Architecture.X86) {
                    return "win-x86";
                }
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                    return "win-arm64";
                }
#if ONNXRUNTIME_CUDA
                return "win-x64-cuda";
#else
                return "win-x64-directml";
#endif
            } else if (IsMacOS()) {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                    return "osx-arm64";
                }
                return "osx-x64";
            } else if (IsLinux()) {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64) {
                    return "linux-arm64";
                }
                return "linux-x64";
            }
            throw new NotSupportedException();
        }

        public static string WhereIs(string filename) {
            if (File.Exists(filename)) {
                return Path.GetFullPath(filename);
            }
            var values = Environment.GetEnvironmentVariable("PATH");
            foreach (var path in values.Split(Path.PathSeparator)) {
                var fullPath = Path.Combine(path, filename);
                if (File.Exists(fullPath)) {
                    return fullPath;
                }
            }
            return null;
        }

        private static readonly string[] wineNames = { "wine", "wine64", "wine32", "wine32on64" };
        private static readonly string[] macWineBundles = { "Wine Devel", "Wine Stable", "Wine Staging" };
        private static readonly string[] macWineFallbacks = { "/opt/homebrew/bin/wine", "/usr/local/bin/wine" };

        public static string FindWine() {
            foreach (var wineName in wineNames) {
                var path = WhereIs(wineName);
                if (!string.IsNullOrEmpty(path)) {
                    return path;
                }
            }
            if (!IsMacOS()) {
                return null;
            }
            var userApplications = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications");
            foreach (var root in new[] { "/Applications", userApplications }) {
                foreach (var bundle in macWineBundles) {
                    var path = Path.Combine(root, $"{bundle}.app", "Contents", "Resources", "wine", "bin", "wine");
                    if (File.Exists(path)) {
                        return path;
                    }
                }
            }
            foreach (var path in macWineFallbacks) {
                if (File.Exists(path)) {
                    return path;
                }
            }
            return null;
        }

        private static readonly string[] linuxOpeners ={ "xdg-open", "mimeopen", "gnome-open", "open" };
        private static string GetOpener() {
            if (IsWindows()) {
                return "explorer.exe";
            }
            if (IsMacOS()) {
                return "open";
            }
            foreach (var opener in linuxOpeners) {
                string fullPath = WhereIs(opener);
                if (!string.IsNullOrEmpty(fullPath)) {
                    return fullPath;
                }
            }
            throw new IOException($"None of {string.Join(", ", linuxOpeners)} found.");
        }
        private static string GetWrappedPath(string path) {
            if (IsWindows()) {
                return path;
            } else {
                return $"\"{path}\"";
            }
        }
    }
}
