using OpenUtau.Api;
using OpenUtau.Core.G2p;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using System.Text; // Needed for StringBuilder in UnpackHint if implementing directly

namespace OpenUtau.Core.DiffSinger {
    // Internal G2P wrapper that applies phoneme replacement and implements full IG2p
    internal class RussianG2pUkrainianReplacer : IG2p {
        private readonly IG2p baseG2p; // The original RussianG2p
        private readonly Dictionary<string, string> phonemeReplacements; // The RU -> UK mapping
        private readonly HashSet<string> validSymbols;
        private readonly HashSet<string> vowels;
        private readonly HashSet<string> glides; // Typically 'j' in this context

        public RussianG2pUkrainianReplacer(IG2p baseG2p, Dictionary<string, string> replacements, string[] targetVowels, string[] targetConsonants, string[] targetGlides) {
            this.baseG2p = baseG2p;
            this.phonemeReplacements = replacements;

            // Build HashSets for efficient lookups based on the TARGET phoneme set
            this.vowels = new HashSet<string>(targetVowels);
            this.validSymbols = new HashSet<string>(targetVowels);
            this.validSymbols.UnionWith(targetConsonants);
            this.glides = new HashSet<string>(targetGlides); // Pass glides explicitly
        }

        // === Implementation of IG2p methods ===

        public bool IsValidSymbol(string symbol) {
            // Check against the target Ukrainian phoneme set
            return validSymbols.Contains(symbol);
        }

        public bool IsVowel(string symbol) {
            // Check against the target Ukrainian vowel set
            return vowels.Contains(symbol);
        }

        public bool IsGlide(string symbol) {
            // Check against the target Ukrainian glide set
            return glides.Contains(symbol);
        }

        // Main query method (unchanged logic)
        public string[] Query(string word) {
            string[]? originalPhonemes = baseG2p.Query(word);
            if (originalPhonemes == null) {
                Log.Warning($"RussianG2p returned null for word: {word}");
                return new string[0];
            }

            List<string> replacedPhonemes = new List<string>();
            foreach (string ph in originalPhonemes) {
                if (phonemeReplacements.TryGetValue(ph, out string? replacedPh)) {
                    replacedPhonemes.Add(replacedPh);
                } else {
                    replacedPhonemes.Add(ph);
                    Log.Warning($"Phoneme '{ph}' from RussianG2p for word '{word}' not found in RU->UK replacement dictionary. Using original '{ph}'.");
                }
            }
            // Log.Debug($"RU->UK G2P Query: '{word}' -> Original: [{string.Join(" ", originalPhonemes)}] -> Replaced: [{string.Join(" ", replacedPhonemes)}]");
            return replacedPhonemes.ToArray();
        }

        // Query method taking graphemes (unchanged logic)
        public string[] Query(string[] graphemes) {
            return Query(string.Join("", graphemes));
        }

        // UnpackHint: This usually deals with user-provided phoneme strings.
        // It's safest to delegate this to the base G2P, as it knows how to parse
        // its own expected hint format (likely space-separated ARPAbet).
        // The phonemizer logic will then use IsValidSymbol etc. on the result.
        public string[] UnpackHint(string hint, char separator = ' ') {
            // Let the original RussianG2p handle the hint parsing logic.
            // It might produce Russian phonemes, which is okay for hints,
            // as the phonemizer uses classification methods on them later if needed.
            return baseG2p.UnpackHint(hint, separator);

            // Alternative implementation if delegation is problematic (less likely):
            // Directly split and validate against the *target* symbols.
            /*
            string[] phonemes = hint.Split(separator);
            List<string> result = new List<string>();
            foreach (string p in phonemes) {
                if (IsValidSymbol(p)) { // Use *our* IsValidSymbol
                    result.Add(p);
                } else {
                    Log.Warning($"Invalid symbol '{p}' in hint '{hint}' ignored.");
                }
            }
            return result.ToArray();
            */
        }
    }

    // Phonemizer implementation (updated constructor call)
    [Phonemizer("DiffSinger RU -> UK Phonemizer", "DIFFS RU->UK", "GPT Copilot", language: "UK")]
    public class DiffSingerRussianToUkrainianPhonemizer : DiffSingerG2pPhonemizer {
        // --- Configuration ---
        protected override string GetDictionaryName() => "dsdict-uk.yaml";
        protected override string GetLangCode() => "uk";

