using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.App;

/// <summary>
/// Loads a font file into Avalonia and exposes it through the global UI font resource.
/// </summary>
public static class UiFontManager {
    private static readonly Uri CollectionKey = new("fonts:OpenUtau.CustomUiFont", UriKind.Absolute);
    private static FontFamily? fallbackFontFamily;
    private static bool customFontApplied;
    private static string? loadedFontPath;
    private static string? loadedFamilyName;

    private static readonly MethodInfo? CreateGlyphTypefaceFromStream = typeof(IFontManagerImpl)
        .GetMethods()
        .FirstOrDefault(method => method.Name == "TryCreateGlyphTypeface"
            && method.GetParameters().Length == 3
            && method.GetParameters()[0].ParameterType == typeof(Stream));

    public static bool IsSupportedFontPath(string? path) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
            return false;
        }
        var extension = Path.GetExtension(path);
        return extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".otf", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSuggestedFamilyName(string path) {
        return Path.GetFileNameWithoutExtension(path).Trim();
    }

    public static List<string> GetAvailableFontFamilies() {
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try {
            foreach (var family in FontManager.Current.SystemFonts) {
                if (!string.IsNullOrWhiteSpace(family.Name)) {
                    families.Add(family.Name);
                }
            }
        } catch (Exception e) {
            Log.Debug(e, "Failed to enumerate system UI fonts");
        }
        if (!string.IsNullOrWhiteSpace(loadedFamilyName)) {
            families.Add(loadedFamilyName);
        }
        var result = new List<string>(families);
        result.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }

    /// <summary>
    /// Re-applies the configured font after a language change or preference edit.
    /// </summary>
    public static void Apply() {
        if (Application.Current == null) {
            return;
        }

        var resources = Application.Current.Resources;
        if (customFontApplied) {
            resources.Remove("ui.fontfamily");
            customFontApplied = false;
            Controls.TextLayoutCache.Clear();
        }
        if (resources.TryGetResource("ui.fontfamily", Avalonia.Styling.ThemeVariant.Default, out var fallback)
            && fallback is FontFamily family) {
            fallbackFontFamily = family;
        }

        var path = Preferences.Default.UiFontPath;
        if (!IsSupportedFontPath(path)) {
            if (loadedFontPath != null) {
                FontManager.Current.RemoveFontCollection(CollectionKey);
                loadedFontPath = null;
                loadedFamilyName = null;
            }
            var systemFamilyName = Preferences.Default.UiFontFamily?.Trim();
            if (!string.IsNullOrWhiteSpace(systemFamilyName)) {
                try {
                    resources["ui.fontfamily"] = new FontFamily(systemFamilyName);
                    customFontApplied = true;
                    Controls.TextLayoutCache.Clear();
                } catch (Exception e) {
                    Log.Warning(e, "Failed to apply UI font family {family}", systemFamilyName);
                    resources.Remove("ui.fontfamily");
                }
            }
            return;
        }

        try {
            if (!string.Equals(loadedFontPath, path, StringComparison.OrdinalIgnoreCase)) {
                if (loadedFontPath != null) {
                    FontManager.Current.RemoveFontCollection(CollectionKey);
                }
                throw new PlatformNotSupportedException("Custom font loading is unavailable on Avalonia 12.");
            }
            if (string.IsNullOrWhiteSpace(loadedFamilyName)) {
                throw new InvalidDataException("The font file does not contain a readable typeface.");
            }

            var familyName = string.IsNullOrWhiteSpace(Preferences.Default.UiFontFamily)
                ? loadedFamilyName!
                : Preferences.Default.UiFontFamily.Trim();
            var fontFamily = new FontFamily($"{CollectionKey.AbsoluteUri}#{familyName}");
            if (!FontManager.Current.TryGetGlyphTypeface(
                    new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal),
                    out _)) {
                familyName = loadedFamilyName!;
                fontFamily = new FontFamily($"{CollectionKey.AbsoluteUri}#{familyName}");
            }

            Preferences.Default.UiFontFamily = familyName;
            resources["ui.fontfamily"] = fontFamily;
            customFontApplied = true;
            Controls.TextLayoutCache.Clear();
        } catch (Exception e) {
            FontManager.Current.RemoveFontCollection(CollectionKey);
            loadedFontPath = null;
            loadedFamilyName = null;
            Log.Warning(e, "Failed to load custom UI font {path}", path);
            resources.Remove("ui.fontfamily");
            Controls.TextLayoutCache.Clear();
        }
    }

    #if false
    private sealed class LocalFontCollection : FontCollectionBase {
        private readonly Uri key;
        private readonly string path;
        private readonly List<FontFamily> families = new();
        public string FamilyName { get; private set; } = string.Empty;

        public LocalFontCollection(Uri key, string path) {
            this.key = key;
            this.path = path;
        }

        public override Uri Key => key;
        public override int Count => families.Count;
        public override FontFamily this[int index] => families[index];

        public override void Initialize(IFontManagerImpl fontManager) {
            using var stream = File.OpenRead(path);
            if (CreateGlyphTypefaceFromStream == null) {
                return;
            }
            var args = new object?[] { stream, FontSimulations.None, null };
            var loaded = (bool)(CreateGlyphTypefaceFromStream.Invoke(fontManager, args) ?? false);
            if (!loaded || args[2] is not IGlyphTypeface glyphTypeface) {
                return;
            }

            var collectionKey = new FontCollectionKey(
                glyphTypeface.Style, glyphTypeface.Weight, glyphTypeface.Stretch);
            FamilyName = glyphTypeface.FamilyName;
            families.Add(new FontFamily(key, FamilyName));
            var typefaces = _glyphTypefaceCache.GetOrAdd(
                FamilyName, _ => new ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?>());
            typefaces[collectionKey] = glyphTypeface;
        }

        public override bool TryGetGlyphTypeface(
            string familyName,
            FontStyle style,
            FontWeight weight,
            FontStretch stretch,
            [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface) {
            glyphTypeface = null;
            if (!_glyphTypefaceCache.TryGetValue(familyName, out var typefaces)) {
                foreach (var pair in _glyphTypefaceCache) {
                    if (pair.Key.StartsWith(familyName, StringComparison.OrdinalIgnoreCase)) {
                        typefaces = pair.Value;
                        break;
                    }
                }
            }
            if (typefaces == null) {
                return false;
            }
            if (typefaces.TryGetValue(new FontCollectionKey(style, weight, stretch), out glyphTypeface)
                && glyphTypeface != null) {
                return true;
            }
            glyphTypeface = typefaces.Values.FirstOrDefault(value => value != null);
            return glyphTypeface != null;
        }

        public override IEnumerator<FontFamily> GetEnumerator() => families.GetEnumerator();
    }
    #endif
}
