using System;
using System.Linq;
using System.Threading.Tasks;
using OpenUtau.Core;
using Xunit;

namespace OpenUtau.Test.Core {
    public class RegistrySourceTest {
        const string voicebanksBody = """
        {
          "registry": "utauv",
          "kind": "singers-registry",
          "version": 2,
          "singers": {
            "visingers-awata-weak": {
              "name": "Awata Weak",
              "team": "ViSingers",
              "website_url": "https://visingers.github.io/",
              "link": "https://visingers.github.io/hhskt/Awata_Weak/",
              "description": "Awata Weak is a robot designed to look like a young girl.",
              "long_description": "A multilingual DiffSinger voicebank. For Russian, this phonemizer is required: https://github.com/Megageorgio/hhskt\_ru\_phonemizer",
              "voicebanks": {
                "DiffSinger": {
                  "id": "visingers-awata-weak-ds",
                  "description": "DiffSinger voicebank package",
                  "url": "https://github.com/hhskt/Awata_Weak/releases/download/diffsinger_v3.0/Awata_Weak_DS_v3.zip",
                  "supportedLanguages": ["en", "ja", "ko", "pt", "ru", "zh"],
                  "types": ["DiffSinger"],
                  "engines": ["DiffSinger"],
                  "install_subdir": "Awata_Weak_DS_v3"
                }
              }
            }
          }
        }
        """;

        [Fact]
        public void VoicebankRegistryToleratesInvalidEscapes() {
            // Markdown descriptions in hand-edited registry files use "\_" to escape an
            // underscore, which strict JSON rejects. The entry must still load.
            var parsed = PackageManager.ParseRegistryBodyForTest(voicebanksBody, "test");
            var singer = Assert.Single(parsed);
            Assert.Contains("hhskt_ru_phonemizer", singer.group_long_description);
            Assert.DoesNotContain("\\", singer.group_long_description);
        }

        [Fact]
        public void VoicebankRegistryKeepsValidEscapes() {
            var body = """
            {
              "registry": "utauv",
              "kind": "singers-registry",
              "singers": {
                "esc": {
                  "name": "Escapes",
                  "description": "quote \" backslash \\ tab \t newline \n end",
                  "voicebanks": {
                    "DiffSinger": { "id": "esc-ds", "url": "https://example.invalid/esc.zip" }
                  }
                }
              }
            }
            """;
            var parsed = PackageManager.ParseRegistryBodyForTest(body, "test");
            var singer = Assert.Single(parsed);
            Assert.Contains("\"", singer.description);
            Assert.Contains("\\", singer.description);
            Assert.Contains("\t", singer.description);
            Assert.Contains("\n", singer.description);
        }
    }
}
