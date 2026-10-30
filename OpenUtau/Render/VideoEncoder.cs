using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenUtau.App.Render {
    public static class VideoEncoder {
        const string Software = "-c:v libx264 -preset ultrafast -crf 20 -threads 0";

        static readonly string[] Hardware = {
            "-c:v h264_nvenc -preset p1 -tune ll -rc vbr -cq 21 -b:v 0",
            "-c:v h264_qsv -preset veryfast -global_quality 21",
            "-c:v h264_amf -quality speed -rc cqp -qp_i 21 -qp_p 21",
        };

        static readonly ConcurrentDictionary<string, string> cache = new();

        public static async Task<string> SelectAsync(string ffmpeg) {
            if (cache.TryGetValue(ffmpeg, out var cached)) {
                return cached;
            }
            var chosen = Software;
            foreach (var args in Hardware) {
                if (await ProbeAsync(ffmpeg, args)) {
                    chosen = args;
                    break;
                }
            }
            cache[ffmpeg] = chosen;
            return chosen;
        }

        public static string SoftwareArgs => Software;

        static async Task<bool> ProbeAsync(string ffmpeg, string codecArgs) {
            try {
                var info = new ProcessStartInfo {
                    FileName = ffmpeg,
                    Arguments = $"-hide_banner -loglevel error -f lavfi -i color=c=black:s=320x240:r=30:d=0.2 -frames:v 4 {codecArgs} -pix_fmt yuv420p -f null -",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(info);
                if (process == null) {
                    return false;
                }
                var exited = process.WaitForExitAsync();
                if (await Task.WhenAny(exited, Task.Delay(8000)) != exited) {
                    process.Kill(true);
                    return false;
                }
                return process.ExitCode == 0;
            } catch (Exception) {
                return false;
            }
        }
    }
}
