using System.Collections.Generic;
using System.Text.RegularExpressions;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    public static class LipSync {
        public static readonly string[] Visemes = { "a", "e", "i", "o", "u", "n" };
        public const int BlendMs = 80;

        public static int LeadTick(UProject project, int absTick) {
            var axis = project.timeAxis;
            return axis.MsPosToTickPos(axis.TickPosToMsPos(absTick) + BlendMs);
        }

        static readonly HashSet<string> Closed = new HashSet<string> { "N", "M", "B", "P", "m", "b", "p", "mm", "bb", "pp" };
        static readonly HashSet<string> Narrow = new HashSet<string> { "s", "sh", "ch", "ss", "y", "yy" };
        static readonly HashSet<string> Round = new HashSet<string> { "w", "ww" };

        static readonly Regex langPrefix = new Regex("^[a-z]{2}[/_]", RegexOptions.Compiled);
        static readonly Regex toneDigits = new Regex("[0-9]+$", RegexOptions.Compiled);

        static string Base(string? sym) {
            var s = sym ?? string.Empty;
            s = langPrefix.Replace(s, string.Empty);
            s = toneDigits.Replace(s, string.Empty);
            return s;
        }

        public static string? VisemeOf(string? sym) {
            var b = Base(sym);
            if (string.IsNullOrEmpty(b)) {
                return null;
            }
            if (Closed.Contains(b)) {
                return "n";
            }
            var l = b.ToLowerInvariant();
            if (l == "sp" || l == "ap" || l == "cl" || l == "pau") {
                return "n";
            }
            if (Narrow.Contains(l)) {
                return "i";
            }
            if (Round.Contains(l)) {
                return "u";
            }
            char c = l[0];
            if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') {
                return c.ToString();
            }
            return null;
        }

        public static string? VisemeAt(IReadOnlyList<UPhoneme> phonemes, int relTick) {
            UPhoneme? cur = null;
            for (int i = 0; i < phonemes.Count; i++) {
                var ph = phonemes[i];
                if (relTick >= ph.position && relTick < ph.End) {
                    cur = ph;
                    break;
                }
            }
            if (cur == null) {
                return null;
            }
            var v = VisemeOf(cur.phoneme);
            if (v != null) {
                return v;
            }
            for (int i = 0; i < phonemes.Count; i++) {
                var p = phonemes[i];
                if (p.Parent == cur.Parent) {
                    var vowel = PlainVowelOf(p.phoneme);
                    if (vowel != null) {
                        return vowel;
                    }
                }
            }
            return "a";
        }

        public static string? PlainVowelOf(string? sym) {
            var b = Base(sym);
            if (string.IsNullOrEmpty(b)) {
                return null;
            }
            char c = char.ToLowerInvariant(b[0]);
            if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') {
                return c.ToString();
            }
            return null;
        }
    }
}
