using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace OpenUtau.App.Roflofic {
    public static class RofloficEffects {
        private static readonly DispatcherTimer Timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
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

        public static Avalonia.Media.Color RainbowColor(double seed, byte alpha = 255) {
            double h = (seed + phase) % 1.0;
            if (h < 0) h += 1;
            double h6 = h * 6;
            int sector = (int)Math.Floor(h6);
            double f = h6 - sector;
            double q = 1 - f;
            double t = f;
            double r, g, b;
            switch (sector) {
                case 0: r = 1; g = t; b = 0; break;
                case 1: r = q; g = 1; b = 0; break;
                case 2: r = 0; g = 1; b = t; break;
                case 3: r = 0; g = q; b = 1; break;
                case 4: r = t; g = 0; b = 1; break;
                default: r = 1; g = 0; b = q; break;
            }
            return Avalonia.Media.Color.FromArgb(alpha, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        public static IBrush Brush(double seed, byte alpha = 255) => new SolidColorBrush(RainbowColor(seed, alpha));

        public static IBrush Gradient(double seed, byte alpha = 255) {
            return new LinearGradientBrush {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = {
                    new GradientStop(RainbowColor(seed - 0.08, alpha), 0),
                    new GradientStop(RainbowColor(seed + 0.08, alpha), 1),
                },
            };
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
