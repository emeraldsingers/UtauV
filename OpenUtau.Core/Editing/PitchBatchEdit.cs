using System;
using System.Collections.Generic;
using System.Linq;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;

namespace OpenUtau.Core.Editing {
    public class PitchBatchEdit : BatchEdit {
        public virtual string Name => name;

        private string name;

        public PitchBatchEdit() {
            name = "PitchBend to PITD WIP!!!";
        }
        public void Run(UProject project, UVoicePart part, List<UNote> selectedNotes, DocManager docManager) {
            var notes = selectedNotes.Count > 0 ? selectedNotes : part.notes.ToList();
            docManager.StartUndoGroup(true);

            float minPitD = -200;
            float maxPitD = 200;
            if (project.expressions.TryGetValue(Format.Ustx.PITD, out var descriptor)) {
                minPitD = descriptor.min;
                maxPitD = descriptor.max;
            }

            foreach (var note in notes) {
                if (note.pitch.data.Count > 0) {
                    var pitchData = note.pitch.data;
                    var phrasePitches = new List<float>();

                    for (int i = 0; i < pitchData.Count - 1; i++) {
                        var startPoint = pitchData[i];
                        var endPoint = pitchData[i + 1];

                        int startTick = note.position + (int)startPoint.X;
                        int endTick = note.position + (int)endPoint.X;
                        float startPitch = (float)(startPoint.Y * 10);
                        float endPitch = (float)(endPoint.Y * 10);

                        startPitch = Math.Clamp(startPitch, minPitD, maxPitD);
                        endPitch = Math.Clamp(endPitch, minPitD, maxPitD);

                        for (int tick = startTick; tick <= endTick; tick += 5) {
                            float t = (float)(tick - startTick) / (endTick - startTick);
                            float smoothT = (float)(1 - Math.Cos(t * Math.PI)) / 2;
                            float interpolatedPitch = startPitch + smoothT * (endPitch - startPitch);
                            phrasePitches.Add(interpolatedPitch);
                        }
                    }

                    for (int i = 0; i < phrasePitches.Count; i++) {
                        int tick = note.position + i * 5;
                        float pitch = phrasePitches[i];

                        if (i > 0) {
                            float prevPitch = phrasePitches[i - 1];
                            if (Math.Abs(prevPitch - pitch) > 20) {
                                pitch = prevPitch + (pitch - prevPitch) / 2;
                            }
                        }

                        if (i == 0 && phrasePitches.Count > 1) {
                            if (phrasePitches.Count > 1) {
                                float nextPitch = phrasePitches[i + 1];
                                pitch = phrasePitches[i] + (nextPitch - phrasePitches[i]) / 2;
                            }
                        }

                        if (i > 0 && Math.Abs(phrasePitches[i] - phrasePitches[i - 1]) > 10) {
                            pitch = phrasePitches[i - 1] + (pitch - phrasePitches[i - 1]) / 2;
                        }

                        int lastX = (i > 0) ? tick - 5 : tick;
                        int lastY = (i > 0) ? (int)Math.Round(phrasePitches[i - 1]) : (int)Math.Round(pitch);

                        docManager.ExecuteCmd(new SetCurveCommand(
                            project, part, Format.Ustx.PITD,
                            tick - part.position, (int)Math.Round(pitch),
                            lastX - part.position, lastY
                        ));
                    }
                }
            }

            docManager.EndUndoGroup();
        }



        // Метод линейной интерполяции
        public static float Lerp(float start, float end, float t) {
            return start + (end - start) * t;
        }


    }

}
