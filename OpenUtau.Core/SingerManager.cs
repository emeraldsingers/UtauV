using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Core {
    public class SingerManager : SingletonBase<SingerManager> {
        public Dictionary<string, USinger> Singers { get; private set; } = new Dictionary<string, USinger>();
        public Dictionary<USingerType, List<USinger>> SingerGroups { get; private set; } = new Dictionary<USingerType, List<USinger>>();

        private readonly ConcurrentQueue<USinger> reloadQueue = new ConcurrentQueue<USinger>();
        private CancellationTokenSource reloadCancellation;

        private HashSet<USinger> singersUsed = new HashSet<USinger>(ReferenceEqualityComparer.Instance);
        private readonly ConditionalWeakTable<USinger, USinger> replacements = new ConditionalWeakTable<USinger, USinger>();

        public void Initialize() {
            SearchAllSingers();
        }

        public void SearchAllSingers() {
            Log.Information("Searching singers.");
            Directory.CreateDirectory(PathManager.Inst.SingersPath);
            var stopWatch = Stopwatch.StartNew();
            int reused = 0, loaded = 0;
            var byLocation = new Dictionary<string, USinger>(StringComparer.OrdinalIgnoreCase);
            foreach (var singer in Singers.Values) {
                if (!string.IsNullOrEmpty(singer.Location) && !byLocation.ContainsKey(singer.Location)) {
                    byLocation[singer.Location] = singer;
                }
            }

            var merged = new Dictionary<string, USinger>();

            void Consider(USinger? singer) {
                if (singer == null) {
                    return;
                }
                if (merged.ContainsKey(singer.Id)) {
                    return;
                }
                if (Singers.TryGetValue(singer.Id, out var previous) && !ReferenceEquals(previous, singer)) {
                    MarkReplaced(previous, singer);
                }
                merged[singer.Id] = singer;
            }

            bool TryReuse(string location, out USinger singer) {
                singer = null!;
                if (string.IsNullOrEmpty(location) || !byLocation.TryGetValue(location, out var existing)) {
                    return false;
                }
                if (!existing.Found || string.IsNullOrEmpty(existing.Fingerprint)) {
                    return false;
                }
                var fingerprint = VoicebankFingerprint.Compute(location);
                if (string.IsNullOrEmpty(fingerprint) ||
                    !string.Equals(fingerprint, existing.Fingerprint, StringComparison.Ordinal)) {
                    return false;
                }
                singer = existing;
                return true;
            }

            foreach (var (basePath, characterFile) in ClassicSingerLoader.FindAllSingerFiles()) {
                try {
                    var location = Path.GetDirectoryName(characterFile);
                    if (TryReuse(location!, out var existing)) {
                        Consider(existing);
                        reused++;
                        continue;
                    }
                    var singer = ClassicSingerLoader.LoadSinger(basePath, characterFile);
                    singer.Fingerprint = VoicebankFingerprint.Compute(location) ?? string.Empty;
                    Consider(singer);
                    loaded++;
                } catch (Exception e) {
                    Log.Error(e, $"Failed to load {characterFile} info.");
                }
            }
            foreach (var vogenFile in Vogen.VogenSingerLoader.FindAllSingerFiles()) {
                try {
                    if (TryReuse(vogenFile, out var existing)) {
                        Consider(existing);
                        reused++;
                        continue;
                    }
                    var singer = Vogen.VogenSingerLoader.LoadSingerAt(vogenFile);
                    singer.Fingerprint = VoicebankFingerprint.Compute(vogenFile) ?? string.Empty;
                    Consider(singer);
                    loaded++;
                } catch (Exception e) {
                    Log.Error(e, $"Failed to load Vogen singer {vogenFile}.");
                }
            }

            Singers = merged;
            SingerGroups = merged.Values
                .GroupBy(s => s.SingerType)
                .ToDictionary(s => s.Key, s => s.LocalizedOrderBy(singer => singer.LocalizedName).ToList());
            stopWatch.Stop();
            Log.Information($"Search all singers: {stopWatch.Elapsed} ({reused} unchanged, {loaded} loaded)");
        }
        void MarkReplaced(USinger existing, USinger candidate) {
            if (!existing.Found || !candidate.Found ||
                string.IsNullOrEmpty(existing.Fingerprint) || string.IsNullOrEmpty(candidate.Fingerprint)) {
                return;
            }
            replacements.Remove(existing);
            replacements.Add(existing, candidate);
        }

        public bool IsOutdated(USinger singer) {
            return singer != null && singer.Found &&
                replacements.TryGetValue(singer, out var latest) && latest != null && latest.Found;
        }

        public USinger GetReplacement(USinger singer) {
            return singer != null && replacements.TryGetValue(singer, out var latest) ? latest : null;
        }

        public USinger GetSinger(string name) {
            Log.Information($"Attach singer to track: {name}");
            name = name.Replace("%VOICE%", "");
            if (Singers.ContainsKey(name)) {
                return Singers[name];
            }
            return null;
        }

        public void ScheduleReload(USinger singer) {
            reloadQueue.Enqueue(singer);
            ScheduleReload();
        }

        private void ScheduleReload() {
            var newCancellation = new CancellationTokenSource();
            var oldCancellation = Interlocked.Exchange(ref reloadCancellation, newCancellation);
            if (oldCancellation != null) {
                oldCancellation.Cancel();
                oldCancellation.Dispose();
            }
            Task.Run(() => {
                Thread.Sleep(200);
                if (newCancellation.IsCancellationRequested) {
                    return;
                }
                Refresh();
            });
        }

        private void Refresh() {
            var singers = new HashSet<USinger>(ReferenceEqualityComparer.Instance);
            while (reloadQueue.TryDequeue(out USinger singer)) {
                singers.Add(singer);
            }
            foreach (var singer in singers) {
                Log.Information($"Reloading {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloading {singer.Id}"));
                }).Start(DocManager.Inst.MainScheduler);
                int retries = 5;
                while (retries > 0) {
                    retries--;
                    try {
                        singer.Reload();
                        break;
                    } catch (Exception e) {
                        if (retries == 0) {
                            Log.Error(e, $"Failed to reload {singer.Id}");
                        } else {
                            Log.Error(e, $"Retrying reload {singer.Id}");
                            Thread.Sleep(200);
                        }
                    }
                }
                Log.Information($"Reloaded {singer.Id}");
                new Task(() => {
                    DocManager.Inst.ExecuteCmd(new ProgressBarNotification(0, $"Reloaded {singer.Id}"));
                    DocManager.Inst.ExecuteCmd(new OtoChangedNotification(external: true));
                }).Start(DocManager.Inst.MainScheduler);
            }
        }

        //Check which singers are in use and free memory for those that are not
        public void ReleaseSingersNotInUse(UProject project) {
            //Check which singers are in use
            var singersInUse = new HashSet<USinger>(ReferenceEqualityComparer.Instance);
            foreach (var track in project.tracks) {
                var singer = track.Singer;
                if (singer != null && singer.Found) {
                    singersInUse.Add(singer);
                }
            }
            //Release singers that are no longer in use
            foreach (var singer in singersUsed) {
                if (!singersInUse.Contains(singer)) {
                    Log.Information($"Releasing memory for singer not in use: {singer.Id} ({RuntimeHelpers.GetHashCode(singer)})");
                    singer.FreeMemory();
                }
            }
            //Update singers used
            singersUsed = singersInUse;
        }
    }
}
