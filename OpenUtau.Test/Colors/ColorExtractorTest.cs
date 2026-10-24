using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Xunit;

namespace OpenUtau.Colors {
    public class ColorExtractorTest {
        static List<Color> Pixels(params Color[] colors) {
            var pixels = new List<Color>();
            foreach (var color in colors) {
                pixels.AddRange(Enumerable.Repeat(color, 100));
            }
            return pixels;
        }

        static double Contrast(string a, string b) {
            Color ca = Color.Parse(a);
            Color cb = Color.Parse(b);
            double la = RelativeLuminance(ca);
            double lb = RelativeLuminance(cb);
            return (System.Math.Max(la, lb) + 0.05) / (System.Math.Min(la, lb) + 0.05);
        }

        static double RelativeLuminance(Color c) {
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

            static double Channel(byte v) {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : System.Math.Pow((s + 0.055) / 1.055, 2.4);
            }
        }

        [Fact]
        public void NoPixelsGivesFallbackTheme() {
            var theme = ColorExtractor.GenerateTheme(new List<Color>(), "fallback");
            Assert.Equal("fallback", theme.Name);
        }

        [Fact]
        public void DarkImageGivesDarkTheme() {
            var theme = ColorExtractor.GenerateTheme(Pixels(
                Color.FromRgb(10, 10, 20),
                Color.FromRgb(20, 20, 30),
                Color.FromRgb(30, 25, 40),
                Color.FromRgb(40, 30, 50),
                Color.FromRgb(15, 15, 25),
                Color.FromRgb(25, 25, 35)), "dark");
            Assert.True(theme.IsDarkMode);
            Assert.True(theme.IsDarkModeEasy);
        }

        [Fact]
        public void LightImageGivesLightTheme() {
            var theme = ColorExtractor.GenerateTheme(Pixels(
                Color.FromRgb(240, 240, 245),
                Color.FromRgb(230, 230, 235),
                Color.FromRgb(220, 220, 225),
                Color.FromRgb(250, 250, 250),
                Color.FromRgb(210, 210, 215),
                Color.FromRgb(235, 235, 240)), "light");
            Assert.False(theme.IsDarkMode);
            Assert.False(theme.IsDarkModeEasy);
        }

        [Fact]
        public void ForceLightOverridesImageBrightness() {
            var theme = ColorExtractor.GenerateTheme(Pixels(Color.FromRgb(10, 10, 20)), "forced", true);
            Assert.False(theme.IsDarkMode);
        }

        [Fact]
        public void TextColorsHaveEnoughContrast() {
            var palettes = new[] {
                Pixels(Color.FromRgb(10, 10, 20), Color.FromRgb(60, 60, 80), Color.FromRgb(120, 120, 140)),
                Pixels(Color.FromRgb(240, 240, 240), Color.FromRgb(200, 200, 200), Color.FromRgb(160, 160, 160)),
                Pixels(Color.FromRgb(128, 128, 128), Color.FromRgb(100, 100, 100), Color.FromRgb(150, 150, 150)),
                Pixels(Color.FromRgb(200, 50, 50), Color.FromRgb(50, 50, 200), Color.FromRgb(50, 200, 50)),
            };
            foreach (var pixels in palettes) {
                var theme = ColorExtractor.GenerateTheme(pixels, "contrast");
                double ratio = Contrast(theme.BackgroundColor, theme.ForegroundColor);
                Assert.True(ratio >= 4.5, $"contrast {ratio} for {theme.BackgroundColor} on {theme.ForegroundColor}");
            }
        }

        [Fact]
        public void StatesDifferFromBaseBackground() {
            foreach (bool forceLight in new[] { true, false }) {
                var theme = ColorExtractor.GenerateTheme(Pixels(
                    Color.FromRgb(90, 120, 200),
                    Color.FromRgb(200, 90, 120),
                    Color.FromRgb(120, 200, 90),
                    Color.FromRgb(200, 200, 120),
                    Color.FromRgb(60, 60, 90)), "states", forceLight);
                Assert.NotEqual(theme.BackgroundColor, theme.BackgroundColorPointerOver);
                Assert.NotEqual(theme.BackgroundColor, theme.BackgroundColorPressed);
                Assert.NotEqual(theme.ForegroundColor, theme.ForegroundColorPressed);
            }
        }

        [Fact]
        public void AllColorsAreParseable() {
            var theme = ColorExtractor.GenerateTheme(Pixels(
                Color.FromRgb(90, 120, 200),
                Color.FromRgb(200, 90, 120),
                Color.FromRgb(120, 200, 90),
                Color.FromRgb(200, 200, 120),
                Color.FromRgb(60, 60, 90),
                Color.FromRgb(30, 30, 60)), "colors");
            var fields = typeof(CustomTheme.ThemeYaml).GetFields();
            Assert.NotEmpty(fields);
            foreach (var field in fields) {
                if (field.FieldType != typeof(string) || field.Name == nameof(CustomTheme.ThemeYaml.Name)) {
                    continue;
                }
                string value = (string)field.GetValue(theme)!;
                Assert.True(Color.TryParse(value, out _), $"{field.Name} = {value}");
            }
        }

        [Fact]
        public void MainColorsMatchAccents() {
            var theme = ColorExtractor.GenerateTheme(Pixels(
                Color.FromRgb(90, 120, 200),
                Color.FromRgb(200, 90, 120),
                Color.FromRgb(120, 200, 90),
                Color.FromRgb(200, 200, 120),
                Color.FromRgb(60, 60, 90)), "main");
            Assert.Equal(theme.AccentColor1, theme.MainColorA);
            Assert.Equal(theme.AccentColor2, theme.MainColorB);
        }
    }
}
