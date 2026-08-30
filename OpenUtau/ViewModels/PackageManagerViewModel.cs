using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Primitives;

namespace OpenUtau.App.ViewModels {

    public partial class ThemeRowViewModel : ViewModelBase {
        public RegistrySoftware? Software { get; }
        public string Id { get; }
        public string Name { get; }
        public string Author { get; }
        public string Description { get; }
        public bool IsSingerTheme { get; }
        public string ImageUrl { get; }
        public string Singers { get; }
        public string LatestVersion { get; }
        public string HomepageUrl { get; }
        public bool HasHomepage => !string.IsNullOrWhiteSpace(HomepageUrl);

        public Dictionary<string, string>? Palette { get; }


        public string PreviewBackground   => GetPaletteColor("#1E1E2E", "background_color");
        public string PreviewToolbar      => GetPaletteColor("#2A2A3E", "tick_line_color", "background_color");
        public string PreviewNote         => GetPaletteColor("#7C6AF7", "accent_color1", "system_accent_color", "track_accent_color");
        public string PreviewSelectedNote => GetPaletteColor("#FF5FA8", "accent_color2", "accent_color1", "system_accent_color", "track_accent_color_light", "track_accent_color");
        public string PreviewPitch        => GetPaletteColor("#FF9F43", "final_pitch_color", "system_accent_color", "accent_color1", "pitch_color");
        public string PreviewGrid         => GetPaletteColor("#333355", "border_color", "tick_line_color");
        public string PreviewForeground   => GetPaletteColor("#FFFFFF", "foreground_color", "phoneme_color");

        string GetPaletteColor(string fallback, params string[] keys) {
            if (Palette != null) {
                foreach (var key in keys) {
                    if (Palette.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val)) {
                        return val;
                    }
                }
            }
            return fallback;
        }

        [Reactive] public partial bool IsInstalled { get; set; }
        [Reactive] public partial string InstalledVersion { get; set; } = string.Empty;
        [Reactive] public partial Bitmap? PreviewImage { get; private set; }
        bool imageLoadAttempted;

        public bool HasRegistry => Software != null;

        public bool IsPullRequest => Software?.tags?.Any(t =>
            string.Equals(t, "pr-source", StringComparison.OrdinalIgnoreCase)) ?? false;
        public bool HasInstallableVersion => Software?.versions?.Any(v =>
            !string.IsNullOrWhiteSpace(v.version) &&
            v.mirrors != null &&
            v.mirrors.Length > 0) ?? false;
        public bool IsUpToDate => IsInstalled && !string.IsNullOrWhiteSpace(LatestVersion) &&
            string.Equals(InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);
        public bool HasUpdate => IsInstalled && !IsUpToDate && !string.IsNullOrWhiteSpace(LatestVersion);
        public bool CanInstallOrUpdate => HasRegistry && HasInstallableVersion && (!IsInstalled || HasUpdate);
        public bool CanUninstall => IsInstalled;
        public string TypeBadge => IsSingerTheme ? "Singer Theme" : "UI Theme";
        public string AuthorDisplay => string.IsNullOrWhiteSpace(Author) ? "-" : Author;
        public string VersionDisplay => string.IsNullOrWhiteSpace(LatestVersion)
            ? ThemeManager.GetString("packages.unknownversion")
            : LatestVersion;
        public string InstalledDisplay => IsInstalled
            ? (string.IsNullOrWhiteSpace(InstalledVersion)
                ? ThemeManager.GetString("packages.unknownversion")
                : InstalledVersion)
            : ThemeManager.GetString("packages.notinstalled");
        public string PrimaryActionLabel => !IsInstalled
            ? ThemeManager.GetString("packages.install")
            : (HasUpdate ? ThemeManager.GetString("packages.update") : ThemeManager.GetString("packages.install"));
        public bool HasPreviewImage => PreviewImage != null;
        public bool HasNoPreviewImage => PreviewImage == null;

        static readonly HttpClient imageClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public ThemeRowViewModel(RegistrySoftware s, bool isSingerTheme) {
            Software = s;
            Id = s.id ?? string.Empty;
            Name = s.LocalizedName();
            Author = (s.developers != null && s.developers.Length > 0)
                ? string.Join(", ", s.developers)
                : string.Empty;
            Description = s.LocalizedDescription();
            IsSingerTheme = isSingerTheme;
            ImageUrl = s.GetVoicebankImageUrl();
            Singers = ExtractSingers(s);
            LatestVersion = (s.versions != null && s.versions.Length > 0)
                ? PackageManager.GetLatestVersionString(s.versions)
                : string.Empty;
            HomepageUrl = !string.IsNullOrWhiteSpace(s.homepage_url) ? s.homepage_url : s.download_page_url ?? string.Empty;
            Palette = s.palette;

            WireRowNotifications();
        }

        public ThemeRowViewModel(OuthemeMetadata m) {
            Id = m.id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(m.name) ? (m.id ?? string.Empty) : (m.name ?? string.Empty);
            Author = m.author ?? string.Empty;
            Description = m.description ?? string.Empty;
            IsSingerTheme = false;
            Singers = string.Empty;
            LatestVersion = m.version ?? string.Empty;
            HomepageUrl = string.Empty;
            ImageUrl = string.Empty;

            WireRowNotifications();
        }

        public ThemeRowViewModel(OusthemeMetadata m) {
            Id = m.id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(m.name) ? (m.id ?? string.Empty) : (m.name ?? string.Empty);
            Author = m.author ?? string.Empty;
            Description = m.description ?? string.Empty;
            IsSingerTheme = true;
            Singers = m.singers ?? string.Empty;
            LatestVersion = m.version ?? string.Empty;
            HomepageUrl = string.Empty;
            ImageUrl = string.Empty;

            WireRowNotifications();
        }

        void WireRowNotifications() {
            this.WhenAnyValue(x => x.IsInstalled, x => x.InstalledVersion)
                .Subscribe(_ => {
                    this.RaisePropertyChanged(nameof(IsUpToDate));
                    this.RaisePropertyChanged(nameof(HasUpdate));
                    this.RaisePropertyChanged(nameof(CanInstallOrUpdate));
                    this.RaisePropertyChanged(nameof(CanUninstall));
                    this.RaisePropertyChanged(nameof(InstalledDisplay));
                    this.RaisePropertyChanged(nameof(PrimaryActionLabel));
                });
            this.WhenAnyValue(x => x.PreviewImage)
                .Subscribe(_ => {
                    this.RaisePropertyChanged(nameof(HasPreviewImage));
                    this.RaisePropertyChanged(nameof(HasNoPreviewImage));
                });
        }

        public void SetInstalled(string version) {
            IsInstalled = true;
            InstalledVersion = version ?? string.Empty;
        }

        public async Task LoadPreviewImageAsync() {
            if (imageLoadAttempted || string.IsNullOrWhiteSpace(ImageUrl)) return;
            imageLoadAttempted = true;
            try {
                var cacheDir = Path.Combine(PathManager.Inst.CachePath, "pkgmgr-theme-covers");
                Directory.CreateDirectory(cacheDir);
                var ext = GuessImageExtension(ImageUrl);
                var cacheFile = Path.Combine(cacheDir, $"{HashShort(ImageUrl)}{ext}");
                if (!File.Exists(cacheFile)) {
                    var req = new HttpRequestMessage(HttpMethod.Get, ImageUrl);
                    req.Headers.UserAgent.ParseAdd("OpenUtau");
                    using var res = await imageClient.SendAsync(req);
                    res.EnsureSuccessStatusCode();
                    var bytes = await res.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(cacheFile, bytes);
                }
                using var stream = File.OpenRead(cacheFile);
                PreviewImage = new Bitmap(stream);
            } catch {
                PreviewImage = null;
            }
        }

