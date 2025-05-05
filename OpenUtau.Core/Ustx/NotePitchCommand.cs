using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenUtau.Core.Ustx {
    public class NotePitchCommand : UCommand {
        private readonly UNote note;
        private readonly List<PitchPoint> newPitchPoints;
        private readonly bool newSnapFirst;
        private readonly List<PitchPoint> oldPitchPoints;
        private readonly bool oldSnapFirst;

        public NotePitchCommand(UNote note, List<PitchPoint> newPitchPoints, bool newSnapFirst) {
            this.note = note;
            this.newPitchPoints = newPitchPoints.Select(p => new PitchPoint(p.X, p.Y, p.shape)).ToList();
            this.newSnapFirst = newSnapFirst;
            this.oldPitchPoints = note.pitch.data.Select(p => new PitchPoint(p.X, p.Y, p.shape)).ToList();
            this.oldSnapFirst = note.pitch.snapFirst;
        }

        public override void Execute() {
            note.pitch.data = newPitchPoints.Select(p => new PitchPoint(p.X, p.Y, p.shape)).ToList();
            note.pitch.snapFirst = newSnapFirst;
        }

        public override void Unexecute() {
            note.pitch.data = oldPitchPoints.Select(p => new PitchPoint(p.X, p.Y, p.shape)).ToList();
            note.pitch.snapFirst = oldSnapFirst;
        }

        public override string ToString() => "Set note pitch";
    }
}
