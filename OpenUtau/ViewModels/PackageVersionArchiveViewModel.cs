using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OpenUtau.Core;
using ReactiveUI.Fody.Helpers;

namespace OpenUtau.App.ViewModels {
    public class PackageVersionRowViewModel : ViewModelBase {
        public RegistryVersion VersionInfo { get; }
        public string Version => VersionInfo.version;
        public string DescriptionDisplay { get; }
        public bool IsLatest { get; }
        public bool IsInstalled { get; }
        public bool CanInstall => VersionInfo.mirrors != null && VersionInfo.mirrors.Length > 0;

        public PackageVersionRowViewModel(RegistryVersion versionInfo, string latestVersion, string installedVersion) {
            VersionInfo = versionInfo;
            DescriptionDisplay = string.IsNullOrWhiteSpace(versionInfo.LocalizedDescription())
                ? ThemeManager.GetString("packages.nodescription")
                : versionInfo.LocalizedDescription();
            IsLatest = !string.IsNullOrWhiteSpace(latestVersion) &&
                string.Equals(versionInfo.version, latestVersion, StringComparison.OrdinalIgnoreCase);
            IsInstalled = !string.IsNullOrWhiteSpace(installedVersion) &&
                string.Equals(versionInfo.version, installedVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    public class PackageVersionArchiveViewModel : ViewModelBase {
        class VersionStringComparer : IComparer<string> {
            public int Compare(string? x, string? y) {
                var a = x ?? string.Empty;
                var b = y ?? string.Empty;
                if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb)) {
                    return va.CompareTo(vb);
                }
                if (Version.TryParse(a, out _)) return 1;
                if (Version.TryParse(b, out _)) return -1;
                return string.Compare(a, b, StringComparison.Ordinal);
            }
        }

        static readonly IComparer<string> VersionComparer = new VersionStringComparer();

        public RegistrySoftware Software { get; }
        public ObservableCollection<PackageVersionRowViewModel> Versions { get; } = new ObservableCollection<PackageVersionRowViewModel>();
        [Reactive] public string Status { get; set; } = string.Empty;
        public string Header => $"{Software.LocalizedName()} ({Software.id})";
        public string DescriptionDisplay => string.IsNullOrWhiteSpace(Software.LocalizedDescription())
            ? ThemeManager.GetString("packages.nodescription")
            : Software.LocalizedDescription();
        public string LatestVersion { get; }
        public string InstalledVersion { get; }

        public PackageVersionArchiveViewModel() : this(new RegistrySoftware {
            id = "sample-package",
            names = new Dictionary<string, string> { { "en", "Sample Package" } },
            description = "Sample package description for version archive preview.",
            versions = new[] {
                new RegistryVersion {
                    version = "1.0.0",
                    description = "Newest release with all fixes.",
                    mirrors = new[] { new RegistryMirror { url = "https://example.com/sample.oudep" } },
                },
                new RegistryVersion {
                    version = "0.9.0",
                    description = "Legacy release.",
                    mirrors = new[] { new RegistryMirror { url = "https://example.com/sample-old.oudep" } },
                },
            },
        }, "0.9.0") { }

        public PackageVersionArchiveViewModel(RegistrySoftware software, string installedVersion = "") {
            Software = software;
            LatestVersion = PackageManager.GetLatestVersionString(software.versions ?? []);
            InstalledVersion = installedVersion ?? string.Empty;
            var rows = (software.versions ?? [])
                .Where(v => !string.IsNullOrWhiteSpace(v.version))
                .Where(v => v.mirrors != null && v.mirrors.Length > 0)
                .OrderByDescending(v => v.version, VersionComparer)
                .Select(v => new PackageVersionRowViewModel(v, LatestVersion, InstalledVersion));
            foreach (var row in rows) {
                Versions.Add(row);
            }
            Status = ThemeManager.GetString("packages.archive.status.ready");
        }
    }
}
