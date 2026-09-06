using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;
using ReactiveUI.Primitives;
using Serilog;

namespace OpenUtau.App.Controls {
    class WaveformImage : Control {
        private const int SampleRate = 44100;
        private const int CacheViewports = 5;

        public static readonly DirectProperty<WaveformImage, double> TickWidthProperty =
            AvaloniaProperty.RegisterDirect<WaveformImage, double>(
                nameof(TickWidth),
                o => o.TickWidth,
                (o, v) => o.TickWidth = v);
        public static readonly DirectProperty<WaveformImage, double> TickOffsetProperty =
            AvaloniaProperty.RegisterDirect<WaveformImage, double>(
                nameof(TickOffset),
                o => o.TickOffset,
                (o, v) => o.TickOffset = v);
        public static readonly DirectProperty<WaveformImage, bool> ShowWaveformProperty =
            AvaloniaProperty.RegisterDirect<WaveformImage, bool>(
                nameof(ShowWaveform),
                o => o.ShowWaveform,
                (o, v) => o.ShowWaveform = v);

        public double TickWidth {
            get => tickWidth;
            set => SetAndRaise(TickWidthProperty, ref tickWidth, value);
        }
        public double TickOffset {
            get => tickOffset;
            set => SetAndRaise(TickOffsetProperty, ref tickOffset, value);
        }
        public bool ShowWaveform {
            get => showWaveform;
            set => SetAndRaise(ShowWaveformProperty, ref showWaveform, value);
        }

        private double tickWidth;
        private double tickOffset;
        private bool showWaveform;

        private WriteableBitmap? bitmap;
        private float[] sampleData = Array.Empty<float>();
        private int[] bitmapData = Array.Empty<int>();
        private int cacheStartX;
        private int cacheWidth;
        private int cacheHeight;
        private double cacheTickWidth = double.NaN;
        private bool waveformDataChanged = true;
        private DateTime mixUnlockTime = DateTime.MinValue;
        private bool wasRendering;

        // Waveform peak color, shared by the bitmap peaks and the phrase bound border.
        private const int WaveformArgb = 0x7F7F7F7F;
        private static readonly IBrush WaveformBorderBrush = new SolidColorBrush(Color.FromArgb(0x7F, 0x7F, 0x7F, 0x7F));
        private IBrush? cachedFillBrush;
        private Color? cachedFillColor;

        public WaveformImage() {
            MessageBus.Current.Listen<WaveformRefreshEvent>()
                .Subscribe(_ => {
                    waveformDataChanged = true;
                    InvalidateVisual();
                });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == DataContextProperty ||
                change.Property == TickWidthProperty ||
                change.Property == ShowWaveformProperty) {
                waveformDataChanged = true;
            }
            if (change.Property == DataContextProperty ||
                change.Property == TickWidthProperty ||
                change.Property == TickOffsetProperty ||
                change.Property == ShowWaveformProperty) {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context) {
            if (DataContext is not NotesViewModel viewModel ||
                double.IsNaN(viewModel.TickOffset) ||
                Bounds.Width <= 0 || Bounds.Height <= 0) {
                return;
            }

            if (!ShowWaveform || viewModel.TickWidth <= ViewConstants.PianoRollTickWidthShowDetails) {
                return;
            }

            var project = viewModel.Project;
            var part = viewModel.Part;
            if (project == null || part == null) {
                return;
            }

            int viewportWidth = (int)Math.Ceiling(Bounds.Width);
            int viewportHeight = (int)Math.Ceiling(Bounds.Height);
            double worldLeftX = (viewModel.TickOrigin + viewModel.TickOffset) * viewModel.TickWidth;
            int visibleStartX = (int)Math.Floor(worldLeftX);
            int visibleEndX = (int)Math.Ceiling(worldLeftX + Bounds.Width);

            bool isRendering = PlaybackManager.Inst.StartingToPlay;
            if (wasRendering && !isRendering) {
                mixUnlockTime = DateTime.Now;
                waveformDataChanged = true;
            }
            wasRendering = isRendering;

            double snapAgeMs = (DateTime.Now - mixUnlockTime).TotalMilliseconds;
            double snapProgress = Math.Clamp(snapAgeMs / 300.0, 0.0, 1.0);
            float snapEase = 1.0f - (float)Math.Pow(1.0 - snapProgress, 3);
            bool needsAnotherFrame = snapProgress < 1.0;

            if (NeedsCacheRebuild(visibleStartX, visibleEndX, viewportWidth, viewportHeight, viewModel.TickWidth)) {
                RebuildCache(
                    project,
                    part,
                    viewModel.TickWidth,
                    visibleStartX,
                    viewportWidth,
                    viewportHeight,
                    snapEase,
                    ref needsAnotherFrame);
                waveformDataChanged = false;
            }

            if (needsAnotherFrame) {
                waveformDataChanged = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(
                    InvalidateVisual,
                    Avalonia.Threading.DispatcherPriority.Background);
            }

            DrawPhraseBounds(context, project, part, viewModel);
            if (bitmap != null) {
                var sourceRect = new Rect(worldLeftX - cacheStartX, 0, Bounds.Width, Bounds.Height);
                var destinationRect = Bounds.WithX(0).WithY(0);
                context.DrawImage(bitmap, sourceRect, destinationRect);
            }
        }

