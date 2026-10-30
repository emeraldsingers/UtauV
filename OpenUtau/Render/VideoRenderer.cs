using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenUtau.App.Controls;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI;
using Serilog;

namespace OpenUtau.App.Render {
    public static class VideoRenderer {
        public static bool IsRenderingVideo { get; private set; }

        static CancellationTokenSource? cancellation;

        public static void Cancel() {
            cancellation?.Cancel();
        }

        static string? ResolveFfmpeg() {
            string ffmpeg = Preferences.Default.FfmpegPath;
            if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg)) {
                ffmpeg = OS.WhereIs(OS.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            }
            return string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg) ? null : ffmpeg;
        }

        static Process StartFfmpeg(string ffmpeg, string arguments, bool redirectInput, Queue<string> errorTail) {
            var info = new ProcessStartInfo {
                FileName = ffmpeg,
                Arguments = arguments,
                RedirectStandardInput = redirectInput,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = Process.Start(info) ?? throw new InvalidOperationException("Failed to start ffmpeg");
            process.ErrorDataReceived += (_, e) => {
                if (string.IsNullOrEmpty(e.Data)) {
                    return;
                }
                lock (errorTail) {
                    errorTail.Enqueue(e.Data);
                    while (errorTail.Count > 20) {
                        errorTail.Dequeue();
                    }
                }
            };
            process.BeginErrorReadLine();
            return process;
        }

        static string Tail(Queue<string> errorTail) {
            lock (errorTail) {
                return string.Join(Environment.NewLine, errorTail.TakeLast(6));
            }
        }

        public static async Task RenderVideoAsync(PianoRoll pianoRoll, string exportPath, int fps, double scale) {
            var project = DocManager.Inst.Project;
            if (project == null || IsRenderingVideo) {
                return;
            }
            string? ffmpeg = ResolveFfmpeg();
            if (ffmpeg == null) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification("ffmpeg not found. Please set its path in Preferences or add it to system PATH."));
                return;
            }
            var canvas = pianoRoll.GetVisualDescendants().OfType<NotesCanvas>().FirstOrDefault();
            int width = (int)(pianoRoll.Bounds.Width * scale) & ~1;
            int height = (int)(pianoRoll.Bounds.Height * scale) & ~1;
            if (width < 2 || height < 2) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification("Open the Piano Roll before exporting video."));
                return;
            }

            string cacheDir = PathManager.Inst.CachePath;
            string tempWav = Path.Combine(cacheDir, "video_temp.wav");
            string tempVideo = Path.Combine(cacheDir, "video_temp.mp4");
            var errorTail = new Queue<string>();
            Process? encoder = null;
            VideoExportOverlay? overlay = null;
            var stopwatch = Stopwatch.StartNew();
            var originalBackground = pianoRoll.Background;

            try {
                DeleteQuietly(tempWav);
                DeleteQuietly(tempVideo);
                overlay = new VideoExportOverlay(pianoRoll,
                    ThemeManager.GetString("videoexport.exporting"),
                    ThemeManager.GetString("videoexport.cancelhint"));
                overlay.SetStatus("...");

                var codecArgs = await VideoEncoder.SelectAsync(ffmpeg);
                Log.Information($"Video export {width}x{height} {fps}fps: {codecArgs}");
                var audioTask = PlaybackManager.Inst.RenderMixdown(project, tempWav);

                encoder = StartFfmpeg(ffmpeg,
                    $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -framerate {fps} -i - -an {codecArgs} -pix_fmt yuv420p -sws_flags fast_bilinear \"{tempVideo}\"",
                    true, errorTail);

                double durationMs = project.timeAxis.TickPosToMsPos(project.EndTick);
                int totalFrames = Math.Max(1, (int)Math.Ceiling(durationMs / 1000.0 * fps));
                var step = TimeSpan.FromSeconds(1.0 / fps);

                if (pianoRoll.TryFindResource("SystemRegionBrush", pianoRoll.ActualThemeVariant, out var resource) && resource is IBrush brush) {
                    pianoRoll.Background = brush;
                } else if (pianoRoll.Background == null) {
                    pianoRoll.Background = Brushes.DarkGray;
                }

                cancellation = new CancellationTokenSource();
                IsRenderingVideo = true;
                PlaybackClock.Begin();
                var restoreShadows = BakeInsetShadows(pianoRoll, scale);
                RefreshPlaybackState(pianoRoll);

                int framesDone = 0;
                long layoutTicks = 0;
                long renderTicks = 0;
                long copyTicks = 0;
                long stateTicks = 0;
                try {
                    using var target = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
                    int stride = 4 * width;
                    int bufferSize = stride * height;
                    var buffers = new[] { new byte[bufferSize], new byte[bufferSize] };
                    var stream = encoder.StandardInput.BaseStream;
                    Task pendingWrite = Task.CompletedTask;
                    var clock = new Stopwatch();

                    for (int i = 0; i < totalFrames && !cancellation.IsCancellationRequested; i++) {
                        clock.Restart();
                        int tick = project.timeAxis.MsPosToTickPos(i * 1000.0 / fps);
                        PlaybackClock.Advance(step);
                        DocManager.Inst.ExecuteCmd(new SetPlayPosTickNotification(tick, false, false));
                        canvas?.StepPlaybackAnimation();
                        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

                        stateTicks += clock.ElapsedTicks;
                        clock.Restart();
                        pianoRoll.UpdateLayout();
                        layoutTicks += clock.ElapsedTicks;
                        clock.Restart();
                        target.Render(pianoRoll);
                        renderTicks += clock.ElapsedTicks;
                        clock.Restart();
                        var buffer = buffers[i & 1];
                        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        try {
                            target.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), bufferSize, stride);
                        } finally {
                            handle.Free();
                        }
                        copyTicks += clock.ElapsedTicks;

                        await pendingWrite;
                        pendingWrite = stream.WriteAsync(buffer, 0, bufferSize);
                        framesDone = i + 1;

                        if (i % 15 == 0) {
                            double elapsed = stopwatch.Elapsed.TotalSeconds;
                            double eta = elapsed / framesDone * (totalFrames - framesDone);
                            overlay.SetStatus($"{framesDone} / {totalFrames}  ({framesDone * 100.0 / totalFrames:0}%)  ~{TimeSpan.FromSeconds(eta):mm\\:ss}");
                            DocManager.Inst.ExecuteCmd(new ProgressBarNotification(framesDone * 100.0 / totalFrames, "Rendering video..."));
                        }
                    }
                    await pendingWrite;
                    double perFrame = 1000.0 / Stopwatch.Frequency / Math.Max(1, framesDone);
                    Log.Information($"Video frames: {framesDone} in {stopwatch.Elapsed.TotalSeconds:0.0}s; per frame ms: state {stateTicks * perFrame:0.0}, layout {layoutTicks * perFrame:0.0}, render {renderTicks * perFrame:0.0}, copy {copyTicks * perFrame:0.0}");
                } finally {
                    IsRenderingVideo = false;
                    PlaybackClock.End();
                    restoreShadows();
                    RefreshPlaybackState(pianoRoll);
                    pianoRoll.Background = originalBackground;
                }

                bool cancelled = cancellation.IsCancellationRequested;
                encoder.StandardInput.Close();
                await encoder.WaitForExitAsync();
                if (encoder.ExitCode != 0 || !File.Exists(tempVideo)) {
                    throw new InvalidOperationException($"ffmpeg video encode failed:{Environment.NewLine}{Tail(errorTail)}");
                }

                overlay.SetStatus("Audio...");
                await audioTask;
                await Mux(ffmpeg, tempVideo, tempWav, exportPath, errorTail);

                DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0,
                    cancelled ? "Video export cancelled." : $"Video exported to {exportPath} in {stopwatch.Elapsed:mm\\:ss}."));
            } catch (Exception ex) {
                Log.Error(ex, "Failed to render video.");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification($"Video export failed: {ex.Message}"));
            } finally {
                IsRenderingVideo = false;
                PlaybackClock.End();
                if (encoder != null && !encoder.HasExited) {
                    encoder.Kill(true);
                }
                encoder?.Dispose();
                overlay?.Dispose();
                cancellation?.Dispose();
                cancellation = null;
                DeleteQuietly(tempWav);
                DeleteQuietly(tempVideo);
            }
        }

        static async Task Mux(string ffmpeg, string video, string wav, string output, Queue<string> errorTail) {
            string audioArgs = File.Exists(wav) ? $"-i \"{wav}\" -c:v copy -c:a aac -b:a 192k -shortest" : "-c:v copy -an";
            using var process = StartFfmpeg(ffmpeg, $"-y -i \"{video}\" {audioArgs} -movflags +faststart \"{output}\"", false, errorTail);
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) {
                throw new InvalidOperationException($"ffmpeg mux failed:{Environment.NewLine}{Tail(errorTail)}");
            }
        }

        static bool AllInset(BoxShadows shadows) {
            foreach (var shadow in shadows) {
                if (!shadow.IsInset) {
                    return false;
                }
            }
            return true;
        }

        static Action BakeInsetShadows(PianoRoll root, double scale) {
            var restores = new List<Action>();
            double minArea = root.Bounds.Width * root.Bounds.Height * 0.0003;
            foreach (var border in root.GetVisualDescendants().OfType<Border>().ToList()) {
                var shadows = border.BoxShadow;
                if (shadows.Count == 0 || !border.IsVisible || !AllInset(shadows)
                    || border.Bounds.Width * border.Bounds.Height < minArea) {
                    continue;
                }
                double w = border.Bounds.Width;
                double h = border.Bounds.Height;
                var clone = new Border {
                    Width = w,
                    Height = h,
                    Background = border.Background,
                    BorderBrush = border.BorderBrush,
                    BorderThickness = border.BorderThickness,
                    CornerRadius = border.CornerRadius,
                    BoxShadow = shadows,
                };
                clone.Measure(new Size(w, h));
                clone.Arrange(new Rect(0, 0, w, h));
                var baked = new RenderTargetBitmap(
                    new PixelSize(Math.Max(1, (int)Math.Ceiling(w * scale)), Math.Max(1, (int)Math.Ceiling(h * scale))),
                    new Vector(96 * scale, 96 * scale));
                baked.Render(clone);
                var originalBackground = border.Background;
                border.Background = new ImageBrush(baked) { Stretch = Stretch.Fill };
                border.BoxShadow = default;
                restores.Add(() => {
                    border.Background = originalBackground;
                    border.BoxShadow = shadows;
                    baked.Dispose();
                });
            }
            return () => restores.ForEach(restore => restore());
        }

        static void RefreshPlaybackState(PianoRoll pianoRoll) {
            var playback = pianoRoll.ViewModel?.PlaybackViewModel;
            if (playback == null) {
                return;
            }
            playback.RaisePropertyChanged(nameof(playback.IsPlaying));
            playback.RaisePropertyChanged(nameof(playback.ShowPlayPosHighlight));
        }

        static void DeleteQuietly(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            } catch (IOException) {
            }
        }
    }
}
