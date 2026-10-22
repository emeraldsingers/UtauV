using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Controls {
    /// <summary>
    /// The rendered audio of the open part, drawn behind the notes as a min/max
    /// envelope with one column per device pixel.
    ///
    /// Columns sit on a fixed grid in song time (column k covers ticks
    /// [TickOrigin + k / p, TickOrigin + (k + 1) / p] for p device pixels per
    /// tick), so scrolling by a fraction of a pixel does not re-bin the samples
    /// and make the peaks shimmer. The envelope is built for a range wider than
    /// the view and kept as geometry; scrolling only moves it, by whole device
    /// pixels so it stays as crisp as the bitmap it replaces, and it is rebuilt on
    /// zoom, on newly rendered audio, or when the view leaves the range.
    /// </summary>
    class WaveformImage : Control {
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
            get { return tickOffset; }
            set { SetAndRaise(TickOffsetProperty, ref tickOffset, value); }
        }
        public bool ShowWaveform {
            get { return showWaveform; }
            set { SetAndRaise(ShowWaveformProperty, ref showWaveform, value); }
        }

        private const int SampleRate = 44100;
        private const int Channels = 2;
        private static readonly IBrush Fill = new SolidColorBrush(Color.FromArgb(0x7F, 0x7F, 0x7F, 0x7F));

        private double tickWidth;
        private double tickOffset;
        private bool showWaveform;

        // The cached envelope covers columns [cacheStart, cacheEnd) and was built
        // for these inputs. It is in device pixels; x = 0 is column cacheStart.
        private StreamGeometry? geometry;
        private int cacheStart;
        private int cacheEnd;
        private UPart? cachePart;
        private int cacheTickOrigin;
        private double cacheTickWidth;
        private double cacheHeight;
        private double cacheScale;
        private bool cacheValid;
        private float[] sampleData = new float[0];

        public WaveformImage() {
            // The projection payload is not read here; deliveries mean new audio.
            OpenUtau.Core.Render.RenderView.Inst.Observe(_ => {
                cacheValid = false;
                InvalidateVisual();
            });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == DataContextProperty ||
                change.Property == TickWidthProperty ||
                change.Property == TickOffsetProperty ||
                change.Property == ShowWaveformProperty ||
                change.Property == BoundsProperty) {
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext context) {
            base.Render(context);
            if (DataContext is not NotesViewModel viewModel || double.IsNaN(viewModel.TickOffset) ||
                !ShowWaveform || viewModel.TickWidth <= ViewConstants.PianoRollTickWidthShowDetails) {
                return;
            }
            var project = viewModel.Project;
            var part = viewModel.Part;
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            // Everything below is in device pixels.
            int width = (int)Math.Ceiling(Bounds.Width * scale);
            double height = Math.Round(Bounds.Height * scale);
            if (project == null || part == null || width <= 0 || height <= 0) {
                return;
            }
            double offsetPx = viewModel.TickOffset * viewModel.TickWidth * scale;
            int firstColumn = (int)Math.Floor(offsetPx);
            if (viewModel.TickWidth != cacheTickWidth || scale != cacheScale) {
                // Zooming rebuilds every frame, so build only what is visible; the
                // first scroll afterwards builds the margins.
                Build(viewModel, project, part, scale, firstColumn, firstColumn + width + 1, height);
            } else if (!cacheValid || geometry == null || part != cachePart ||
                viewModel.TickOrigin != cacheTickOrigin || height != cacheHeight ||
                firstColumn < cacheStart || firstColumn + width + 1 > cacheEnd) {
                // Half a view of margin on each side, so scrolling rarely rebuilds.
                Build(viewModel, project, part, scale, firstColumn - width / 2, firstColumn + width + width / 2 + 1, height);
            }
            if (geometry != null) {
                // Shift by whole device pixels, then map device pixels to the control's units.
                var transform = Matrix.CreateTranslation(Math.Round(cacheStart - offsetPx), 0) * Matrix.CreateScale(1 / scale, 1 / scale);
                using (context.PushTransform(transform)) {
                    context.DrawGeometry(Fill, null, geometry);
                }
            }
        }

        private void Build(NotesViewModel viewModel, UProject project, UPart part, double scale, int start, int end, double height) {
            cachePart = part;
            cacheTickOrigin = viewModel.TickOrigin;
            cacheTickWidth = viewModel.TickWidth;
            cacheScale = scale;
            cacheHeight = height;
            cacheStart = start;
            cacheEnd = end;
            cacheValid = true;
            geometry = null;

            int columns = end - start;
            // Song time of each column's left edge; edges[columns] is the right edge of the last one.
            double pixelsPerTick = viewModel.TickWidth * scale;
            var edges = new double[columns + 1];
            for (int i = 0; i <= columns; ++i) {
                edges[i] = project.timeAxis.TickPosToMsPos(viewModel.TickOrigin + (start + i) / pixelsPerTick);
            }
            int firstSample = Math.Max(0, SampleIndex(edges[0]));
            int sampleCount = Math.Max(0, SampleIndex(edges[columns]) - firstSample);
            if (sampleCount == 0) {
                return;
            }
            if (sampleData.Length < sampleCount) {
                sampleData = new float[sampleCount];
            }
            Array.Clear(sampleData, 0, sampleCount);

            var projection = OpenUtau.Core.Render.RenderView.Inst.Current(part);
            var phraseView = new (ulong hash, double startMs, double endMs)[projection.Phrases.Count];
            for (int p = 0; p < projection.Phrases.Count; ++p) {
                var view = projection.Phrases[p];
                phraseView[p] = (view.Hash, view.Layout.StartMs, view.Layout.EndMs);
            }
            // Only phrases whose pcm has rendered appear, so a part still
            // rendering draws only what has finished.
            var planner = OpenUtau.Core.PlaybackManager.Inst.MixPlanner;
            if (!MixPlanner.TryGetPartPlacements(planner, part, phraseView, out var pcmList)) {
                return;
            }
            var slots = new OpenUtau.Core.SignalChain.SampleSlot[pcmList.Count];
            for (int i = 0; i < pcmList.Count; ++i) {
                var p = pcmList[i];
                slots[i] = new OpenUtau.Core.SignalChain.SampleSlot(
                    p.posMs, p.durMs, 0, p.channels, p.pcm,
                    OpenUtau.Core.SignalChain.SlotState.Ready);
            }
            var source = new OpenUtau.Core.SignalChain.SlotMixSource();
            source.SetSlots(slots);
            source.Mix(firstSample, sampleData, 0, sampleCount);

            // Top and bottom of each column in pixels, NaN where no phrase has
            // audio, so those ranges are left blank instead of drawing a
            // zero-volume line. Silence inside a phrase still draws.
            var top = new double[columns];
            var bottom = new double[columns];
            float lastValue = 0;
            for (int i = 0; i < columns; ++i) {
                double fromMs = edges[i], toMs = edges[i + 1];
                bool covered = false;
                foreach (var phrase in phraseView) {
                    if (phrase.endMs > fromMs && phrase.startMs < toMs) {
                        covered = true;
                        break;
                    }
                }
                int s0 = Math.Clamp(SampleIndex(fromMs) - firstSample, 0, sampleCount);
                int s1 = Math.Clamp(SampleIndex(toMs) - firstSample, 0, sampleCount);
                if (!covered || fromMs < 0) {
                    top[i] = bottom[i] = double.NaN;
                    if (s1 > 0) {
                        lastValue = sampleData[s1 - 1];
                    }
                    continue;
                }
                float min, max;
                if (s1 > s0) {
                    min = float.MaxValue;
                    max = float.MinValue;
                    for (int s = s0; s < s1; ++s) {
                        float v = sampleData[s];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                    lastValue = sampleData[s1 - 1];
                } else {
                    // Zoomed in past one sample per column: hold the last sample.
                    min = max = lastValue;
                }
                // Whole pixel rows, at least one, so edges stay sharp and quiet
                // audio still shows a line.
                double yTop = Math.Clamp(Math.Round((0.5 - max * 0.5) * height), 0, height - 1);
                double yBottom = Math.Clamp(Math.Round((0.5 - min * 0.5) * height), yTop + 1, height);
                top[i] = yTop;
                bottom[i] = yBottom;
            }

            // One filled figure per run of covered columns: along the tops, then
            // back along the bottoms. Each column spans [i, i + 1).
            var g = new StreamGeometry();
            using (var ctx = g.Open()) {
                int i = 0;
                while (i < columns) {
                    if (double.IsNaN(top[i])) {
                        ++i;
                        continue;
                    }
                    int runStart = i;
                    while (i < columns && !double.IsNaN(top[i])) {
                        ++i;
                    }
                    ctx.BeginFigure(new Point(runStart, top[runStart]), true);
                    for (int k = runStart; k < i; ++k) {
                        ctx.LineTo(new Point(k, top[k]));
                        ctx.LineTo(new Point(k + 1, top[k]));
                    }
                    for (int k = i - 1; k >= runStart; --k) {
                        ctx.LineTo(new Point(k + 1, bottom[k]));
                        ctx.LineTo(new Point(k, bottom[k]));
                    }
                    ctx.EndFigure(true);
                }
            }
            geometry = g;
        }

        // Index of the interleaved sample at a song time, as the mix lays them out.
        private static int SampleIndex(double ms) {
            return (int)(ms * SampleRate / 1000) * Channels;
        }
    }
}
