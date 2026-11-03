using System;
using System.Collections.Generic;

namespace OpenUtau.Core.Coeiroink {
    public static class CoeiroinkMelWarp {
        public const int EdgeFrames = 3;

        public static double[] SourceIndexes(int source, int output, bool sustain) {
            var result = new double[output];
            if (source <= 0 || output <= 0) {
                return result;
            }
            if (output == source) {
                for (int i = 0; i < output; i++) {
                    result[i] = i;
                }
                return result;
            }
            int head = Math.Min(EdgeFrames, Math.Max(1, source / 3));
            int tail = head;
            int sourceMiddle = source - head - tail;
            int outputMiddle = output - head - tail;
            if (!sustain || output < source || sourceMiddle < 1 || outputMiddle < 1) {
                for (int i = 0; i < output; i++) {
                    result[i] = output == 1 ? 0 : i * (source - 1.0) / (output - 1);
                }
                return result;
            }
            for (int i = 0; i < head; i++) {
                result[i] = i;
            }
            for (int i = 0; i < outputMiddle; i++) {
                result[head + i] = head + (outputMiddle == 1 ? 0 : i * (sourceMiddle - 1.0) / (outputMiddle - 1));
            }
            for (int i = 0; i < tail; i++) {
                result[head + outputMiddle + i] = source - tail + i;
            }
            return result;
        }

        public static float[][] Warp(float[][] mel, IReadOnlyList<int> model, IReadOnlyList<int> output, IReadOnlyList<bool> sustain) {
            int total = 0;
            foreach (var frames in output) {
                total += frames;
            }
            var result = new float[total][];
            int sourceStart = 0;
            int outputStart = 0;
            for (int token = 0; token < model.Count; token++) {
                int source = model[token];
                var indexes = SourceIndexes(source, output[token], sustain[token]);
                for (int i = 0; i < indexes.Length; i++) {
                    result[outputStart + i] = Interpolate(mel, sourceStart, source, indexes[i]);
                }
                sourceStart += source;
                outputStart += output[token];
            }
            return result;
        }

        public static double[] OutputPositions(IReadOnlyList<int> model, IReadOnlyList<int> output) {
            int total = 0;
            foreach (var frames in model) {
                total += frames;
            }
            var result = new double[total];
            int index = 0;
            int outputStart = 0;
            for (int token = 0; token < model.Count; token++) {
                int source = model[token];
                for (int j = 0; j < source; j++) {
                    result[index++] = outputStart + (j + 0.5) * output[token] / source - 0.5;
                }
                outputStart += output[token];
            }
            return result;
        }

        static float[] Interpolate(float[][] mel, int start, int count, double position) {
            int last = mel.Length - 1;
            int lower = Math.Clamp(start + (int)Math.Floor(position), 0, last);
            int upper = Math.Clamp(Math.Min(lower + 1, start + count - 1), 0, last);
            double fraction = position - Math.Floor(position);
            var a = mel[lower];
            var b = mel[upper];
            var row = new float[a.Length];
            for (int k = 0; k < row.Length; k++) {
                row[k] = (float)(a[k] * (1 - fraction) + b[k] * fraction);
            }
            return row;
        }
    }
}
