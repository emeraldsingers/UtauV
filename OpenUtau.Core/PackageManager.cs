using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SharpCompress.Archives;
using SharpCompress.Readers;
using OpenUtau.Core.Util;
using YamlDotNet.Serialization;

namespace OpenUtau.Core {
    public class RegistryMirror {
        public string url = string.Empty;
        public string hash = string.Empty;
    }
    public class RegistryVersion {
        public string version = string.Empty;
        public string description = string.Empty;
        public Dictionary<string, string> descriptions = new Dictionary<string, string>();
        public RegistryMirror[] mirrors = [];

        public string LocalizedDescription() {
            if (descriptions != null && descriptions.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) {
                return en;
            }
            if (descriptions != null && descriptions.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) is string any) {
                return any;
            }
            return description ?? string.Empty;
        }
    }

    public class RegistryVoicebankInfo {
        public string image_url = string.Empty;
        public string thumbnail_url = string.Empty;
        public string website_url = string.Empty;
        public string singer_link = string.Empty;
        public string group_id = string.Empty;
        public string group_name = string.Empty;
        public string group_description = string.Empty;
        public string variant_name = string.Empty;
        public string[] image_urls = [];
        public string[] languages = [];
        public string[] types = [];
        public string[] engines = [];
        public string[] attributes = [];
        public string gender = string.Empty;
        public string character = string.Empty;
        public string install_subdir = string.Empty;
        public string demo_url = string.Empty;
    }

    public class RegistrySoftware {
        public string id = string.Empty;
        public string team = string.Empty;
        public string name = string.Empty;
        public Dictionary<string, string> names = new Dictionary<string, string>();
        public string description = string.Empty;
        public string long_description = string.Empty;
        public Dictionary<string, string> descriptions = new Dictionary<string, string>();
        public string category = string.Empty;
        public string[] developers = [];
        public string homepage_url = string.Empty;
        public string download_page_url = string.Empty;
        public string[] tags = [];
        public RegistryVersion[] versions = [];

        public string image_url = string.Empty;
        public string thumbnail_url = string.Empty;
        public string website_url = string.Empty;
        public string singer_link = string.Empty;
        public string group_id = string.Empty;
        public string group_name = string.Empty;
        public string group_description = string.Empty;
        public string group_long_description = string.Empty;
        public string variant_name = string.Empty;
        public string[] image_urls = [];
        public string[] languages = [];
        public string[] voicebank_types = [];
        public string[] engines = [];
        public string[] attributes = [];
        public string gender = string.Empty;
        public string character = string.Empty;
        public string install_subdir = string.Empty;
        public string demo_url = string.Empty;

        public RegistryVoicebankInfo voicebank = new RegistryVoicebankInfo();

        public Dictionary<string, string>? palette = null;

        public Dictionary<string, string>? theme_manifest = null;

        public string LocalizedName() {
            if (!string.IsNullOrWhiteSpace(name)) return name;
            if (names.TryGetValue("en", out var n)) return n;
            if (names.Values.FirstOrDefault() is string v) return v;
            return id;
        }

        public string LocalizedDescription() {
            if (descriptions != null && descriptions.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en)) {
                return en;
            }
            if (descriptions != null && descriptions.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) is string any) {
                return any;
            }
            return description ?? string.Empty;
        }

        public string GetVoicebankWebsiteUrl() {
            if (!string.IsNullOrWhiteSpace(voicebank?.website_url)) {
                return voicebank.website_url;
            }
            if (!string.IsNullOrWhiteSpace(website_url)) {
                return website_url;
            }
            return homepage_url ?? string.Empty;
        }

        public string GetVoicebankImageUrl() {
            if (!string.IsNullOrWhiteSpace(voicebank?.image_url)) {
                return voicebank.image_url;
            }
            if (!string.IsNullOrWhiteSpace(voicebank?.thumbnail_url)) {
                return voicebank.thumbnail_url;
            }
            if (!string.IsNullOrWhiteSpace(image_url)) {
                return image_url;
            }
            if (!string.IsNullOrWhiteSpace(thumbnail_url)) {
                return thumbnail_url;
            }
            if (voicebank?.image_urls != null) {
                var nested = voicebank.image_urls.FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
                if (!string.IsNullOrWhiteSpace(nested)) {
                    return nested;
                }
            }
            if (image_urls != null) {
                var top = image_urls.FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
                if (!string.IsNullOrWhiteSpace(top)) {
                    return top;
                }
            }
            return string.Empty;
        }

        public string[] GetVoicebankLanguages() {
            return MergeDistinct(voicebank?.languages, languages);
        }

        public string[] GetVoicebankTypes() {
            return MergeDistinct(voicebank?.types, voicebank_types);
        }

        public string[] GetVoicebankEngines() {
            return MergeDistinct(voicebank?.engines, engines);
        }

        public string[] GetVoicebankAttributes() {
            return MergeDistinct(voicebank?.attributes, attributes);
        }

        public string GetVoicebankGender() {
            if (!string.IsNullOrWhiteSpace(voicebank?.gender)) {
                return voicebank.gender;
            }
            return gender ?? string.Empty;
        }

        public string GetVoicebankCharacter() {
            if (!string.IsNullOrWhiteSpace(voicebank?.character)) {
                return voicebank.character;
            }
            return character ?? string.Empty;
        }

        public string GetVoicebankInstallSubdir() {
            if (!string.IsNullOrWhiteSpace(voicebank?.install_subdir)) {
                return voicebank.install_subdir;
            }
            return install_subdir ?? string.Empty;
        }

        public string GetVoicebankGroupId() {
            if (!string.IsNullOrWhiteSpace(voicebank?.group_id)) {
                return voicebank.group_id;
            }
            if (!string.IsNullOrWhiteSpace(group_id)) {
                return group_id;
            }
            return DeriveGroupIdFromId(id);
        }

        public string GetVoicebankGroupName() {
            if (!string.IsNullOrWhiteSpace(voicebank?.group_name)) {
                return voicebank.group_name;
            }
            if (!string.IsNullOrWhiteSpace(group_name)) {
                return group_name;
            }
            var localized = LocalizedName();
            var separators = new[] { " - ", " | ", ":" };
            foreach (var sep in separators) {
                var idx = localized.IndexOf(sep, StringComparison.Ordinal);
                if (idx > 0) {
                    return localized.Substring(0, idx).Trim();
                }
            }
            var split = localized.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0) {
                return split[0];
            }
            return GetVoicebankGroupId();
        }

        public string GetVoicebankGroupDescription() {
            if (!string.IsNullOrWhiteSpace(voicebank?.group_description)) {
                return voicebank.group_description;
            }
            if (!string.IsNullOrWhiteSpace(group_description)) {
                return group_description;
            }
            return string.Empty;
        }

        public string GetVoicebankVariantName() {
            if (!string.IsNullOrWhiteSpace(voicebank?.variant_name)) {
                return voicebank.variant_name;
            }
            if (!string.IsNullOrWhiteSpace(variant_name)) {
                return variant_name;
            }
            var groupName = GetVoicebankGroupName();
            var localized = LocalizedName();
            if (!string.IsNullOrWhiteSpace(groupName) &&
                !string.IsNullOrWhiteSpace(localized) &&
                localized.StartsWith(groupName + " ", StringComparison.OrdinalIgnoreCase)) {
                var suffixByName = localized.Substring(groupName.Length).Trim().TrimStart('-', '_', '|', ':');
                if (!string.IsNullOrWhiteSpace(suffixByName)) {
                    return suffixByName;
                }
            }
            var group = GetVoicebankGroupId();
            if (!string.IsNullOrWhiteSpace(id) &&
                id.StartsWith(group + "-", StringComparison.OrdinalIgnoreCase)) {
                return FormatVariantLabel(id.Substring(group.Length + 1));
            }
            if (!string.IsNullOrWhiteSpace(id) &&
                id.StartsWith(group + "_", StringComparison.OrdinalIgnoreCase)) {
                return FormatVariantLabel(id.Substring(group.Length + 1));
            }
            return !string.IsNullOrWhiteSpace(localized) ? localized : FormatVariantLabel(id);
        }

        static string[] MergeDistinct(params string[][]? arrays) {
            return arrays
                .Where(arr => arr != null)
                .SelectMany(arr => arr!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static string DeriveGroupIdFromId(string rawId) {
            if (string.IsNullOrWhiteSpace(rawId)) {
                return "voicebank";
            }
            var split = rawId.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0) {
                return split[0].ToLowerInvariant();
            }
            return rawId.ToLowerInvariant();
        }

        static string FormatVariantLabel(string raw) {
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
    }

    public class OudepEntrypoint {
        public string loader = string.Empty;

        public string path = string.Empty;
    }

    public class OudepMetadata {
        public string id = string.Empty;
        [Obsolete] public string? name = null;
        public string version = string.Empty;
        public string description = string.Empty;
        [Obsolete] public string @class = string.Empty;
        public OudepEntrypoint[] entrypoints = [];
    }

    public class OuvbMetadata {
        public string id = string.Empty;
        public string name = string.Empty;
        public string team = string.Empty;
        public string version = string.Empty;
        public string description = string.Empty;
        public string long_description = string.Empty;
        public string website_url = string.Empty;
        public string singer_link = string.Empty;
        public string source_url = string.Empty;
        public string[] languages = [];
        public string[] types = [];
        public string[] engines = [];
        public string installed_at_utc = string.Empty;

        [YamlIgnore]
        public string install_path = string.Empty;
    }

    public class OuplugMetadata {
        public string id = string.Empty;
        public string name = string.Empty;
        public string team = string.Empty;
        public string version = string.Empty;
        public string description = string.Empty;
        public string long_description = string.Empty;
        public string category = string.Empty;
        public string url = string.Empty;
        public string installed_at_utc = string.Empty;

        [YamlIgnore]
        public string install_path = string.Empty;
    }




    public class OuthemeMetadata {
        public string id = string.Empty;
        public string name = string.Empty;
        public string author = string.Empty;
        public string version = string.Empty;
        public string description = string.Empty;
        public string long_description = string.Empty;
        public string type = "theme";
        public string git_username = string.Empty;
        public string repo = string.Empty;
        public string preview_image = string.Empty;
        public string installed_at_utc = string.Empty;

        [YamlIgnore]
        public string install_path = string.Empty;
    }

    public class OusthemeMetadata {
        public string id = string.Empty;
        public string name = string.Empty;
        public string author = string.Empty;
        public string version = string.Empty;
        public string description = string.Empty;
        public string long_description = string.Empty;
        public string type = "singer_theme";
        public string git_username = string.Empty;
        public string repo = string.Empty;
        public string preview_image = string.Empty;
        public string singers = string.Empty;
        public string installed_at_utc = string.Empty;

        [YamlIgnore]
        public string install_path = string.Empty;
    }

    public class PackageManager : SingletonBase<PackageManager> {
        public const string OudepExt = ".oudep";
        public const string OuvbMetadataFile = "ouvb.yaml";
        public const string OuplugMetadataFile = "ouplug.yaml";

        public const string OuthemeExt = ".outheme";

        public const string OusthemeExt = ".oustheme";

        public const string OuthemeMetadataFile = "outheme.yaml";

        public const string OusthemeMetadataFile = "oustheme.yaml";

        const string themeRegistryUrlUtauVRequested = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/refs/heads/main/themes.json";
        const string registryUrl = "https://openutau.github.io/svs-index/registry/v1/softwares/all.json";
        const string registryUrlUtauVRequested = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/refs/heads/main/utauv.json";
        const string pluginRegistryUrlUtauVRequested = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/refs/heads/main/utauv_plugins.json";
        const string registryUrlUtauVFallback = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/main/all.json";
        const string voicebankRegistryUrlUtauVRequested = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/refs/heads/main/voicebanks.json";
        const string voicebankRegistryUrlUtauVFallback = "https://raw.githubusercontent.com/emeraldsingers/UtauV_Packages/main/voicebanks.json";
        static readonly string[] voicebankTags = [
            "voicebank", "voicebanks", "singer", "singers", "utau-voicebank", "utau-singer"
        ];
        static readonly string[] voicebankCategories = [
            "voicebank", "voicebanks", "singer", "singers"
        ];

        static readonly string[] themeTags = [
            "UtauV_Theme"
        ];

        static readonly string[] singerThemeTags = [
            "UtauV_SingerTheme"
        ];
        static readonly Regex googleDriveFileIdRegex = new Regex(@"\/file\/d\/([A-Za-z0-9_-]+)", RegexOptions.Compiled);

        sealed class RegistrySource {
            public string Primary { get; }
            public string Fallback { get; }

            public RegistrySource(string primary, string fallback = "") {
                Primary = primary;
                Fallback = fallback;
            }
        }

        public async Task<List<RegistrySoftware>> FetchRegistryAsync() {
            var merged = await FetchMergedRegistryAsync([
                new RegistrySource(registryUrl),
                new RegistrySource(registryUrlUtauVRequested, registryUrlUtauVFallback),
            ], "package");
            return merged.Values
                .Where(IsDependencyEntry)
                .OrderBy(s => s.LocalizedName(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<RegistrySoftware>> FetchVoicebankRegistryAsync() {
            var merged = await FetchMergedRegistryAsync([
                new RegistrySource(voicebankRegistryUrlUtauVRequested, voicebankRegistryUrlUtauVFallback),
                new RegistrySource(registryUrlUtauVRequested, registryUrlUtauVFallback),
            ], "voicebank");
            return merged.Values
                .Where(IsVoicebankEntry)
                .OrderBy(s => s.LocalizedName(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<RegistrySoftware>> FetchPluginRegistryAsync() {
            var merged = await FetchMergedRegistryAsync([
                new RegistrySource(pluginRegistryUrlUtauVRequested)
            ], "plugin");
            return merged.Values
                .Where(IsPluginEntry)
                .OrderBy(s => s.LocalizedName(), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static bool IsPluginEntry(RegistrySoftware software) {
            if (string.Equals(software.category, "Batch Edits", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(software.category, "Phonemizer", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(software.category, "plugin", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (software.tags != null && software.tags.Any(tag => string.Equals("plugin", tag, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
            return false;
        }

        static bool IsDependencyEntry(RegistrySoftware software) {
            return software.tags != null &&
                software.tags.Any(tag => string.Equals(tag, "oudep", StringComparison.OrdinalIgnoreCase));
        }

        static bool IsVoicebankEntry(RegistrySoftware software) {
            if (software.tags != null && software.tags.Any(tag =>
                voicebankTags.Any(vTag => string.Equals(vTag, tag, StringComparison.OrdinalIgnoreCase)))) {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(software.category) &&
                voicebankCategories.Any(cat => string.Equals(cat, software.category, StringComparison.OrdinalIgnoreCase))) {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(software.GetVoicebankImageUrl())) {
                return true;
            }
            if (software.GetVoicebankLanguages().Length > 0 ||
                software.GetVoicebankTypes().Length > 0 ||
                software.GetVoicebankEngines().Length > 0) {
                return true;
            }
            return false;
        }

        static bool IsThemeEntry(RegistrySoftware software) {
            return software.tags != null && software.tags.Any(tag =>
                themeTags.Any(tTag => string.Equals(tTag, tag, StringComparison.OrdinalIgnoreCase)));
        }

        static bool IsSingerThemeEntry(RegistrySoftware software) {
            return software.tags != null && software.tags.Any(tag =>
                singerThemeTags.Any(stTag => string.Equals(stTag, tag, StringComparison.OrdinalIgnoreCase)));
        }

        public static string BuildThemeId(string gitUsername, string repo, string themeName) {
            if (string.IsNullOrWhiteSpace(gitUsername)) throw new ArgumentException("gitUsername is required", nameof(gitUsername));
            if (string.IsNullOrWhiteSpace(repo)) throw new ArgumentException("repo is required", nameof(repo));
            if (string.IsNullOrWhiteSpace(themeName)) throw new ArgumentException("themeName is required", nameof(themeName));

            var slug = string.Join(".",
                NormalizeThemeSlug(gitUsername),
                NormalizeThemeSlug(repo),
                NormalizeThemeSlug(themeName));

            var raw = $"{gitUsername.Trim()}/{repo.Trim()}/{themeName.Trim()}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            var hash8 = BitConverter.ToString(hashBytes, 0, 4).Replace("-", string.Empty).ToLowerInvariant();

            return $"{slug}.{hash8}";
        }

        static string NormalizeThemeSlug(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "unknown";
            }
            var chars = value.Trim().ToLowerInvariant().Select(ch =>
                (char.IsLetterOrDigit(ch) || ch == '.') ? ch : '-').ToArray();
            var normalized = new string(chars);
            while (normalized.Contains("--", StringComparison.Ordinal)) {
                normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
            }
            normalized = normalized.Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
        }

        async Task<Dictionary<string, RegistrySoftware>> FetchMergedRegistryAsync(
            IEnumerable<RegistrySource> sources,
            string sourceKind) {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "OpenUtau");
            client.Timeout = TimeSpan.FromSeconds(30);

            var merged = new Dictionary<string, RegistrySoftware>(StringComparer.OrdinalIgnoreCase);
            Exception? lastError = null;
            bool hasSuccess = false;

            foreach (var source in sources) {
                try {
                    var items = string.IsNullOrWhiteSpace(source.Fallback)
                        ? await FetchRegistrySourceAsync(client, source.Primary)
                        : await FetchRegistrySourceWithFallbackAsync(client, source.Primary, source.Fallback);
                    foreach (var software in items) {
                        MergeSoftware(merged, software);
                    }
                    hasSuccess = true;
                } catch (Exception e) {
                    lastError = e;
                    Log.Warning(e, "Failed to fetch {sourceKind} registry source {sourceUrl}", sourceKind, source.Primary);
                }
            }

            if (!hasSuccess) {
                throw lastError ?? new InvalidOperationException($"Failed to fetch {sourceKind} registry.");
            }
            return merged;
        }

        async Task<List<RegistrySoftware>> FetchRegistrySourceAsync(HttpClient client, string sourceUrl) {
            using var response = await client.GetAsync(sourceUrl);
            response.EnsureSuccessStatusCode();
            string body = await response.Content.ReadAsStringAsync();
            return ParseRegistryBody(body, sourceUrl);
        }

        async Task<List<RegistrySoftware>> FetchRegistrySourceWithFallbackAsync(
            HttpClient client,
            string primarySourceUrl,
            string fallbackSourceUrl) {
            try {
                return await FetchRegistrySourceAsync(client, primarySourceUrl);
            } catch {
                return await FetchRegistrySourceAsync(client, fallbackSourceUrl);
            }
        }

        static List<RegistrySoftware> ParseRegistryBody(string body, string sourceUrl) {
            try {
                var token = JToken.Parse(body);
                if (token.Type == JTokenType.Array) {
                    return token.ToObject<List<RegistrySoftware>>() ?? new List<RegistrySoftware>();
                }
                if (token is JObject obj) {
                    var items = obj["items"];
                    if (items != null && items.Type == JTokenType.Array) {
                        return items.ToObject<List<RegistrySoftware>>() ?? new List<RegistrySoftware>();
                    }
                    var singers = ParseSingerRegistryBody(obj);
                    if (singers.Count > 0) {
                        return singers;
                    }
                }
            } catch (Exception e) {
                throw new InvalidDataException($"Failed to parse registry JSON from {sourceUrl}", e);
            }
            return new List<RegistrySoftware>();
        }

        static List<RegistrySoftware> ParseSingerRegistryBody(JObject root) {
            var singersToken = root["singers"];
            if (singersToken == null) {
                return new List<RegistrySoftware>();
            }

            var result = new List<RegistrySoftware>();
            foreach (var (singerKey, singerObj) in EnumerateNamedObjects(singersToken, "singer")) {
                var singerNames = ParseLocalizedMap(singerObj["names"]);
                singerNames.TryGetValue("en", out var singerNameEn);
                var singerDescriptions = ParseLocalizedMap(singerObj["descriptions"]);
                singerDescriptions.TryGetValue("en", out var singerDescriptionEn);
                var singerId = NormalizeRegistryId(FirstNonEmpty(
                    AsString(singerObj["id"]),
                    singerKey,
                    AsString(singerObj["name"]),
                    "singer"));
                var singerName = FirstNonEmpty(
                    AsString(singerObj["name"]),
                    singerNameEn,
                    singerKey,
                    singerId);
                var singerDescription = FirstNonEmpty(
                    AsString(singerObj["description"]),
                    singerDescriptionEn);
                var singerLongDescription = AsString(singerObj["long_description"]);

                var singerWebsite = FirstNonEmpty(
                    AsString(singerObj["website_url"]),
                    AsString(singerObj["homepage_url"]),
                    AsString(singerObj["url"]));
                var singerLink = AsString(singerObj["link"]) ?? string.Empty;
                
                if (string.IsNullOrWhiteSpace(singerWebsite)) {
                    singerWebsite = string.Empty;
                }
                var singerImages = MergeStringArrays(
                    ParseStringArray(singerObj["images"]),
                    ParseStringArray(singerObj["image_urls"]));
                var singerImage = FirstNonEmpty(
                    AsString(singerObj["image_url"]),
                    singerImages.FirstOrDefault());
                var singerDevelopers = MergeStringArrays(
                    ParseStringArray(singerObj["developers"]),
                    ParseStringArray(singerObj["authors"]));
                var cvName = AsString(singerObj["cv"]);
                if (!string.IsNullOrWhiteSpace(cvName)) {
                    singerDevelopers = MergeStringArrays(singerDevelopers, new[] { cvName });
                }
                var team = AsString(singerObj["team"]) ?? string.Empty;

                var singerVoicebanksToken = singerObj["voicebanks"];
                if (singerVoicebanksToken == null) {
                    continue;
                }
                foreach (var (voicebankKey, voicebankObj) in EnumerateNamedObjects(singerVoicebanksToken, "voicebank")) {
                    var variantName = FirstNonEmpty(
                        AsString(voicebankObj["variant_name"]),
                        AsString(voicebankObj["name"]),
                        voicebankKey,
                        "Unknown");
                    var rawId = FirstNonEmpty(
                        AsString(voicebankObj["id"]),
                        $"{singerId}-{variantName}");
                    var voicebankId = NormalizeRegistryId(rawId);
                    var variantTypes = ParseStringArray(voicebankObj["types"]);
                    if (variantTypes.Length == 0 && !string.IsNullOrWhiteSpace(variantName)) {
                        variantTypes = new[] { variantName };
                    }
                    var engines = ParseStringArray(voicebankObj["engines"]);
                    var engineFromType = AsString(voicebankObj["type"]);
                    if (engines.Length == 0 && !string.IsNullOrWhiteSpace(engineFromType)) {
                        engines = new[] { engineFromType };
                    }
                    var languages = MergeStringArrays(
                        ParseStringArray(voicebankObj["supportedLanguages"]),
                        ParseStringArray(voicebankObj["languages"]));
                    var attributes = ParseStringArray(voicebankObj["attributes"]);
                    var voicebankWebsite = FirstNonEmpty(
                        AsString(voicebankObj["website_url"]),
                        AsString(voicebankObj["homepage_url"]),
                        singerWebsite);
                    var voicebankLink = FirstNonEmpty(
                        AsString(voicebankObj["link"]),
                        singerLink);
                    var voicebankImages = MergeStringArrays(
                        ParseStringArray(voicebankObj["images"]),
                        MergeStringArrays(
                            ParseStringArray(voicebankObj["image_urls"]),
                            singerImages));
                    var voicebankImage = FirstNonEmpty(
                        AsString(voicebankObj["image_url"]),
                        voicebankImages.FirstOrDefault(),
                        singerImage);
                    var description = FirstNonEmpty(
                        AsString(voicebankObj["description"]),
                        singerDescription);
                    var displayName = FirstNonEmpty(
                        AsString(voicebankObj["display_name"]),
                        AsString(voicebankObj["name"]),
                        $"{singerName} {variantName}".Trim());
                    var installSubdir = FirstNonEmpty(
                        AsString(voicebankObj["install_subdir"]),
                        $"{singerId}_{NormalizeRegistryId(variantName)}");

                    var versions = ParseVoicebankVersions(voicebankObj);
                    var tags = MergeStringArrays(
                        ParseStringArray(voicebankObj["tags"]),
                        MergeStringArrays(
                            ParseStringArray(singerObj["tags"]),
                            new[] { "voicebank", "utau-singer" }));
                    var developers = MergeStringArrays(
                        ParseStringArray(voicebankObj["developers"]),
                        singerDevelopers);
                    var gender = FirstNonEmpty(
                        AsString(voicebankObj["gender"]),
                        AsString(singerObj["gender"]),
                        AsString(singerObj.SelectToken("characterData.Gender")));
                    var character = FirstNonEmpty(
                        AsString(voicebankObj["character"]),
                        AsString(singerObj["character"]),
                        AsString(singerObj.SelectToken("characterData.Species")));

                    var names = ParseLocalizedMap(voicebankObj["names"]);
                    if (names.Count == 0) {
                        names["en"] = displayName;
                    }
                    var descriptions = ParseLocalizedMap(voicebankObj["descriptions"]);

                    result.Add(new RegistrySoftware {
                        id = voicebankId,
                        team = team,
                        names = names,
                        description = description ?? string.Empty,
                        descriptions = descriptions,
                        category = "voicebank",
                        developers = developers,
                        homepage_url = voicebankWebsite ?? string.Empty,
                        download_page_url = FirstNonEmpty(
                            AsString(voicebankObj["download_page_url"]),
                            voicebankWebsite) ?? string.Empty,
                        tags = tags,
                        versions = versions,
                        image_url = voicebankImage ?? string.Empty,
                        thumbnail_url = voicebankImage ?? string.Empty,
                        website_url = voicebankWebsite ?? string.Empty,
                        singer_link = voicebankLink ?? string.Empty,
                        group_id = singerId,
                        group_name = singerName ?? singerId,
                        group_description = singerDescription ?? string.Empty,
                        group_long_description = singerLongDescription ?? string.Empty,
                        variant_name = variantName ?? string.Empty,
                        image_urls = voicebankImages,
                        languages = languages,
                        voicebank_types = variantTypes,
                        engines = engines,
                        attributes = attributes,
                        gender = gender ?? string.Empty,
                        character = character ?? string.Empty,
                        install_subdir = installSubdir ?? string.Empty,
                        voicebank = new RegistryVoicebankInfo {
                            image_url = voicebankImage ?? string.Empty,
                            thumbnail_url = voicebankImage ?? string.Empty,
                            website_url = voicebankWebsite ?? string.Empty,
                            singer_link = voicebankLink ?? string.Empty,
                            group_id = singerId,
                            group_name = singerName ?? singerId,
                            group_description = singerDescription ?? string.Empty,
                            variant_name = variantName ?? string.Empty,
                            image_urls = voicebankImages,
                            languages = languages,
                            types = variantTypes,
                            engines = engines,
                            attributes = attributes,
                            gender = gender ?? string.Empty,
                            character = character ?? string.Empty,
                            install_subdir = installSubdir ?? string.Empty,
                        },
                    });
                }
            }
            return result;
        }

        static RegistryVersion[] ParseVoicebankVersions(JObject voicebankObj) {
            var map = new Dictionary<string, RegistryVersion>(StringComparer.OrdinalIgnoreCase);
            void AddVersion(RegistryVersion candidate) {
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.version)) {
                    return;
                }
                candidate.mirrors ??= [];
                candidate.mirrors = candidate.mirrors
                    .Where(m => !string.IsNullOrWhiteSpace(m.url))
                    .GroupBy(m => m.url, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToArray();
                if (candidate.mirrors.Length == 0) {
                    return;
                }
                if (!map.TryGetValue(candidate.version, out var existing)) {
                    map[candidate.version] = candidate;
                } else {
                    MergeVersion(existing, candidate);
                }
            }

            if (voicebankObj["versions"] is JArray explicitVersions) {
                foreach (var token in explicitVersions.OfType<JObject>()) {
                    var version = FirstNonEmpty(
                        AsString(token["version"]),
                        AsString(token["name"]),
                        AsString(token["label"]));
                    var mirrors = ParseMirrors(token["mirrors"], AsString(token["url"]), AsString(token["hash"]));
                    if (string.IsNullOrWhiteSpace(version) || mirrors.Length == 0) {
                        continue;
                    }
                    AddVersion(new RegistryVersion {
                        version = version,
                        description = FirstNonEmpty(AsString(token["description"]), AsString(token["desc"])) ?? string.Empty,
                        mirrors = mirrors,
                    });
                }
            }

            var historyToken = voicebankObj["history"];
            if (historyToken is JObject historyObj) {
                foreach (var prop in historyObj.Properties()) {
                    if (prop.Value is JObject entryObj) {
                        var version = FirstNonEmpty(
                            AsString(entryObj["version"]),
                            prop.Name);
                        var mirrors = ParseMirrors(entryObj["mirrors"], AsString(entryObj["url"]), AsString(entryObj["hash"]));
                        if (string.IsNullOrWhiteSpace(version) || mirrors.Length == 0) {
                            continue;
                        }
                        AddVersion(new RegistryVersion {
                            version = version,
                            description = FirstNonEmpty(AsString(entryObj["description"]), AsString(entryObj["desc"])) ?? string.Empty,
                            mirrors = mirrors,
                        });
                    } else {
                        var url = AsString(prop.Value);
                        if (string.IsNullOrWhiteSpace(url)) {
                            continue;
                        }
                        AddVersion(new RegistryVersion {
                            version = prop.Name,
                            description = "Archived release",
                            mirrors = ParseMirrors(null, url, null),
                        });
                    }
                }
            }

            var latestUrl = FirstNonEmpty(
                AsString(voicebankObj["url"]),
                AsString(voicebankObj["download_url"]),
                AsString(voicebankObj["downloadUrl"]));
            if (!string.IsNullOrWhiteSpace(latestUrl) && map.Count == 0) {
                AddVersion(new RegistryVersion {
                    version = FirstNonEmpty(
                        AsString(voicebankObj["latest_version"]),
                        AsString(voicebankObj["version"]),
                        "1.0.0") ?? "1.0.0",
                    description = FirstNonEmpty(
                        AsString(voicebankObj["latest_description"]),
                        "Release") ?? "Release",
                    mirrors = ParseMirrors(null, latestUrl, AsString(voicebankObj["hash"])),
                });
            }

            return map.Values.ToArray();
        }

        static RegistryMirror[] ParseMirrors(JToken? mirrorsToken, string? fallbackUrl, string? fallbackHash) {
            var mirrors = new List<RegistryMirror>();
            if (mirrorsToken is JArray mirrorsArray) {
                foreach (var token in mirrorsArray) {
                    if (token is JObject mirrorObj) {
                        var url = AsString(mirrorObj["url"]);
                        if (string.IsNullOrWhiteSpace(url)) {
                            continue;
                        }
                        mirrors.Add(new RegistryMirror {
                            url = url,
                            hash = AsString(mirrorObj["hash"]) ?? string.Empty,
                        });
                    } else {
                        var url = AsString(token);
                        if (!string.IsNullOrWhiteSpace(url)) {
                            mirrors.Add(new RegistryMirror { url = url });
                        }
                    }
                }
            }
            if (mirrors.Count == 0 && !string.IsNullOrWhiteSpace(fallbackUrl)) {
                mirrors.Add(new RegistryMirror {
                    url = fallbackUrl,
                    hash = fallbackHash ?? string.Empty,
                });
            }
            return mirrors
                .Where(m => !string.IsNullOrWhiteSpace(m.url))
                .GroupBy(m => m.url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
        }

        static IEnumerable<(string Key, JObject Obj)> EnumerateNamedObjects(JToken token, string fallbackPrefix) {
            if (token is JObject obj) {
                foreach (var prop in obj.Properties()) {
                    if (prop.Value is JObject childObj) {
                        yield return (prop.Name, childObj);
                    } else if (prop.Value is JValue value && value.Type == JTokenType.String) {
                        yield return (prop.Name, new JObject {
                            ["name"] = prop.Name,
                            ["url"] = value.Value<string>() ?? string.Empty,
                        });
                    }
                }
                yield break;
            }
            if (token is JArray array) {
                int index = 0;
                foreach (var item in array) {
                    index++;
                    if (item is JObject itemObj) {
                        var key = FirstNonEmpty(
                            AsString(itemObj["id"]),
                            AsString(itemObj["name"]),
                            $"{fallbackPrefix}-{index}") ?? $"{fallbackPrefix}-{index}";
                        yield return (key, itemObj);
                    } else if (item is JValue value && value.Type == JTokenType.String) {
                        var key = $"{fallbackPrefix}-{index}";
                        yield return (key, new JObject {
                            ["name"] = key,
                            ["url"] = value.Value<string>() ?? string.Empty,
                        });
                    }
                }
            }
        }

        static Dictionary<string, string> ParseLocalizedMap(JToken? token) {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (token is not JObject obj) {
                return map;
            }
            foreach (var prop in obj.Properties()) {
                var value = AsString(prop.Value);
                if (!string.IsNullOrWhiteSpace(prop.Name) && !string.IsNullOrWhiteSpace(value)) {
                    map[prop.Name] = value;
                }
            }
            return map;
        }

        static string[] ParseStringArray(JToken? token) {
            if (token == null) {
                return [];
            }
            if (token is JArray array) {
                return array
                    .Select(AsString)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()!;
            }
            var single = AsString(token);
            return string.IsNullOrWhiteSpace(single) ? [] : new[] { single };
        }

        static string NormalizeRegistryId(string? value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return "voicebank";
            }
            var chars = value.Trim().ToLowerInvariant().Select(ch => {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.') {
                    return ch;
                }
                return '-';
            }).ToArray();
            var normalized = new string(chars);
            while (normalized.Contains("--", StringComparison.Ordinal)) {
                normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
            }
            normalized = normalized.Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "voicebank" : normalized;
        }

        static string? AsString(JToken? token) {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) {
                return null;
            }
            if (token.Type == JTokenType.String) {
                var value = token.Value<string>();
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            var str = token.ToString();
            return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
        }

        static string? FirstNonEmpty(params string?[] values) {
            foreach (var value in values) {
                if (!string.IsNullOrWhiteSpace(value)) {
                    return value;
                }
            }
            return null;
        }

        static void MergeSoftware(Dictionary<string, RegistrySoftware> merged, RegistrySoftware incoming) {
            if (incoming == null || string.IsNullOrWhiteSpace(incoming.id)) {
                return;
            }
            if (!merged.TryGetValue(incoming.id, out var existing)) {
                merged[incoming.id] = incoming;
                return;
            }

            existing.names ??= new Dictionary<string, string>();
            MergeLocalizedMap(existing.names, incoming.names);
            existing.descriptions ??= new Dictionary<string, string>();
            MergeLocalizedMap(existing.descriptions, incoming.descriptions);

            if (string.IsNullOrEmpty(existing.category) && !string.IsNullOrEmpty(incoming.category)) {
                existing.category = incoming.category;
            }
            if (string.IsNullOrEmpty(existing.description) && !string.IsNullOrEmpty(incoming.description)) {
                existing.description = incoming.description;
            }
            if (string.IsNullOrEmpty(existing.long_description) && !string.IsNullOrEmpty(incoming.long_description)) {
                existing.long_description = incoming.long_description;
            }
            if (string.IsNullOrEmpty(existing.homepage_url) && !string.IsNullOrEmpty(incoming.homepage_url)) {
                existing.homepage_url = incoming.homepage_url;
            }
            if (string.IsNullOrEmpty(existing.download_page_url) && !string.IsNullOrEmpty(incoming.download_page_url)) {
                existing.download_page_url = incoming.download_page_url;
            }

            existing.developers = (existing.developers ?? [])
                .Concat(incoming.developers ?? [])
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            existing.tags = (existing.tags ?? [])
                .Concat(incoming.tags ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            existing.image_url = PickFirstNonEmpty(existing.image_url, incoming.image_url);
            existing.thumbnail_url = PickFirstNonEmpty(existing.thumbnail_url, incoming.thumbnail_url);
            existing.website_url = PickFirstNonEmpty(existing.website_url, incoming.website_url);
            existing.group_id = PickFirstNonEmpty(existing.group_id, incoming.group_id);
            existing.group_name = PickFirstNonEmpty(existing.group_name, incoming.group_name);
            existing.group_description = PickFirstNonEmpty(existing.group_description, incoming.group_description);
            existing.variant_name = PickFirstNonEmpty(existing.variant_name, incoming.variant_name);
            existing.languages = MergeStringArrays(existing.languages, incoming.languages);
            existing.voicebank_types = MergeStringArrays(existing.voicebank_types, incoming.voicebank_types);
            existing.engines = MergeStringArrays(existing.engines, incoming.engines);
            existing.attributes = MergeStringArrays(existing.attributes, incoming.attributes);
            existing.gender = PickFirstNonEmpty(existing.gender, incoming.gender);
            existing.character = PickFirstNonEmpty(existing.character, incoming.character);
            existing.install_subdir = PickFirstNonEmpty(existing.install_subdir, incoming.install_subdir);
            existing.demo_url = PickFirstNonEmpty(existing.demo_url, incoming.demo_url);
            existing.image_urls = MergeStringArrays(existing.image_urls, incoming.image_urls);

            existing.voicebank ??= new RegistryVoicebankInfo();
            incoming.voicebank ??= new RegistryVoicebankInfo();
            existing.voicebank.image_url = PickFirstNonEmpty(existing.voicebank.image_url, incoming.voicebank.image_url);
            existing.voicebank.thumbnail_url = PickFirstNonEmpty(existing.voicebank.thumbnail_url, incoming.voicebank.thumbnail_url);
            existing.voicebank.website_url = PickFirstNonEmpty(existing.voicebank.website_url, incoming.voicebank.website_url);
            existing.voicebank.group_id = PickFirstNonEmpty(existing.voicebank.group_id, incoming.voicebank.group_id);
            existing.voicebank.group_name = PickFirstNonEmpty(existing.voicebank.group_name, incoming.voicebank.group_name);
            existing.voicebank.group_description = PickFirstNonEmpty(existing.voicebank.group_description, incoming.voicebank.group_description);
            existing.voicebank.variant_name = PickFirstNonEmpty(existing.voicebank.variant_name, incoming.voicebank.variant_name);
            existing.voicebank.image_urls = MergeStringArrays(existing.voicebank.image_urls, incoming.voicebank.image_urls);
            existing.voicebank.languages = MergeStringArrays(existing.voicebank.languages, incoming.voicebank.languages);
            existing.voicebank.types = MergeStringArrays(existing.voicebank.types, incoming.voicebank.types);
            existing.voicebank.engines = MergeStringArrays(existing.voicebank.engines, incoming.voicebank.engines);
            existing.voicebank.attributes = MergeStringArrays(existing.voicebank.attributes, incoming.voicebank.attributes);
            existing.voicebank.gender = PickFirstNonEmpty(existing.voicebank.gender, incoming.voicebank.gender);
            existing.voicebank.character = PickFirstNonEmpty(existing.voicebank.character, incoming.voicebank.character);
            existing.voicebank.install_subdir = PickFirstNonEmpty(existing.voicebank.install_subdir, incoming.voicebank.install_subdir);
            existing.voicebank.demo_url = PickFirstNonEmpty(existing.voicebank.demo_url, incoming.voicebank.demo_url);

            var versions = new Dictionary<string, RegistryVersion>(StringComparer.OrdinalIgnoreCase);
            foreach (var version in existing.versions ?? []) {
                if (!string.IsNullOrWhiteSpace(version.version) && !versions.ContainsKey(version.version)) {
                    versions[version.version] = version;
                }
            }
            foreach (var version in incoming.versions ?? []) {
                if (string.IsNullOrWhiteSpace(version.version)) {
                    continue;
                }
                if (!versions.TryGetValue(version.version, out var existingVersion)) {
                    versions[version.version] = version;
                } else {
                    MergeVersion(existingVersion, version);
                }
            }
            existing.versions = versions.Values.ToArray();
        }

        static string[] MergeStringArrays(string[] current, string[] incoming) {
            return (current ?? [])
                .Concat(incoming ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static string PickFirstNonEmpty(string current, string incoming) {
            return string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(incoming)
                ? incoming
                : current;
        }

        static void MergeLocalizedMap(Dictionary<string, string> target, Dictionary<string, string> source) {
            if (target == null || source == null) {
                return;
            }
            foreach (var kv in source) {
                if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value)) {
                    continue;
                }
                if (!target.ContainsKey(kv.Key)) {
                    target[kv.Key] = kv.Value;
                }
            }
        }

        static void MergeVersion(RegistryVersion target, RegistryVersion source) {
            if (target == null || source == null) {
                return;
            }
            target.descriptions ??= new Dictionary<string, string>();
            MergeLocalizedMap(target.descriptions, source.descriptions ?? new Dictionary<string, string>());
            if (string.IsNullOrWhiteSpace(target.description) && !string.IsNullOrWhiteSpace(source.description)) {
                target.description = source.description;
            }
            target.mirrors = (target.mirrors ?? [])
                .Concat(source.mirrors ?? [])
                .Where(m => !string.IsNullOrWhiteSpace(m.url))
                .GroupBy(m => m.url, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
        }

        public async Task<List<OudepMetadata>> GetInstalledAsync() {
            var list = new List<OudepMetadata>();
            var depPath = PathManager.Inst.DependencyPath;
            if (!Directory.Exists(depPath)) {
                return list;
            }
            return await Task.Run(() => {
                var dirs = Directory.GetDirectories(depPath);
                var results = dirs.Select(dir => {
                    try {
                        var yamlPath = Path.Combine(dir, "oudep.yaml");
                        using var reader = new StreamReader(yamlPath, Encoding.UTF8);
                        var metadata = Core.Yaml.DefaultDeserializer.Deserialize<OudepMetadata>(reader) ?? new OudepMetadata();
                        if (string.IsNullOrEmpty(metadata.id)) {
                            metadata.id = Path.GetFileName(dir);
                        }
                        if (string.IsNullOrEmpty(metadata.version)) {
                            metadata.version = string.Empty;
                        }
                        return metadata;
                    } catch (Exception e) {
                        Log.Error($"Failed to read oudep.yaml in {dir} {e}");
                        return null;
                    }
                }).Where(r => r != null).Select(r => r!).ToList();
                return results;
            });
        }

        public class InstalledEntrypoint {
            public OudepMetadata Package { get; set; } = null!;
            public OudepEntrypoint Entrypoint { get; set; } = null!;
            public string PackagePath { get; set; } = string.Empty;
            public string EntrypointPath => Path.Combine(PackagePath, Entrypoint.path);
        }

        public async Task<List<InstalledEntrypoint>> GetInstalledEntrypointsAsync() {
            var list = new List<InstalledEntrypoint>();
            var depPath = PathManager.Inst.DependencyPath;
            if (!Directory.Exists(depPath)) {
                return list;
            }
            return await Task.Run(() => {
                var dirs = Directory.GetDirectories(depPath);
                foreach (var dir in dirs) {
                    OudepMetadata? metadata = null;
                    try {
                        var yamlPath = Path.Combine(dir, "oudep.yaml");
                        using var reader = new StreamReader(yamlPath, Encoding.UTF8);
                        metadata = Core.Yaml.DefaultDeserializer.Deserialize<OudepMetadata>(reader) ?? new OudepMetadata();
                        if (string.IsNullOrEmpty(metadata.id)) {
                            metadata.id = Path.GetFileName(dir);
                        }
                    } catch (Exception e) {
                        Log.Error(e, "Failed to read oudep.yaml in {dir}", dir);
                        continue;
                    }

                    if (metadata.entrypoints != null) {
                        foreach (var ep in metadata.entrypoints) {
                            try {
                                if (string.IsNullOrEmpty(ep.loader) || string.IsNullOrEmpty(ep.path)) {
                                    Log.Warning("Skipping entrypoint in package {id} with empty loader or path", metadata.id);
                                    continue;
                                }
                                list.Add(new InstalledEntrypoint {
                                    Package = metadata,
                                    Entrypoint = ep,
                                    PackagePath = dir,
                                });
                            } catch (Exception e) {
                                Log.Error(e, "Failed to load entrypoint '{path}' in package {id}", ep.path, metadata.id);
                            }
                        }
                    }

                    if ((metadata.entrypoints == null || metadata.entrypoints.Length == 0) && !string.IsNullOrEmpty(metadata.@class)) {
                        try {
                            list.Add(new InstalledEntrypoint {
                                Package = metadata,
                                Entrypoint = new OudepEntrypoint { loader = metadata.@class, path = string.Empty },
                                PackagePath = dir,
                            });
                        } catch (Exception e) {
                            Log.Error(e, "Failed to load fallback @class entrypoint in package {id}", metadata.id);
                        }
                    }
                }
                return list;
            });
        }

        public async Task<List<OuvbMetadata>> GetInstalledVoicebanksAsync() {
            return await Task.Run(() => {
                var found = new Dictionary<string, OuvbMetadata>(StringComparer.OrdinalIgnoreCase);
                foreach (var singersPath in PathManager.Inst.SingersPaths) {
                    if (!Directory.Exists(singersPath)) {
                        continue;
                    }
                    IEnumerable<string> manifestPaths;
                    try {
                        manifestPaths = Directory.EnumerateFiles(singersPath, OuvbMetadataFile, SearchOption.AllDirectories);
                    } catch {
                        continue;
                    }
                    foreach (var manifestPath in manifestPaths) {
                        try {
                            using var reader = new StreamReader(manifestPath, Encoding.UTF8);
                            var metadata = Core.Yaml.DefaultDeserializer.Deserialize<OuvbMetadata>(reader) ?? new OuvbMetadata();
                            var installPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(metadata.id)) {
                                metadata.id = Path.GetFileName(installPath);
                            }
                            metadata.version ??= string.Empty;
                            metadata.install_path = installPath;
                            if (!found.ContainsKey(metadata.id)) {
                                found[metadata.id] = metadata;
                            }
                        } catch (Exception e) {
                            Log.Warning(e, "Failed to read {manifest}", manifestPath);
                        }
                    }
                }
                return found.Values
                    .OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
        }

        public async Task<List<OuplugMetadata>> GetInstalledPluginsAsync() {
            return await Task.Run(() => {
                var found = new Dictionary<string, OuplugMetadata>(StringComparer.OrdinalIgnoreCase);
                var pluginsPath = PathManager.Inst.PluginsPath;
                if (!Directory.Exists(pluginsPath)) return found.Values.ToList();

                var manifestPaths = Directory.EnumerateFiles(pluginsPath, OuplugMetadataFile, SearchOption.AllDirectories);
                foreach (var manifestPath in manifestPaths) {
                    try {
                        using var reader = new StreamReader(manifestPath, Encoding.UTF8);
                        var metadata = Core.Yaml.DefaultDeserializer.Deserialize<OuplugMetadata>(reader) ?? new OuplugMetadata();
                        var installPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(metadata.id)) metadata.id = Path.GetFileName(installPath);
                        metadata.version ??= string.Empty;
                        metadata.install_path = installPath;
                        if (!found.ContainsKey(metadata.id)) found[metadata.id] = metadata;
                    } catch (Exception e) {
                        Log.Warning(e, "Failed to read {manifest}", manifestPath);
                    }
                }
                return found.Values.OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase).ToList();
            });
        }

        static string GetSha256Hex(byte[] data) {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            var sb = new StringBuilder();
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private class VersionStringComparer : IComparer<string> {
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

        private static readonly IComparer<string> VersionComparer = new VersionStringComparer();

        public static string GetLatestVersionString(RegistryVersion[] versions) {
            if (versions == null || versions.Length == 0) return string.Empty;
            return versions.OrderByDescending(v => v.version, VersionComparer).First().version;
        }

        public async Task InstallAsync(RegistrySoftware software, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            var latestVersion = software.versions.OrderByDescending(v => v.version, VersionComparer).First().version;
            await InstallVersionAsync(software, latestVersion, progress);
        }

        public async Task InstallVersionAsync(RegistrySoftware software, string versionString, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            if (string.IsNullOrWhiteSpace(versionString)) {
                throw new ArgumentException("Version is required", nameof(versionString));
            }
            var version = software.versions.FirstOrDefault(v =>
                string.Equals(v.version, versionString, StringComparison.OrdinalIgnoreCase));
            if (version == null) {
                throw new ArgumentException($"Version {versionString} not found for package {software.id}");
            }
            if (version.mirrors == null || version.mirrors.Length == 0) {
                throw new ArgumentException("No mirrors available");
            }
            var mirror = version.mirrors[0];
            var data = await DownloadMirrorAsync(mirror, progress);
            using var ms = new MemoryStream(data);
            await InstallFromStreamAsync(ms, software.id, version.version);
        }

        public async Task UninstallAsync(string id) {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            var basePath = Path.Combine(PathManager.Inst.DependencyPath, id);
            if (!Directory.Exists(basePath)) return;
            try {
                await Task.Run(() => Directory.Delete(basePath, true));
            } catch (Exception e) {
                Log.Warning(e, "Failed to uninstall dependency {id}", id);
                throw;
            }
        }

        public async Task InstallFromFileAsync(string archivePath) {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            await InstallFromStreamAsync(stream, string.Empty, string.Empty);
        }

        public async Task InstallFromStreamAsync(Stream stream, string expectedId, string expectedVersion) {
            using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions());
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, "Installing dependency"));
            var metadataEntry = archive.Entries.FirstOrDefault(e => e.Key == "oudep.yaml");
            if (metadataEntry == null) {
                throw new ArgumentException("Missing oudep.yaml");
            }
            OudepMetadata metadata;
            using (var entryStream = metadataEntry.OpenEntryStream()) {
                using var reader = new StreamReader(entryStream, Encoding.UTF8);
                metadata = Core.Yaml.DefaultDeserializer.Deserialize<OudepMetadata>(reader);
            }
            if (!string.IsNullOrEmpty(expectedId) && metadata.id != expectedId ||
                !string.IsNullOrEmpty(expectedVersion) && metadata.version != expectedVersion) {
                throw new ArgumentException("Archive metadata does not match expected id/version");
            }
            var id = metadata.id;
            if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(metadata.name)) {
                id = metadata.name;
            }
            await Task.Run(() => {
                var basePath = Path.Combine(PathManager.Inst.DependencyPath, id);
                try {
                    if (Directory.Exists(basePath)) {
                        Directory.Delete(basePath, true);
                    }
                } catch (Exception e) {
                    Log.Error(e, $"Failed to remove old dependency folder {basePath}");
                }
                foreach (var entry in archive.Entries) {
                    if (string.IsNullOrEmpty(entry.Key) || entry.Key.Contains("..")) {
                        continue;
                    }
                    var filePath = Path.Combine(basePath, entry.Key);
                    var dir = Path.GetDirectoryName(filePath);
                    if (!entry.IsDirectory && !string.IsNullOrEmpty(dir)) {
                        Directory.CreateDirectory(dir);
                        entry.WriteToFile(Path.Combine(basePath, entry.Key));
                    }
                }
            });
            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Installed dependency \"{id}\""));
        }

        public async Task InstallVoicebankAsync(RegistrySoftware software, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            var latestVersion = software.versions.OrderByDescending(v => v.version, VersionComparer).First().version;
            await InstallVoicebankVersionAsync(software, latestVersion, progress);
        }

        public async Task InstallVoicebankVersionAsync(RegistrySoftware software, string versionString, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            if (string.IsNullOrWhiteSpace(versionString)) {
                throw new ArgumentException("Version is required", nameof(versionString));
            }
            var version = software.versions.FirstOrDefault(v =>
                string.Equals(v.version, versionString, StringComparison.OrdinalIgnoreCase));
            if (version == null) {
                throw new ArgumentException($"Version {versionString} not found for voicebank {software.id}");
            }
            if (version.mirrors == null || version.mirrors.Length == 0) {
                throw new ArgumentException("No mirrors available");
            }
            var mirror = version.mirrors[0];
            var data = await DownloadMirrorAsync(mirror, progress, verifyHash: false);
            using var ms = new MemoryStream(data);
            await InstallVoicebankFromStreamAsync(ms, software, version.version, mirror.url);
            await RefreshSingerListAsync();
        }

        public async Task UninstallVoicebankAsync(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new ArgumentNullException(nameof(id));
            }
            var installed = await GetInstalledVoicebanksAsync();
            var metadata = installed.FirstOrDefault(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.install_path)) {
                var fallback = Path.Combine(PathManager.Inst.SingersInstallPath, SanitizeDirectoryName(id));
                if (!Directory.Exists(fallback)) {
                    return;
                }
                metadata = new OuvbMetadata {
                    id = id,
                    install_path = fallback,
                };
            }

            if (!Directory.Exists(metadata.install_path)) {
                return;
            }
            await Task.Run(() => Directory.Delete(metadata.install_path, true));
            await RefreshSingerListAsync();
        }

        public async Task InstallPluginAsync(RegistrySoftware software, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) throw new ArgumentException("No versions available");
            var latestVersion = software.versions.OrderByDescending(v => v.version, VersionComparer).First().version;
            await InstallPluginVersionAsync(software, latestVersion, progress);
        }

        public async Task InstallPluginVersionAsync(RegistrySoftware software, string versionString, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) throw new ArgumentException("No versions available");
            var version = software.versions.FirstOrDefault(v => string.Equals(v.version, versionString, StringComparison.OrdinalIgnoreCase));
            if (version == null) throw new ArgumentException($"Version {versionString} not found for plugin {software.id}");
            if (version.mirrors == null || version.mirrors.Length == 0) throw new ArgumentException("No mirrors available");
            var mirror = version.mirrors[0];
            var data = await DownloadMirrorAsync(mirror, progress, verifyHash: false);
            
            string installBase = PathManager.Inst.PluginsPath;
            Directory.CreateDirectory(installBase);
            string installDirName = SanitizeDirectoryName(software.id);
            string installDir = Path.Combine(installBase, installDirName);
            
            if (Directory.Exists(installDir)) {
                Directory.Delete(installDir, true);
            }
            Directory.CreateDirectory(installDir);

            if (mirror.url.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                string fileName = Path.GetFileName(new Uri(mirror.url).AbsolutePath);
                if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                    fileName = $"{installDirName}.dll";
                }
                string destFile = Path.Combine(installDir, fileName);
                await File.WriteAllBytesAsync(destFile, data);
            } else {
                using var ms = new MemoryStream(data);
                string tempDir = Path.Combine(PathManager.Inst.CachePath, "plugin-install", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                try {
                    using var archive = ArchiveFactory.OpenArchive(ms, new ReaderOptions());
                    await Task.Run(() => ExtractArchiveToDirectory(archive, tempDir));
                    string sourceRoot = GetArchiveContentRoot(tempDir);
                    CopyDirectoryContent(sourceRoot, installDir);
                } finally {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
                }
            }
            
            WritePluginMetadata(installDir, software, versionString, mirror.url);
        }

        static void WritePluginMetadata(string installDir, RegistrySoftware software, string versionString, string sourceUrl) {
            var metadata = new OuplugMetadata {
                id = software.id ?? string.Empty,
                name = software.LocalizedName(),
                version = versionString ?? string.Empty,
                description = software.LocalizedDescription(),
                category = software.category ?? string.Empty,
                url = sourceUrl ?? string.Empty,
                installed_at_utc = DateTime.UtcNow.ToString("o"),
            };
            using var writer = new StreamWriter(Path.Combine(installDir, OuplugMetadataFile), false, Encoding.UTF8);
            Yaml.DefaultSerializer.Serialize(writer, metadata);
        }

        public async Task UninstallPluginAsync(string id) {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            var installed = await GetInstalledPluginsAsync();
            var metadata = installed.FirstOrDefault(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
            var installPath = metadata?.install_path ?? Path.Combine(PathManager.Inst.PluginsPath, SanitizeDirectoryName(id));
            if (Directory.Exists(installPath)) {
                await Task.Run(() => Directory.Delete(installPath, true));
            }
        }

        async Task<byte[]> DownloadMirrorAsync(
            RegistryMirror mirror,
            IProgress<int>? progress = null,
            bool verifyHash = true) {
            if (mirror == null || string.IsNullOrWhiteSpace(mirror.url)) {
                throw new ArgumentException("Mirror URL is missing.");
            }
            string resolvedUrl = NormalizeMirrorUrl(mirror.url);
            byte[] data = await DownloadUrlToBytesAsync(resolvedUrl, progress);

            if (IsGoogleDriveUrl(resolvedUrl)) {
                for (int i = 0; i < 3 && IsProbablyHtml(data); i++) {
                    var nextUrl = TryExtractGoogleDriveDownloadUrl(data, resolvedUrl);
                    if (string.IsNullOrWhiteSpace(nextUrl)) {
                        break;
                    }
                    resolvedUrl = nextUrl;
                    data = await DownloadUrlToBytesAsync(resolvedUrl, progress);
                }
            }

            if (verifyHash &&
                !string.IsNullOrWhiteSpace(mirror.hash) &&
                mirror.hash.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) {
                var expected = mirror.hash.Substring("sha256:".Length).ToLowerInvariant();
                var actual = GetSha256Hex(data);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidOperationException("Downloaded file hash does not match expected value");
                }
            }
            return data;
        }

        async Task<byte[]> DownloadUrlToBytesAsync(string url, IProgress<int>? progress = null) {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(15);
            client.DefaultRequestHeaders.Add("User-Agent", "OpenUtau");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            Log.Information("Downloading: {url}", url);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) {
                throw new HttpRequestException(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} 窶・URL: {url}");
            }
            var contentLength = response.Content.Headers.ContentLength;
            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            int lastPercent = -1;
            void report(int percent) {
                if (progress == null) {
                    return;
                }
                int clamped = Math.Clamp(percent, 0, 100);
                if (clamped != lastPercent) {
                    lastPercent = clamped;
                    progress.Report(clamped);
                }
            }
            report(0);
            while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                ms.Write(buffer, 0, read);
                totalRead += read;
                if (contentLength.HasValue && contentLength.Value > 0) {
                    var percent = (int)(totalRead * 100 / contentLength.Value);
                    report(Math.Min(100, percent));
                } else {
                    var softPercent = 5 + (int)(90 * (1.0 - Math.Exp(-totalRead / (32.0 * 1024.0 * 1024.0))));
                    report(Math.Min(95, softPercent));
                }
            }
            report(100);
            return ms.ToArray();
        }

        static string NormalizeMirrorUrl(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return string.Empty;
            }
            if (Uri.TryCreate(url, UriKind.Absolute, out var parsedUri) &&
                parsedUri.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)) {
                return url;
            }
            var fileId = TryExtractGoogleDriveFileId(url);
            if (!string.IsNullOrWhiteSpace(fileId)) {
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }
            return url;
        }

        static bool IsGoogleDriveUrl(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return false;
            }
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Host.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
                 uri.Host.Contains("drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase));
        }

        static string? TryExtractGoogleDriveFileId(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                return null;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                return null;
            }
            if (!uri.Host.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }
            var match = googleDriveFileIdRegex.Match(uri.AbsoluteUri);
            if (match.Success) {
                return match.Groups[1].Value;
            }
            var query = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);
            if (query.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id)) {
                return id;
            }
            return null;
        }

        static bool IsProbablyHtml(byte[] data) {
            if (data == null || data.Length == 0) {
                return false;
            }
            var prefixLength = Math.Min(data.Length, 2048);
            var prefix = Encoding.UTF8.GetString(data, 0, prefixLength);
            return prefix.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string? TryExtractGoogleDriveDownloadUrl(byte[] htmlData, string initialUrl) {
            if (htmlData == null || htmlData.Length == 0) {
                return null;
            }
            var html = Encoding.UTF8.GetString(htmlData);

            var jsUrl = Regex.Match(html, "\"downloadUrl\":\"([^\"]+)\"");
            if (jsUrl.Success) {
                var encoded = jsUrl.Groups[1].Value;
                var unescaped = Regex.Unescape(encoded)
                    .Replace("\\u003d", "=")
                    .Replace("\\u0026", "&")
                    .Replace("\\/", "/");
                if (Uri.TryCreate(unescaped, UriKind.Absolute, out var direct)) {
                    return direct.ToString();
                }
            }

            var relLink = Regex.Match(html, "(?:href|action)=\"(/uc\\?export=download[^\"]+)\"");
            if (relLink.Success) {
                var relative = WebUtility.HtmlDecode(relLink.Groups[1].Value);
                return $"https://drive.google.com{relative}";
            }

            var fileId = TryExtractGoogleDriveFileId(initialUrl);
            if (!string.IsNullOrWhiteSpace(fileId)) {
                var confirmMatch = Regex.Match(html, @"confirm=([0-9A-Za-z_-]+)");
                if (confirmMatch.Success) {
                    var confirm = confirmMatch.Groups[1].Value;
                    return $"https://drive.google.com/uc?export=download&confirm={confirm}&id={fileId}";
                }
            }
            return null;
        }

        async Task InstallVoicebankFromStreamAsync(
            Stream stream,
            RegistrySoftware software,
            string versionString,
            string sourceUrl) {
            string installBase = PathManager.Inst.SingersInstallPath;
            Directory.CreateDirectory(installBase);

            string installDirName = ResolveVoicebankInstallDirectoryName(software);
            string installDir = Path.Combine(installBase, installDirName);
            string tempDir = Path.Combine(PathManager.Inst.CachePath, "voicebank-install", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try {
                using var archive = ArchiveFactory.OpenArchive(stream, new ReaderOptions());
                await Task.Run(() => ExtractArchiveToDirectory(archive, tempDir));

                string sourceRoot = GetArchiveContentRoot(tempDir);
                if (Directory.Exists(installDir)) {
                    Directory.Delete(installDir, true);
                }
                Directory.CreateDirectory(installDir);
                CopyDirectoryContent(sourceRoot, installDir);
                WriteVoicebankMetadata(installDir, software, versionString, sourceUrl);
            } finally {
                try {
                    if (Directory.Exists(tempDir)) {
                        Directory.Delete(tempDir, true);
                    }
                } catch (Exception e) {
                    Log.Warning(e, "Failed to clean temporary directory {tempDir}", tempDir);
                }
            }
        }

        static void ExtractArchiveToDirectory(IArchive archive, string destinationDir) {
            string destinationRoot = Path.GetFullPath(destinationDir);
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory)) {
                if (string.IsNullOrWhiteSpace(entry.Key)) {
                    continue;
                }
                var normalizedKey = entry.Key.Replace('\\', '/');
                if (normalizedKey.Contains("..")) {
                    continue;
                }
                string fullPath = Path.GetFullPath(Path.Combine(destinationRoot, normalizedKey));
                if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir)) {
                    Directory.CreateDirectory(dir);
                }
                entry.WriteToFile(fullPath);
            }
        }

        static string GetArchiveContentRoot(string tempDir) {
            var dirs = Directory.GetDirectories(tempDir);
            var files = Directory.GetFiles(tempDir);
            if (dirs.Length == 1 && files.Length == 0) {
                return dirs[0];
            }
            return tempDir;
        }

        static void CopyDirectoryContent(string sourceDir, string destinationDir) {
            foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories)) {
                var relative = Path.GetRelativePath(sourceDir, dir);
                Directory.CreateDirectory(Path.Combine(destinationDir, relative));
            }
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)) {
                var relative = Path.GetRelativePath(sourceDir, file);
                var targetPath = Path.Combine(destinationDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? destinationDir);
                File.Copy(file, targetPath, true);
            }
        }

        static void WriteVoicebankMetadata(string installDir, RegistrySoftware software, string versionString, string sourceUrl) {
            var metadata = new OuvbMetadata {
                id = software.id ?? string.Empty,
                team = software.team ?? string.Empty,
                name = software.LocalizedName(),
                version = versionString ?? string.Empty,
                description = software.LocalizedDescription(),
                website_url = software.GetVoicebankWebsiteUrl(),
                source_url = sourceUrl ?? string.Empty,
                languages = software.GetVoicebankLanguages(),
                types = software.GetVoicebankTypes(),
                engines = software.GetVoicebankEngines(),
                installed_at_utc = DateTime.UtcNow.ToString("o"),
            };
            using var writer = new StreamWriter(Path.Combine(installDir, OuvbMetadataFile), false, Encoding.UTF8);
            Yaml.DefaultSerializer.Serialize(writer, metadata);
        }

        static string ResolveVoicebankInstallDirectoryName(RegistrySoftware software) {
            var preferred = software.GetVoicebankInstallSubdir();
            if (!string.IsNullOrWhiteSpace(preferred)) {
                return SanitizeDirectoryName(preferred);
            }
            if (!string.IsNullOrWhiteSpace(software.id)) {
                return SanitizeDirectoryName(software.id);
            }
            return "voicebank";
        }

        static string SanitizeDirectoryName(string input) {
            if (string.IsNullOrWhiteSpace(input)) {
                return "voicebank";
            }
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(input.Length);
            foreach (var c in input) {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            var sanitized = sb.ToString().Trim().Trim('.');
            return string.IsNullOrWhiteSpace(sanitized) ? "voicebank" : sanitized;
        }

        async Task RefreshSingerListAsync() {
            await Task.Run(() => SingerManager.Inst.SearchAllSingers());
            DocManager.Inst.ExecuteCmd(new SingersChangedNotification());
            DocManager.Inst.ExecuteCmd(new OtoChangedNotification(external: true));
        }

        public string? GetInstalledPath(string id) {
            var path = Path.Combine(PathManager.Inst.DependencyPath, id);
            if (Directory.Exists(path)) {
                return path;
            }
            return null;
        }

        public string? GetInstalledVoicebankPath(string id) {
            if (string.IsNullOrWhiteSpace(id)) {
                return null;
            }
            var sanitized = SanitizeDirectoryName(id);
            foreach (var singersPath in PathManager.Inst.SingersPaths) {
                var path = Path.Combine(singersPath, sanitized);
                if (Directory.Exists(path)) {
                    return path;
                }
            }
            return null;
        }


        public static bool EnablePullRequestThemeSource { get; set; } = true;

        static readonly TimeSpan ThemeRegistryCacheTtl = TimeSpan.FromHours(6);

        const string ThemeRegistryCacheSubdir = "theme-registry";

        const string ThemeRegistryCacheFile = "themes.json";

        const string SingerThemeRegistryCacheFile = "singer-themes.json";

        const string GitHubPullRequestsUrl =
            "https://api.github.com/repos/emeraldsingers/UtauV_Packages/pulls?state=open&per_page=100";

        const string GitHubPullRequestFilesUrlFormat =
            "https://api.github.com/repos/emeraldsingers/UtauV_Packages/pulls/{0}/files?per_page=100";

        public async Task<List<RegistrySoftware>> FetchThemeRegistryAsync(bool forceRefresh = false) {
            var cacheFile = GetThemeRegistryCachePath(ThemeRegistryCacheFile);
            if (!forceRefresh && TryReadThemeRegistryCache(cacheFile, out var cached)) {
                return cached!;
            }

            var merged = await FetchMergedRegistryAsync([
                new RegistrySource(themeRegistryUrlUtauVRequested),
            ], "theme");

            if (EnablePullRequestThemeSource) {
                var prEntries = await FetchThemePullRequestsAsync();
                foreach (var entry in prEntries) {
                    if (!merged.ContainsKey(entry.id)) {
                        MergeSoftware(merged, entry);
                    }
                }
            }

            var result = merged.Values
                .Where(IsThemeEntry)
                .OrderBy(s => s.LocalizedName(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            WriteThemeRegistryCache(cacheFile, result);
            return result;
        }

        public async Task<List<RegistrySoftware>> FetchSingerThemeRegistryAsync(bool forceRefresh = false) {
            var cacheFile = GetThemeRegistryCachePath(SingerThemeRegistryCacheFile);
            if (!forceRefresh && TryReadThemeRegistryCache(cacheFile, out var cached)) {
                return cached!;
            }

            var merged = await FetchMergedRegistryAsync([
                new RegistrySource(themeRegistryUrlUtauVRequested),
            ], "singer-theme");

            if (EnablePullRequestThemeSource) {
                var prEntries = await FetchThemePullRequestsAsync();
                foreach (var entry in prEntries) {
                    if (!merged.ContainsKey(entry.id)) {
                        MergeSoftware(merged, entry);
                    }
                }
            }

            var result = merged.Values
                .Where(IsSingerThemeEntry)
                .OrderBy(s => s.LocalizedName(), StringComparer.OrdinalIgnoreCase)
                .ToList();

            WriteThemeRegistryCache(cacheFile, result);
            return result;
        }

        public async Task<List<RegistrySoftware>> FetchThemePullRequestsAsync() {
            try {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "OpenUtau");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
                client.Timeout = TimeSpan.FromSeconds(30);

                using var response = await client.GetAsync(GitHubPullRequestsUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                    response.StatusCode == (System.Net.HttpStatusCode)429) {
                    Log.Warning("GitHub PR API rate-limit reached (HTTP {status}). Skipping PR theme source.",
                        (int)response.StatusCode);
                    return new List<RegistrySoftware>();
                }

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                return await ParseThemePullRequestsAsync(client, body);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to fetch theme pull requests from GitHub. PR source will be skipped.");
                return new List<RegistrySoftware>();
            }
        }

        static async Task<List<string>> ResolvePrThemeFilePathsAsync(HttpClient client, int prNumber) {
            try {
                var url = string.Format(GitHubPullRequestFilesUrlFormat, prNumber);
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) {
                    Log.Warning("GitHub PR files API returned HTTP {status} for PR #{pr}.",
                        (int)response.StatusCode, prNumber);
                    return new List<string>();
                }
                var body = await response.Content.ReadAsStringAsync();
                var files = JArray.Parse(body);

                string FilenameOf(JToken f) => AsString(f["filename"]) ?? string.Empty;
                string StatusOf(JToken f) => AsString(f["status"]) ?? string.Empty;

                bool IsYaml(string path) =>
                    path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
                bool IsRelevantStatus(string status) =>
                    !string.Equals(status, "removed", StringComparison.OrdinalIgnoreCase);

                var allCandidates = files.OfType<JObject>()
                    .Where(f => IsYaml(FilenameOf(f)) && IsRelevantStatus(StatusOf(f)))
                    .Select(FilenameOf)
                    .ToList();

                var preferred = allCandidates
                    .Where(p => p.StartsWith("themes/", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return preferred.Count > 0 ? preferred : allCandidates;
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to resolve theme file paths for PR #{pr}.", prNumber);
                return new List<string>();
            }
        }

        static async Task<List<RegistrySoftware>> ParseThemePullRequestsAsync(HttpClient client, string json) {
            var result = new List<RegistrySoftware>();
            JArray prs;
            try {
                prs = JArray.Parse(json);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to parse GitHub PR JSON response.");
                return result;
            }

            foreach (var pr in prs.OfType<JObject>()) {
                var labels = pr["labels"] is JArray labelsArr
                    ? labelsArr.OfType<JObject>()
                        .Select(l => AsString(l["name"]) ?? string.Empty)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .ToArray()
                    : Array.Empty<string>();

                bool isThemePr = labels.Any(l =>
                    string.Equals(l, "UtauV_Theme", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(l, "UtauV_SingerTheme", StringComparison.OrdinalIgnoreCase));

                if (!isThemePr) {
                    continue;
                }

                var head = pr["head"] as JObject;
                var headRepo = head?["repo"] as JObject;
                var headRef = AsString(head?["ref"]) ?? string.Empty;
                var fullName = AsString(headRepo?["full_name"]) ?? string.Empty;

                var prNumber = pr["number"]?.Value<int>() ?? 0;
                var prTitle = AsString(pr["title"]) ?? $"PR #{prNumber}";
                var prBody = AsString(pr["body"]) ?? string.Empty;
                var prUrl = AsString(pr["html_url"]) ?? string.Empty;

                var ownerLogin = AsString(headRepo?["owner"]?["login"]) ?? string.Empty;

                var descriptionText = string.IsNullOrWhiteSpace(prBody)
                    ? $"Theme from open PR #{prNumber}"
                    : prBody.Length > 300 ? prBody.Substring(0, 300) + "\u2026" : prBody;

                var themeFilePaths = await ResolvePrThemeFilePathsAsync(client, prNumber);

                if (themeFilePaths.Count == 0) {
                    Log.Warning("PR #{pr} has no theme YAML files; skipping.", prNumber);
                    continue;
                }

                foreach (var themeFilePath in themeFilePaths) {
                    string mirrorUrl = BuildPrRawMirrorUrl(fullName, headRef, themeFilePath);

                    string themeId = BuildThemeIdFromFilePath(themeFilePath, headRef, ownerLogin, prNumber);

                    var tags = new List<string> { "UtauV_Theme", "pr-source" };

                    var entry = new RegistrySoftware {
                        id = themeId,
                        name = prTitle,
                        description = descriptionText,
                        homepage_url = prUrl,
                        download_page_url = prUrl,
                        tags = tags.ToArray(),
                        versions = string.IsNullOrWhiteSpace(mirrorUrl)
                            ? Array.Empty<RegistryVersion>()
                            : new[] {
                                new RegistryVersion {
                                    version = $"pr-{prNumber}",
                                    description = $"Open pull request #{prNumber}",
                                    mirrors = new[] {
                                        new RegistryMirror { url = mirrorUrl },
                                    },
                                },
                            },
                        names = new Dictionary<string, string> { ["en"] = prTitle },
                        descriptions = new Dictionary<string, string> { ["en"] = descriptionText },
                    };

                    if (!string.IsNullOrWhiteSpace(mirrorUrl)) {
                        await EnrichPrThemeEntryAsync(client, entry, mirrorUrl, ownerLogin);
                    }

                    result.Add(entry);
                }
            }

            return result;
        }

        static string BuildThemeIdFromFilePath(string filePath, string headRef, string ownerLogin, int prNumber) {
            if (!string.IsNullOrWhiteSpace(filePath)) {
                var parts = filePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    string.Equals(parts[0], "themes", StringComparison.OrdinalIgnoreCase)) {
                    var gitUsername = parts[1];
                    var themeIdDir = parts[2];
                    var themeName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
                    if (!string.IsNullOrWhiteSpace(gitUsername) &&
                        !string.IsNullOrWhiteSpace(themeIdDir) &&
                        !string.IsNullOrWhiteSpace(themeName)) {
                        try {
                            return BuildThemeId(gitUsername, themeIdDir, themeName);
                        } catch {
                        }
                    }
                }
            }
            return BuildThemeIdFromPrBranch(headRef, ownerLogin, prNumber);
        }

        static readonly HashSet<string> ThemeManifestMetaKeys = new(StringComparer.OrdinalIgnoreCase) {
            "type", "id", "name", "author", "version", "description", "long_description",
            "git_username", "repo", "preview_image", "singers", "tags",
        };

        static async Task EnrichPrThemeEntryAsync(
            HttpClient client, RegistrySoftware entry, string mirrorUrl, string ownerLogin) {
            try {
                var yamlText = await client.GetStringAsync(mirrorUrl);
                if (string.IsNullOrWhiteSpace(yamlText)) {
                    return;
                }

                var manifest = ParseFlatYaml(yamlText);
                if (manifest.Count == 0) {
                    Log.Warning("PR theme manifest at {url} parsed to zero keys.", mirrorUrl);
                    return;
                }

                string Get(string key) =>
                    manifest.TryGetValue(key, out var v) ? v?.Trim() ?? string.Empty : string.Empty;

                entry.theme_manifest = manifest;

                var manifestType = Get("type").ToLowerInvariant();
                bool isSingerTheme = string.Equals(manifestType, "singer_theme", StringComparison.OrdinalIgnoreCase);
                var typeTag = isSingerTheme ? "UtauV_SingerTheme" : "UtauV_Theme";

                var existingTags = (entry.tags ?? Array.Empty<string>())
                    .Where(t => !string.Equals(t, "UtauV_Theme", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(t, "UtauV_SingerTheme", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                existingTags.Insert(0, typeTag);
                entry.tags = existingTags.ToArray();

                var palette = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in manifest) {
                    if (!ThemeManifestMetaKeys.Contains(kv.Key)) {
                        palette[kv.Key] = kv.Value;
                    }
                }
                if (palette.Count > 0) {
                    entry.palette = palette;
                }

                var name = Get("name");
                if (!string.IsNullOrWhiteSpace(name)) {
                    entry.name = name;
                    entry.names = new Dictionary<string, string> { ["en"] = name };
                }

                var author = Get("author");
                if (string.IsNullOrWhiteSpace(author)) {
                    author = ownerLogin;
                }
                if (!string.IsNullOrWhiteSpace(author)) {
                    entry.developers = new[] { author };
                }

                var description = Get("description");
                if (!string.IsNullOrWhiteSpace(description)) {
                    entry.description = description;
                    entry.descriptions = new Dictionary<string, string> { ["en"] = description };
                }

                var longDescription = Get("long_description");
                if (!string.IsNullOrWhiteSpace(longDescription)) {
                    entry.long_description = longDescription;
                }

                var previewImage = Get("preview_image");
                if (!string.IsNullOrWhiteSpace(previewImage)) {
                    entry.image_url = previewImage;
                }

                var singers = Get("singers");
                if (!string.IsNullOrWhiteSpace(singers)) {
                    entry.character = singers;
                }

                var version = Get("version");
                if (!string.IsNullOrWhiteSpace(version) &&
                    entry.versions != null && entry.versions.Length > 0) {
                    entry.versions[0].version = version;
                }

                Log.Information(
                    "Enriched PR theme '{name}' (type={type}) from {url}: {count} palette colours.",
                    entry.name, typeTag, mirrorUrl, entry.palette?.Count ?? 0);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to enrich PR theme entry from {url}.", mirrorUrl);
            }
        }

        static Dictionary<string, string> ParseFlatYaml(string text) {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(text)) {
                return result;
            }

            foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n')) {
                var line = rawLine;
                if (string.IsNullOrWhiteSpace(line)) {
                    continue;
                }
                if (char.IsWhiteSpace(line[0])) {
                    continue;
                }
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("#") || trimmed.StartsWith("-") || trimmed.StartsWith("---")) {
                    continue;
                }

                int colon = trimmed.IndexOf(':');
                if (colon <= 0) {
                    continue;
                }
                var key = trimmed.Substring(0, colon).Trim();
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }
                var value = trimmed.Substring(colon + 1).Trim();

                if (value.Length == 0) {
                    continue;
                }

                value = StripYamlValue(value);
                if (!string.IsNullOrEmpty(key)) {
                    result[key] = value;
                }
            }

            return result;
        }

        static string StripYamlValue(string value) {
            value = value.Trim();
            if (value.Length == 0) {
                return value;
            }

            char first = value[0];
            if ((first == '"' || first == '\'') && value.Length >= 2) {
                int end = value.IndexOf(first, 1);
                if (end > 0) {
                    return value.Substring(1, end - 1);
                }
            }

            int hash = value.IndexOf(" #", StringComparison.Ordinal);
            if (hash >= 0) {
                value = value.Substring(0, hash).Trim();
            }
            return value;
        }

        static string BuildThemeIdFromPrBranch(string headRef, string ownerLogin, int prNumber) {
            if (string.IsNullOrWhiteSpace(headRef)) {
                return $"pr-{prNumber}";
            }

            var parts = headRef.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3) {
                var gitUsername = parts[0];
                var repo = parts[1];
                var themeName = string.Join("-", parts.Skip(2));
                try {
                    return BuildThemeId(gitUsername, repo, themeName);
                } catch {
                }
            }

            if (!string.IsNullOrWhiteSpace(ownerLogin)) {
                try {
                    return BuildThemeId(ownerLogin, "pr", headRef);
                } catch {
                }
            }

            return $"pr-{prNumber}";
        }

        static string BuildPrRawMirrorUrl(string fullName, string headRef, string filePath) {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(headRef) ||
                string.IsNullOrWhiteSpace(filePath)) {
                return string.Empty;
            }
            var encodedRef = Uri.EscapeDataString(headRef);
            var encodedPath = string.Join("/",
                filePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Uri.EscapeDataString));
            return $"https://raw.githubusercontent.com/{fullName}/{encodedRef}/{encodedPath}";
        }


        static string GetThemeRegistryCachePath(string fileName) {
            var dir = Path.Combine(PathManager.Inst.CachePath, ThemeRegistryCacheSubdir);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, fileName);
        }

        static bool TryReadThemeRegistryCache(string cacheFile, out List<RegistrySoftware>? entries) {
            entries = null;
            try {
                if (!File.Exists(cacheFile)) {
                    return false;
                }
                var lastWrite = File.GetLastWriteTimeUtc(cacheFile);
                if (DateTime.UtcNow - lastWrite > ThemeRegistryCacheTtl) {
                    return false;
                }
                var json = File.ReadAllText(cacheFile, Encoding.UTF8);
                entries = JsonConvert.DeserializeObject<List<RegistrySoftware>>(json)
                          ?? new List<RegistrySoftware>();
                return true;
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to read theme registry cache from {cacheFile}. Will re-fetch.", cacheFile);
                return false;
            }
        }

        static void WriteThemeRegistryCache(string cacheFile, List<RegistrySoftware> entries) {
            try {
                var json = JsonConvert.SerializeObject(entries, Formatting.Indented);
                File.WriteAllText(cacheFile, json, Encoding.UTF8);
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to write theme registry cache to {cacheFile}.", cacheFile);
            }
        }

        public static void InvalidateThemeRegistryCache() {
            try {
                var dir = Path.Combine(PathManager.Inst.CachePath, ThemeRegistryCacheSubdir);
                if (!Directory.Exists(dir)) {
                    return;
                }
                foreach (var file in Directory.GetFiles(dir, "*.json")) {
                    try {
                        File.Delete(file);
                    } catch (Exception ex) {
                        Log.Warning(ex, "Failed to delete theme registry cache file {file}.", file);
                    }
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Failed to invalidate theme registry cache.");
            }
        }


        public async Task<List<OuthemeMetadata>> GetInstalledThemesAsync() {
            return await Task.Run(() => {
                var found = new Dictionary<string, OuthemeMetadata>(StringComparer.OrdinalIgnoreCase);
                var themesPath = PathManager.Inst.ThemesPath;
                if (!Directory.Exists(themesPath)) return found.Values.ToList();

                IEnumerable<string> manifestPaths;
                try {
                    manifestPaths = Directory.EnumerateFiles(themesPath, OuthemeMetadataFile, SearchOption.AllDirectories);
                } catch {
                    return found.Values.ToList();
                }

                foreach (var manifestPath in manifestPaths) {
                    try {
                        using var reader = new StreamReader(manifestPath, Encoding.UTF8);
                        var metadata = Core.Yaml.DefaultDeserializer.Deserialize<OuthemeMetadata>(reader) ?? new OuthemeMetadata();
                        var installPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(metadata.id)) {
                            metadata.id = Path.GetFileName(installPath);
                        }
                        metadata.version ??= string.Empty;
                        metadata.install_path = installPath;
                        if (!found.ContainsKey(metadata.id)) {
                            found[metadata.id] = metadata;
                        }
                    } catch (Exception e) {
                        Log.Warning(e, "Failed to read {manifest}", manifestPath);
                    }
                }
                return found.Values
                    .OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
        }

        public async Task<List<OusthemeMetadata>> GetInstalledSingerThemesAsync() {
            return await Task.Run(() => {
                var found = new Dictionary<string, OusthemeMetadata>(StringComparer.OrdinalIgnoreCase);
                var singerThemesPath = PathManager.Inst.SingerThemesPath;
                if (!Directory.Exists(singerThemesPath)) return found.Values.ToList();

                IEnumerable<string> manifestPaths;
                try {
                    manifestPaths = Directory.EnumerateFiles(singerThemesPath, OusthemeMetadataFile, SearchOption.AllDirectories);
                } catch {
                    return found.Values.ToList();
                }

                foreach (var manifestPath in manifestPaths) {
                    try {
                        using var reader = new StreamReader(manifestPath, Encoding.UTF8);
                        var metadata = Core.Yaml.DefaultDeserializer.Deserialize<OusthemeMetadata>(reader) ?? new OusthemeMetadata();
                        var installPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(metadata.id)) {
                            metadata.id = Path.GetFileName(installPath);
                        }
                        metadata.version ??= string.Empty;
                        metadata.install_path = installPath;
                        if (!found.ContainsKey(metadata.id)) {
                            found[metadata.id] = metadata;
                        }
                    } catch (Exception e) {
                        Log.Warning(e, "Failed to read {manifest}", manifestPath);
                    }
                }
                return found.Values
                    .OrderBy(v => v.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });
        }


        public async Task InstallThemeAsync(RegistrySoftware software, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            var latestVersion = software.versions.OrderByDescending(v => v.version, VersionComparer).First().version;
            await InstallThemeVersionAsync(software, latestVersion, progress);
        }

        public async Task InstallThemeVersionAsync(RegistrySoftware software, string versionString, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            if (string.IsNullOrWhiteSpace(versionString)) {
                throw new ArgumentException("Version is required", nameof(versionString));
            }
            var version = software.versions.FirstOrDefault(v =>
                string.Equals(v.version, versionString, StringComparison.OrdinalIgnoreCase));
            if (version == null) {
                throw new ArgumentException($"Version {versionString} not found for theme {software.id}");
            }
            if (version.mirrors == null || version.mirrors.Length == 0) {
                throw new ArgumentException("No mirrors available");
            }
            var mirror = version.mirrors[0];
            var data = await DownloadMirrorAsync(mirror, progress, verifyHash: false);
            using var ms = new MemoryStream(data);
            await InstallThemeFromStreamAsync(ms, software, versionString, mirror.url);
        }

        public async Task InstallSingerThemeAsync(RegistrySoftware software, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            var latestVersion = software.versions.OrderByDescending(v => v.version, VersionComparer).First().version;
            await InstallSingerThemeVersionAsync(software, latestVersion, progress);
        }

        public async Task InstallSingerThemeVersionAsync(RegistrySoftware software, string versionString, IProgress<int>? progress = null) {
            ArgumentNullException.ThrowIfNull(software);
            if (software.versions == null || software.versions.Length == 0) {
                throw new ArgumentException("No versions available");
            }
            if (string.IsNullOrWhiteSpace(versionString)) {
                throw new ArgumentException("Version is required", nameof(versionString));
            }
            var version = software.versions.FirstOrDefault(v =>
                string.Equals(v.version, versionString, StringComparison.OrdinalIgnoreCase));
            if (version == null) {
                throw new ArgumentException($"Version {versionString} not found for singer theme {software.id}");
            }
            if (version.mirrors == null || version.mirrors.Length == 0) {
                throw new ArgumentException("No mirrors available");
            }
            var mirror = version.mirrors[0];
            var data = await DownloadMirrorAsync(mirror, progress, verifyHash: false);
            using var ms = new MemoryStream(data);
            await InstallSingerThemeFromStreamAsync(ms, software, versionString, mirror.url);
        }


        public async Task InstallThemeFromFileAsync(string archivePath) {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            await InstallThemeFromStreamAsync(stream, software: null, versionString: string.Empty, sourceUrl: archivePath);
        }

        public async Task InstallSingerThemeFromFileAsync(string archivePath) {
            using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read);
            await InstallSingerThemeFromStreamAsync(stream, software: null, versionString: string.Empty, sourceUrl: archivePath);
        }


        public async Task UninstallThemeAsync(string id) {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            var installed = await GetInstalledThemesAsync();
            var metadata = installed.FirstOrDefault(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.install_path)) {
                Log.Warning("Theme {id} not found for uninstall", id);
                return;
            }
            if (!Directory.Exists(metadata.install_path)) {
                return;
            }
            await Task.Run(() => Directory.Delete(metadata.install_path, true));
        }

        public async Task UninstallSingerThemeAsync(string id) {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));
            var installed = await GetInstalledSingerThemesAsync();
            var metadata = installed.FirstOrDefault(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.install_path)) {
                Log.Warning("Singer theme {id} not found for uninstall", id);
                return;
            }
            if (!Directory.Exists(metadata.install_path)) {
                return;
            }
            await Task.Run(() => Directory.Delete(metadata.install_path, true));
        }


        async Task InstallThemeFromStreamAsync(
            Stream stream,
            RegistrySoftware? software,
            string versionString,
            string sourceUrl) {
            OuthemeMetadata metadata;
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true)) {
                var yamlText = await reader.ReadToEndAsync();
                try {
                    metadata = Core.Yaml.DefaultDeserializer.Deserialize<OuthemeMetadata>(yamlText) ?? new OuthemeMetadata();
                } catch {
                    metadata = new OuthemeMetadata();
                }
            }

            var gitUsername = !string.IsNullOrWhiteSpace(metadata.git_username)
                ? metadata.git_username
                : (software != null ? DeriveGitUsername(software.id) : "unknown");
            var themeId = !string.IsNullOrWhiteSpace(metadata.id)
                ? metadata.id
                : (software != null ? software.id : Path.GetFileNameWithoutExtension(sourceUrl));

            if (software != null) {
                if (string.IsNullOrWhiteSpace(metadata.id)) metadata.id = software.id;
                if (string.IsNullOrWhiteSpace(metadata.name)) metadata.name = software.LocalizedName();
                if (string.IsNullOrWhiteSpace(metadata.description)) metadata.description = software.LocalizedDescription();
                if (string.IsNullOrWhiteSpace(metadata.version)) metadata.version = versionString;
            }
            metadata.installed_at_utc = DateTime.UtcNow.ToString("o");

            string installBase = PathManager.Inst.ThemesPath;
            string installDir = Path.Combine(installBase,
                SanitizeDirectoryName(gitUsername),
                SanitizeDirectoryName(themeId));

            await Task.Run(() => {
                if (Directory.Exists(installDir)) {
                    Directory.Delete(installDir, true);
                }
                Directory.CreateDirectory(installDir);
            });

            var manifestDestPath = Path.Combine(installDir, OuthemeMetadataFile);
            using (var writer = new StreamWriter(manifestDestPath, false, Encoding.UTF8)) {
                Yaml.DefaultSerializer.Serialize(writer, metadata);
            }

            if (!string.IsNullOrWhiteSpace(sourceUrl)) {
                var themeFileName = Path.GetFileName(sourceUrl);
                if (!string.IsNullOrWhiteSpace(themeFileName) &&
                    !string.Equals(themeFileName, OuthemeMetadataFile, StringComparison.OrdinalIgnoreCase)) {
                    stream.Position = 0;
                    var themeFileDest = Path.Combine(installDir, themeFileName);
                    using var fs = new FileStream(themeFileDest, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs);
                }
            }
        }

        async Task InstallSingerThemeFromStreamAsync(
            Stream stream,
            RegistrySoftware? software,
            string versionString,
            string sourceUrl) {
            OusthemeMetadata metadata;
            using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true)) {
                var yamlText = await reader.ReadToEndAsync();
                try {
                    metadata = Core.Yaml.DefaultDeserializer.Deserialize<OusthemeMetadata>(yamlText) ?? new OusthemeMetadata();
                } catch {
                    metadata = new OusthemeMetadata();
                }
            }

            var gitUsername = !string.IsNullOrWhiteSpace(metadata.git_username)
                ? metadata.git_username
                : (software != null ? DeriveGitUsername(software.id) : "unknown");
            var themeId = !string.IsNullOrWhiteSpace(metadata.id)
                ? metadata.id
                : (software != null ? software.id : Path.GetFileNameWithoutExtension(sourceUrl));

            if (software != null) {
                if (string.IsNullOrWhiteSpace(metadata.id)) metadata.id = software.id;
                if (string.IsNullOrWhiteSpace(metadata.name)) metadata.name = software.LocalizedName();
                if (string.IsNullOrWhiteSpace(metadata.description)) metadata.description = software.LocalizedDescription();
                if (string.IsNullOrWhiteSpace(metadata.version)) metadata.version = versionString;
            }
            metadata.installed_at_utc = DateTime.UtcNow.ToString("o");

            string installBase = PathManager.Inst.SingerThemesPath;
            string installDir = Path.Combine(installBase,
                SanitizeDirectoryName(gitUsername),
                SanitizeDirectoryName(themeId));

            await Task.Run(() => {
                if (Directory.Exists(installDir)) {
                    Directory.Delete(installDir, true);
                }
                Directory.CreateDirectory(installDir);
            });

            var manifestDestPath = Path.Combine(installDir, OusthemeMetadataFile);
            using (var writer = new StreamWriter(manifestDestPath, false, Encoding.UTF8)) {
                Yaml.DefaultSerializer.Serialize(writer, metadata);
            }

            if (!string.IsNullOrWhiteSpace(sourceUrl)) {
                var themeFileName = Path.GetFileName(sourceUrl);
                if (!string.IsNullOrWhiteSpace(themeFileName) &&
                    !string.Equals(themeFileName, OusthemeMetadataFile, StringComparison.OrdinalIgnoreCase)) {
                    stream.Position = 0;
                    var themeFileDest = Path.Combine(installDir, themeFileName);
                    using var fs = new FileStream(themeFileDest, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs);
                }
            }
        }

        static string DeriveGitUsername(string softwareId) {
            if (string.IsNullOrWhiteSpace(softwareId)) return "unknown";
            var parts = softwareId.Split(new[] { '.', '-', '_' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : "unknown";
        }
    }
}
