namespace OpenUtau.Core.Format {
    public class SvpImportOptions {
        public bool Pitch { get; set; } = true;
        public bool Dynamics { get; set; } = true;
        public bool Tension { get; set; } = true;
        public bool Breathiness { get; set; } = true;
        public bool Gender { get; set; } = true;
        public bool Voicing { get; set; } = true;
        public bool ToneShift { get; set; } = true;
        public bool MouthOpening { get; set; } = true;
        public bool VocalModes { get; set; } = true;
        public bool PhonemeHints { get; set; } = true;
        public bool Vibrato { get; set; } = true;
    }
}
