using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Colors {
    public class CustomSingerTheme {
        public static Dictionary<string, SingerThemeYaml> Themes = new Dictionary<string, SingerThemeYaml>();
        private static Dictionary<string, SingerThemeYaml?>? themeCache;

        private static SingerThemeYaml? activeEditTheme;
        public static SingerThemeYaml? ActiveEditTheme {
            get => activeEditTheme;
            set {
                if (!ReferenceEquals(activeEditTheme, value)) {
                    activeEditTheme = value;
                    themeCache = null;
                }
            }
        }

        public static void Load(string themeName) {
            // Not strictly needed to load a single default like CustomTheme because singer themes map to multiple singers.
        }

        public static void ListThemes() {
            Themes.Clear();
            themeCache = null;
            Directory.CreateDirectory(PathManager.Inst.SingerThemesPath);
            foreach (var item in Directory.EnumerateFiles(
                PathManager.Inst.SingerThemesPath, "*.yaml", SearchOption.AllDirectories)) {
                var fileName = Path.GetFileName(item);
                if (string.Equals(fileName, PackageManager.OuthemeMetadataFile, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, PackageManager.OusthemeMetadataFile, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                try {
                    var yamlText = File.ReadAllText(item, Encoding.UTF8);
                    var theme = Yaml.DefaultDeserializer.Deserialize<SingerThemeYaml>(yamlText);
                    theme.ResolveColorsFromFlatSchema();
                    string baseName = theme.Name;
                    string themeName = baseName;
                    int dupIter = 1;
                    while (Themes.ContainsKey(themeName)) {
                        themeName = $"{baseName} ({dupIter})";
                        dupIter++;
                    }
                    Themes.Add(themeName, theme);
                } catch (Exception e) {
                    Log.Error(e, $"Failed to parse yaml in {item}");
                }
            }
        }

        public static SingerThemeYaml? GetThemeForSinger(string singerName) {
            if (themeCache != null && themeCache.TryGetValue(singerName, out var cached)) {
                return cached;
            }
            var result = ResolveThemeForSinger(singerName);
            (themeCache ??= new Dictionary<string, SingerThemeYaml?>())[singerName] = result;
            return result;
        }

        private static SingerThemeYaml? ResolveThemeForSinger(string singerName) {
            if (ActiveEditTheme != null) {
                var activeSingers = ActiveEditTheme.Singers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in activeSingers) {
                    if (singerName.IndexOf(s.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) {
                        return ActiveEditTheme;
                    }
                }
            }

            foreach (var theme in Themes.Values) {
                var singers = theme.Singers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var s in singers) {
                    if (singerName.IndexOf(s.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) {
                        return theme;
                    }
                }
                // Package themes occasionally omit a precise singer token and use the
                // singer's display name in the theme name instead.
                if (!string.IsNullOrWhiteSpace(theme.Name)) {
                    var nameTokens = theme.Name.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (nameTokens.Any(token => token.Length >= 3 &&
                        !string.Equals(token, "theme", StringComparison.OrdinalIgnoreCase) &&
                        singerName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)) {
                        return theme;
                    }
                }
            }
            return null;
        }

        [Serializable]
        public class SingerThemeYaml {
            public string Name = "Custom Singer Theme";
            public string Singers = "SingerName1,SingerName2";

            // Track Color
            public bool HasTrackAccentColor = true;
            public string TrackAccentColor = "#4EA6EA";
            public bool HasTrackAccentColorDark = true;
            public string TrackAccentColorDark = "#1E88E5";
            public bool HasTrackAccentColorLight = true;
            public string TrackAccentColorLight = "#90CAF9";
            public bool HasTrackCenterKeyColor = true;
            public string TrackCenterKeyColor = "#FFFFFF";

            // Phoneme Color
            public bool HasPhonemeColor = true;
            public string PhonemeColor = "#FFFFFF";
            public bool HasPhonemeColor2 = true;
            public string PhonemeColor2 = "#FFFFFF";
            public bool HasAccentPen2Color = true;
            public string AccentPen2Color = "#FFFFFF";
            public bool HasAccentColorSemi = true;
            public string AccentColorSemi = "#80FFFFFF";

            // Pitch Color
            public bool HasPitchColor = true;
            public string PitchColor = "#FFFFFF";
            public bool HasPitchBendColor = true;
            public string PitchBendColor = "#FFFFFF";
            public bool HasPitchBendBrushColor = true;
            public string PitchBendBrushColor = "#FFFFFF";
            public string BackgroundColor = string.Empty;
            public string ForegroundColor = string.Empty;
            public string SystemAccentColor = string.Empty;
            public string AccentColor1 = string.Empty;
            public string AccentColor2 = string.Empty;
            public string FinalPitchColor = string.Empty;

            public void ResolveColorsFromFlatSchema() {
                static string First(params string[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                if (!HasTrackAccentColor && !string.IsNullOrWhiteSpace(AccentColor1)) { TrackAccentColor = AccentColor1; HasTrackAccentColor = true; }
                if (!HasTrackAccentColorDark && !string.IsNullOrWhiteSpace(AccentColor1)) { TrackAccentColorDark = AccentColor1; HasTrackAccentColorDark = true; }
                if (!HasTrackAccentColorLight) { var c = First(AccentColor2, AccentColor1, SystemAccentColor); if (c.Length > 0) { TrackAccentColorLight = c; HasTrackAccentColorLight = true; } }
                if (!HasPhonemeColor && !string.IsNullOrWhiteSpace(ForegroundColor)) { PhonemeColor = ForegroundColor; HasPhonemeColor = true; }
                if (!HasPitchColor) { var c = First(FinalPitchColor, SystemAccentColor, AccentColor1); if (c.Length > 0) { PitchColor = c; HasPitchColor = true; } }
                if (!HasPitchBendColor) { var c = First(FinalPitchColor, SystemAccentColor, AccentColor1); if (c.Length > 0) { PitchBendColor = c; HasPitchBendColor = true; } }
                if (!HasPitchBendBrushColor) { var c = First(FinalPitchColor, SystemAccentColor, AccentColor1); if (c.Length > 0) { PitchBendBrushColor = c; HasPitchBendBrushColor = true; } }
            }

            public IBrush GetBrush(string colorHex) {
                if (Color.TryParse(colorHex, out var color)) {
                    return new SolidColorBrush(color);
                }
                return Brushes.White; // fallback
            }

            public IPen GetPen(string colorHex, double thickness = 1, DashStyle? dashStyle = null) {
                if (Color.TryParse(colorHex, out var color)) {
                    return new Pen(new SolidColorBrush(color), thickness, dashStyle);
                }
                return new Pen(Brushes.White, thickness, dashStyle); // fallback
            }
        }
    }
}
