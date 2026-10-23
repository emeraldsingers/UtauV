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

        const string pluginsBody = """
        [
          {
            "id": "autocontextualalts",
            "name": "Auto Contextual Alts",
            "category": "Batch Edits",
            "developers": ["Cadlaxa"],
            "long_description": "Aligns allophone alts.",
            "homepage_url": "https://github.com/Cadlaxa/Batch-OpenUTAU-Plugins",
            "versions": [
              { "version": "1.0.0", "description": "Initial Release" }
            ]
          }
        ]
        """;

        const string packagesBody = """
        {
          "registry": "utauv",
          "kind": "softwares-all",
          "version": 1,
          "count": 2,
          "items": [
            {
              "id": "rmvpe",
              "category": "host_extension",
              "tags": ["openutau", "oudep", "rmvpe"]
            },
            {
              "id": "autopitchcurve",
              "category": "host_extension",
              "tags": ["openutau", "oudep", "autopitch"]
            }
          ]
        }
        """;

        [Fact]
        public void PluginRegistryMapsFields() {
            // RegistrySoftware is built from public fields, and System.Text.Json only
            // fills properties unless IncludeFields is set. Without it every entry came
            // back with an empty id, which made whole categories look empty.
            var parsed = PackageManager.ParseRegistryBodyForTest(pluginsBody, "test");
            var plugin = Assert.Single(parsed);
            Assert.Equal("autocontextualalts", plugin.id);
            Assert.Equal("Auto Contextual Alts", plugin.LocalizedName());
            Assert.Equal("Batch Edits", plugin.category);
            Assert.Equal("Cadlaxa", Assert.Single(plugin.developers));
            Assert.Contains("Aligns", plugin.long_description);
            Assert.Equal("https://github.com/Cadlaxa/Batch-OpenUTAU-Plugins", plugin.homepage_url);
            var version = Assert.Single(plugin.versions);
            Assert.Equal("1.0.0", version.version);
        }

        [Fact]
        public void PackageRegistryMapsFields() {
            var parsed = PackageManager.ParseRegistryBodyForTest(packagesBody, "test");
            Assert.Equal(2, parsed.Count);
            var rmvpe = parsed.Single(s => s.id == "rmvpe");
            Assert.Equal("host_extension", rmvpe.category);
            Assert.Contains("oudep", rmvpe.tags);
            var curve = parsed.Single(s => s.id == "autopitchcurve");
            Assert.Contains("oudep", curve.tags);
        }

        [Fact]
        public void RegistryFieldsMapCaseInsensitively() {
            var body = """
            [ { "Id": "cased", "Name": "Cased", "Category": "Batch Edits" } ]
            """;
            var parsed = PackageManager.ParseRegistryBodyForTest(body, "test");
            var entry = Assert.Single(parsed);
            Assert.Equal("cased", entry.id);
            Assert.Equal("Cased", entry.LocalizedName());
            Assert.Equal("Batch Edits", entry.category);
        }
    }
}
