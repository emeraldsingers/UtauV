using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenUtau.Colors {
    public static class ColorExtractor {
        const int PaletteSize = 16;
        const int SampleSize = 200;
        const double MinTextContrast = 4.5;

        public static CustomTheme.ThemeYaml GenerateTheme(Bitmap bitmap, string themeName, bool? forceLight = null) {
            return GenerateTheme(SamplePixels(bitmap), themeName, forceLight);
        }

        public static CustomTheme.ThemeYaml GenerateTheme(IEnumerable<Color> pixels, string themeName, bool? forceLight = null) {
            List<Color> samples = pixels.Where(c => c.A > 128).ToList();
            if (samples.Count == 0) {
                return CreateThemeFallback(themeName);
            }
            bool isDark = forceLight.HasValue
                ? !forceLight.Value
                : AverageLuma(samples) <= 128.0;
            List<Color> palette = MedianCut(samples, PaletteSize).OrderBy(Luma).ToList();
            return BuildTheme(themeName, AdjustPalette(palette, isDark), isDark);
        }

        private static List<Color> SamplePixels(Bitmap bitmap) {
            int width = bitmap.PixelSize.Width;
            int height = bitmap.PixelSize.Height;
            if (width <= 0 || height <= 0) {
                return new List<Color>();
            }
            int scaleW = Math.Max(1, width / SampleSize);
            int scaleH = Math.Max(1, height / SampleSize);
            int sampleW = width / scaleW;
            int sampleH = height / scaleH;
            int stride = width * 4;
            int bufferSize = stride * height;
            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
            try {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), buffer, bufferSize, stride);
                byte[] data = new byte[bufferSize];
                Marshal.Copy(buffer, data, 0, bufferSize);
                var pixels = new List<Color>(sampleW * sampleH);
                for (int y = 0; y < sampleH; y++) {
                    int row = y * scaleH * stride;
                    for (int x = 0; x < sampleW; x++) {
                        int idx = row + x * scaleW * 4;
                        if (data[idx + 3] > 128) {
                            pixels.Add(Color.FromRgb(data[idx + 2], data[idx + 1], data[idx]));
                        }
                    }
                }
                return pixels;
            } finally {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static List<Color> MedianCut(List<Color> pixels, int targetCount) {
            if (pixels.Count == 0) {
                return Enumerable.Repeat(Avalonia.Media.Colors.Black, targetCount).ToList();
            }
            var boxes = new List<List<Color>> { new(pixels) };
            while (boxes.Count < targetCount) {
                int boxIdx = -1;
                int maxRange = 0;
                int bestDim = 0;
                for (int i = 0; i < boxes.Count; i++) {
                    List<Color> box = boxes[i];
                    if (box.Count < 2) {
                        continue;
                    }
                    int rMin = box.Min(c => (int)c.R);
                    int rMax = box.Max(c => (int)c.R);
                    int gMin = box.Min(c => (int)c.G);
                    int gMax = box.Max(c => (int)c.G);
                    int bMin = box.Min(c => (int)c.B);
                    int bMax = box.Max(c => (int)c.B);
                    int rRange = rMax - rMin;
                    int gRange = gMax - gMin;
                    int bRange = bMax - bMin;
                    int maxDimRange = Math.Max(rRange, Math.Max(gRange, bRange));
                    if (maxDimRange > maxRange) {
                        maxRange = maxDimRange;
                        boxIdx = i;
                        bestDim = rRange >= gRange && rRange >= bRange ? 0 : (gRange >= bRange ? 1 : 2);
                    }
                }
                if (boxIdx < 0) {
                    break;
                }
                List<Color> chosen = boxes[boxIdx];
                chosen.Sort(bestDim switch {
                    0 => Comparer<Color>.Create((a, b) => a.R.CompareTo(b.R)),
                    1 => Comparer<Color>.Create((a, b) => a.G.CompareTo(b.G)),
                    _ => Comparer<Color>.Create((a, b) => a.B.CompareTo(b.B)),
                });
                int mid = chosen.Count / 2;
                boxes[boxIdx] = chosen.Take(mid).ToList();
                boxes.Add(chosen.Skip(mid).ToList());
            }
            return boxes.Select(AverageColor).ToList();
        }

        private static Color AverageColor(List<Color> pixels) {
            if (pixels.Count == 0) {
                return Avalonia.Media.Colors.Black;
            }
            int r = 0;
            int g = 0;
            int b = 0;
            foreach (Color c in pixels) {
                r += c.R;
                g += c.G;
                b += c.B;
            }
            return Color.FromRgb((byte)(r / pixels.Count), (byte)(g / pixels.Count), (byte)(b / pixels.Count));
        }

        private static List<Color> AdjustPalette(List<Color> sorted, bool isDark) {
            int last = sorted.Count - 1;
            var slots = new List<Color>(PaletteSize);
            slots.Add(sorted[0]);
            for (int i = 8; i <= 15; i++) {
                slots.Add(sorted[Math.Min(i, last)]);
            }
            for (int i = 8; i <= 14; i++) {
                slots.Add(sorted[Math.Min(i, last)]);
            }
            if (isDark) {
                slots[0] = ShiftLuma(slots[0], -0.4);
                slots[7] = Blend(slots[7], Color.FromRgb(238, 238, 238));
                slots[8] = ShiftLuma(slots[7], -0.3);
                slots[15] = Blend(slots[15], Color.FromRgb(238, 238, 238));
            } else {
                for (int i = 0; i < PaletteSize; i++) {
                    if (Saturation(slots[i]) >= 0.1) {
                        slots[i] = Saturate(slots[i], 0.5);
                    }
                }
                slots[0] = ShiftLuma(sorted[last], 0.85);
                slots[7] = sorted[0];
                slots[8] = ShiftLuma(sorted[last], -0.4);
                slots[15] = sorted[0];
            }
            return slots;
        }

        private static CustomTheme.ThemeYaml BuildTheme(string themeName, List<Color> clr, bool isDark) {
            Color bg = clr[0];
            Color fg = clr[7];
            for (int i = 0; i < 8 && ContrastRatio(bg, fg) < MinTextContrast; i++) {
                fg = ShiftLuma(fg, isDark ? 0.15 : -0.15);
            }
            if (ContrastRatio(bg, fg) < MinTextContrast) {
                fg = isDark ? Avalonia.Media.Colors.White : Avalonia.Media.Colors.Black;
            }
            Color accent = clr[4];
            Color accent2 = clr[2];
            Color accent3 = clr[1];
            Color dim = clr[8];
            Color muted = clr[3];
            Color darkest = clr.OrderBy(Luma).First();
            return new CustomTheme.ThemeYaml {
                Name = themeName,
                IsDarkMode = isDark,
                BackgroundColor = Hex(bg),
                BackgroundColorPointerOver = Hex(ShiftLuma(bg, isDark ? 0.05 : -0.04)),
                BackgroundColorPressed = Hex(ShiftLuma(bg, isDark ? 0.1 : -0.08)),
                BackgroundColorDisabled = Hex(ShiftLuma(bg, isDark ? 0.03 : -0.02)),
                ForegroundColor = Hex(fg),
                ForegroundColorPointerOver = Hex(fg),
                ForegroundColorPressed = Hex(ShiftLuma(fg, isDark ? -0.1 : 0.1)),
                ForegroundColorDisabled = Hex(Blend(fg, bg, 0.5)),
                BorderColor = Hex(dim),
                BorderColorPointerOver = Hex(ShiftLuma(dim, isDark ? 0.15 : -0.15)),
                SystemAccentColor = Hex(accent),
                SystemAccentColorLight1 = Hex(ShiftLuma(accent, 0.25)),
                SystemAccentColorDark1 = Hex(ShiftLuma(accent, -0.25)),
                NeutralAccentColor = Hex(dim),
                NeutralAccentColorPointerOver = Hex(ShiftLuma(dim, isDark ? 0.1 : -0.1)),
                AccentColor1 = Hex(accent),
                AccentColor2 = Hex(accent2),
                AccentColor3 = Hex(accent3),
                TickLineColor = Hex(muted),
                BarNumberColor = Hex(muted),
                FinalPitchColor = Hex(muted),
                TrackBackgroundAltColor = Hex(ShiftLuma(bg, isDark ? 0.04 : -0.03)),
                WhiteKeyColorLeft = Hex(ShiftLuma(bg, isDark ? 0.08 : -0.06)),
                WhiteKeyColorRight = Hex(ShiftLuma(bg, isDark ? 0.04 : -0.03)),
                WhiteKeyNameColor = Hex(Blend(fg, accent, 0.6)),
                CenterKeyColorLeft = Hex(ShiftLuma(accent, isDark ? 0.3 : 0.45)),
                CenterKeyColorRight = Hex(ShiftLuma(accent, isDark ? 0.2 : 0.35)),
                CenterKeyNameColor = Hex(darkest),
                BlackKeyColorLeft = Hex(ShiftLuma(accent, -0.2)),
                BlackKeyColorRight = Hex(ShiftLuma(accent, -0.35)),
                BlackKeyNameColor = "#FFFFFF",
                MainColorA = Hex(accent),
                MainColorB = Hex(accent2),
                IsDarkModeEasy = isDark,
            };
        }

        private static CustomTheme.ThemeYaml CreateThemeFallback(string themeName) {
            return new CustomTheme.ThemeYaml {
                Name = themeName,
            };
        }

        private static double AverageLuma(List<Color> pixels) {
            double sum = 0.0;
            foreach (Color c in pixels) {
                sum += Luma(c);
            }
            return sum / pixels.Count;
        }

        private static double Luma(Color c) {
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        }

        private static Color ShiftLuma(Color c, double amount) {
            amount = Math.Clamp(amount, -1.0, 1.0);
            if (amount >= 0) {
                return Color.FromRgb(
                    (byte)(c.R + (255 - c.R) * amount),
                    (byte)(c.G + (255 - c.G) * amount),
                    (byte)(c.B + (255 - c.B) * amount));
            }
            double k = 1.0 + amount;
            return Color.FromRgb((byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
        }

        private static Color Blend(Color a, Color b) {
            return Blend(a, b, 0.5);
        }

        private static Color Blend(Color a, Color b, double t) {
            t = Math.Clamp(t, 0.0, 1.0);
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        private static double Saturation(Color c) {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            if (max == min) {
                return 0.0;
            }
            double l = (max + min) / 2.0;
            return l <= 0.5 ? (max - min) / (max + min) : (max - min) / (2.0 - max - min);
        }

        private static Color Saturate(Color c, double amount) {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2.0;
            double h = max == min ? 0.0
                : max == r ? (g - b) / (max - min) * 60.0
                : max == g ? (2.0 + (b - r) / (max - min)) * 60.0
                : (4.0 + (r - g) / (max - min)) * 60.0;
            if (h < 0.0) {
                h += 360.0;
            }
            double s = Math.Clamp(amount, 0.0, 1.0);
            double cx = (1.0 - Math.Abs(2.0 * l - 1.0)) * s;
            double xx = cx * (1.0 - Math.Abs(h / 60.0 % 2.0 - 1.0));
            double m = l - cx / 2.0;
            double rr;
            double gg;
            double bb;
            if (h < 60.0) {
                rr = cx;
                gg = xx;
                bb = 0.0;
            } else if (h < 120.0) {
                rr = xx;
                gg = cx;
                bb = 0.0;
            } else if (h < 180.0) {
                rr = 0.0;
                gg = cx;
                bb = xx;
            } else if (h < 240.0) {
                rr = 0.0;
                gg = xx;
                bb = cx;
            } else if (h < 300.0) {
                rr = xx;
                gg = 0.0;
                bb = cx;
            } else {
                rr = cx;
                gg = 0.0;
                bb = xx;
            }
            return Color.FromRgb(
                (byte)Math.Clamp((int)((rr + m) * 255.0), 0, 255),
                (byte)Math.Clamp((int)((gg + m) * 255.0), 0, 255),
                (byte)Math.Clamp((int)((bb + m) * 255.0), 0, 255));
        }

        private static double RelativeLuminance(Color c) {
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

            static double Channel(byte v) {
                double s = v / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
        }

        private static double ContrastRatio(Color a, Color b) {
            double la = RelativeLuminance(a);
            double lb = RelativeLuminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        private static string Hex(Color c) {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
