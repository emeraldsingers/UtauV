using System;
using System.Collections.Generic;
using OpenUtau.Api;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Coeiroink {
    [Phonemizer("COEIROINK Japanese Phonemizer", "COEIROINK JA", language: "JA")]
    public class CoeiroinkPhonemizer : Phonemizer {
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
                if (moras.Length == 1) {
                    phonemes.Add(new Phoneme { phoneme = lyric.Trim(), position = offset });
                    continue;
                }
                for (int i = 0; i < moras.Length; i++) {
                    phonemes.Add(new Phoneme {
                        phoneme = moras[i].ToString(),
                        position = offset + note.duration * i / moras.Length,
                    });
                }
            }
            return new Result { phonemes = phonemes.ToArray() };
        }
    }
}
