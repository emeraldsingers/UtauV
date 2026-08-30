using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using static ReactiveUI.Primitives.SubscribeExtensions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DynamicData.Binding;
using OpenUtau.App.Controls;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Primitives;

namespace OpenUtau.App.ViewModels {
    partial class LyricBoxViewModel : ViewModelBase {
        static readonly Regex phoneticHintPattern = new Regex(@"\[(.*)\]");
        public class SuggestionItem {
            public string Alias { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }

        [Reactive] public partial UVoicePart? Part { get; set; }
        [Reactive] public partial LyricBoxNoteOrPhoneme? NoteOrPhoneme { get; set; }
        [Reactive] public partial bool IsVisible { get; set; }
        [Reactive] public partial string? Text { get; set; }
        [Reactive] public partial SuggestionItem? SelectedSuggestion { get; set; }
        [Reactive] public partial ObservableCollectionExtended<SuggestionItem> Suggestions { get; set; }

        public bool IsAliasBox => isAliasBox.Value;
        private readonly ObservableAsPropertyHelper<bool> isAliasBox;

        public LyricBoxViewModel() {
            Text = string.Empty;
            Suggestions = new ObservableCollectionExtended<SuggestionItem>();

            this.WhenAnyValue(x => x.Text, x => x.IsVisible)
                .Subscribe(_ => UpdateSuggestion());
            this.WhenAnyValue(x => x.SelectedSuggestion)
                .Where(x => x != null).Select(x => x!)
                .Subscribe(ss => Serilog.Log.Information(ss.Alias));

            isAliasBox = this.WhenAnyValue(x => x.NoteOrPhoneme)
                .Select(v => v is LyricBoxPhoneme || v is LyricBoxNotePhonemes)
                .ToProperty(this, x => x.IsAliasBox);
        }

        private void UpdateSuggestion() {
            if (Part == null || NoteOrPhoneme == null) {
                Suggestions.Clear();
                return;
            }
            var singer = DocManager.Inst.Project.tracks[Part.trackNo].Singer;
            if (singer == null || !singer.Found || !singer.Loaded) {
                Suggestions.Clear();
                Suggestions.Add(new SuggestionItem() {
                    Alias = "No Singer",
                });
                return;
            }
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => singer.GetSuggestions(Text ?? "").Select(oto => new SuggestionItem() {
                Alias = oto.Alias,
                Source = string.IsNullOrEmpty(oto.Set) ? singer.Id : $"{oto.Set}",
            }).Take(32).ToList()).ContinueWith(task => {
                Suggestions.Clear();
                if (!string.IsNullOrEmpty(Text) && Core.Util.ActiveLyricsHelper.Inst.Current != null) {
                    string text = Core.Util.ActiveLyricsHelper.Inst.Current.Convert(Text);
                    if (Core.Util.Preferences.Default.LyricsHelperBrackets) {
                        text = $"[{text}]";
                    }
                    Suggestions.Add(new SuggestionItem() {
                        Alias = text,
                        Source = Core.Util.ActiveLyricsHelper.Inst.Current.Source,
                    });
                }
                if (!task.IsFaulted) {
                    Suggestions.AddRange(task.Result);
                }
            }, scheduler);
        }

