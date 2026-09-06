using Avalonia;
using Avalonia.Media.TextFormatting;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.DiffSinger;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using OpenUtau.Colors;

namespace OpenUtau.App.Controls {
    static class PhonemeUIRender {
        public static bool IsDiffSinger(UVoicePart part) {
            var track = DocManager.Inst.Project.tracks[part.trackNo];
            return track.Phonemizer is DiffSingerBasePhonemizer;
        }

        public static bool UseDiffSingerBarStyle(UVoicePart part) {
            return IsDiffSinger(part) && Preferences.Default.DiffSingerBarStyle;
        }

        public static string getLangCode(UVoicePart part) {
            int trackNo = part.trackNo;
            var track = DocManager.Inst.Project.tracks[trackNo];
            string langCode = "";
            if (track.Phonemizer is DiffSingerG2pPhonemizer g2pPhonemizer) {
                langCode = g2pPhonemizer.GetLangCode();
            } else if (track.Phonemizer is DiffSingerBasePhonemizer basePhonemizer) {
                langCode = basePhonemizer.GetLangCode();
            }
            return langCode;
        }
        //Calculates the position of a phoneme alias on a piano roll view, 
        //considering factors like tick width, phoneme text, and text layout. 
        //It returns the x-coordinate and text y-coordinate of the alias
        public static (double textX, double textY, Size size, TextLayout textLayout)
            AliasPosition(NotesViewModel viewModel, UPhoneme phoneme, string? langCode, ref double lastTextEndX, ref bool raiseText) {

            string phonemeText = !string.IsNullOrEmpty(phoneme.phonemeMapped) ? phoneme.phonemeMapped : phoneme.phoneme;
            if (Preferences.Default.DiffSingerLangCodeHide && !string.IsNullOrEmpty(langCode) && phonemeText.StartsWith(langCode + "/")) {
                phonemeText = phonemeText.Substring(langCode.Length + 1);
            }
            var x = viewModel.TickToneToPoint(phoneme.position, 0).X;
            var bold = phoneme.phoneme != phoneme.rawPhoneme;
            var textBrush = ThemeManager.ForegroundBrush!;
            if (Preferences.Default.UseCustomSingerTheme && viewModel.Part != null) {
                var singerName = viewModel.Project.tracks[viewModel.Part.trackNo].Singer?.Name ?? string.Empty;
                var theme = CustomSingerTheme.GetThemeForSinger(singerName);
                if (theme?.HasPhonemeColor == true) {
                    textBrush = theme.GetBrush(theme.PhonemeColor);
                }
            }
            var textLayout = TextLayoutCache.Get(phonemeText, textBrush, 12, bold,
                useUiFont: Preferences.Default.UseUiFontForNotes);
            if (x < lastTextEndX) {
                raiseText = !raiseText;
            } else {
                raiseText = false;
            }
            double textY = raiseText ? 2 : 18;
            var size = new Size(textLayout.Width + 4, textLayout.Height - 2);
            //var rect = new Rect(new Point(x - 2, textY + 1.5), size);
            /*if (rect.Contains(mousePos)) {
                result.phoneme = phoneme;
                result.hit = true;
                return result;
            }*/
            lastTextEndX = x + size.Width;
            return (x, textY, size, textLayout);
        }
    }
}
