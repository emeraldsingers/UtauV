using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using OpenUtau.Core.Util;

namespace OpenUtau.App {
    static class LowPerformance {
        public static void StripShadows(Visual root) {
            if (!Preferences.Default.LowPerformanceMode) {
                return;
            }
            foreach (var border in root.GetVisualDescendants().OfType<Border>()) {
                if (border.BoxShadow.Count > 0) {
                    border.BoxShadow = default;
                }
            }
        }
    }
}
