using OpenUtau.Core.Ustx;

namespace OpenUtau.Core {
    public readonly struct RenderInvalidation {
        public readonly UVoicePart part;
        public readonly int startTick;
        public readonly int endTick;

        public RenderInvalidation(UVoicePart part, int startTick, int endTick) {
            this.part = part;
            this.startTick = startTick;
            this.endTick = endTick;
        }

        public bool IsValid => part != null && endTick > startTick;
    }
}
