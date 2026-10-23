using OpenUtau.Api;
using OpenUtau.Core.G2p;

namespace OpenUtau.Core.DiffSinger
{
    [Phonemizer("DiffSinger Russian m & hhskt Phonemizer", "DIFFS RU HHSKT", language: "RU")]
    public class DiffSingerRussianHhsktPhonemizer : DiffSingerG2pPhonemizer
    {
        protected override string GetDictionaryName()=>"dsdict-ru-hhskt.yaml";
        public override string GetLangCode()=>"ru";
        protected override IG2p LoadBaseG2p() => new RussianHhsktG2p();
        protected override string[] GetBaseG2pVowels() => new string[] {
            "i", "y", "e", "a", "o", "u", "ax", "x", "ex"
        };

        protected override string[] GetBaseG2pConsonants() => new string[] {
            "p", "py", "b", "by", "ch", "ts", "t", "ty", "d", "dy", "k", "ky",
            "g", "gy", "f", "fy", "v", "vy", "s", "sy", "sh", "shy", "z", "zy",
            "zh", "h", "hy", "m", "my", "n", "ny", "l", "ly", "r", "ry", "j"
        };
    }
}
