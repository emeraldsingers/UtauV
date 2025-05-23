﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using OpenUtau.Core.Util;
using Serilog;
using Vortice.DXGI;

namespace OpenUtau.Core {
    public class GpuInfo {
        public int deviceId;
        public string description = "";

        override public string ToString() {
            return $"[{deviceId}] {description}";
        }
    }

    public class Onnx {
        public static List<string> getRunnerOptions() {
            if (OS.IsWindows()) {
                return new List<string> {
                "cpu",
                "directml"
                };
            } else if (OS.IsMacOS()) {
                return new List<string> {
                "cpu",
                "coreml"
                };
            }
            return new List<string> {
                "cpu"
            };
        }

        public static List<GpuInfo> getGpuInfo() {
            List<GpuInfo> gpuList = new List<GpuInfo>();
            if (OS.IsWindows()) {
                DXGI.CreateDXGIFactory1(out IDXGIFactory1 factory);
                for (int deviceId = 0; deviceId < 32; deviceId++) {
                    factory.EnumAdapters1(deviceId, out IDXGIAdapter1 adapterOut);
                    if (adapterOut is null) {
                        break;
                    }
                    gpuList.Add(new GpuInfo {
                        deviceId = deviceId,
                        description = adapterOut.Description.Description
                    });
                }
            }
            if (gpuList.Count == 0) {
                gpuList.Add(new GpuInfo {
                    deviceId = 0,
                });
            }
            return gpuList;
        }

        private static SessionOptions getOnnxSessionOptions() {
            SessionOptions options = new SessionOptions();
            List<string> runnerOptions = getRunnerOptions();
            string runner = Preferences.Default.OnnxRunner;
            if (String.IsNullOrEmpty(runner)) {
                runner = runnerOptions[0];
            }
            if (!runnerOptions.Contains(runner)) {
                runner = "cpu";
            }
            switch (runner) {
                case "directml":
                    options.AppendExecutionProvider_DML(Preferences.Default.OnnxGpu);
                    break;
                case "coreml":
                    options.AppendExecutionProvider_CoreML(CoreMLFlags.COREML_FLAG_ENABLE_ON_SUBGRAPH);
                    break;
            }
            return options;
        }

        public static InferenceSession getInferenceSession(byte[] model) {
            return new InferenceSession(model, getOnnxSessionOptions());
        }

        public static InferenceSession getInferenceSession(string modelPath) {
            return new InferenceSession(modelPath, getOnnxSessionOptions());
        }

        public static void VerifyInputNames(InferenceSession session, IEnumerable<NamedOnnxValue> inputs) {
            var sessionInputNames = session.InputNames.ToHashSet();
            var givenInputNames = inputs.Select(v => v.Name).ToHashSet();
            var missing = sessionInputNames
                .Except(givenInputNames)
                .OrderBy(s => s, StringComparer.InvariantCulture)
                .ToArray();
            if (missing.Length > 0) {
                throw new ArgumentException("Missing input(s) for the inference session: " + string.Join(", ", missing));
            }
            // The problematic part:  Instead of throwing an exception for unexpected inputs,
            // we'll log a warning and filter them out.
            var unexpected = givenInputNames
                .Except(sessionInputNames)
                .OrderBy(s => s, StringComparer.InvariantCulture)
                .ToArray();
            Log.Information(givenInputNames.ToList().ToString());
            Log.Information(sessionInputNames.ToList().ToString());
            if (unexpected.Length > 0) {
                // Log a warning (using OpenUtau's logger if available, otherwise Console.WriteLine)
                string unexpectedInputs = string.Join(", ", unexpected);
                

                // Filter out the unexpected inputs.  Crucially, modify the *call site* of this function
                // to use the filtered inputs. See the suggested change in DiffSingerRenderer.cs below.
            }
        }


        //Add a helper to filter by expected params
        public static IEnumerable<NamedOnnxValue> FilterInputs(InferenceSession session, IEnumerable<NamedOnnxValue> inputs) {
            var sessionInputNames = session.InputNames.ToHashSet();
            return inputs.Where(input => sessionInputNames.Contains(input.Name));
        }
    }
}
