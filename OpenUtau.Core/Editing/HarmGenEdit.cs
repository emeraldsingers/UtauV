using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Core.Editing {
    public class AutoHarmonies : BatchEdit {
        public string Name => "Auto Harmonies";

        private int harmonyType; 
        private int semitoneInterval;

        public AutoHarmonies(int harmonyType, int semitoneInterval) {
            this.harmonyType = harmonyType;
            this.semitoneInterval = semitoneInterval;
        }

        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();

            if (notes.Count == 0)
                return;

            var (detectedKeyToneIndex, keyMode) = DetectKey(notes);

            var harmonyNotesList = GenerateAllHarmonyNotes(notes, detectedKeyToneIndex, keyMode);

            if (harmonyNotesList.Count == 0)
                return;

            AddHarmonyTracksAndParts(project, part, harmonyNotesList, docManager);
        }

        private (int keyToneIndex, string keyMode) DetectKey(IList<UNote> notes) {
            int[] noteCounts = new int[12];
            foreach (var note in notes) {
                int tone = note.tone % 12;
                noteCounts[tone]++;
            }

            double[] majorProfile = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 3.78, 2.14, 4.04, 2.0, 3.5 };
            double[] minorProfile = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 2.53, 4.48, 2.42, 3.17, 2.35 };
            string[] keyNames = { "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B" };

            var scores = new List<(string key, double score)>();
            for (int keyIndex = 0; keyIndex < 12; keyIndex++) {
                double majorScore = 0;
                double minorScore = 0;
                for (int i = 0; i < 12; i++) {
                    int noteIndex = (keyIndex + i) % 12;
                    majorScore += noteCounts[noteIndex] * majorProfile[i];
                    minorScore += noteCounts[noteIndex] * minorProfile[i];
                }
                scores.Add(($"{keyNames[keyIndex]} Major", majorScore));
                scores.Add(($"{keyNames[keyIndex]} Minor", minorScore));
            }

            var bestScore = scores.OrderByDescending(s => s.score).First();
            string bestKey = bestScore.key;
            int keyToneIndex = Array.IndexOf(keyNames, bestKey.Split(' ')[0]);
            string keyMode = bestKey.Split(' ')[1].ToLower();

            return (keyToneIndex, keyMode);
        }

        private List<(List<UNote> notes, string name)> GenerateAllHarmonyNotes(IList<UNote> originalNotes, int keyToneIndex, string keyMode) {
            var harmonyNotesList = new List<(List<UNote> notes, string name)>();

            if (harmonyType == 1) {
                harmonyNotesList.Add((GenerateHarmonyNotes(originalNotes, -semitoneInterval, keyToneIndex, keyMode), "Lower Harmony"));
            } else if (harmonyType == 2) {
                harmonyNotesList.Add((GenerateHarmonyNotes(originalNotes, semitoneInterval, keyToneIndex, keyMode), "Upper Harmony"));
            } else if (harmonyType == 3) {
                harmonyNotesList.Add((GenerateHarmonyNotes(originalNotes, -semitoneInterval, keyToneIndex, keyMode), "Lower Harmony"));
                harmonyNotesList.Add((GenerateHarmonyNotes(originalNotes, semitoneInterval, keyToneIndex, keyMode), "Upper Harmony"));
            }

            return harmonyNotesList;
        }

        private List<UNote> GenerateHarmonyNotes(IList<UNote> originalNotes, int semitoneShift, int keyToneIndex, string keyMode) {
            var harmonyNotes = new List<UNote>();
            int direction = semitoneShift < 0 ? 1 : -1;

            foreach (var note in originalNotes) {
                int harmonyTone = note.tone + semitoneShift;
                int originalHarmonyTone = harmonyTone;
                int correctionAttempts = 0;

                while (!IsNoteInKey(harmonyTone, keyToneIndex, keyMode) && correctionAttempts < 3) {
                    harmonyTone += direction;
                    correctionAttempts++;
                }

                if (!IsNoteInKey(harmonyTone, keyToneIndex, keyMode)) {
                    harmonyTone = originalHarmonyTone;
                }

                var harmonyNote = note.Clone() as UNote;
                harmonyNote.tone = harmonyTone;
                harmonyNotes.Add(harmonyNote);
            }

            return harmonyNotes;
        }

        private bool IsNoteInKey(int tone, int keyToneIndex, string mode) {
            var scale = GetScaleIntervals(keyToneIndex, mode);
            int noteClass = tone % 12;
            return scale.Contains(noteClass);
        }

        private List<int> GetScaleIntervals(int keyToneIndex, string mode) {
            List<int> intervals = mode switch {
                "major" => new List<int> { 0, 2, 4, 5, 7, 9, 11 },
                "minor" => new List<int> { 0, 2, 3, 5, 7, 8, 10 },
                _ => new List<int>()
            };
            return intervals.Select(i => (keyToneIndex + i) % 12).ToList();
        }

        private void AddHarmonyTracksAndParts(UProject project, UVoicePart originalPart, List<(List<UNote> notes, string name)> harmonyNotesList, DocManager docManager) {
            docManager.StartUndoGroup(true);
            var originalTrack = project.tracks[originalPart.trackNo];

            foreach (var (harmonyNotes, harmonyName) in harmonyNotesList) {
                var newTrack = new UTrack(project) {
                    TrackNo = project.tracks.Count,
                    TrackName = $"{originalTrack.TrackName} - {harmonyName}",
                    Singer = originalTrack.Singer,
                    RendererSettings = originalTrack.RendererSettings
                };
                docManager.ExecuteCmd(new AddTrackCommand(project, newTrack));

                var newPart = new UVoicePart {
                    name = $"{newTrack.TrackName} Part",
                    trackNo = newTrack.TrackNo,
                    position = originalPart.position,
                    notes = new SortedSet<UNote>(harmonyNotes)
                };
                newPart.Duration = originalPart.Duration;
                docManager.ExecuteCmd(new AddPartCommand(project, newPart));
            }

            docManager.EndUndoGroup();
        }
    }
}
