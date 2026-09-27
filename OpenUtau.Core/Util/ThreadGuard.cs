using System;
using System.Diagnostics;
using System.Threading;

namespace OpenUtau.Core.Util {
    /// <summary>
    /// Debug-only thread-affinity asserts. Every function that must run on a
    /// specific context calls the matching assert at its entry; in release
    /// builds the calls compile out entirely.
    /// </summary>
    public static class ThreadGuard {
        static Thread uiThread;

        /// <summary>Registers the UI thread. Call once at startup.</summary>
        public static void SetUiThread(Thread thread) {
            uiThread = thread;
        }

        [Conditional("DEBUG")]
        public static void AssertUi() {
            Debug.Assert(uiThread != null, "ThreadGuard: UI thread not registered");
            Debug.Assert(uiThread == Thread.CurrentThread, "ThreadGuard: expected the UI thread");
        }
    }
}
