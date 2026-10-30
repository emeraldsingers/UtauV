using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace OpenUtau.App.Render {
    sealed class VideoExportOverlay : IDisposable {
        readonly OverlayLayer? layer;
        readonly Border root;
        readonly TextBlock status;

        public VideoExportOverlay(Visual anchor, string title, string hint) {
            layer = OverlayLayer.GetOverlayLayer(anchor);
            var heading = new TextBlock {
                Text = title,
                FontSize = 28,
                FontWeight = FontWeight.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            status = new TextBlock {
                FontSize = 16,
                Foreground = Brushes.White,
                Opacity = 0.85,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
            var hintText = new TextBlock {
                Text = hint,
                FontSize = 13,
                Foreground = Brushes.White,
                Opacity = 0.6,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            root = new Border {
                Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = new StackPanel {
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { heading, status, hintText },
                },
            };
            layer?.Children.Add(root);
        }

        public void SetStatus(string text) {
            status.Text = text;
        }

        public void Dispose() {
            layer?.Children.Remove(root);
        }
    }
}
