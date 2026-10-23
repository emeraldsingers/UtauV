using System;
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
            var screen = Screens.ScreenFromVisual(this) ?? Screens.Primary;
            if (screen != null) {
                double scaling = screen.Scaling > 0 ? screen.Scaling : 1;
                MaxHeight = Math.Max(360, screen.WorkingArea.Height / scaling - 140);
            }
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
