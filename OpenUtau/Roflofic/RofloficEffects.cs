using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace OpenUtau.App.Roflofic {
    public static class RofloficEffects {
        private const int HueSteps = 256;

        private static readonly DispatcherTimer Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        private static readonly Dictionary<(int hue, byte alpha), IBrush> brushCache = new();
        private static readonly Dictionary<(int fromHue, int toHue, byte alpha), IBrush> gradientCache = new();
        private static readonly Dictionary<(int hue, byte alpha, double thickness), Pen> penCache = new();
        private static double phase;
        public static event Action? Changed;
        public static bool RainbowEnabled { get; private set; }
        public static double Phase => phase;

        static RofloficEffects() {
            Timer.Tick += (_, _) => {
                phase = (phase + 0.012) % 1.0;
                Changed?.Invoke();
            };
        }

        public static void SetRainbowEnabled(bool enabled) {
            if (!Dispatcher.UIThread.CheckAccess()) {
                Dispatcher.UIThread.Post(() => SetRainbowEnabled(enabled));
                return;
            }
            RainbowEnabled = enabled;
            if (enabled) Timer.Start(); else Timer.Stop();
            Changed?.Invoke();
        }

        public static Color RainbowColor(double seed, byte alpha = 255) {
            double h = ((seed + phase) % 1 + 1) % 1;
            return RainbowColorOf(h, alpha);
        }

        static Color RainbowColorOf(double h, byte alpha) {
            double h6 = h * 6;
            int sector = (int)Math.Floor(h6);
            double f = h6 - sector;
            double q = 1 - f;
            double t = f;
            byte r, g, b;
            switch (sector) {
                case 0: r = 255; g = ToByte(t); b = 0; break;
                case 1: r = ToByte(q); g = 255; b = 0; break;
                case 2: r = 0; g = 255; b = ToByte(t); break;
                case 3: r = 0; g = ToByte(q); b = 255; break;
                case 4: r = ToByte(t); g = 0; b = 255; break;
                default: r = 255; g = 0; b = ToByte(q); break;
            }
            return Color.FromArgb(alpha, r, g, b);
        }

        static byte ToByte(double v) => (byte)Math.Round(v * 255);

        static int QuantizedHue(double seed) {
            double h = ((seed + phase) % 1 + 1) % 1;
            return (int)(h * HueSteps) % HueSteps;
        }

        public static IBrush Brush(double seed, byte alpha = 255) {
            var key = (QuantizedHue(seed), alpha);
            if (!brushCache.TryGetValue(key, out var brush)) {
                if (brushCache.Count > 4096) {
                    brushCache.Clear();
                }
                brush = new ImmutableSolidColorBrush(RainbowColorOf(key.Item1 / (double)HueSteps, alpha));
                brushCache[key] = brush;
            }
            return brush;
        }

        public static IBrush Gradient(double seed, byte alpha = 255) {
            var key = (fromHue: QuantizedHue(seed - 0.08), toHue: QuantizedHue(seed + 0.08), alpha);
            if (!gradientCache.TryGetValue(key, out var brush)) {
                if (gradientCache.Count > 16384) {
                    gradientCache.Clear();
                }
                brush = new LinearGradientBrush {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = {
                        new GradientStop(RainbowColorOf(key.fromHue / (double)HueSteps, alpha), 0),
                        new GradientStop(RainbowColorOf(key.toHue / (double)HueSteps, alpha), 1),
                    },
                };
                gradientCache[key] = brush;
            }
            return brush;
        }

        public static Pen Pen(double seed, double thickness, byte alpha = 255) {
            var key = (QuantizedHue(seed), alpha, thickness);
            if (!penCache.TryGetValue(key, out var pen)) {
                if (penCache.Count > 4096) {
                    penCache.Clear();
                }
                pen = new Pen(Brush(seed, alpha), thickness);
                penCache[key] = pen;
            }
            return pen;
        }

        public static Vector OrbitOffset(double notePosition, double elapsed, double trackHeight, bool enabled) {
            if (!enabled) return default;
            double radius = Math.Min(22, trackHeight * 0.45);
            double angle = elapsed * Math.PI * 8 + notePosition * 0.021;
            return new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
        }

        public static Matrix OrbitRotation(double notePosition, double elapsed, Point center, bool enabled) {
            if (!enabled) return Matrix.Identity;
            double angle = elapsed * Math.PI * 12 + notePosition * 0.031;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            return new Matrix(cos, sin, -sin, cos,
                center.X - center.X * cos + center.Y * sin,
                center.Y - center.Y * cos - center.X * sin);
        }
    }
}