        static string ExtractSingers(RegistrySoftware s) {
            if (!string.IsNullOrWhiteSpace(s.character)) return s.character;
            var vbChar = s.GetVoicebankCharacter();
            if (!string.IsNullOrWhiteSpace(vbChar)) return vbChar;
            if (s.tags != null) {
                var singerTags = s.tags
                    .Where(t => !string.Equals(t, "UtauV_SingerTheme", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(t, "UtauV_Theme", StringComparison.OrdinalIgnoreCase)
                             && !string.Equals(t, "pr-source", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (singerTags.Length > 0) return string.Join(", ", singerTags);
            }
            return string.Empty;
        }

        static string HashShort(string text) {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        static string GuessImageExtension(string url) {
            var path = string.Empty;
            try { path = new Uri(url).AbsolutePath.ToLowerInvariant(); } catch { return ".img"; }
            if (path.EndsWith(".webp")) return ".webp";
            if (path.EndsWith(".png")) return ".png";
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return ".jpg";
            return ".img";
        }
    }


    public partial class PluginRowViewModel : ViewModelBase {
        public RegistrySoftware? Software { get; }
        public string Id { get; }
        public string Name { get; }
        public string Developer { get; }
        public string Version { get; }
        public string Category { get; }
        public string LongDescription { get; }
        public string RepoUrl { get; }

        [Reactive] public partial bool IsInstalled { get; set; }
        [Reactive] public partial string InstalledVersion { get; set; } = string.Empty;

        public bool HasRegistry => Software != null;
        public bool HasInstallableVersion => Software?.versions?.Any(v =>
            !string.IsNullOrWhiteSpace(v.version) &&
            v.mirrors != null &&
            v.mirrors.Length > 0) ?? false;
        public bool IsUpToDate => IsInstalled && HasRegistry && !string.IsNullOrEmpty(InstalledVersion) && InstalledVersion == Version;
        public bool HasUpdate => IsInstalled && HasRegistry && !IsUpToDate;
        public bool CanInstallOrUpdate => HasRegistry && HasInstallableVersion && (!IsInstalled || !IsUpToDate);
        public string PrimaryActionLabel => !HasRegistry
            ? ThemeManager.GetString("packages.install")
            : (!IsInstalled
                ? ThemeManager.GetString("packages.install")
                : (IsUpToDate ? ThemeManager.GetString("packages.install") : ThemeManager.GetString("packages.update")));
        public bool CanUninstall => IsInstalled;
        public string DeveloperDisplay => string.IsNullOrWhiteSpace(Developer) ? "-" : Developer;
        public string LatestDisplay => string.IsNullOrWhiteSpace(Version) ? ThemeManager.GetString("packages.unknownversion") : Version;
        public string DescriptionDisplay => string.IsNullOrWhiteSpace(Software?.LocalizedDescription())
            ? ThemeManager.GetString("packages.nodescription")
            : Software.LocalizedDescription();
        public string InstalledDisplay => IsInstalled
            ? (string.IsNullOrEmpty(InstalledVersion) ? ThemeManager.GetString("packages.unknownversion") : InstalledVersion)
            : ThemeManager.GetString("packages.notinstalled");

        public PluginRowViewModel(RegistrySoftware s) {
            Software = s;
            Id = s.id ?? string.Empty;
            Name = s.LocalizedName() ?? Id;
            Developer = (s.developers != null && s.developers.Length > 0) ? string.Join(", ", s.developers) : string.Empty;
            Version = (s.versions != null && s.versions.Length > 0) ? PackageManager.GetLatestVersionString(s.versions) : string.Empty;
            Category = NormalizePluginCategory(s.category);
            LongDescription = s.long_description ?? string.Empty;
            RepoUrl = string.IsNullOrWhiteSpace(s.homepage_url) ? s.download_page_url ?? string.Empty : s.homepage_url;

            this.WhenAnyValue(x => x.IsInstalled, x => x.InstalledVersion)
                .Subscribe(_ => {
                    this.RaisePropertyChanged(nameof(IsUpToDate));
                    this.RaisePropertyChanged(nameof(HasUpdate));
                    this.RaisePropertyChanged(nameof(CanInstallOrUpdate));
                    this.RaisePropertyChanged(nameof(PrimaryActionLabel));
                    this.RaisePropertyChanged(nameof(CanUninstall));
                    this.RaisePropertyChanged(nameof(InstalledDisplay));
                });
        }

        public PluginRowViewModel(OuplugMetadata info) {
            Software = null;
            Id = info.id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(info.name) ? Id : info.name;
            Developer = string.Empty;
            Version = info.version ?? string.Empty;
            Category = NormalizePluginCategory(info.category);
            LongDescription = string.Empty;
            RepoUrl = info.url ?? string.Empty;
        }

        static string NormalizePluginCategory(string? category) {
            if (string.IsNullOrWhiteSpace(category)) {
                return "Other";
            }

            var knownCategories = new[] { "Batch Edits", "Phonemizer" };

            foreach (var known in knownCategories) {
                if (string.Equals(category, known, StringComparison.OrdinalIgnoreCase)) {
                    return known;
                }
            }

            return "Other";
        }

        public void SetInstalled(OuplugMetadata info) {
            IsInstalled = true;
            InstalledVersion = info.version ?? string.Empty;
            this.RaisePropertyChanged(nameof(IsUpToDate));
            this.RaisePropertyChanged(nameof(CanInstallOrUpdate));
            this.RaisePropertyChanged(nameof(PrimaryActionLabel));
            this.RaisePropertyChanged(nameof(CanUninstall));
            this.RaisePropertyChanged(nameof(InstalledDisplay));
        }
    }

    public partial class PackageRowViewModel : ViewModelBase {
        public RegistrySoftware? Software { get; }
        public string Id { get; }
        public string Name { get; }
        public string Developer { get; }
        public string Version { get; }
        [Reactive] public partial bool IsInstalled { get; set; }
        [Reactive] public partial string InstalledVersion { get; set; } = string.Empty;

        public bool HasRegistry => Software != null;
        public bool HasInstallableVersion => Software?.versions?.Any(v =>
            !string.IsNullOrWhiteSpace(v.version) &&
            v.mirrors != null &&
            v.mirrors.Length > 0) ?? false;
        public bool IsUpToDate => IsInstalled && HasRegistry && !string.IsNullOrEmpty(InstalledVersion) && InstalledVersion == Version;
        public bool HasUpdate => IsInstalled && HasRegistry && !IsUpToDate;
        public bool HasVersionArchive => (Software?.versions?.Count(v =>
            !string.IsNullOrWhiteSpace(v.version) &&
            v.mirrors != null &&
            v.mirrors.Length > 0) ?? 0) > 1;
        public bool CanInstallOrUpdate => HasRegistry && HasInstallableVersion && (!IsInstalled || !IsUpToDate);
        public string PrimaryActionLabel => !HasRegistry
            ? ThemeManager.GetString("packages.install")
            : (!IsInstalled
                ? ThemeManager.GetString("packages.install")
                : (IsUpToDate ? ThemeManager.GetString("packages.install") : ThemeManager.GetString("packages.update")));
        public bool CanUninstall => IsInstalled;
        public string DeveloperDisplay => string.IsNullOrWhiteSpace(Developer) ? "-" : Developer;
        public string LatestDisplay => string.IsNullOrWhiteSpace(Version) ? ThemeManager.GetString("packages.unknownversion") : Version;
        public string DescriptionDisplay => string.IsNullOrWhiteSpace(Software?.LocalizedDescription())
            ? ThemeManager.GetString("packages.nodescription")
            : Software.LocalizedDescription();
        public string InstalledDisplay => IsInstalled
            ? (string.IsNullOrEmpty(InstalledVersion) ? ThemeManager.GetString("packages.unknownversion") : InstalledVersion)
            : ThemeManager.GetString("packages.notinstalled");

        public PackageRowViewModel(RegistrySoftware s) {
            Software = s;
            Id = s.id;
            Name = s.LocalizedName();
            Developer = (s.developers != null && s.developers.Length > 0) ? string.Join(", ", s.developers) : string.Empty;
            Version = (s.versions != null && s.versions.Length > 0) ? PackageManager.GetLatestVersionString(s.versions) : string.Empty;
            this.WhenAnyValue(x => x.IsInstalled, x => x.InstalledVersion)
                .Subscribe(_ => {
                    this.RaisePropertyChanged(nameof(IsUpToDate));
                    this.RaisePropertyChanged(nameof(HasUpdate));
                    this.RaisePropertyChanged(nameof(CanInstallOrUpdate));
                    this.RaisePropertyChanged(nameof(PrimaryActionLabel));
                    this.RaisePropertyChanged(nameof(CanUninstall));
                    this.RaisePropertyChanged(nameof(InstalledDisplay));
                });
        }

        public PackageRowViewModel(string id, string name, string developer, string version) {
            Software = null;
            Id = id;
            Name = name;
            Developer = developer;
            Version = version;
        }

        public void SetInstalled(OudepMetadata info) {
            IsInstalled = true;
            InstalledVersion = info.version ?? string.Empty;
            this.RaisePropertyChanged(nameof(IsUpToDate));
            this.RaisePropertyChanged(nameof(CanInstallOrUpdate));
            this.RaisePropertyChanged(nameof(PrimaryActionLabel));
            this.RaisePropertyChanged(nameof(CanUninstall));
            this.RaisePropertyChanged(nameof(InstalledDisplay));
        }
    }

    public partial class VoicebankVersionEntryViewModel : ViewModelBase {
        public VoicebankVariantViewModel Variant { get; }
        public RegistryVersion VersionInfo { get; }
        public string Version => VersionInfo.version;
        public string DescriptionDisplay { get; }
        public bool IsInstalled { get; }
        public bool IsLatest { get; }
        public bool CanInstall => Variant.Software != null;

        public VoicebankVersionEntryViewModel(
            VoicebankVariantViewModel variant,
            RegistryVersion version,
            string latestVersion,
            string installedVersion) {
            Variant = variant;
            VersionInfo = version;
            DescriptionDisplay = string.IsNullOrWhiteSpace(version.LocalizedDescription())
                ? ThemeManager.GetString("packages.nodescription")
                : version.LocalizedDescription();
            IsInstalled = !string.IsNullOrWhiteSpace(installedVersion) &&
                string.Equals(version.version, installedVersion, StringComparison.OrdinalIgnoreCase);
            IsLatest = !string.IsNullOrWhiteSpace(latestVersion) &&
                string.Equals(version.version, latestVersion, StringComparison.OrdinalIgnoreCase);
        }
    }

    public partial class VoicebankVariantViewModel : ViewModelBase {
        public RegistrySoftware? Software { get; }
        public string Team { get; }
        public string Id { get; }
        public string Name { get; }
        public string Developer { get; }
        public string DescriptionDisplay { get; }
        public string LongDescription { get; }
        public string WebsiteUrl { get; }
        public bool HasWebsite => !string.IsNullOrWhiteSpace(WebsiteUrl);
        public string SingerLink { get; }
        public bool HasSingerLink => !string.IsNullOrWhiteSpace(SingerLink);
        public string GroupId { get; }
        public string GroupName { get; }
        public string VariantName { get; }
        public string MetaDisplay { get; }
        public bool IsDiffSinger => Software != null && Software.voicebank_types != null && Software.voicebank_types.Contains("DiffSinger", StringComparer.OrdinalIgnoreCase);
        public string LatestVersion { get; }
        public ObservableCollection<VoicebankVersionEntryViewModel> Versions { get; } = new ObservableCollection<VoicebankVersionEntryViewModel>();

        [Reactive] public partial bool IsInstalled { get; set; }
        [Reactive] public partial string InstalledVersion { get; set; } = string.Empty;
        [Reactive] public partial string InstallPath { get; set; } = string.Empty;

        public bool IsUpToDate => IsInstalled && !string.IsNullOrWhiteSpace(LatestVersion) &&
            string.Equals(InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);
        public bool HasUpdate => IsInstalled && !IsUpToDate && !string.IsNullOrWhiteSpace(LatestVersion);
        public bool CanInstallLatest => Software != null && !string.IsNullOrWhiteSpace(LatestVersion) && (!IsInstalled || HasUpdate);
        public bool CanUninstall => IsInstalled;
        public string InstalledDisplay => IsInstalled
            ? (string.IsNullOrWhiteSpace(InstalledVersion) ? ThemeManager.GetString("voicebanks.unknownversion") : InstalledVersion)
            : ThemeManager.GetString("packages.notinstalled");
        public string PrimaryActionLabel => !IsInstalled
            ? ThemeManager.GetString("voicebanks.install")
            : (HasUpdate ? ThemeManager.GetString("voicebanks.update") : ThemeManager.GetString("voicebanks.install"));

        public VoicebankVariantViewModel(RegistrySoftware software) {
            Software = software;
            Team = software.team ?? string.Empty;
            Id = software.id ?? string.Empty;
            Name = software.LocalizedName();
            Developer = software.developers != null && software.developers.Length > 0
                ? string.Join(", ", software.developers)
                : string.Empty;
            DescriptionDisplay = string.IsNullOrWhiteSpace(software.LocalizedDescription())
                ? ThemeManager.GetString("packages.nodescription")
                : software.LocalizedDescription();
            LongDescription = !string.IsNullOrWhiteSpace(software.long_description)
                ? software.long_description
                : DescriptionDisplay;
            WebsiteUrl = software.website_url;
            SingerLink = software.singer_link;
            GroupId = software.GetVoicebankGroupId();
            GroupName = software.GetVoicebankGroupName();
            VariantName = software.GetVoicebankVariantName();
            MetaDisplay = BuildMetaDisplay(software);
            LatestVersion = software.versions != null && software.versions.Length > 0
                ? PackageManager.GetLatestVersionString(software.versions)
                : string.Empty;
            RebuildVersions();

            this.WhenAnyValue(x => x.IsInstalled, x => x.InstalledVersion)
                .Subscribe(_ => {
                    RebuildVersions();
                    this.RaisePropertyChanged(nameof(IsUpToDate));
                    this.RaisePropertyChanged(nameof(HasUpdate));
                    this.RaisePropertyChanged(nameof(CanInstallLatest));
                    this.RaisePropertyChanged(nameof(CanUninstall));
                    this.RaisePropertyChanged(nameof(InstalledDisplay));
                    this.RaisePropertyChanged(nameof(PrimaryActionLabel));
                });
        }

        public VoicebankVariantViewModel(OuvbMetadata metadata) {
            Software = null;
            Team = metadata.team ?? string.Empty;
            Id = metadata.id ?? string.Empty;
            Name = !string.IsNullOrWhiteSpace(metadata.name) ? metadata.name : Id;
            Developer = string.Empty;
            DescriptionDisplay = string.IsNullOrWhiteSpace(metadata.description)
                ? ThemeManager.GetString("packages.nodescription")
                : metadata.description;
            LongDescription = !string.IsNullOrWhiteSpace(metadata.long_description)
                ? metadata.long_description
                : DescriptionDisplay;
            WebsiteUrl = metadata.website_url ?? string.Empty;
            SingerLink = metadata.singer_link ?? string.Empty;
            GroupId = DeriveGroupId(Id);
            GroupName = DeriveGroupName(Name, Id);
            VariantName = DeriveVariantName(Name, Id, GroupName, GroupId);
            MetaDisplay = BuildMetaDisplay(metadata.languages ?? [], metadata.types ?? [], metadata.engines ?? []);
            LatestVersion = metadata.version ?? string.Empty;
            IsInstalled = true;
            InstalledVersion = metadata.version ?? string.Empty;
            InstallPath = metadata.install_path ?? string.Empty;
        }

        public void SetInstalled(OuvbMetadata installed) {
            IsInstalled = true;
            InstalledVersion = installed.version ?? string.Empty;
            InstallPath = installed.install_path ?? string.Empty;
            RebuildVersions();
        }

        void RebuildVersions() {
            Versions.Clear();
            if (Software == null || Software.versions == null) {
                return;
            }
            var rows = Software.versions
                .Where(v => !string.IsNullOrWhiteSpace(v.version))
                .Where(v => v.mirrors != null && v.mirrors.Length > 0)
                .OrderByDescending(v => v.version, StringComparer.OrdinalIgnoreCase)
                .Select(v => new VoicebankVersionEntryViewModel(this, v, LatestVersion, InstalledVersion));
            foreach (var row in rows) {
                Versions.Add(row);
            }
        }

        static string DeriveGroupId(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                return "voicebank";
            }
            var parts = id.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0].ToLowerInvariant() : id.ToLowerInvariant();
        }

        static string DeriveGroupName(string name, string id) {
            if (!string.IsNullOrWhiteSpace(name)) {
                var split = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length > 0) {
                    return split[0];
                }
            }
            var group = DeriveGroupId(id);
            if (string.IsNullOrWhiteSpace(group)) {
                return "Voicebank";
            }
            return char.ToUpperInvariant(group[0]) + group.Substring(1);
        }

        static string DeriveVariantName(string name, string id, string groupName, string groupId) {
            if (!string.IsNullOrWhiteSpace(name) &&
                !string.IsNullOrWhiteSpace(groupName) &&
                name.StartsWith(groupName + " ", StringComparison.OrdinalIgnoreCase)) {
                var suffix = name.Substring(groupName.Length).Trim().TrimStart('-', '_', '|', ':');
                if (!string.IsNullOrWhiteSpace(suffix)) {
                    return suffix;
                }
            }
            if (!string.IsNullOrWhiteSpace(id)) {
                if (!string.IsNullOrWhiteSpace(groupId) &&
                    id.StartsWith(groupId + "-", StringComparison.OrdinalIgnoreCase)) {
                    return FormatVariantToken(id.Substring(groupId.Length + 1));
                }
                if (!string.IsNullOrWhiteSpace(groupId) &&
                    id.StartsWith(groupId + "_", StringComparison.OrdinalIgnoreCase)) {
                    return FormatVariantToken(id.Substring(groupId.Length + 1));
                }
            }
            return !string.IsNullOrWhiteSpace(name) ? name : id;
        }

        static string FormatVariantToken(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return string.Empty;
            }
            var words = raw.Replace('_', ' ').Replace('-', ' ')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.All(char.IsLetter) && word.Length <= 5
                    ? word.ToUpperInvariant()
                    : word)
                .ToArray();
            return words.Length > 0 ? string.Join(" ", words) : raw;
        }

        static string BuildMetaDisplay(RegistrySoftware software) {
            return BuildMetaDisplay(
                software.GetVoicebankLanguages(),
                software.GetVoicebankTypes(),
                software.GetVoicebankEngines());
        }

        static string BuildMetaDisplay(string[] languages, string[] types, string[] engines) {
            var parts = new List<string>();
            if (languages != null && languages.Length > 0) {
                parts.Add(string.Join("/", languages.Where(s => !string.IsNullOrWhiteSpace(s)).Take(3)));
            }
            if (types != null && types.Length > 0) {
                parts.Add(string.Join("/", types.Where(s => !string.IsNullOrWhiteSpace(s)).Take(2)));
            }
            if (engines != null && engines.Length > 0) {
                parts.Add(string.Join("/", engines.Where(s => !string.IsNullOrWhiteSpace(s)).Take(2)));
            }
            return parts.Count > 0 ? string.Join(", ", parts) : ThemeManager.GetString("voicebanks.meta.unknown");
        }
    }

    public partial class VoicebankTeamViewModel : ViewModelBase {
        public string Name { get; }
        public ObservableCollection<VoicebankGroupViewModel> Groups { get; } = new ObservableCollection<VoicebankGroupViewModel>();

        public VoicebankTeamViewModel(string name) {
            Name = name;
        }

        public int VariantCount => Groups.Sum(g => g.VariantCount);
        public bool IsInstalled => Groups.Any(g => g.IsInstalled);
        public bool HasUpdate => Groups.Any(g => g.HasUpdate);
    }

    public partial class VoicebankGroupViewModel : ViewModelBase {
        static readonly HttpClient imageClient = new HttpClient {
            Timeout = TimeSpan.FromSeconds(20),
        };

        public string GroupId { get; }
        public string Team { get; }
        public string Name { get; }
        public string DescriptionDisplay { get; }
        public string LongDescription { get; }
        public IEnumerable<string> AllLanguages { get; }
        public bool IsDiffSingerGroup => Variants.Any(v => v.IsDiffSinger);
        public string WebsiteUrl { get; }
        public bool HasWebsite => !string.IsNullOrWhiteSpace(WebsiteUrl);
        public string SingerLink { get; }
        public bool HasSingerLink => !string.IsNullOrWhiteSpace(SingerLink);
        public string CoverImageUrl { get; }
        public string MetaDisplay { get; }
        public ObservableCollection<VoicebankVariantViewModel> Variants { get; } = new ObservableCollection<VoicebankVariantViewModel>();

        [Reactive] public partial Bitmap? CoverImage { get; private set; }
        bool coverLoadAttempted;
        public bool HasCover => CoverImage != null;
        public bool HasNoCover => CoverImage == null;

        public int VariantCount => Variants.Count;
        public bool IsInstalled => Variants.Any(v => v.IsInstalled);
        public bool HasUpdate => Variants.Any(v => v.HasUpdate);
        public string WelcomeStatusText => IsInstalled
            ? (HasUpdate ? "Update Avail." : "Installed")
            : "Not Installed";
        public string WelcomeStatusIcon => IsInstalled
            ? (HasUpdate ? "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" : "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z")
            : "M19 9h-4V3H9v6H5l7 7 7-7zM5 18v2h14v-2H5z";
        public string WelcomeActionLabel => VariantCount > 1
                ? "Open"
                : (IsInstalled ? (HasUpdate ? "Update Now" : "Uninstall") : "Download");
        public IBrush WelcomeStatusBrush => IsInstalled
            ? (HasUpdate ? Brushes.Orange : Brushes.LimeGreen)
            : Brushes.Gray;

        public string InstalledBadgeText => IsInstalled
            ? $"{ThemeManager.GetString("packages.installed")}: {Variants.Count(v => v.IsInstalled)}/{Variants.Count}"
            : ThemeManager.GetString("packages.notinstalled");

        public VoicebankGroupViewModel(
            string groupId,
            string team,
            string name,
            string description,
            string longDescription,
            IEnumerable<string> allLanguages,
            string websiteUrl,
            string singerLink,
            string coverImageUrl,
            string metaDisplay) {
            GroupId = groupId;
            Team = team;
            Name = name;
            DescriptionDisplay = description;
            LongDescription = longDescription;
            AllLanguages = allLanguages;
            WebsiteUrl = websiteUrl;
            SingerLink = singerLink;
            CoverImageUrl = coverImageUrl;
            MetaDisplay = metaDisplay;
            this.WhenAnyValue(x => x.CoverImage).Subscribe(_ => {
                this.RaisePropertyChanged(nameof(HasCover));
                this.RaisePropertyChanged(nameof(HasNoCover));
            });
        }

        public void AddVariant(VoicebankVariantViewModel variant) {
            Variants.Add(variant);
            variant.WhenAnyValue(v => v.IsInstalled, v => v.InstalledVersion).Subscribe(_ => RaiseAggregate());
            RaiseAggregate();
        }

        public void SortVariants() {
            var ordered = Variants
                .OrderByDescending(v => v.IsInstalled)
                .ThenBy(v => v.VariantName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Variants.Clear();
            foreach (var row in ordered) {
                Variants.Add(row);
            }
            RaiseAggregate();
        }

        void RaiseAggregate() {
            this.RaisePropertyChanged(nameof(VariantCount));
            this.RaisePropertyChanged(nameof(IsInstalled));
            this.RaisePropertyChanged(nameof(HasUpdate));
            this.RaisePropertyChanged(nameof(InstalledBadgeText));
        }

        public async Task LoadCoverAsync() {
            if (coverLoadAttempted || string.IsNullOrWhiteSpace(CoverImageUrl)) {
                return;
            }
            coverLoadAttempted = true;
            try {
                var cacheDir = Path.Combine(PathManager.Inst.CachePath, "pkgmgr-covers");
                Directory.CreateDirectory(cacheDir);
                var ext = GuessImageExtension(CoverImageUrl);
                var cacheFile = Path.Combine(cacheDir, $"{HashShort(CoverImageUrl)}{ext}");
                if (!File.Exists(cacheFile)) {
                    var req = new HttpRequestMessage(HttpMethod.Get, CoverImageUrl);
                    req.Headers.UserAgent.ParseAdd("OpenUtau");
                    using var res = await imageClient.SendAsync(req);
                    res.EnsureSuccessStatusCode();
                    var bytes = await res.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(cacheFile, bytes);
                }
                using var stream = File.OpenRead(cacheFile);
                CoverImage = new Bitmap(stream);
            } catch {
                CoverImage = null;
            }
        }

        static string HashShort(string text) {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        static string GuessImageExtension(string url) {
            var path = string.Empty;
            try {
                path = new Uri(url).AbsolutePath.ToLowerInvariant();
            } catch {
                return ".img";
            }
            if (path.EndsWith(".webp")) return ".webp";
            if (path.EndsWith(".png")) return ".png";
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return ".jpg";
            return ".img";
        }
    }

    public partial class PackageManagerViewModel : ViewModelBase {
        public ObservableCollection<PackageRowViewModel> Available { get; } = new ObservableCollection<PackageRowViewModel>();
        public ObservableCollection<VoicebankTeamViewModel> VoicebankGroups { get; } = new ObservableCollection<VoicebankTeamViewModel>();

        [Reactive] public partial string Status { get; set; } = string.Empty;
        [Reactive] public partial int TotalCount { get; private set; }
        [Reactive] public partial int InstalledCount { get; private set; }
        [Reactive] public partial int UpdateCount { get; private set; }
        [Reactive] public partial int VoicebankTotalCount { get; private set; }
        [Reactive] public partial int VoicebankInstalledCount { get; private set; }
        [Reactive] public partial int VoicebankUpdateCount { get; private set; }
        [Reactive] public partial bool IsPackagesSection { get; private set; } = true;
        [Reactive] public partial bool IsVoicebanksSection { get; private set; } = false;
        [Reactive] public partial bool IsPluginsSection { get; private set; } = false;

        public ObservableCollection<PluginRowViewModel> Plugins { get; } = new ObservableCollection<PluginRowViewModel>();
        public ObservableCollection<PluginRowViewModel> FilteredPlugins { get; } = new ObservableCollection<PluginRowViewModel>();
        public ObservableCollection<string> PluginCategories { get; } = new ObservableCollection<string>();

        [Reactive] public partial string SearchQuery { get; set; } = string.Empty;
        [Reactive] public partial string SelectedCategory { get; set; } = "All";

        [Reactive] public partial int PluginTotalCount { get; private set; }
        [Reactive] public partial int PluginInstalledCount { get; private set; }
        [Reactive] public partial int PluginUpdateCount { get; private set; }

        public ObservableCollection<ThemeRowViewModel> Themes { get; } = new ObservableCollection<ThemeRowViewModel>();
        public ObservableCollection<ThemeRowViewModel> FilteredThemes { get; } = new ObservableCollection<ThemeRowViewModel>();

        [Reactive] public partial string ThemeSearchText { get; set; } = string.Empty;
        [Reactive] public partial string SelectedThemeTypeFilter { get; set; } = "All";
        [Reactive] public partial ThemeRowViewModel? SelectedTheme { get; set; }
        [Reactive] public partial bool IsThemesLoading { get; private set; }
        [Reactive] public partial bool IsThemesSection { get; private set; } = false;

        [Reactive] public partial int ThemeTotalCount { get; private set; }
        [Reactive] public partial int ThemeInstalledCount { get; private set; }
        [Reactive] public partial int ThemeUpdateCount { get; private set; }

        public int SelectedTotalCount => IsPackagesSection ? TotalCount : (IsVoicebanksSection ? VoicebankTotalCount : (IsPluginsSection ? PluginTotalCount : ThemeTotalCount));
        public int SelectedInstalledCount => IsPackagesSection ? InstalledCount : (IsVoicebanksSection ? VoicebankInstalledCount : (IsPluginsSection ? PluginInstalledCount : ThemeInstalledCount));
        public int SelectedUpdateCount => IsPackagesSection ? UpdateCount : (IsVoicebanksSection ? VoicebankUpdateCount : (IsPluginsSection ? PluginUpdateCount : ThemeUpdateCount));
        public string CurrentSectionTitle => IsPackagesSection
            ? ThemeManager.GetString("packages.caption")
            : (IsVoicebanksSection ? ThemeManager.GetString("voicebanks.caption")
            : (IsPluginsSection ? "Plugins"
            : ThemeManager.GetString("packages.themes.caption")));
        public bool ShowInstallFromFileButton => IsPackagesSection;
        public string OpenInstallLocationLabel => IsPackagesSection
            ? ThemeManager.GetString("packages.openinstalllocation")
            : (IsVoicebanksSection ? ThemeManager.GetString("voicebanks.openinstalllocation")
            : (IsPluginsSection ? "Open Plugins Location"
            : ThemeManager.GetString("packages.themes.openinstalllocation")));

        public ReactiveCommand<RxVoid, RxVoid> RefreshCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SelectPackagesCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SelectVoicebanksCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SelectPluginsCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SelectThemesCommand { get; }
        public ReactiveCommand<PackageRowViewModel, RxVoid> InstallCommand { get; }
        public ReactiveCommand<PackageRowViewModel, RxVoid> UninstallCommand { get; }
        public ReactiveCommand<VoicebankVariantViewModel, RxVoid> InstallVoicebankCommand { get; }
        public ReactiveCommand<VoicebankVariantViewModel, RxVoid> UninstallVoicebankCommand { get; }
        public ReactiveCommand<PluginRowViewModel, RxVoid> InstallPluginCommand { get; }
        public ReactiveCommand<PluginRowViewModel, RxVoid> UninstallPluginCommand { get; }
        public ReactiveCommand<ThemeRowViewModel, RxVoid> InstallThemeCommand { get; }
        public ReactiveCommand<ThemeRowViewModel, RxVoid> UninstallThemeCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> RefreshThemesCommand { get; }

        public PackageManagerViewModel() {
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
            SelectPackagesCommand = ReactiveCommand.Create(() => SetSection(0));
            SelectVoicebanksCommand = ReactiveCommand.Create(() => SetSection(1));
            SelectPluginsCommand = ReactiveCommand.Create(() => SetSection(2));
            SelectThemesCommand = ReactiveCommand.Create(() => SetSection(3));
            InstallCommand = ReactiveCommand.CreateFromTask<PackageRowViewModel>(InstallAsync);
            UninstallCommand = ReactiveCommand.CreateFromTask<PackageRowViewModel>(UninstallAsync);
            InstallVoicebankCommand = ReactiveCommand.CreateFromTask<VoicebankVariantViewModel>(InstallVoicebankLatestAsync);
            UninstallVoicebankCommand = ReactiveCommand.CreateFromTask<VoicebankVariantViewModel>(UninstallVoicebankAsync);
            InstallPluginCommand = ReactiveCommand.CreateFromTask<PluginRowViewModel>(InstallPluginAsync);
            UninstallPluginCommand = ReactiveCommand.CreateFromTask<PluginRowViewModel>(UninstallPluginAsync);
            InstallThemeCommand = ReactiveCommand.CreateFromTask<ThemeRowViewModel>(InstallThemeAsync);
            UninstallThemeCommand = ReactiveCommand.CreateFromTask<ThemeRowViewModel>(UninstallThemeAsync);
            RefreshThemesCommand = ReactiveCommand.CreateFromTask(() => RefreshThemesAsync(forceRefresh: true));

            this.WhenAnyValue(x => x.SearchQuery, x => x.SelectedCategory)
                .Subscribe(_ => ApplyPluginFilter());
            this.WhenAnyValue(x => x.ThemeSearchText, x => x.SelectedThemeTypeFilter)
                .Subscribe(_ => ApplyThemeFilter());
            this.WhenAnyValue(x => x.SelectedTheme)
                .Subscribe(theme => {
                    if (theme != null) {
                        _ = theme.LoadPreviewImageAsync();
                    }
                });

            _ = RefreshAsync();
        }

        void SetSection(int section) {
            IsPackagesSection = section == 0;
            IsVoicebanksSection = section == 1;
            IsPluginsSection = section == 2;
            IsThemesSection = section == 3;
            RaiseSectionDependent();
            if (section == 3 && Themes.Count == 0 && !IsThemesLoading) {
                _ = RefreshThemesAsync(forceRefresh: false);
            }
        }

        void RaiseSectionDependent() {
            this.RaisePropertyChanged(nameof(SelectedTotalCount));
            this.RaisePropertyChanged(nameof(SelectedInstalledCount));
            this.RaisePropertyChanged(nameof(SelectedUpdateCount));
            this.RaisePropertyChanged(nameof(CurrentSectionTitle));
            this.RaisePropertyChanged(nameof(ShowInstallFromFileButton));
            this.RaisePropertyChanged(nameof(OpenInstallLocationLabel));
        }

        public async Task RefreshAsync() {
            var errors = new List<Exception>();
            Status = ThemeManager.GetString("packages.status.fetching");

            try {
                await RefreshPackagesAsync();
            } catch (Exception e) {
                errors.Add(e);
            }

            try {
                await RefreshVoicebanksAsync();
            } catch (Exception e) {
                errors.Add(e);
            }

            try {
                await RefreshPluginsAsync();
            } catch (Exception e) {
                errors.Add(e);
            }

            if (errors.Count > 0) {
                Status = ThemeManager.GetString("packages.status.error");
                foreach (var error in errors) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(error));
                }
            } else {
                Status = ThemeManager.GetString("packages.status.ready");
            }
        }

        async Task RefreshPackagesAsync() {
            var registry = await PackageManager.Inst.FetchRegistryAsync();
            var installed = await PackageManager.Inst.GetInstalledAsync();
            var installedById = installed.ToDictionary(i => i.id, i => i);

            var rows = new List<PackageRowViewModel>();
            foreach (var software in registry) {
                var row = new PackageRowViewModel(software);
                if (installedById.TryGetValue(row.Id, out var info)) {
                    row.SetInstalled(info);
                    installedById.Remove(row.Id);
                }
                rows.Add(row);
            }
            foreach (var info in installedById.Values) {
                var row = new PackageRowViewModel(info.id, info.id, string.Empty, string.Empty);
                row.SetInstalled(info);
                rows.Add(row);
            }

            var ordered = rows
                .OrderByDescending(r => r.IsInstalled)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Available.Clear();
            foreach (var row in ordered) {
                Available.Add(row);
            }
            UpdatePackageCounters();
        }

        async Task RefreshVoicebanksAsync() {
            var registry = await PackageManager.Inst.FetchVoicebankRegistryAsync();
            var installed = await PackageManager.Inst.GetInstalledVoicebanksAsync();
            var installedById = installed
                .Where(v => !string.IsNullOrWhiteSpace(v.id))
                .ToDictionary(v => v.id, v => v, StringComparer.OrdinalIgnoreCase);

            var variants = new List<VoicebankVariantViewModel>();
            foreach (var software in registry) {
                var variant = new VoicebankVariantViewModel(software);
                if (installedById.TryGetValue(variant.Id, out var info)) {
                    variant.SetInstalled(info);
                    installedById.Remove(variant.Id);
                }
                variants.Add(variant);
            }

            foreach (var info in installedById.Values) {
                variants.Add(new VoicebankVariantViewModel(info));
            }

            var groupedVariants = variants
                .GroupBy(v => string.IsNullOrWhiteSpace(v.GroupId) ? v.Id : v.GroupId, StringComparer.OrdinalIgnoreCase);
            var emptyDesc = ThemeManager.GetString("packages.nodescription");

            var ordered = groupedVariants
                .Select(groupSet => {
                    var groupVariants = groupSet.ToList();
                    var rep = groupVariants.FirstOrDefault(v =>
                        v.Software != null && !string.IsNullOrWhiteSpace(v.Software.GetVoicebankImageUrl()))
                        ?? groupVariants.First();
                    var groupName = groupVariants
                        .Select(v => v.GroupName)
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                        ?? rep.Name;
                    var groupDescription = groupVariants
                        .Select(v => v.Software?.GetVoicebankGroupDescription())
                        .FirstOrDefault(desc => !string.IsNullOrWhiteSpace(desc));
                    var description = !string.IsNullOrWhiteSpace(groupDescription)
                        ? groupDescription
                        : groupVariants
                        .Select(v => v.DescriptionDisplay)
                        .FirstOrDefault(desc =>
                            !string.IsNullOrWhiteSpace(desc) &&
                            !string.Equals(desc, emptyDesc, StringComparison.Ordinal))
                        ?? rep.DescriptionDisplay;
                    var website = groupVariants
                        .Select(v => v.WebsiteUrl)
                        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
                        ?? string.Empty;
                    var singerLink = groupVariants
                        .Select(v => v.SingerLink)
                        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
                        ?? string.Empty;
                    var cover = groupVariants
                        .Select(v => v.Software?.GetVoicebankImageUrl())
                        .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url))
                        ?? string.Empty;
                    var meta = BuildGroupMetaDisplay(groupVariants);
                    var teamName = groupVariants
                        .Select(v => v.Team)
                        .FirstOrDefault(team => !string.IsNullOrWhiteSpace(team))
                        ?? string.Empty;
                    var groupLongDescription = groupVariants
                        .Select(v => v.Software?.group_long_description)
                        .FirstOrDefault(desc => !string.IsNullOrWhiteSpace(desc))
                        ?? string.Empty;
                    var allLanguages = groupVariants
                        .SelectMany(v => v.Software?.languages ?? Array.Empty<string>())
                        .Where(lang => !string.IsNullOrWhiteSpace(lang))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    var group = new VoicebankGroupViewModel(
                        groupSet.Key,
                        teamName,
                        groupName,
                        description,
                        groupLongDescription,
                        allLanguages,
                        website,
                        singerLink,
                        cover,
                        meta);
                    foreach (var variant in groupVariants) {
                        group.AddVariant(variant);
                    }
                    group.SortVariants();
                    return group;
                })
                .OrderByDescending(group => group.IsInstalled)
                .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var teamGroups = ordered
                .GroupBy(g => string.IsNullOrWhiteSpace(g.Team) ? "Other" : g.Team, StringComparer.OrdinalIgnoreCase)
                .Select(tg => {
                    var team = new VoicebankTeamViewModel(tg.Key);
                    foreach (var group in tg) {
                        team.Groups.Add(group);
                    }
                    return team;
                })
                .OrderByDescending(t => string.Equals(t.Name ?? "", "Emerald Project", StringComparison.OrdinalIgnoreCase))
                .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            VoicebankGroups.Clear();
            foreach (var team in teamGroups) {
                VoicebankGroups.Add(team);
                foreach (var group in team.Groups) {
                    _ = group.LoadCoverAsync();
                }
            }

            UpdateVoicebankCounters();
        }

        static string BuildGroupMetaDisplay(List<VoicebankVariantViewModel> variants) {
            var meta = variants
                .Select(v => v.MetaDisplay)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (meta.Count == 0) {
                return ThemeManager.GetString("voicebanks.meta.unknown");
            }
            if (meta.Count == 1) {
                return meta[0];
            }
            return string.Format(ThemeManager.GetString("voicebanks.meta.multiple"), meta.Count);
        }

        public async Task InstallAsync(PackageRowViewModel row) {
            try {
                if (row.Software == null) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                        new InvalidOperationException("No registry entry to install.")));
                    return;
                }
                if (row.IsUpToDate) return;
                var installingTemplate = ThemeManager.GetString("packages.status.installing");
                var baseStatus = string.Format(installingTemplate, row.Id);
                Status = baseStatus;
                var progress = new Progress<int>(p => {
                    Status = $"{baseStatus} ({p}%)";
                });
                await PackageManager.Inst.InstallAsync(row.Software, progress);
                await RefreshAsync();
                Status = ThemeManager.GetString("packages.status.installfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("packages.status.installfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public async Task UninstallAsync(PackageRowViewModel row) {
            try {
                if (!row.IsInstalled) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                        new InvalidOperationException("Package is not installed.")));
                    return;
                }
                Status = string.Format(ThemeManager.GetString("packages.status.uninstalling"), row.Id);
                await PackageManager.Inst.UninstallAsync(row.Id);
                await RefreshAsync();
                Status = ThemeManager.GetString("packages.status.uninstallfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("packages.status.uninstallfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public async Task InstallVoicebankLatestAsync(VoicebankVariantViewModel row) {
            if (row.Software == null || string.IsNullOrWhiteSpace(row.LatestVersion)) {
                return;
            }
            await InstallVoicebankVersionAsync(row, row.LatestVersion);
        }

        public async Task InstallVoicebankVersionAsync(VoicebankVariantViewModel row, string version) {
            try {
                if (row.Software == null) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                        new InvalidOperationException("No registry entry to install.")));
                    return;
                }
                var installingTemplate = ThemeManager.GetString("voicebanks.status.installing");
                var baseStatus = string.Format(installingTemplate, row.Id);
                Status = baseStatus;
                var progress = new Progress<int>(p => {
                    Status = $"{baseStatus} ({p}%)";
                });
                await PackageManager.Inst.InstallVoicebankVersionAsync(row.Software, version, progress);
                await RefreshAsync();
                Status = ThemeManager.GetString("voicebanks.status.installfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("voicebanks.status.installfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public async Task UninstallVoicebankAsync(VoicebankVariantViewModel row) {
            try {
                if (!row.IsInstalled) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                        new InvalidOperationException("Voicebank is not installed.")));
                    return;
                }
                Status = string.Format(ThemeManager.GetString("voicebanks.status.uninstalling"), row.Id);
                await PackageManager.Inst.UninstallVoicebankAsync(row.Id);
                await RefreshAsync();
                Status = ThemeManager.GetString("voicebanks.status.uninstallfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("voicebanks.status.uninstallfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        void UpdatePackageCounters() {
            TotalCount = Available.Count;
            InstalledCount = Available.Count(r => r.IsInstalled);
            UpdateCount = Available.Count(r => r.HasUpdate);
            RaiseSectionDependent();
        }

        void UpdateVoicebankCounters() {
            VoicebankTotalCount = VoicebankGroups.Sum(t => t.Groups.Count);
            VoicebankInstalledCount = VoicebankGroups.Sum(t => t.Groups.Count(r => r.IsInstalled));
            VoicebankUpdateCount = VoicebankGroups.Sum(t => t.Groups.Count(r => r.HasUpdate));
            RaiseSectionDependent();
        }

        void ApplyPluginFilter() {
            FilteredPlugins.Clear();
            var search = (SearchQuery ?? string.Empty).ToLowerInvariant();
            var category = string.IsNullOrWhiteSpace(SelectedCategory) ? "All" : SelectedCategory;

            foreach (var plugin in Plugins) {
                if (category != "All" && !string.Equals(plugin.Category, category, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(search)) {
                    if (!plugin.Name.ToLowerInvariant().Contains(search) &&
                        !plugin.DescriptionDisplay.ToLowerInvariant().Contains(search) &&
                        !plugin.DeveloperDisplay.ToLowerInvariant().Contains(search)) {
                        continue;
                    }
                }
                FilteredPlugins.Add(plugin);
            }
        }

        async Task RefreshPluginsAsync() {
            var registry = await PackageManager.Inst.FetchPluginRegistryAsync();
            var installed = await PackageManager.Inst.GetInstalledPluginsAsync();
            var installedById = installed.Where(i => !string.IsNullOrEmpty(i.id))
                .ToDictionary(i => i.id, i => i, StringComparer.OrdinalIgnoreCase);

            var rows = new List<PluginRowViewModel>();
            foreach (var software in registry) {
                var row = new PluginRowViewModel(software);
                if (installedById.TryGetValue(row.Id, out var info)) {
                    row.SetInstalled(info);
                    installedById.Remove(row.Id);
                }
                rows.Add(row);
            }
            foreach (var info in installedById.Values) {
                var row = new PluginRowViewModel(info);
                row.SetInstalled(info);
                rows.Add(row);
            }

            var ordered = rows
                .OrderByDescending(r => r.IsInstalled)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            Plugins.Clear();
            foreach (var row in ordered) Plugins.Add(row);

            var categories = rows.Select(r => r.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
            PluginCategories.Clear();
            PluginCategories.Add("All");
            foreach(var cat in categories) PluginCategories.Add(cat);
            if (!PluginCategories.Contains(SelectedCategory ?? "")) SelectedCategory = "All";

            ApplyPluginFilter();
            UpdatePluginCounters();
        }

        public async Task InstallPluginAsync(PluginRowViewModel row) {
            try {
                if (row.Software == null) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(new Exception("Cannot install an unknown plugin.")));
                    return;
                }
                Status = $"Downloading plugin: {row.Id}";
                await PackageManager.Inst.InstallPluginAsync(row.Software);
                DocManager.Inst.SearchAllPlugins();
                await RefreshAsync();
                Status = $"Plugin {row.Id} installed.";
            } catch (Exception e) {
                Status = $"Failed to install plugin {row.Id}.";
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public async Task UninstallPluginAsync(PluginRowViewModel row) {
            try {
                if (!row.IsInstalled) return;
                Status = $"Uninstalling plugin: {row.Id}";
                await PackageManager.Inst.UninstallPluginAsync(row.Id);
                DocManager.Inst.SearchAllPlugins();
                await RefreshAsync();
                Status = $"Plugin {row.Id} uninstalled.";
            } catch (Exception e) {
                Status = $"Failed to uninstall plugin {row.Id}.";
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        void UpdatePluginCounters() {
            PluginTotalCount = Plugins.Count;
            PluginInstalledCount = Plugins.Count(r => r.IsInstalled);
            PluginUpdateCount = Plugins.Count(r => r.HasUpdate);
            RaiseSectionDependent();
        }


        public async Task RefreshThemesAsync(bool forceRefresh = false) {
            if (IsThemesLoading) return;
            IsThemesLoading = true;
            Status = ThemeManager.GetString("packages.themes.status.fetching");
            try {
                var uiThemes = await PackageManager.Inst.FetchThemeRegistryAsync(forceRefresh);
                var singerThemes = await PackageManager.Inst.FetchSingerThemeRegistryAsync(forceRefresh);

                var installedUi = await PackageManager.Inst.GetInstalledThemesAsync();
                var installedSinger = await PackageManager.Inst.GetInstalledSingerThemesAsync();
                var installedUiById = installedUi.ToDictionary(t => t.id, t => t.version, StringComparer.OrdinalIgnoreCase);
                var installedSingerById = installedSinger.ToDictionary(t => t.id, t => t.version, StringComparer.OrdinalIgnoreCase);

                var rows = new List<ThemeRowViewModel>();

                foreach (var s in uiThemes) {
                    var row = new ThemeRowViewModel(s, isSingerTheme: false);
                    if (installedUiById.TryGetValue(row.Id, out var ver)) {
                        row.SetInstalled(ver);
                    }
                    rows.Add(row);
                }

                var existingIds = new HashSet<string>(rows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
                foreach (var s in singerThemes) {
                    if (existingIds.Contains(s.id)) continue;
                    var row = new ThemeRowViewModel(s, isSingerTheme: true);
                    if (installedSingerById.TryGetValue(row.Id, out var ver)) {
                        row.SetInstalled(ver);
                    }
                    rows.Add(row);
                    existingIds.Add(s.id);
                }

                foreach (var t in installedUi) {
                    if (existingIds.Contains(t.id)) continue;
                    var row = new ThemeRowViewModel(t);
                    row.SetInstalled(t.version);
                    rows.Add(row);
                    existingIds.Add(t.id);
                }
                foreach (var t in installedSinger) {
                    if (existingIds.Contains(t.id)) continue;
                    var row = new ThemeRowViewModel(t);
                    row.SetInstalled(t.version);
                    rows.Add(row);
                    existingIds.Add(t.id);
                }

                var ordered = rows
                    .OrderBy(r => r.IsPullRequest)
                    .ThenByDescending(r => r.IsInstalled)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                Themes.Clear();
                foreach (var row in ordered) {
                    Themes.Add(row);
                }

                ApplyThemeFilter();
                UpdateThemeCounters();
                Status = ThemeManager.GetString("packages.status.ready");
            } catch (Exception e) {
                Status = ThemeManager.GetString("packages.status.error");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            } finally {
                IsThemesLoading = false;
            }
        }

        void ApplyThemeFilter() {
            FilteredThemes.Clear();
            var search = (ThemeSearchText ?? string.Empty).ToLowerInvariant();
            var filter = SelectedThemeTypeFilter ?? "All";

            foreach (var theme in Themes) {
                if (filter == "UI Themes" && theme.IsSingerTheme) continue;
                if (filter == "Singer Themes" && !theme.IsSingerTheme) continue;

                if (!string.IsNullOrWhiteSpace(search)) {
                    bool nameMatch = theme.Name.ToLowerInvariant().Contains(search);
                    bool authorMatch = theme.Author.ToLowerInvariant().Contains(search);
                    bool singerMatch = theme.Singers.ToLowerInvariant().Contains(search);
                    bool descMatch = theme.Description.ToLowerInvariant().Contains(search);
                    if (!nameMatch && !authorMatch && !singerMatch && !descMatch) continue;
                }

                FilteredThemes.Add(theme);
            }
        }

        public async Task InstallThemeAsync(ThemeRowViewModel row) {
            try {
                if (row.Software == null) {
                    DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(
                        new InvalidOperationException("No registry entry to install.")));
                    return;
                }
                var baseStatus = string.Format(ThemeManager.GetString("packages.themes.status.installing"), row.Id);
                Status = baseStatus;
                var progress = new Progress<int>(p => Status = $"{baseStatus} ({p}%)");
                if (row.IsSingerTheme) {
                    await PackageManager.Inst.InstallSingerThemeAsync(row.Software, progress);
                } else {
                    await PackageManager.Inst.InstallThemeAsync(row.Software, progress);
                }
                await RefreshThemesAsync(forceRefresh: false);
                Status = ThemeManager.GetString("packages.themes.status.installfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("packages.themes.status.installfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public async Task UninstallThemeAsync(ThemeRowViewModel row) {
            try {
                if (!row.IsInstalled) return;
                Status = string.Format(ThemeManager.GetString("packages.status.uninstalling"), row.Id);
                if (row.IsSingerTheme) {
                    await PackageManager.Inst.UninstallSingerThemeAsync(row.Id);
                } else {
                    await PackageManager.Inst.UninstallThemeAsync(row.Id);
                }
                await RefreshThemesAsync(forceRefresh: false);
                Status = ThemeManager.GetString("packages.status.uninstallfinished");
            } catch (Exception e) {
                Status = ThemeManager.GetString("packages.status.uninstallfailed");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        void UpdateThemeCounters() {
            ThemeTotalCount = Themes.Count;
            ThemeInstalledCount = Themes.Count(r => r.IsInstalled);
            ThemeUpdateCount = Themes.Count(r => r.HasUpdate);
            RaiseSectionDependent();
        }
    }
}
