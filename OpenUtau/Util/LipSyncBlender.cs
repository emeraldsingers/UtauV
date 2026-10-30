using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using OpenUtau.Core;
using Serilog;

namespace OpenUtau.App.Util {
    internal sealed class LipSyncBlender {
        private readonly Action<Bitmap?> apply;
        private readonly object gate;
        private bool running;
        private DateTime startedAt;
        private readonly List<IDisposable> retired = new List<IDisposable>();
        private Bitmap? shown;
        private Bitmap? from;
        private bool fromOwned;
        private Bitmap? target;
        private RenderTargetBitmap? frame;

        public LipSyncBlender(object gate, Action<Bitmap?> apply) {
            this.gate = gate;
            this.apply = apply;
        }

        public void Show(Bitmap? next, bool smooth) {
            if (ReferenceEquals(next, target ?? shown)) {
                return;
            }
            if (!smooth || next == null || (shown == null && frame == null)) {
                Reset();
                shown = next;
                apply(next);
                return;
            }
            if (target != null && frame != null) {
                if (fromOwned && from != null) {
                    retired.Add(from);
                }
                from = frame;
                fromOwned = true;
                frame = null;
            } else if (target == null) {
                from = shown;
                fromOwned = false;
            }
            target = next;
            startedAt = PlaybackClock.Now;
            if (!running) {
                running = true;
                PlaybackClock.Advanced += Tick;
                Dispatcher.UIThread.Post(Run);
            }
        }

        private async void Run() {
            try {
                while (target != null) {
                    Tick();
                    await Task.Delay(16);
                }
            } finally {
                PlaybackClock.Advanced -= Tick;
                running = false;
            }
        }

        public void Reset() {
            target = null;
            shown = null;
            DisposeAll();
        }

        private void DisposeAll() {
            if (fromOwned) {
                from?.Dispose();
            }
            from = null;
            fromOwned = false;
            frame?.Dispose();
            frame = null;
            DisposeRetired();
        }

        private void DisposeRetired() {
            foreach (var item in retired) {
                item.Dispose();
            }
            retired.Clear();
        }

        private void Tick() {
            var to = target;
            var source = from;
            if (to == null || source == null) {
                return;
            }
            if (!System.Threading.Monitor.TryEnter(gate)) {
                return;
            }
            try {
                double t = Math.Min(1.0, (PlaybackClock.Now - startedAt).TotalMilliseconds / LipSync.BlendMs);
                if (t >= 1.0) {
                    shown = to;
                    target = null;
                    apply(to);
                    DisposeAll();
                    return;
                }
                var next = new RenderTargetBitmap(to.PixelSize, to.Dpi);
                using (var ctx = next.CreateDrawingContext()) {
                    var dest = new Rect(to.Size);
                    ctx.DrawImage(source, new Rect(source.Size), dest);
                    using (ctx.PushOpacity(t)) {
                        ctx.DrawImage(to, new Rect(to.Size), dest);
                    }
                }
                var previous = frame;
                frame = next;
                apply(next);
                previous?.Dispose();
                DisposeRetired();
            } catch (Exception e) {
                Log.Error(e, "Lip sync blend failed.");
                var fallback = target;
                Reset();
                shown = fallback;
                apply(fallback);
            } finally {
                System.Threading.Monitor.Exit(gate);
            }
        }
    }
}
