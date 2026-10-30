using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;

namespace OpenUtau.App.Views {
    public partial class VideoExportDialog : Window {
        public VideoExportViewModel ViewModel { get; private set; }

        public VideoExportDialog() {
            InitializeComponent();
            DataContext = ViewModel = new VideoExportViewModel();
        }

        private void OnOkClick(object sender, RoutedEventArgs e) {
            Close(true);
        }

        private void OnCancelClick(object sender, RoutedEventArgs e) {
            Close(false);
        }
    }
}
