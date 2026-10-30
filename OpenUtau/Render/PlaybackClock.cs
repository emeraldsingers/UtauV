using System;
using OpenUtau.App.Render;
using OpenUtau.Core;

namespace OpenUtau.App {
    public static class PlaybackClock {
        static DateTime? virtualNow;

        public static event Action? Advanced;

        public static DateTime Now => virtualNow ?? DateTime.UtcNow;

        public static bool IsActive => PlaybackManager.Inst.PlayingMaster || VideoRenderer.IsRenderingVideo;

        internal static void Begin() {
            virtualNow = DateTime.UtcNow;
        }

        internal static void Advance(TimeSpan step) {
            virtualNow += step;
            Advanced?.Invoke();
        }

        internal static void End() {
            virtualNow = null;
        }
    }
}
