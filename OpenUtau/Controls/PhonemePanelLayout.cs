using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media.TextFormatting;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Controls {
    class PhonemePanelLayout {
        public struct Token {
            public TextLayout layout;
            public double x;
        }

        public List<Token> tokens = new List<Token>();
        public List<UPhoneme> phonemes = new List<UPhoneme>();
        public List<int> indices = new List<int>();
        public UNote? leading;
        public List<UPhoneme> groupPhonemes = new List<UPhoneme>();
        public string text = string.Empty;
        public double width;
        public double height;

        const double FontSize = 12;
        const double Padding = 2;
        const double Gap = 2;

        public static string GetPhonemeText(UPhoneme phoneme, string langCode) {
            string text = !string.IsNullOrEmpty(phoneme.phonemeMapped) ? phoneme.phonemeMapped : phoneme.phoneme;
            if (!string.IsNullOrEmpty(langCode) && text.StartsWith(langCode + "/")) {
                text = text.Substring(langCode.Length + 1);
            }
            return text;
        }

        public static List<UNote> GetNoteGroup(UNote note) {
            var leading = note.Extends ?? note;
            var group = new List<UNote> { leading };
            var g = leading;
            while (g.Next != null && g.Next.Extends == g) {
                g = g.Next;
                group.Add(g);
            }
            return group;
        }

        public static Dictionary<UNote, List<UPhoneme>> GetPhonemesByParent(UVoicePart part) {
            var map = new Dictionary<UNote, List<UPhoneme>>();
            foreach (var phoneme in part.phonemes) {
                if (phoneme.Parent == null) {
                    continue;
                }
                if (!map.TryGetValue(phoneme.Parent, out var list)) {
                    list = new List<UPhoneme>();
                    map[phoneme.Parent] = list;
                }
                list.Add(phoneme);
            }
            return map;
        }

        public static (List<UPhoneme> phonemes, List<int> indices)? GetNotePhonemes(UNote note, Dictionary<UNote, List<UPhoneme>> phonemesByParent) {
            var leading = note.Extends ?? note;
            if (!phonemesByParent.TryGetValue(leading, out var parentPhonemes)) {
                return null;
            }
            var group = GetNoteGroup(note);
            var phonemes = new List<UPhoneme>();
            var indices = new List<int>();
            for (int gi = 0; gi < parentPhonemes.Count; gi++) {
                var phoneme = parentPhonemes[gi];
                int index = group.Count - 1;
                for (int i = 0; i < group.Count; i++) {
                    if (phoneme.position < group[i].End) {
                        index = i;
                        break;
                    }
                }
                if (group[index] == note) {
                    phonemes.Add(phoneme);
                    indices.Add(gi);
                }
            }
            return (phonemes, indices);
        }

        public static Rect GetPanelBounds(NotesViewModel viewModel, UNote note, PhonemePanelLayout layout) {
            var leftTop = viewModel.TickToneToPoint(note.position, note.AdjustedTone);
            return new Rect(leftTop.X + Padding, leftTop.Y - layout.height - Gap, layout.width, layout.height);
        }

        public static PhonemePanelLayout? Build(UNote note, string langCode, double maxWidth, Dictionary<UNote, List<UPhoneme>> phonemesByParent) {
            var notePhonemes = GetNotePhonemes(note, phonemesByParent);
            if (notePhonemes == null || notePhonemes.Value.phonemes.Count == 0) {
                return null;
            }
            bool useUiFont = Preferences.Default.UseUiFontForNotes;
            var spaceLayout = TextLayoutCache.Get("n n", ThemeManager.ForegroundBrush!, FontSize, false, useUiFont);
            var noSpaceLayout = TextLayoutCache.Get("nn", ThemeManager.ForegroundBrush!, FontSize, false, useUiFont);
            double spaceWidth = Math.Max(1, spaceLayout.Width - noSpaceLayout.Width);
            var ellipsisLayout = TextLayoutCache.Get("...", ThemeManager.ForegroundBrush!, FontSize, false, useUiFont);
            var tokenLayouts = new List<(TextLayout layout, UPhoneme phoneme, int globalIndex)>();
            var fullText = new List<string>();
            for (int i = 0; i < notePhonemes.Value.phonemes.Count; i++) {
                var phoneme = notePhonemes.Value.phonemes[i];
                string text = GetPhonemeText(phoneme, langCode);
                if (string.IsNullOrEmpty(text)) {
                    continue;
                }
                bool modified = phoneme.phoneme != phoneme.rawPhoneme;
                var brush = modified ? ThemeManager.AccentBrush3 : ThemeManager.ForegroundBrush;
                tokenLayouts.Add((TextLayoutCache.Get(text, brush, FontSize, modified, useUiFont), phoneme, notePhonemes.Value.indices[i]));
                fullText.Add(text);
            }
            if (tokenLayouts.Count == 0) {
                return null;
            }
            double avail = maxWidth - Padding * 2;
            if (avail <= 0) {
                return null;
            }
            double fullWidth = 0;
            for (int i = 0; i < tokenLayouts.Count; i++) {
                fullWidth += (i > 0 ? spaceWidth : 0) + tokenLayouts[i].layout.Width;
            }
            int count;
            bool ellipsis;
            if (fullWidth <= avail) {
                count = tokenLayouts.Count;
                ellipsis = false;
            } else {
                double acc = 0;
                count = 0;
                while (count < tokenLayouts.Count) {
                    double w = acc + (count > 0 ? spaceWidth : 0) + tokenLayouts[count].layout.Width;
                    if (w + ellipsisLayout.Width <= avail) {
                        acc = w;
                        count++;
                    } else {
                        break;
                    }
                }
                ellipsis = count > 0;
                if (!ellipsis) {
                    acc = 0;
                    count = 0;
                    while (count < tokenLayouts.Count) {
                        double w = acc + (count > 0 ? spaceWidth : 0) + tokenLayouts[count].layout.Width;
                        if (w <= avail) {
                            acc = w;
                            count++;
                        } else {
                            break;
                        }
                    }
                }
                if (count == 0) {
                    return null;
                }
            }
            var leading = note.Extends ?? note;
            var layout = new PhonemePanelLayout {
                phonemes = tokenLayouts.Select(t => t.phoneme).ToList(),
                indices = tokenLayouts.Select(t => t.globalIndex).ToList(),
                leading = leading,
                groupPhonemes = phonemesByParent[leading],
                text = string.Join(" ", fullText),
                height = tokenLayouts[0].layout.Height,
            };
            double x = 0;
            for (int i = 0; i < count; i++) {
                layout.tokens.Add(new Token {
                    layout = tokenLayouts[i].layout,
                    x = x,
                });
                x += tokenLayouts[i].layout.Width + spaceWidth;
            }
            if (ellipsis) {
                layout.tokens.Add(new Token {
                    layout = ellipsisLayout,
                    x = x - spaceWidth,
                });
                x += ellipsisLayout.Width - spaceWidth;
            }
            layout.width = x;
            return layout;
        }
    }
}