        public void Commit() {
            if (Part == null || NoteOrPhoneme == null || Text == null) {
                return;
            }
            if (NoteOrPhoneme is LyricBoxNotePhonemes notePhonemes) {
                var leading = notePhonemes.leading;
                string langCode = PhonemeUIRender.getLangCode(Part);
                string baseLyric = phoneticHintPattern.Replace(leading.lyric, string.Empty).Trim();
                Match hintMatch = phoneticHintPattern.Match(leading.lyric);
                List<string> fullTokens;
                if (hintMatch.Success && hintMatch.Groups[1].Value.Trim().Length > 0) {
                    fullTokens = hintMatch.Groups[1].Value
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                } else {
                    fullTokens = notePhonemes.groupPhonemes
                        .Select(p => PhonemePanelLayout.GetPhonemeText(p, langCode))
                        .ToList();
                }
                string[] tokens = Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int editedCount = notePhonemes.indices.Count;
                var removed = new List<int>();
                for (int i = 0; i < editedCount; i++) {
                    int pos = notePhonemes.indices[i];
                    if (pos >= fullTokens.Count) {
                        continue;
                    }
                    if (i < tokens.Length) {
                        fullTokens[pos] = tokens[i];
                    } else {
                        removed.Add(pos);
                    }
                }
                int insertAt = editedCount > 0 ? notePhonemes.indices[editedCount - 1] + 1 : fullTokens.Count;
                foreach (int pos in removed.OrderByDescending(p => p)) {
                    fullTokens.RemoveAt(pos);
                    if (pos < insertAt) {
                        insertAt--;
                    }
                }
                for (int i = editedCount; i < tokens.Length; i++) {
                    fullTokens.Insert(Math.Min(insertAt, fullTokens.Count), tokens[i]);
                    insertAt++;
                }
                string hint = string.Join(" ", fullTokens);
                string newLyric = string.IsNullOrEmpty(hint)
                    ? baseLyric
                    : baseLyric.Length > 0 ? $"{baseLyric} [{hint}]" : $"[{hint}]";
                if (newLyric == leading.lyric) {
                    return;
                }
                DocManager.Inst.StartUndoGroup("command.phoneme.edit");
                foreach (var o in leading.phonemeOverrides.ToList()) {
                    if (!string.IsNullOrWhiteSpace(o.phoneme)) {
                        DocManager.Inst.ExecuteCmd(new ChangePhonemeAliasCommand(Part, leading, o.index, null));
                    }
                }
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(Part, leading, newLyric));
                DocManager.Inst.EndUndoGroup();
                return;
            }
            if (!IsAliasBox) {
                var note = NoteOrPhoneme as LyricBoxNote;
                if (Text == note!.Unwrap().lyric) {
                    return;
                }
            } else {
                var phoneme = NoteOrPhoneme as LyricBoxPhoneme;
                if (Text == phoneme!.Unwrap().phoneme) {
                    return;
                }
            }
            if (IsAliasBox) {
                DocManager.Inst.StartUndoGroup("command.phoneme.edit");
                var phoneme = (NoteOrPhoneme as LyricBoxPhoneme)!.Unwrap();
                var note = phoneme.Parent;
                int index = phoneme.index;
                DocManager.Inst.ExecuteCmd(new ChangePhonemeAliasCommand(Part, note.Extends ?? note, index, Text));
            } else {
                DocManager.Inst.StartUndoGroup("command.note.lyric");
                DocManager.Inst.ExecuteCmd(new ChangeNoteLyricCommand(Part, (NoteOrPhoneme as LyricBoxNote)!.Unwrap(), Text));
            }
            DocManager.Inst.EndUndoGroup();
        }
    }

    public abstract class LyricBoxNoteOrPhoneme { }
    public class LyricBoxNote : LyricBoxNoteOrPhoneme {
        public UNote note;
        public LyricBoxNote(UNote note) { this.note = note; }
        public UNote Unwrap() => note;
    }
    public class LyricBoxPhoneme : LyricBoxNoteOrPhoneme {
        public UPhoneme phoneme;
        public LyricBoxPhoneme(UPhoneme phoneme) { this.phoneme = phoneme; }
        public UPhoneme Unwrap() => phoneme;
    }

    public class LyricBoxNotePhonemes : LyricBoxNoteOrPhoneme {
        public UNote note;
        public UNote leading;
        public List<UPhoneme> groupPhonemes;
        public List<UPhoneme> phonemes;
        public List<int> indices;
        public string originalText;
        public LyricBoxNotePhonemes(UNote note, UNote leading, List<UPhoneme> groupPhonemes,
            List<UPhoneme> phonemes, List<int> indices, string originalText) {
            this.note = note;
            this.leading = leading;
            this.groupPhonemes = groupPhonemes;
            this.phonemes = phonemes;
            this.indices = indices;
            this.originalText = originalText;
        }
    }}
