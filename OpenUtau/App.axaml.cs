using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using OpenUtau.App.Views;
using OpenUtau.Colors;
using Serilog;

namespace OpenUtau.App {
    public class App : Application {
        public override void Initialize() {
            Log.Information("Initializing application.");
            AvaloniaXamlLoader.Load(this);
            SetUiCornerRadii();
#if DEBUG
            this.AttachDeveloperTools();
#endif
            InitializeCulture();
            InitializeTheme();
            Log.Information("Initialized application.");
        }

        public override void OnFrameworkInitializationCompleted() {
            Log.Information("Framework initialization completed.");
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                desktop.MainWindow = new SplashWindow();
            }

            base.OnFrameworkInitializationCompleted();
            UiFontManager.Apply();
        }

        public void InitializeCulture() {
            Log.Information("Initializing culture.");
            string sysLang = CultureInfo.InstalledUICulture.Name;
            string prefLang = Core.Util.Preferences.Default.Language;
            var languages = GetLanguages();
            if (languages.ContainsKey(prefLang)) {
                SetLanguage(prefLang);
            } else if (languages.ContainsKey(sysLang)) {
                SetLanguage(sysLang);
                Core.Util.Preferences.Default.Language = sysLang;
                Core.Util.Preferences.Save();
            } else {
                SetLanguage("en-US");
            }

            // Force using InvariantCulture to prevent issues caused by culture dependent string conversion, especially for floating point numbers.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            UiFontManager.Apply();
            Log.Information("Initialized culture.");
        }

        public static Dictionary<string, IResourceProvider> GetLanguages() {
            if (Current == null) {
                return new();
            }
            var result = new Dictionary<string, IResourceProvider>();
            foreach (string key in Current.Resources.Keys.OfType<string>()) {
                if (key.StartsWith("strings-") &&
                    Current.Resources.TryGetResource(key, ThemeVariant.Default, out var res) &&
                    res is IResourceProvider rp) {
                    result.Add(key.Replace("strings-", ""), rp);
                }
            }
            return result;
        }

        public static void SetLanguage(string language) {
            if (Current == null) {
                return;
            }
            var languages = GetLanguages();
            foreach (var res in languages.Values) {
                Current.Resources.MergedDictionaries.Remove(res);
            }
            if (language != "en-US") {
                Current.Resources.MergedDictionaries.Add(languages["en-US"]);
            }
            if (languages.TryGetValue(language, out var res1)) {
                Current.Resources.MergedDictionaries.Add(res1);
            }
            UiFontManager.Apply();
        }

        static async void InitializeTheme() {
            Log.Information("Initializing theme.");
            try {
                CustomTheme.ListThemes();
                await OudepLoaderRegistry.LoadAllAsync();
            } catch (Exception e) {
                Log.Error(e, "Failed to load themes from packages.");
            }
            SetTheme();
            Log.Information("Initialized theme.");
        }

        public static void SetTheme() {
            if (Current == null) {
                return;
            }
            var light = (IResourceDictionary) Current.Resources["themes-light"]!;
            var dark = (IResourceDictionary) Current.Resources["themes-dark"]!;
            var synthV = (IResourceDictionary) Current.Resources["themes-synthv"]!;
            var neapolitan = (IResourceDictionary) Current.Resources["themes-neapolitan"]!;
            var teal = (IResourceDictionary) Current.Resources["themes-teal"]!;
            var lightBreeze = (IResourceDictionary) Current.Resources["themes-light-breeze"]!;
            var graphite = (IResourceDictionary) Current.Resources["themes-graphite"]!;
            var ice = (IResourceDictionary) Current.Resources["themes-ice"]!;
            var silver = (IResourceDictionary) Current.Resources["themes-silver"]!;
            var custom = (IResourceDictionary) Current.Resources["themes-custom"]!;
            switch (Core.Util.Preferences.Default.ThemeName) { 
                case "Light":
                    ApplyTheme(light);
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case "Dark":
                    ApplyTheme(dark);
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "SynthV":
                    ApplyTheme(synthV);
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "Neapolitan":
                    ApplyTheme(neapolitan);
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case "Dark Teal":
                    ApplyTheme(teal);
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "Light Breeze":
                    ApplyTheme(lightBreeze);
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case "Graphite":
                    ApplyTheme(graphite);
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "Ice":
                    ApplyTheme(ice);
                    Current.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                case "Silver":
                    ApplyTheme(silver);
                    Current.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                default:
                    ApplyTheme(custom);
                    CustomTheme.ApplyTheme(Core.Util.Preferences.Default.ThemeName);
                    if (CustomTheme.Default.IsDarkMode == true) {
                        Current.RequestedThemeVariant = ThemeVariant.Dark;
                    } else {
                        Current.RequestedThemeVariant = ThemeVariant.Light;
                    }
                    break;
            }
            ThemeManager.LoadTheme();
            CustomSingerTheme.ListThemes();
        }

        public static void SetUiCornerRadii() {
            if (Current == null) {
                return;
            }
            Current.Resources["ButtonCornerRadius"] = new CornerRadius(
                Math.Clamp(Core.Util.Preferences.Default.ButtonCornerRadius, 0, 10));
            var uiRadius = new CornerRadius(
                Math.Clamp(Core.Util.Preferences.Default.UiCornerRadius, 0, 10));
            Current.Resources["UiCornerRadius"] = uiRadius;
            Current.Resources["ControlCornerRadius"] = uiRadius;
            Current.Resources["OverlayCornerRadius"] = uiRadius;
        }

        private static void ApplyTheme(IResourceDictionary resDict) { 
            var res = Current?.Resources;
            foreach (var item in resDict) {
                res![item.Key] = item.Value;
            }
        }
    }
}