        private void DrawPhraseBounds(
            DrawingContext context,
            UProject project,
            UVoicePart part,
            NotesViewModel viewModel) {
            IBrush fill;
            if (ThemeManager.BackgroundBrush is SolidColorBrush background) {
                if (cachedFillBrush == null || cachedFillColor != background.Color) {
                    cachedFillBrush = new SolidColorBrush(background.Color) { Opacity = 0.75 };
                    cachedFillColor = background.Color;
                }
                fill = cachedFillBrush;
            } else {
                fill = ThemeManager.BackgroundBrush;
            }

            double width = Bounds.Width;
            double height = Bounds.Height;
            double tickOrigin = viewModel.TickOrigin + viewModel.TickOffset;
            var pen = new Pen(WaveformBorderBrush, 0.5);
            using var state = context.PushClip(new RoundedRect(new Rect(0, 0, width, height), 0, 0));
            foreach (int pass in new[] { 0, 1 }) {
                var brush = pass == 0 ? fill : null;
                var stroke = pass == 0 ? null : pen;
                foreach (var phrase in part.renderPhrases) {
                    (double startMs, double endMs) = phrase.AudioRange;
                    double x1 = Math.Round((project.timeAxis.MsPosToTickPos(startMs) - tickOrigin) * viewModel.TickWidth) + 0.5;
                    double x2 = Math.Round((project.timeAxis.MsPosToTickPos(endMs) - tickOrigin) * viewModel.TickWidth) + 0.5;
                    if (x2 < 0 || x1 > width) {
                        continue;
                    }
                    var rect = new Rect(x1, 0.5, Math.Max(1.0, x2 - x1), Math.Max(1.0, height - 1.0));
                    context.DrawGeometry(brush, stroke, new RectangleGeometry(rect, height / 4.0, height / 4.0));
                }
            }
        }

        private bool NeedsCacheRebuild(
            int visibleStartX,
            int visibleEndX,
            int viewportWidth,
            int viewportHeight,
            double currentTickWidth) {
            if (waveformDataChanged || bitmap == null ||
                cacheHeight != viewportHeight ||
                Math.Abs(cacheTickWidth - currentTickWidth) > double.Epsilon) {
                return true;
            }

            // Keep the viewport away from the cache edges, so normal stationary
            // scrolling only translates the existing peaks instead of rebuilding them.
            return (cacheStartX > 0 && visibleStartX < cacheStartX + viewportWidth) ||
                visibleEndX > cacheStartX + cacheWidth - viewportWidth;
        }

