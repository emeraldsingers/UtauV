using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using Serilog;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Ukrainian CV Phonemizer (Simple V2)", "UK CV Simple V2", "Your Name/Alias", language: "UK")]
    public class UkrainianCVPhonemizer : SyllableBasedPhonemizer {

        // Using the simple phoneme representation defined previously
        private readonly string[] vowels = "a,e,i,o,u,ay,aw,ey,oy".Split(',');
        private readonly string[] consonants = "b,ch,d,dh,dz,f,g,h,j,k,l,m,n,ng,p,r,s,sh,t,th,v,w,wh,y,z,zh".Split(',');
        private readonly Dictionary<string, string> aliasesFallback = new Dictionary<string, string>();

        // Re-verified Dictionary Replacements (ARPAbet -> Simpler Transcription)
        private readonly Dictionary<string, string> dictionaryReplacements = new Dictionary<string, string>() {
            // Vowels
            {"AA", "a"}, {"AE", "e"}, {"AH", "a"}, // Map AE, AH to 'a' or 'e' based on sound, let's try 'a' for AH
            {"AO", "o"}, {"AW", "aw"}, {"AY", "ay"},
            {"EH", "e"}, {"ER", "er"}, // Added 'er' if needed
            {"EY", "ey"}, {"IH", "e"}, // Explicitly map IH to 'e'
            {"IY", "i"}, {"OW", "o"}, {"OY", "oy"},
            {"UH", "u"}, {"UW", "u"},
            // Consonants
            {"B", "b"}, {"CH", "ch"}, {"D", "d"}, {"DH", "dh"},
            {"EL", "l"}, {"EM", "m"}, {"EN", "n"}, // Syllabic consonants if needed
            {"F", "f"}, {"G", "g"}, {"HH", "h"},
            {"JH", "j"}, {"K", "k"}, {"L", "l"}, {"M", "m"}, {"N", "n"},
            {"NG", "ng"}, {"P", "p"}, {"R", "r"}, {"S", "s"}, {"SH", "sh"},
            {"T", "t"}, {"TH", "th"}, {"V", "v"}, {"W", "v"}, {"WH", "wh"},
            {"Y", "y"}, {"Z", "z"}, {"ZH", "zh"},
            // Common G2P variations / potential needs
             {"DX", "d"}, // Flap T often maps to D
             {"NX", "n"}, // Flap N
             // Add DZ if your G2P outputs it explicitly
             {"DZ", "dz"}
        };

        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "g2p_uk";
        protected override IG2p LoadBaseDictionary() => new UkrainianG2p();
        protected override Dictionary<string, string> GetAliasesFallback() => aliasesFallback;
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

        protected override List<string> ProcessSyllable(Syllable syllable) {
            // syllable.v, syllable.cc, syllable.prevV now contain the simple phonemes (a, e, i, b, k, sh etc.)
            var phonemes = new List<string>();
            string basePhoneme = null;
            string lastC = syllable.cc.Length > 0 ? syllable.cc.Last() : null;

            // --- For Debugging: Check phonemes after replacement ---
            // Log.Debug($"Processing Syllable: PrevV='{syllable.prevV}', CC='{string.Join(" ", syllable.cc)}', V='{syllable.v}'");
            // ---

            if (syllable.IsStartingV) {
                // Start with Vowel: [V]
                basePhoneme = syllable.v;
            } else if (syllable.IsVV) {
                // Vowel after Vowel: Check for [VV] alias, fallback to [V]
                string vvAlias = $"{syllable.prevV}{syllable.v}";
                if (HasOto(vvAlias, syllable.tone)) {
                    basePhoneme = vvAlias;
                } else {
                    basePhoneme = syllable.v; // Fallback to current vowel
                }
            } else if (syllable.IsStartingCV) {
                // Start with Consonant(s) + Vowel: [CV]
                basePhoneme = $"{lastC}{syllable.v}";
                // Add preceding consonants as single phonemes if they exist
                for (int i = 0; i < syllable.cc.Length - 1; i++) {
                    if (HasOto(syllable.cc[i], syllable.tone)) {
                        phonemes.Add(syllable.cc[i]);
                    }
                }
            } else { // VCV
                // Vowel-Consonant(s)-Vowel: [CV]
                basePhoneme = $"{lastC}{syllable.v}";
                // Add preceding consonants as single phonemes if they exist
                for (int i = 0; i < syllable.cc.Length - 1; i++) {
                    if (HasOto(syllable.cc[i], syllable.tone)) {
                        phonemes.Add(syllable.cc[i]);
                    }
                }
            }

            if (basePhoneme != null) {
                // Ensure the expected alias (e.g., "sh e") exists before adding
                if (HasOto(basePhoneme, syllable.tone)) {
                    phonemes.Add(basePhoneme);
                } else {
                    Log.Warning($"Alias '{basePhoneme}' not found in oto for tone {syllable.tone}. Original V: {syllable.v}, LastC: {lastC}");
                    // Fallback maybe? Add V if C exists? Or just V?
                    if (lastC != null && HasOto(lastC, syllable.tone)) phonemes.Add(lastC);
                    if (HasOto(syllable.v, syllable.tone)) phonemes.Add(syllable.v);
                }

            }
            return phonemes;
        }

        protected override List<string> ProcessEnding(Ending ending) {
            var phonemes = new List<string>();
            string prevV = ending.prevV; // Already replaced e.g., "a", "e", "i"
            string[] cc = ending.cc;    // Already replaced e.g., ["k"], ["s", "t"]

            if (ending.IsEndingV) {
                // Word ends in a vowel: Add [V-]
                Console.WriteLine("hi");
                
            } else { // Word ends in consonant(s)
                // First, add the vowel decay [V-] if it exists
                var vEndAlias = $"{prevV}-";
                if (HasOto(vEndAlias, ending.tone)) {
                    phonemes.Add(vEndAlias);
                } else {
                    // Fallback if V- doesn't exist - this might lead to abrupt sound before C-
                    Log.Debug($"Ending V- alias {vEndAlias} not found before C- in tone {ending.tone}");
                    // Optionally add the plain V as a fallback before C-?
                    // if (HasOto(prevV, ending.tone)) {
                    //    phonemes.Add(prevV);
                    // }
                }

                // Then, add the final consonant decay [C-] for the *last* consonant
                string lastC = cc.LastOrDefault();
                if (lastC != null) {
                    var cEndAlias = $"{lastC}-";
                    if (HasOto(cEndAlias, ending.tone)) {
                        phonemes.Add(cEndAlias);
                    } else {
                        // Fallback if C- doesn't exist: add C?
                        if (HasOto(lastC, ending.tone)) {
                            phonemes.Add(lastC);
                            Log.Debug($"Ending C- alias not found: {cEndAlias}, using C fallback: {lastC} in tone {ending.tone}");
                        } else {
                            Log.Debug($"Ending C- alias {cEndAlias} and C fallback {lastC} not found in tone {ending.tone}");
                        }
                    }
                }
            }
            return phonemes;
        }

        protected override string ValidateAlias(string alias) {
            // Apply fallbacks if any defined
            return aliasesFallback.ContainsKey(alias) ? aliasesFallback[alias] : alias;
        }
    }
}
