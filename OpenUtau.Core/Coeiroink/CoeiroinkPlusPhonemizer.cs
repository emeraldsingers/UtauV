using System;
using System.Collections.Generic;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Coeiroink {
    [Phonemizer("COEIROINK Japanese Phonemizer Plus", "COEIROINK JA+", language: "JA")]
    public class CoeiroinkPlusPhonemizer : Phonemizer {
        public override void SetSinger(USinger singer) { }

        public override Result Process(Note[] notes, Note? prev, Note? next, Note? prevNeighbour, Note? nextNeighbour, Note[] prevNeighbours) {
            var phonemes = new List<Phoneme>();
            foreach (var note in notes) {
                var lyric = string.IsNullOrEmpty(note.phoneticHint) ? note.lyric : note.phoneticHint;
                if (string.IsNullOrWhiteSpace(lyric) || CoeiroinkKana.IsExtension(lyric)) {
                    continue;
                }
                if (CoeiroinkKana.IsRest(lyric)) {
                    phonemes.Add(new Phoneme { phoneme = CoeiroinkKana.PausePhoneme, position = note.position - notes[0].position });
                    continue;
                }
                if (!CoeiroinkKana.TryParse(lyric, out var moras)) {
                    throw new Exception($"Unrecognized lyric \"{lyric}\"");
                }
                int offset = note.position - notes[0].position;
                for (int i = 0; i < moras.Length; i++) {
                    int position = offset + note.duration * i / moras.Length;
                    var mora = moras[i];
                    if (mora.Consonant == null || mora.IsGeminate) {
                        phonemes.Add(new Phoneme { phoneme = moras.Length == 1 ? lyric.Trim() : mora.ToString(), position = position });
                        continue;
                    }
                    int moraDuration = note.duration / moras.Length;
                    int lead = ConsonantTicks(mora.Consonant, notes[0].position + position, moraDuration);
                    phonemes.Add(new Phoneme { phoneme = mora.Consonant + mora.Vowel, position = position });
                    phonemes.Add(new Phoneme { phoneme = mora.Vowel, position = position + lead });
                }
            }
            return new Result { phonemes = phonemes.ToArray() };
        }

        int ConsonantTicks(string consonant, int tickPosition, int moraDuration) {
            double startMs = timeAxis.TickPosToMsPos(tickPosition);
            double consonantMs = CoeiroinkKana.DefaultConsonantMs(consonant);
            int ticks = timeAxis.MsPosToTickPos(startMs + consonantMs) - tickPosition;
            return Math.Clamp(ticks, 1, Math.Max(1, moraDuration / 2));
        }
    }
}