        private void RebuildCache(
            UProject project,
            UVoicePart part,
            double currentTickWidth,
            int visibleStartX,
            int viewportWidth,
            int viewportHeight,
            float snapEase,
            ref bool needsAnotherFrame) {
            cacheWidth = checked(viewportWidth * CacheViewports);
            cacheHeight = viewportHeight;
            cacheTickWidth = currentTickWidth;
            cacheStartX = Math.Max(0, visibleStartX - viewportWidth * ((CacheViewports - 1) / 2));
            EnsureBitmap(cacheWidth, cacheHeight);

            double leftTick = cacheStartX / currentTickWidth;
            double rightTick = (cacheStartX + cacheWidth) / currentTickWidth;
            double leftMs = project.timeAxis.TickPosToMsPos(leftTick);
            double rightMs = project.timeAxis.TickPosToMsPos(rightTick);
            int leftFrame = (int)(leftMs * SampleRate / 1000);
            int rightFrame = (int)(rightMs * SampleRate / 1000);
            int sampleCount = Math.Max(0, (rightFrame - leftFrame) * 2);

            if (sampleData.Length < sampleCount) {
                Array.Resize(ref sampleData, sampleCount);
            }
            Array.Clear(sampleData, 0, sampleCount);

            if (!PlaybackManager.Inst.IsWaveformBlanked) {
                if (PlaybackManager.Inst.StartingToPlay || part.Mix == null) {
                    var phraseHashes = new HashSet<string>(
                        part.renderPhrases.Select(phrase => phrase.hash.ToString()));
                    foreach (var cacheItem in PlaybackManager.Inst.LiveWaveformCache) {
                        if (!phraseHashes.Contains(cacheItem.Key)) {
                            continue;
                        }
                        var cacheValue = cacheItem.Value;
                        if (cacheValue.trackNo != part.trackNo) {
                            continue;
                        }

                        int phraseStartFrame = (int)(cacheValue.posMs * SampleRate / 1000);
                        int phraseStartSample = phraseStartFrame - leftFrame;
                        double ageMs = (DateTime.Now - cacheValue.renderTime).TotalMilliseconds;
                        double animationProgress = Math.Clamp(ageMs / 300.0, 0.0, 1.0);
                        if (animationProgress < 1.0) {
                            needsAnotherFrame = true;
                        }
                        float scale = 1.0f - (float)Math.Pow(1.0 - animationProgress, 3);

                        int start = Math.Max(0, -phraseStartSample);
                        int end = Math.Min(cacheValue.samples.Length, sampleCount / 2 - phraseStartSample);
                        for (int i = start; i < end; i++) {
                            int target = (phraseStartSample + i) * 2;
                            float sample = cacheValue.samples[i] * scale;
                            sampleData[target] += sample;
                            sampleData[target + 1] += sample;
                        }
                    }
                } else {
                    part.Mix.Mix(leftFrame * 2, sampleData, 0, sampleCount);
                }
            }

            Array.Clear(bitmapData, 0, bitmapData.Length);
            int startSample = 0;
            for (int x = 0; x < cacheWidth; x++) {
                double endTick = (cacheStartX + x + 1.0) / currentTickWidth;
                double endMs = project.timeAxis.TickPosToMsPos(endTick);
                int endFrame = (int)(endMs * SampleRate / 1000);
                int endSample = Math.Clamp((endFrame - leftFrame) * 2, 0, sampleCount);
                if (endSample > startSample) {
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    for (int sample = startSample; sample < endSample; sample++) {
                        float value = sampleData[sample];
                        if (value < min) {
                            min = value;
                        }
                        if (value > max) {
                            max = value;
                        }
                    }
                    if (min == float.MaxValue) {
                        min = 0;
                    }
                    if (max == float.MinValue) {
                        max = 0;
                    }
                    DrawPeak(
                        bitmapData,
                        cacheWidth,
                        x,
                        (int)Math.Round((0.5f + min * snapEase * 0.5f) * cacheHeight),
                        (int)Math.Round((0.5f + max * snapEase * 0.5f) * cacheHeight),
                        cacheHeight);
                }
                startSample = endSample;
            }

            using var frameBuffer = bitmap!.Lock();
            Marshal.Copy(bitmapData, 0, frameBuffer.Address, bitmapData.Length);
        }

        private void EnsureBitmap(int width, int height) {
            if (bitmap != null && bitmap.PixelSize.Width == width && bitmap.PixelSize.Height == height) {
                return;
            }
            bitmap?.Dispose();
            var size = new PixelSize(width, height);
            bitmap = new WriteableBitmap(
                size,
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgba8888,
                Avalonia.Platform.AlphaFormat.Unpremul);
            bitmapData = new int[width * height];
            Log.Information($"Created waveform cache bitmap {size}");
        }

        private static void DrawPeak(int[] data, int width, int x, int y1, int y2, int height) {
            y1 = Math.Clamp(y1, 0, height - 1);
            y2 = Math.Clamp(y2, 0, height - 1);
            if (y1 > y2) {
                (y1, y2) = (y2, y1);
            }
            for (int y = y1; y <= y2; y++) {
                data[x + width * y] = WaveformArgb;
            }
        }
    }
}
