using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkSyllable {
        public CoeiroinkMora Mora;
        public double StartMs;
        public double EndMs;
        public int Tone;
        public double? ConsonantMs;
        public bool PauseBefore;
    }

    public enum CoeiroinkTokenRole {
        Start,
        Boundary,
        Pause,
        Consonant,
        Vowel,
        Marker,
        End,
    }

    public class CoeiroinkTokenPlan {
        public readonly List<string> Tokens = new List<string>();
        public readonly List<CoeiroinkTokenRole> Roles = new List<CoeiroinkTokenRole>();
        public readonly List<int> MoraIndexes = new List<int>();

        public void Add(string token, CoeiroinkTokenRole role, int mora) {
            Tokens.Add(token);
            Roles.Add(role);
            MoraIndexes.Add(mora);
        }
    }

    public static class CoeiroinkTimeline {
        public const string StartToken = "^";
        public const string EndToken = "$";
        public const string PauseToken = "_";
        public const string BoundaryToken = "#";
        public const string FallToken = "]";

        public const double PauseThresholdMs = 50;
        public const int MaxPhraseMoras = 8;
        public const int MaxConsonantFrames = 14;
        public const int MaxMarkerFrames = 3;
        public const int TailFrames = 9;

        public static CoeiroinkTokenPlan BuildPlan(IReadOnlyList<CoeiroinkSyllable> syllables) {
            var plan = new CoeiroinkTokenPlan();
            plan.Add(StartToken, CoeiroinkTokenRole.Start, -1);
            var markers = BuildMarkers(syllables);
            for (int k = 0; k < syllables.Count; k++) {
                var mora = syllables[k].Mora;
                if (k > 0) {
                    if (IsPause(syllables, k)) {
                        plan.Add(PauseToken, CoeiroinkTokenRole.Pause, k);
                    } else if (markers.PhraseStarts.Contains(k)) {
                        plan.Add(BoundaryToken, CoeiroinkTokenRole.Boundary, k);
                    }
                }
                if (mora.Consonant != null) {
                    plan.Add(mora.Consonant, CoeiroinkTokenRole.Consonant, k);
                }
                plan.Add(mora.Vowel, CoeiroinkTokenRole.Vowel, k);
                foreach (var marker in markers.After[k]) {
                    plan.Add(marker, CoeiroinkTokenRole.Marker, k);
                }
            }
            plan.Add(EndToken, CoeiroinkTokenRole.End, syllables.Count);
            return plan;
        }

        static bool IsPause(IReadOnlyList<CoeiroinkSyllable> syllables, int k) {
            return syllables[k].PauseBefore || syllables[k].StartMs - syllables[k - 1].EndMs >= PauseThresholdMs;
        }

        class Markers {
            public readonly HashSet<int> PhraseStarts = new HashSet<int>();
            public List<string>[] After = Array.Empty<List<string>>();
        }

        static Markers BuildMarkers(IReadOnlyList<CoeiroinkSyllable> syllables) {
            var markers = new Markers { After = Enumerable.Range(0, syllables.Count).Select(_ => new List<string>()).ToArray() };
            int start = 0;
            for (int k = 1; k <= syllables.Count; k++) {
                bool end = k == syllables.Count;
                bool pause = !end && IsPause(syllables, k);
                bool forced = !end && k - start >= MaxPhraseMoras;
                if (end || pause || forced) {
                    AddAccent(syllables, start, k, markers);
                    if (!end) {
                        markers.PhraseStarts.Add(k);
                    }
                    start = k;
                }
            }
            return markers;
        }

        static void AddAccent(IReadOnlyList<CoeiroinkSyllable> syllables, int start, int end, Markers markers) {
            int count = end - start;
            if (count < 2) {
                return;
            }
            var tones = Enumerable.Range(start, count).Select(i => syllables[i].Tone).ToArray();
            int max = tones.Max();
            int min = tones.Min();
            if (max == min) {
                return;
            }
            int high = Array.IndexOf(tones, max);
            int nucleus = high;
            while (nucleus + 1 < count && tones[nucleus + 1] >= max - 1) {
                nucleus++;
            }
            if (nucleus < count - 1) {
                markers.After[start + nucleus].Add(FallToken);
            }
        }

        public static int[] Allocate(
            CoeiroinkTokenPlan plan,
            IReadOnlyList<CoeiroinkSyllable> syllables,
            IReadOnlyList<float> predicted,
            double startMs,
            double frameMs) {
            int count = syllables.Count;
            var anchors = new int[count];
            var ends = new int[count];
            for (int k = 0; k < count; k++) {
                int minAnchor = k == 0 ? 2 : anchors[k - 1] + 3;
                anchors[k] = Math.Max(minAnchor, (int)Math.Round((syllables[k].StartMs - startMs) / frameMs));
                ends[k] = Math.Max(anchors[k] + 2, (int)Math.Round((syllables[k].EndMs - startMs) / frameMs));
            }

            var consonant = new int[count];
            var boundary = new int[count];
            var markerFrames = new int[count];
            var pause = new bool[count];
            for (int i = 0; i < plan.Tokens.Count; i++) {
                int k = plan.MoraIndexes[i];
                int frames = Math.Max(1, (int)Math.Ceiling(predicted[i]));
                switch (plan.Roles[i]) {
                    case CoeiroinkTokenRole.Consonant: {
                        int available = k == 0 ? anchors[0] - 1 : (anchors[k] - anchors[k - 1]) / 2;
                        if (syllables[k].ConsonantMs.HasValue) {
                            frames = Math.Max(1, (int)Math.Round(syllables[k].ConsonantMs.Value / frameMs));
                        }
                        consonant[k] = Math.Clamp(frames, 1, Math.Max(1, Math.Min(MaxConsonantFrames, available)));
                        break;
                    }
                    case CoeiroinkTokenRole.Boundary:
                        boundary[k] = Math.Min(frames, MaxMarkerFrames);
                        break;
                    case CoeiroinkTokenRole.Pause:
                        pause[k] = true;
                        break;
                    case CoeiroinkTokenRole.Marker:
                        markerFrames[k] += Math.Min(frames, MaxMarkerFrames);
                        break;
                }
            }

            int PreStart(int k) => anchors[k] - consonant[k] - boundary[k];

            var result = new int[plan.Tokens.Count];
            for (int i = 0; i < plan.Tokens.Count; i++) {
                int k = plan.MoraIndexes[i];
                switch (plan.Roles[i]) {
                    case CoeiroinkTokenRole.Start:
                        result[i] = Math.Max(1, PreStart(0));
                        break;
                    case CoeiroinkTokenRole.Boundary:
                        result[i] = boundary[k];
                        break;
                    case CoeiroinkTokenRole.Pause: {
                        int vowelEnd = VowelEnd(k - 1);
                        result[i] = Math.Max(1, PreStart(k) - vowelEnd);
                        break;
                    }
                    case CoeiroinkTokenRole.Consonant:
                        result[i] = consonant[k];
                        break;
                    case CoeiroinkTokenRole.Vowel: {
                        int span = VowelEnd(k) - anchors[k] - markerFrames[k];
                        result[i] = Math.Max(1, span);
                        break;
                    }
                    case CoeiroinkTokenRole.Marker:
                        result[i] = Math.Max(1, Math.Min(MaxMarkerFrames, (int)Math.Ceiling(predicted[i])));
                        break;
                    case CoeiroinkTokenRole.End:
                        result[i] = TailFrames;
                        break;
                }
            }
            return result;

            int VowelEnd(int k) {
                if (k == count - 1) {
                    return ends[k];
                }
                int limit = PreStart(k + 1);
                if (pause[k + 1]) {
                    return Math.Min(ends[k], limit - 1);
                }
                return limit;
            }
        }
    }
}
