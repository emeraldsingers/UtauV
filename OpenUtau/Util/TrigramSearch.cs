using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenUtau.App.Util {
    internal static class TrigramSearch {
        public static IEnumerable<T> FilterByQuery<T>(IEnumerable<T> items, Func<T, string> textSelector, string query) {
            var normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery)) {
                return items;
            }
            if (normalizedQuery.Length < 3) {
                return items.Where(item => Normalize(textSelector(item)).Contains(normalizedQuery, StringComparison.Ordinal));
            }
            var queryTrigrams = BuildTrigrams(normalizedQuery);
            return items.Select(item => (item, score: BuildTrigrams(Normalize(textSelector(item))).Intersect(queryTrigrams).Count()))
                .Where(result => result.score > 0)
                .OrderByDescending(result => result.score)
                .Select(result => result.item);
        }

        static HashSet<string> BuildTrigrams(string text) {
            var trigrams = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i <= text.Length - 3; i++) {
                trigrams.Add(text.Substring(i, 3));
            }
            return trigrams;
        }

        static string Normalize(string text) {
            var builder = new StringBuilder(text.Length);
            foreach (var ch in text.Trim().ToLowerInvariant()) {
                if (char.IsLetterOrDigit(ch)) {
                    builder.Append(ch);
                }
            }
            return builder.ToString();
        }
    }
}
