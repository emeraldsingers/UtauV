using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace OpenUtau.Core.DiffSinger {
    internal sealed class VariancePatchState {
        public readonly float[] pitch;
        public readonly float[]? speakerEmbed;
        public readonly VarianceResult result;

        public VariancePatchState(float[] pitch, float[]? speakerEmbed, VarianceResult result) {
            this.pitch = pitch.ToArray();
            this.speakerEmbed = speakerEmbed?.ToArray();
            this.result = DiffSingerVariancePatch.CloneResult(result);
        }
    }

    internal sealed class VariancePatchStateCache {
        readonly int capacity;
        readonly Dictionary<ulong, LinkedListNode<(ulong key, VariancePatchState state)>> entries = new();
        readonly LinkedList<(ulong key, VariancePatchState state)> recency = new();

        internal VariancePatchStateCache(int capacity) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            this.capacity = capacity;
        }

        internal int Count => entries.Count;

        internal bool TryGetValue(ulong key, out VariancePatchState state) {
            if (!entries.TryGetValue(key, out var node)) {
                state = null!;
                return false;
            }
            recency.Remove(node);
            recency.AddFirst(node);
            state = node.Value.state;
            return true;
        }

        internal void Set(ulong key, VariancePatchState state) {
            if (entries.TryGetValue(key, out var existing)) {
                existing.Value = (key, state);
                recency.Remove(existing);
                recency.AddFirst(existing);
                return;
            }
            var node = recency.AddFirst((key, state));
            entries.Add(key, node);
            if (entries.Count <= capacity) {
                return;
            }
            var oldest = recency.Last!;
            recency.RemoveLast();
            entries.Remove(oldest.Value.key);
        }
    }

    internal static class DiffSingerVariancePatch {
        const string StateCacheVersionName = "variance_patch_state_version";
        const string StatePitchName = "variance_patch_pitch";
        const string StateSpeakerEmbedName = "variance_patch_speaker_embed";
        const string StateEnergyName = "variance_patch_energy";
        const string StateBreathinessName = "variance_patch_breathiness";
        const string StateVoicingName = "variance_patch_voicing";
        const string StateTensionName = "variance_patch_tension";
        const long StateCacheVersion = 1;

        public static ulong BuildStateKey(ulong baseHash, int phrasePosition, int phraseEnd) {
            unchecked {
                ulong hash = baseHash;
                hash = (hash ^ (uint)phrasePosition) * 1099511628211UL;
                hash = (hash ^ (uint)phraseEnd) * 1099511628211UL;
                return hash;
            }
        }

        internal static List<NamedOnnxValue> BuildStateCacheKeyInputs() {
            return new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(
                    StateCacheVersionName,
                    new DenseTensor<long>(new[] { StateCacheVersion }, new[] { 1 }, false)),
            };
        }

        internal static List<NamedOnnxValue> BuildStateCacheOutputs(VariancePatchState state) {
            var outputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(
                    StateCacheVersionName,
                    new DenseTensor<long>(new[] { StateCacheVersion }, new[] { 1 }, false)),
                FloatTensor(StatePitchName, state.pitch),
            };
            AddFloatTensor(outputs, StateSpeakerEmbedName, state.speakerEmbed);
            AddFloatTensor(outputs, StateEnergyName, state.result.energy);
            AddFloatTensor(outputs, StateBreathinessName, state.result.breathiness);
            AddFloatTensor(outputs, StateVoicingName, state.result.voicing);
            AddFloatTensor(outputs, StateTensionName, state.result.tension);
            return outputs;
        }

        internal static bool TryParseStateCacheOutputs(
            ICollection<NamedOnnxValue> outputs,
            float frameMs,
            int headFrames,
            int tailFrames,
            int totalFrames,
            out VariancePatchState state) {
            state = null!;
            try {
                var version = outputs.FirstOrDefault(value => value.Name == StateCacheVersionName)
                    ?.AsTensor<long>()
                    .ToArray();
                var pitch = GetFloatTensor(outputs, StatePitchName);
                if (version is not { Length: 1 } || version[0] != StateCacheVersion || pitch == null) {
                    return false;
                }
                var result = new VarianceResult {
                    energy = GetFloatTensor(outputs, StateEnergyName),
                    breathiness = GetFloatTensor(outputs, StateBreathinessName),
                    voicing = GetFloatTensor(outputs, StateVoicingName),
                    tension = GetFloatTensor(outputs, StateTensionName),
                    frameMs = frameMs,
                    headFrames = headFrames,
                    tailFrames = tailFrames,
                    totalFrames = totalFrames,
                };
                state = new VariancePatchState(
                    pitch,
                    GetFloatTensor(outputs, StateSpeakerEmbedName),
                    result);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        internal static bool[] BuildChangedFrameMask(
            IReadOnlyList<float> previous,
            IReadOnlyList<float> current,
            float epsilon) {
            int length = Math.Max(previous.Count, current.Count);
            var mask = new bool[length];
            for (int i = 0; i < length; i++) {
                mask[i] = i >= previous.Count || i >= current.Count ||
                    Math.Abs(previous[i] - current[i]) > epsilon;
            }
            return mask;
        }

        internal static bool[] BuildChangedFrameMask(
            IReadOnlyList<float> previous,
            IReadOnlyList<float> current,
            int frameCount,
            float epsilon) {
            if (frameCount <= 0) {
                return Array.Empty<bool>();
            }
            if (previous.Count != current.Count || previous.Count % frameCount != 0) {
                return Enumerable.Repeat(true, frameCount).ToArray();
            }
            int valuesPerFrame = previous.Count / frameCount;
            var mask = new bool[frameCount];
            for (int frame = 0; frame < frameCount; frame++) {
                int offset = frame * valuesPerFrame;
                for (int i = 0; i < valuesPerFrame; i++) {
                    if (Math.Abs(previous[offset + i] - current[offset + i]) > epsilon) {
                        mask[frame] = true;
                        break;
                    }
                }
            }
            return mask;
        }

        internal static bool[] ExpandToChannels(
            IReadOnlyList<bool> frameMask,
            int channelCount) {
            if (channelCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(channelCount));
            }
            var mask = new bool[frameMask.Count * channelCount];
            for (int frame = 0; frame < frameMask.Count; frame++) {
                if (!frameMask[frame]) continue;
                for (int channel = 0; channel < channelCount; channel++) {
                    mask[frame * channelCount + channel] = true;
                }
            }
            return mask;
        }

        internal static VarianceResult HardCompose(
            VarianceResult previous,
            VarianceResult predicted,
            IReadOnlyList<bool> retakeMask,
            int channelCount) {
            if (!IsCompatible(previous, predicted) ||
                retakeMask.Count != previous.totalFrames * channelCount) {
                return CloneResult(predicted);
            }
            int channel = 0;
            var energy = ComposeEnabledChannel(previous.energy, predicted.energy, retakeMask, previous.totalFrames, ref channel, channelCount);
            var breathiness = ComposeEnabledChannel(previous.breathiness, predicted.breathiness, retakeMask, previous.totalFrames, ref channel, channelCount);
            var voicing = ComposeEnabledChannel(previous.voicing, predicted.voicing, retakeMask, previous.totalFrames, ref channel, channelCount);
            var tension = ComposeEnabledChannel(previous.tension, predicted.tension, retakeMask, previous.totalFrames, ref channel, channelCount);
            return new VarianceResult {
                energy = energy,
                breathiness = breathiness,
                voicing = voicing,
                tension = tension,
                frameMs = predicted.frameMs,
                headFrames = predicted.headFrames,
                tailFrames = predicted.tailFrames,
                totalFrames = predicted.totalFrames,
            };
        }

        static float[]? ComposeEnabledChannel(
            float[]? previous,
            float[]? predicted,
            IReadOnlyList<bool> mask,
            int frameCount,
            ref int channel,
            int channelCount) {
            if (previous == null && predicted == null) {
                return null;
            }
            int currentChannel = channel++;
            return ComposeChannel(previous, predicted, mask, frameCount, currentChannel, channelCount);
        }

        static float[]? ComposeChannel(
            float[]? previous,
            float[]? predicted,
            IReadOnlyList<bool> mask,
            int frameCount,
            int channel,
            int channelCount) {
            if (previous == null || predicted == null) {
                return predicted?.ToArray();
            }
            if (previous.Length != frameCount || predicted.Length != frameCount) {
                return predicted.ToArray();
            }
            var result = previous.ToArray();
            for (int frame = 0; frame < frameCount; frame++) {
                if (mask[frame * channelCount + channel]) {
                    result[frame] = predicted[frame];
                }
            }
            return result;
        }

        internal static bool IsMetadataCompatible(VarianceResult previous, VarianceResult current) {
            return previous.totalFrames == current.totalFrames &&
                previous.headFrames == current.headFrames &&
                previous.tailFrames == current.tailFrames &&
                Math.Abs(previous.frameMs - current.frameMs) < 1e-4f;
        }

        internal static bool IsChannelLayoutCompatible(
            VarianceResult result,
            int totalFrames,
            bool predictEnergy,
            bool predictBreathiness,
            bool predictVoicing,
            bool predictTension) {
            return ChannelMatches(result.energy, predictEnergy, totalFrames) &&
                ChannelMatches(result.breathiness, predictBreathiness, totalFrames) &&
                ChannelMatches(result.voicing, predictVoicing, totalFrames) &&
                ChannelMatches(result.tension, predictTension, totalFrames);
        }

        internal static bool IsCompatible(VarianceResult previous, VarianceResult current) {
            return IsMetadataCompatible(previous, current) &&
                SameLength(previous.energy, current.energy) &&
                SameLength(previous.breathiness, current.breathiness) &&
                SameLength(previous.voicing, current.voicing) &&
                SameLength(previous.tension, current.tension);
        }

        static bool ChannelMatches(float[]? values, bool enabled, int totalFrames) {
            return enabled ? values?.Length == totalFrames : values == null;
        }

        static bool SameLength(float[]? a, float[]? b) {
            return (a == null) == (b == null) && (a == null || a.Length == b!.Length);
        }

        internal static VarianceResult CloneResult(VarianceResult result) {
            return new VarianceResult {
                energy = result.energy?.ToArray(),
                breathiness = result.breathiness?.ToArray(),
                voicing = result.voicing?.ToArray(),
                tension = result.tension?.ToArray(),
                frameMs = result.frameMs,
                headFrames = result.headFrames,
                tailFrames = result.tailFrames,
                totalFrames = result.totalFrames,
            };
        }

        static void AddFloatTensor(List<NamedOnnxValue> outputs, string name, float[]? values) {
            if (values != null) {
                outputs.Add(FloatTensor(name, values));
            }
        }

        static NamedOnnxValue FloatTensor(string name, float[] values) {
            return NamedOnnxValue.CreateFromTensor(
                name,
                new DenseTensor<float>(values, new[] { values.Length }, false)
                    .Reshape(new[] { 1, values.Length }));
        }

        static float[]? GetFloatTensor(ICollection<NamedOnnxValue> values, string name) {
            return values.FirstOrDefault(value => value.Name == name)
                ?.AsTensor<float>()
                .ToArray();
        }
    }
}
