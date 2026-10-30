using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenUtau.Classic;
using OpenUtau.Core.Ustx;
using Serilog;

namespace OpenUtau.Core.Coeiroink {
    public class CoeiroinkSinger : USinger {
        public override string Id => voicebank.Id;
        public override string Name => voicebank.Name;
        public override Dictionary<string, string> LocalizedNames => voicebank.LocalizedNames;
        public override IList<string> SearchTerms => voicebank.SearchTerms;
        public override USingerType SingerType => voicebank.SingerType;
        public override string BasePath => voicebank.BasePath;
        public override string Author => voicebank.Author;
        public override string Team => voicebank.Team;
        public override string Voice => voicebank.Voice;
        public override string Location => Path.GetDirectoryName(voicebank.File);
        public override string Web => voicebank.Web;
        public override string Version => string.IsNullOrEmpty(voicebank.Version) ? config?.Version ?? string.Empty : voicebank.Version;
        public override string OtherInfo => voicebank.OtherInfo;
        public override IList<string> Errors => errors;
        public override string Avatar => AvatarPath();
        public override byte[] AvatarData => avatarData;
        public override string Portrait => PortraitPath();
        public override float PortraitOpacity => voicebank.PortraitOpacity;
        public override int PortraitHeight => voicebank.PortraitHeight;
        public override string Sample => voicebank.Sample == null ? SampleFromFolder() : Path.Combine(Location, voicebank.Sample);
        public override string DefaultPhonemizer => voicebank.DefaultPhonemizer;
        public override Encoding TextFileEncoding => voicebank.TextFileEncoding;
        public override IList<USubbank> Subbanks => subbanks;
        public override IList<UOto> Otos => otos;

        public CoeiroinkConfig? Config => config;

        Voicebank voicebank;
        CoeiroinkConfig? config;
        List<string> errors = new List<string>();
        List<USubbank> subbanks = new List<USubbank>();
        List<UOto> otos = new List<UOto>();
        byte[]? avatarData;

        public CoeiroinkSinger(Voicebank voicebank) {
            this.voicebank = voicebank;
            found = true;
        }

        public override void EnsureLoaded() {
            if (Loaded) {
                return;
            }
            Reload();
        }

        public override void Reload() {
            if (!Found) {
                return;
            }
            errors.Clear();
            try {
                voicebank.Reload();
                Load();
                loaded = true;
            } catch (Exception e) {
                errors.Add($"Failed to load {voicebank.File}: {e.Message}");
                Log.Error(e, $"Failed to load {voicebank.File}");
            }
        }

        void Load() {
            config = CoeiroinkConfig.Load(Location);
            subbanks.Clear();
            otos.Clear();
            subbanks.Add(new USubbank(new Subbank() {
                Prefix = string.Empty,
                Suffix = string.Empty,
                ToneRanges = new[] { "C1-B7" },
                Color = string.Empty,
            }));
            foreach (var style in config.Styles) {
                subbanks.Add(new USubbank(new Subbank() {
                    Prefix = string.Empty,
                    Suffix = style.Name,
                    ToneRanges = new[] { "C1-B7" },
                    Color = style.Name,
                }));
                if (!style.IsExported) {
                    errors.Add($"ONNX models of the style \"{style.Name}\" are missing. Export them with {CoeiroinkConfig.ExporterUrl}");
                }
            }
            if (config.Styles.Count == 0) {
                errors.Add("metas.json has no styles.");
            }
            var seen = new HashSet<string>();
            foreach (var kana in CoeiroinkKana.AllKana) {
                if (seen.Add(kana)) {
                    otos.Add(UOto.OfDummy(kana));
                }
            }
            LoadAvatar();
        }

        public CoeiroinkStyle? GetStyle(string? suffix) {
            if (config == null || config.Styles.Count == 0) {
                return null;
            }
            if (!string.IsNullOrEmpty(suffix)) {
                var match = config.Styles.FirstOrDefault(s => s.Name == suffix);
                if (match != null) {
                    return match;
                }
            }
            return config.Styles[0];
        }

        string AvatarPath() {
            if (voicebank.Image != null) {
                return Path.Combine(Location, voicebank.Image);
            }
            return config?.Styles.FirstOrDefault(s => File.Exists(s.Icon))?.Icon;
        }

        string PortraitPath() {
            if (voicebank.Portrait != null) {
                return Path.Combine(Location, voicebank.Portrait);
            }
            var path = Path.Combine(Location, "portrait.png");
            return File.Exists(path) ? path : null;
        }

        string SampleFromFolder() {
            var dir = Path.Combine(Location, "voice_samples");
            if (!Directory.Exists(dir)) {
                return null;
            }
            return Directory.EnumerateFiles(dir, "*.wav").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        void LoadAvatar() {
            var path = Avatar;
            if (path != null && File.Exists(path)) {
                try {
                    avatarData = File.ReadAllBytes(path);
                } catch (Exception e) {
                    avatarData = null;
                    Log.Error(e, "Failed to load avatar data.");
                }
            } else {
                avatarData = null;
            }
        }

        public override bool TryGetOto(string phoneme, out UOto oto) {
            if (phoneme != null && (CoeiroinkKana.TryParse(phoneme, out _) || CoeiroinkKana.IsConsonantToken(phoneme) || CoeiroinkKana.IsRest(phoneme))) {
                oto = UOto.OfDummy(phoneme);
                return true;
            }
            oto = null;
            return false;
        }

        public override Dictionary<string, UOto> GetSuggestions(string text, bool isAlias) {
            bool all = string.IsNullOrEmpty(text);
            return otos
                .Where(o => all || o.Alias.Contains(text))
                .ToDictionary(o => o.Alias, o => o);
        }

        public override byte[] LoadPortrait() {
            return string.IsNullOrEmpty(Portrait) || !File.Exists(Portrait)
                ? null
                : File.ReadAllBytes(Portrait);
        }

        public override byte[] LoadSample() {
            return string.IsNullOrEmpty(Sample) || !File.Exists(Sample)
                ? null
                : File.ReadAllBytes(Sample);
        }
    }
}