        // --- Replacement Dictionary (RU G2P Output -> UK DiffSinger Phonemes) ---
        private static readonly Dictionary<string, string> RuToUkReplacements = new Dictionary<string, string> {
             // Vowels (Mapping Russian G2P ARPAbet to Ukrainian DiffSinger phonemes)
            { "AA", "a" }, // Stressed A
            { "AE", "e" }, // Russian Э equivalent? Map to E
            { "AH", "a" }, // Unstressed A/O -> typically 'a' or 'y' in UK, map to 'a' for simplicity
            { "AO", "o" }, // Stressed O
            { "AW", "av" }, // ai diphthong -> map to a + j ? Or handle differently? Let's try 'aj' if needed, or map AY->a? Map to 'a' for now. Needs UK dict check.
            { "AY", "aj" }, // ei diphthong -> map to e + j ? Or handle differently? Map EY->e for now.
            { "EH", "e" }, // Stressed E
            { "ER", "er" }, // If present (e.g., for foreign words), map as is or to 'er'? Map to 'e' for now.
            { "EY", "ej" }, // Stressed I -> map to Ukrainian 'i' (і)
            { "IH", "y" }, // Unstressed I/E -> map to Ukrainian 'y' (и)
            { "IY", "i" }, // oi diphthong -> map to o + j ? Or handle differently? Map OY->o for now.
            { "OW", "o" }, // Stressed O -> o
            { "OY", "oj" }, // Unstressed U/O -> u
            { "UH", "u" }, // Stressed U -> u
            { "UW", "u" }, // Russian Ы vowel sound -> map to Ukrainian 'y' (и)
            { "Y",  "y" }, 

            // Consonants (Mapping Russian G2P ARPAbet to Ukrainian DiffSinger phonemes)
            { "B", "b" },
            { "CH", "ch" }, // Russian Ч -> Ukrainian Ч
            { "D", "d" },
            { "DH", "z" }, // TH (voiced) often becomes Z in Slavic adaptations
            { "F", "f" },
            { "G", "h" }, // *** KEY MAPPING: Russian Г -> Ukrainian Г (h sound) ***
            { "HH", "h" }, // Aspirated H -> Ukrainian Г (h sound)
            { "JH", "dzh" }, // Russian Дж -> Ukrainian ДЖ
            { "K", "k" },
            { "L", "l" },
            { "M", "m" },
            { "N", "n" },
            { "NG", "n" }, // NG often simplifies to N
            { "P", "p" },
            { "R", "r" },
            { "S", "s" },
            { "SH", "sh" }, // Russian Ш -> Ukrainian Ш
            { "T", "t" },
            { "TH", "s" }, // TH (voiceless) often becomes S in Slavic adaptations
            { "V", "v" },
            { "W", "v" }, // W sound often becomes V
            { "WH", "v" }, // WH sound often becomes V
            { "J", "j" },  // Map G2P's J (Й sound) to target j.
            { "Z", "z" },
            { "ZH", "zh" }, // Russian Ж -> Ukrainian Ж

            // Affricates / Special Russian sounds mapped to Ukrainian
            { "TS", "c" },  // Russian Ц -> Ukrainian Ц
            { "SHCH", "shch" }, // Russian Щ -> Ukrainian Щ (Need to verify G2P output symbol)
            { "SCH", "shch" }, // Add mapping for SCH just in case (based on RU phonemizer example)

             // Palatalized Consonants (Assuming G2P outputs BB, DD etc. based on other examples)
             // Mapping to the 'bb', 'dd' style assumed for the target UK dict.
            { "BB", "bb" }, // Russian Бь -> Ukrainian Бь (bb)
            { "DD", "dd" }, // Russian Дь -> Ukrainian Дь (dd)
            { "FF", "ff" }, // Russian Фь -> Ukrainian Фь (ff)
            { "GG", "hh" }, // *** Russian Гь -> Ukrainian Гь (hh sound) ***
            { "KK", "kk" }, // Russian Кь -> Ukrainian Кь (kk)
            { "LL", "ll" }, // Russian Ль -> Ukrainian Ль (ll)
            { "MM", "mm" }, // Russian Мь -> Ukrainian Мь? (Less common in UK phonetics, keep for now)
            { "NN", "nn" }, // Russian Нь -> Ukrainian Нь (nn)
            { "PP", "pp" }, // Russian Пь -> Ukrainian Пь (pp)
            { "RR", "rr" }, // Russian Рь -> Ukrainian Рь (rr)
            { "SS", "ss" }, // Russian Сь -> Ukrainian Сь (ss)
            { "TT", "tt" }, // Russian Ть -> Ukrainian Ть (tt)
            { "VV", "vv" }, // Russian Вь -> Ukrainian Вь (vv)
            { "ZZ", "zz" }, // Russian Зь -> Ukrainian Зь (zz)

            // Handling G2P output variations if necessary (Examples from other files)
            { "DX", "d" }, // Flap T -> d
            { "NX", "n" }, // Flap N -> n
            { "EL", "l" }, // Syllabic L -> l
            { "EM", "m" }, // Syllabic M -> m
            { "EN", "n" }, // Syllabic N -> n
        };

        // --- Target Phoneme Set Definition ---
        // These lists define the *target* Ukrainian phonemes expected in dsdict-uk.yaml
        private static readonly string[] TargetVowels = new string[] {
            "a", "e", "y", "i", "o", "u",
            "ja", "je", "jy", "ji", "jo", "ju",
            "er" // Keep 'er' if it's a distinct vowel sound in the target dict
        };

        private static readonly string[] TargetConsonants = new string[] {
            // Hard Stops/Fricatives/Nasals/Liquids
            "b", "p", "v", "f", "h", "k", "g",
            "d", "t", "z", "s", "zh", "sh", "l", "m", "n", "r",
            // Ukrainian Specific / Affricates
            "shch", "j", "c", "ch", "dz", "dzh",
            // Palatalized Consonants
            "bb", "pp", "vv", "ff", "hh", "kk", "gg",
            "dd", "tt", "zz", "ss", "ll", "mm", "nn", "rr"
        };

        // Define the glides for the target phoneme set
        private static readonly string[] TargetGlides = new string[] {
            "j" // Ukrainian Й is the primary glide in this context
            // Consider adding "v" if the voicebank treats it similarly to English 'w' sometimes,
            // but phonetically it's usually just a consonant. Stick with 'j' for now.
        };

        // --- G2P Loading ---
        // Pass the target phoneme sets to the wrapper constructor
        protected override IG2p LoadBaseG2p() =>
            new RussianG2pUkrainianReplacer(
                new UkrainianG2p(),
                RuToUkReplacements,
                TargetVowels,
                TargetConsonants,
                TargetGlides // Pass the glides list
            );


        // --- Base Phoneme Lists for Phonemizer ---
        // These overrides tell the DiffSingerG2pPhonemizer base class
        // about the TARGET phoneme set it should be working with.
        protected override string[] GetBaseG2pVowels() => TargetVowels;
        protected override string[] GetBaseG2pConsonants() => TargetConsonants;

    }
}
