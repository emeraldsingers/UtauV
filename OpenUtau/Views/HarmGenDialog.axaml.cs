using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core.Editing;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    public partial class HarmGenDialog : Window {
        public HarmGenConfig? Config { get; private set; }

        public HarmGenDialog() {
            InitializeComponent();
        }

        public void LoadData(UProject project, int defaultTrackNo) {
            DataContext = new HarmGenViewModel(project, defaultTrackNo);
        }

        void OnSelectAllTracks(object? sender, RoutedEventArgs e) {
            if (DataContext is HarmGenViewModel viewModel) {
                viewModel.SelectAllTracks();
            }
        }

        void OnSelectNoTracks(object? sender, RoutedEventArgs e) {
            if (DataContext is HarmGenViewModel viewModel) {
                viewModel.SelectNoTracks();
            }
        }

        void OnOkClicked(object? sender, RoutedEventArgs e) {
            if (DataContext is not HarmGenViewModel viewModel) {
                Close();
                return;
            }
            if (!viewModel.CanGenerate) {
                _ = MessageBox.Show(this,
                    ThemeManager.GetString("dialogs.harmgen.notracks"),
                    ThemeManager.GetString("dialogs.harmgen.caption"),
                    MessageBox.MessageBoxButtons.Ok);
                return;
            }
            Config = viewModel.BuildConfig();
            Close();
        }

        void OnCancelClicked(object? sender, RoutedEventArgs e) {
            Config = null;
            Close();
        }
    }
}
