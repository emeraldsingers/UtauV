using System;
using System.Runtime.InteropServices;
using OpenUtau.Core.Util;

namespace OpenUtau.App {
    static class DisplayInfo {
        private const int VREFRESH = 116;
        private const int FallbackMax = 240;
        private static int? cached;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hDc, int index);

        public static int MaxRefreshRate() {
            if (cached.HasValue) {
                return cached.Value;
            }
            int rate = FallbackMax;
            if (OS.IsWindows()) {
                try {
                    var dc = GetDC(IntPtr.Zero);
                    if (dc != IntPtr.Zero) {
                        int hz = GetDeviceCaps(dc, VREFRESH);
                        ReleaseDC(IntPtr.Zero, dc);
                        if (hz > 1) {
                            rate = hz;
                        }
                    }
                } catch (Exception) {
                }
            }
            cached = Math.Max(60, rate);
            return cached.Value;
        }
    }
}
