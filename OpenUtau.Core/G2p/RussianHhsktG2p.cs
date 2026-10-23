using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using OpenUtau.Api;

namespace OpenUtau.Core.G2p {
    // G2p model from https://github.com/Megageorgio/hhskt_ru_phonemizer (MIT license)
    public class RussianHhsktG2p : G2pPack {
        private static readonly string[] graphemes = new string[] {
            "", "", "", "", "-", "а", "б", "в", "г", "д", "е", "ё", "ж", "з",
            "и", "й", "к", "л", "м", "н", "о", "п", "р", "с", "т", "у",
            "ф", "х", "ц", "ч", "ш", "щ", "ъ", "ы", "ь", "э", "ю", "я"
        };

        private static readonly string[] phonemes = new string[] {
            "", "", "", "", "i", "y", "e", "a", "o", "u", "ax", "x", "ex", "p",
            "py", "b", "by", "ch", "ts", "t", "ty", "d", "dy", "k", "ky", "g",
            "gy", "f", "fy", "v", "vy", "s", "sy", "sh", "shy", "z", "zy",
            "zh", "h", "hy", "m", "my", "n", "ny", "l", "ly", "r", "ry", "j"
        };

        private static object lockObj = new object();
        private static Dictionary<string, int> graphemeIndexes;
        private static IG2p dict;
        private static InferenceSession session;
        private static Dictionary<string, string[]> predCache = new Dictionary<string, string[]>();

        public RussianHhsktG2p() {
            lock (lockObj) {
                if (graphemeIndexes == null) {
                    graphemeIndexes = graphemes
                        .Skip(4)
                        .Select((g, i) => Tuple.Create(g, i))
                        .ToDictionary(t => t.Item1, t => t.Item2 + 4);
                    var tuple = LoadPack(Data.Resources.g2p_ru_hhskt);
                    dict = tuple.Item1;
                    session = tuple.Item2;
                }
            }
            GraphemeIndexes = graphemeIndexes;
            Phonemes = phonemes;
            Dict = dict;
            Session = session;
            PredCache = predCache;
        }
    }
}
