namespace OpenUtau.App.Views {
    public static class PhonemeNameExtensions // Extension class for phoneme name checks
    {
        public static bool StartsWithVowel(this string phonemeName) {
            string vowels = "aeiou"; // Basic vowels, extend as needed for your language
            return !string.IsNullOrEmpty(phonemeName) && vowels.Contains(phonemeName.ToLower()[0]);
        }

        public static bool StartsWithSibilant(this string phonemeName) {
            string sibilants = "sfshz"; // Basic sibilants, extend as needed
            return !string.IsNullOrEmpty(phonemeName) && sibilants.Contains(phonemeName.ToLower()[0]);
        }
    }
}
