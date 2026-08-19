using System;
using System.IO;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Neutrino {
    public enum NeutrinoVersion {
        Unsupported,
        V27,
        V3,
    }

    /// <summary>
    /// Shared lookup of NEUTRINO installations placed under the dependency folder.
    /// </summary>
    public static class NeutrinoUtils {
        public const string ConfFile = "japanese.utf_8.conf";
        public const string TableFile = "japanese.utf_8.table";

        const string BaseDirName = "NEUTRINO";
        const string V27DirName = "NEUTRINO_v27";
        const string V3DirName = "NEUTRINO_v3";

        public static NeutrinoVersion DetectVersion(string singerVersion) {
            if (string.IsNullOrEmpty(singerVersion)) {
                return NeutrinoVersion.Unsupported;
            }
            if (singerVersion.StartsWith("v2.7")) {
                return NeutrinoVersion.V27;
            }
            if (singerVersion.StartsWith("v3") && !singerVersion.StartsWith("v3.1")) {
                return NeutrinoVersion.V3;
            }
            return NeutrinoVersion.Unsupported;
        }

        /// <summary>
        /// Resolves the NEUTRINO folder to use for a singer version. A folder named
        /// "NEUTRINO" takes precedence over the version specific folders.
        /// </summary>
        public static bool TryResolveBasePath(string singerVersion, out string basePath, out string error) {
            error = string.Empty;
            basePath = Path.Join(PathManager.Inst.DependencyPath, BaseDirName);
            if (Directory.Exists(basePath)) {
                return true;
            }
            string dirName;
            switch (DetectVersion(singerVersion)) {
                case NeutrinoVersion.V27:
                    dirName = V27DirName;
                    break;
                case NeutrinoVersion.V3:
                    dirName = V3DirName;
                    break;
                default:
                    error = $"Unsupported NEUTRINO version: {singerVersion}";
                    return false;
            }
            basePath = Path.Join(PathManager.Inst.DependencyPath, dirName);
            if (!Directory.Exists(basePath)) {
                error = $"NEUTRINO not found at {basePath}";
                return false;
            }
            return true;
        }

        public static string DicPath(string basePath, string fileName) {
            return Path.Join(basePath, "settings", "dic", fileName);
        }

        public static string ModelDir(USinger singer) {
            return singer.Location + "/";
        }
    }

    /// <summary>
    /// Executable paths of a NEUTRINO installation. Paths not available on the
    /// running platform are left empty.
    /// </summary>
    public class NeutrinoPaths {
        public string Neutrino = string.Empty;
        public string NeutrinoClient = string.Empty;
        public string NeutrinoServer = string.Empty;
        public string Nsf = string.Empty;
        public string World = string.Empty;
        public string VocoderClient = string.Empty;
        public string VocoderServer = string.Empty;

        public bool HasClient => !string.IsNullOrEmpty(NeutrinoClient) && File.Exists(NeutrinoClient);

        public static NeutrinoPaths Create(string basePath) {
            string binPath = Path.Join(basePath, "bin");
            if (OS.IsWindows()) {
                return new NeutrinoPaths {
                    Neutrino = Path.Join(binPath, "NEUTRINO.exe"),
                    NeutrinoClient = Path.Join(binPath, "neutrino_client.exe"),
                    NeutrinoServer = Path.Join(binPath, "neutrino_server.exe"),
                    Nsf = Path.Join(binPath, "NSF.exe"),
                    World = Path.Join(binPath, "WORLD.exe"),
                    VocoderClient = Path.Join(binPath, "vocoder_client.exe"),
                    VocoderServer = Path.Join(binPath, "vocoder_server.exe"),
                };
            } else if (OS.IsMacOS() || OS.IsLinux()) {
                return new NeutrinoPaths {
                    Neutrino = Path.Join(binPath, "NEUTRINO"),
                    Nsf = Path.Join(binPath, "NSF"),
                    World = Path.Join(binPath, "WORLD"),
                };
            }
            throw new NotSupportedException("Platform not supported.");
        }
    }
}
