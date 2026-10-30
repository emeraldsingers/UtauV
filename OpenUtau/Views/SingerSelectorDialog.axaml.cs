using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.App.Views {
    public partial class SingerSelectorDialog : Window {
        public USinger? SelectedSinger { get; private set; }
        private bool acceptDialogKeys;

        public SingerSelectorDialog() {
            InitializeComponent();
            Opened += (_, _) => Dispatcher.UIThread.Post(
                () => {
                    acceptDialogKeys = true;
                    SearchTextBox.Focus();
                }, DispatcherPriority.Background);
        }

        void OnConfirm(object? sender, RoutedEventArgs e) {
            Finish();
        }

        void OnCancel(object? sender, RoutedEventArgs e) {
            SelectedSinger = null;
            Close();
        }

        void OnKeyDown(object? sender, KeyEventArgs e) {
            // Do not let the Enter/Escape used to open the dialog be handled by
            // the newly-created window (Avalonia 12 can route that key event).
            if (!acceptDialogKeys) {
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape) {
                e.Handled = true;
                OnCancel(sender, e);
            } else if (e.Key == Key.Enter) {
                e.Handled = true;
                OnConfirm(sender, e);
            }
        }

        void OnSingerDoubleTapped(object? sender, RoutedEventArgs e) {
            Finish();
        }

        void OnSingerPointerPressed(object? sender, PointerPressedEventArgs e) {
            if (sender is Control { DataContext: SingerSelectorViewModel.SingerOption option }
                && DataContext is SingerSelectorViewModel vm) {
                vm.SelectedSingerOption = option;
            }
        }

        void OnRefresh(object? sender, RoutedEventArgs e) {
            if (DataContext is SingerSelectorViewModel vm) {
                vm.RefreshSingers();
            }
        }

        void OnPlaySample(object? sender, RoutedEventArgs e) {
            if (DataContext is not SingerSelectorViewModel vm || vm.SelectedSinger == null) {
                return;
            }
            var sample = FindSample(vm.SelectedSinger);
            if (!string.IsNullOrEmpty(sample)) {
                PlaybackManager.Inst.PlayFile(sample);
            }
        }

        void Finish() {
            if (DataContext is not SingerSelectorViewModel vm || vm.SelectedSinger == null) {
                return;
            }
            SelectedSinger = vm.SelectedSinger;
            Close();
        }

        static string? FindSample(USinger singer) {
            var sample = singer.Sample;
            if (!string.IsNullOrEmpty(sample) && File.Exists(sample)) {
                return sample;
            }
            if (singer.SingerType != USingerType.Classic && singer.SingerType != USingerType.Voicevox && singer.SingerType != USingerType.Coeiroink) {
                return null;
            }
            if (!Directory.Exists(singer.Location)) {
                return null;
            }
            var files = Directory.EnumerateFiles(singer.Location, "*.wav", SearchOption.AllDirectories)
                .Union(Directory.EnumerateFiles(singer.Location, "*.mp3", SearchOption.AllDirectories))
                .Union(Directory.EnumerateFiles(singer.Location, "*.flac", SearchOption.AllDirectories))
                .Union(Directory.EnumerateFiles(singer.Location, "*.aiff", SearchOption.AllDirectories))
                .Union(Directory.EnumerateFiles(singer.Location, "*.ogg", SearchOption.AllDirectories))
                .Union(Directory.EnumerateFiles(singer.Location, "*.opus", SearchOption.AllDirectories))
                .ToArray();
            if (files.Length == 0) {
                return null;
            }
            var random = new Random(Guid.NewGuid().GetHashCode());
            return files[random.Next(files.Length)];
        }
    }
}
