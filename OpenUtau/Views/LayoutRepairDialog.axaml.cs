using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OpenUtau.App.Views {
    public partial class LayoutRepairDialog : Window {
        public bool Accepted { get; private set; }

        public LayoutRepairDialog() {
            InitializeComponent();
        }

        private void Fix(object? sender, RoutedEventArgs e) {
            Accepted = true;
            Close();
        }

        private void Later(object? sender, RoutedEventArgs e) {
            Close();
        }
    }
}
