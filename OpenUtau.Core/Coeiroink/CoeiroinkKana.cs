using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenUtau.Core.Coeiroink {
    public readonly struct CoeiroinkMora {
        public readonly string? Consonant;
        public readonly string Vowel;

        public CoeiroinkMora(string? consonant, string vowel) {
            Consonant = string.IsNullOrEmpty(consonant) ? null : consonant;
            Vowel = vowel;
        }

        public bool IsGeminate => Vowel == CoeiroinkKana.Geminate;

        public override string ToString() => Consonant == null ? Vowel : $"{Consonant} {Vowel}";
    }

    public static class CoeiroinkKana {
        public const string Geminate = "cl";
        public const string Nasal = "N";

        static readonly string[] vowelTokens = { "a", "i", "u", "e", "o" };
        static readonly HashSet<string> consonantTokens = new HashSet<string> {
            "k", "s", "sh", "t", "ch", "ts", "n", "h", "f", "m", "y", "r", "w", "g", "z", "j", "d", "b", "p", "v",
            "ky", "gy", "ny", "hy", "my", "by", "py", "ry", "dy", "ty",
        };

        static readonly Dictionary<string, (string? c, string v)> table = BuildTable();
        static readonly Regex romaji = new Regex("^(ky|gy|ny|hy|my|by|py|ry|dy|ty|sh|ch|ts|[kstnhfmyrwgzjdbpv])?([aiueo])$", RegexOptions.Compiled);

        public static bool IsConsonantToken(string text) {
            return consonantTokens.Contains(text.Trim());
        }

        public static double DefaultConsonantMs(string consonant) {
            switch (consonant) {
                case "k": case "t": case "p": case "g": case "d": case "b":
                    return 50;
                case "ch": case "ts":
                    return 100;
                case "s": case "sh": case "h": case "f": case "z": case "j": case "hy": case "v":
                    return 90;
                case "y": case "w":
                    return 50;
                default:
                    return 65;
            }
        }

        public static IEnumerable<string> AllKana => table.Keys.Concat(new[] { "ん", "っ" });

        static Dictionary<string, (string? c, string v)> BuildTable() {
            var map = new Dictionary<string, (string? c, string v)>();
            void Row(string? consonant, string kana) {
                for (int i = 0; i < 5; i++) {
                    if (i < kana.Length && kana[i] != '-') {
                        map[kana[i].ToString()] = (consonant, vowelTokens[i]);
                    }
                }
            }
            Row(null, "あいうえお");
            Row("k", "かきくけこ");
            Row("s", "さしすせそ");
            Row("t", "たちつてと");
            Row("n", "なにぬねの");
            Row("h", "はひふへほ");
            Row("m", "まみむめも");
            Row("y", "や-ゆ-よ");
            Row("r", "らりるれろ");
            Row("w", "わ---を");
            Row("g", "がぎぐげご");
            Row("z", "ざじずぜぞ");
            Row("d", "だぢづでど");
            Row("b", "ばびぶべぼ");
            Row("p", "ぱぴぷぺぽ");
            map["を"] = (null, "o");
            map["し"] = ("sh", "i");
            map["ち"] = ("ch", "i");
            map["つ"] = ("ts", "u");
            map["ふ"] = ("f", "u");
            map["じ"] = ("j", "i");
            map["ぢ"] = ("j", "i");
            map["づ"] = ("z", "u");
            map["ゔ"] = ("v", "u");
            map["ゐ"] = (null, "i");
            map["ゑ"] = (null, "e");

            var smallVowels = new Dictionary<char, string> { ['ぁ'] = "a", ['ぃ'] = "i", ['ぅ'] = "u", ['ぇ'] = "e", ['ぉ'] = "o" };
            var smallYa = new Dictionary<char, string> { ['ゃ'] = "a", ['ゅ'] = "u", ['ょ'] = "o" };
            foreach (var (small, vowel) in smallVowels) {
                map[small.ToString()] = (null, vowel);
            }
            map["ゃ"] = ("y", "a");
            map["ゅ"] = ("y", "u");
            map["ょ"] = ("y", "o");
            map["ゎ"] = ("w", "a");

            var palatal = new Dictionary<char, string> {
                ['き'] = "ky", ['し'] = "sh", ['ち'] = "ch", ['に'] = "ny", ['ひ'] = "hy", ['み'] = "my",
                ['り'] = "ry", ['ぎ'] = "gy", ['じ'] = "j", ['ぢ'] = "j", ['び'] = "by", ['ぴ'] = "py",
                ['て'] = "ty", ['で'] = "dy", ['ゔ'] = "v",
            };
            foreach (var (baseKana, consonant) in palatal) {
                foreach (var (small, vowel) in smallYa) {
                    map[$"{baseKana}{small}"] = (consonant, vowel);
                }
            }
            foreach (var (baseKana, consonant) in palatal) {
                foreach (var (small, vowel) in smallVowels) {
                    if (consonant == "ty" || consonant == "dy") {
                        map[$"{baseKana}{small}"] = (consonant == "ty" ? "t" : "d", vowel);
                    } else {
                        map[$"{baseKana}{small}"] = (consonant, vowel);
                    }
                }
            }
            foreach (var (small, vowel) in smallVowels) {
                map[$"ふ{small}"] = ("f", vowel);
                map[$"つ{small}"] = ("ts", vowel);
                map[$"う{small}"] = ("w", vowel);
                map[$"く{small}"] = ("k", vowel);
                map[$"ぐ{small}"] = ("g", vowel);
            }
            map["とぅ"] = ("t", "u");
            map["どぅ"] = ("d", "u");
            map["いぇ"] = ("y", "e");
            return map;
        }

        public static string ToHiragana(string text) {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text) {
                if (c >= 'ァ' && c <= 'ヶ') {
                    sb.Append((char)(c - 0x60));
                } else {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public const string PausePhoneme = "pau";

        public static bool IsRest(string lyric) {
            var text = lyric.Trim();
            return text.Length == 0
                || text.Equals("R", StringComparison.OrdinalIgnoreCase)
                || text.Equals("SP", StringComparison.OrdinalIgnoreCase)
                || text.Equals("AP", StringComparison.OrdinalIgnoreCase)
                || text.Equals("pau", StringComparison.OrdinalIgnoreCase)
                || text.Equals("br", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExtension(string lyric) {
            var text = lyric.Trim();
            return text == "ー" || text == "-" || text.StartsWith("+");
        }

        public static bool TryParse(string lyric, out CoeiroinkMora[] moras) {
            moras = Array.Empty<CoeiroinkMora>();
            if (string.IsNullOrWhiteSpace(lyric)) {
                return false;
            }
            var text = lyric.Normalize(NormalizationForm.FormKC).Trim();
            if (TryParseKana(ToHiragana(text), out moras)) {
                return true;
            }
            if (TryParseTokens(text.ToLowerInvariant(), out moras)) {
                return true;
            }
            return false;
        }

        static bool TryParseKana(string text, out CoeiroinkMora[] moras) {
            var result = new List<CoeiroinkMora>();
            moras = Array.Empty<CoeiroinkMora>();
            int i = 0;
            while (i < text.Length) {
                char c = text[i];
                if (c == 'ん') {
                    result.Add(new CoeiroinkMora(null, Nasal));
                    i++;
                    continue;
                }
                if (c == 'っ') {
                    result.Add(new CoeiroinkMora(null, Geminate));
                    i++;
                    continue;
                }
                if (c == 'ー' && result.Count > 0 && !result[^1].IsGeminate && result[^1].Vowel != Nasal) {
                    result.Add(new CoeiroinkMora(null, result[^1].Vowel));
                    i++;
                    continue;
                }
                if (i + 1 < text.Length && table.TryGetValue(text.Substring(i, 2), out var pair)) {
                    result.Add(new CoeiroinkMora(pair.c, pair.v));
                    i += 2;
                    continue;
                }
                if (table.TryGetValue(c.ToString(), out var single)) {
                    result.Add(new CoeiroinkMora(single.c, single.v));
                    i++;
                    continue;
                }
                return false;
            }
            moras = result.ToArray();
            return moras.Length > 0;
        }

        static bool TryParseTokens(string text, out CoeiroinkMora[] moras) {
            moras = Array.Empty<CoeiroinkMora>();
            if (text == "n") {
                moras = new[] { new CoeiroinkMora(null, Nasal) };
                return true;
            }
            var match = romaji.Match(text.ToLowerInvariant());
            if (match.Success) {
                moras = new[] { new CoeiroinkMora(match.Groups[1].Success ? match.Groups[1].Value : null, match.Groups[2].Value) };
                return true;
            }
            var result = new List<CoeiroinkMora>();
            string? pending = null;
            foreach (var raw in text.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
                var part = raw == Nasal ? raw : raw.ToLowerInvariant();
                if (consonantTokens.Contains(part)) {
                    if (pending != null) {
                        return false;
                    }
                    pending = part;
                } else if (vowelTokens.Contains(part) || part == Nasal || part == Geminate) {
                    result.Add(new CoeiroinkMora(pending, part));
                    pending = null;
                } else {
                    return false;
                }
            }
            if (pending != null || result.Count == 0) {
                return false;
            }
            moras = result.ToArray();
            return true;
        }
    }
}
