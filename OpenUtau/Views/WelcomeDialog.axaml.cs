using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App;
using OpenUtau.App.ViewModels;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Util;

namespace OpenUtau.App.Views {
    public partial class WelcomeDialog : Window {
        public WelcomeDialog() {
            InitializeComponent();
            RefreshThemeSelector();
        }

        async void ImportPrefs(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFile(this, "welcome.importprefs", FilePicker.PrefsJson);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            try {
                Preferences.ImportFrom(path);
                if (!string.IsNullOrEmpty(Preferences.Default.Language)) {
                    App.SetLanguage(Preferences.Default.Language);
                }
                App.SetTheme();
                UiFontManager.Apply();
                RefreshThemeSelector();
                RefreshMainWindowFromPrefs();
                PrefsPathText.Text = path;
                StatusText.Text = ThemeManager.GetString("welcome.importprefs.done");
                await RefreshSingers();
                RefreshResamplers();
            } catch (Exception ex) {
                await MessageBox.ShowError(this, ex, ThemeManager.GetString("welcome.importprefs.failed"));
            }
        }

        async void ImportSingers(object sender, RoutedEventArgs e) {
            var path = await FilePicker.OpenFolderAboutSinger(this, "welcome.importsingers");
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            try {
                Preferences.Default.AdditionalSingerPath = path;
                Preferences.Save();
                SingersPathText.Text = path;
                StatusText.Text = ThemeManager.GetString("welcome.importsingers.done");
                await RefreshSingers();
            } catch (Exception ex) {
                await MessageBox.ShowError(this, ex, ThemeManager.GetString("welcome.importsingers.failed"));
            }
        }

        void ImportResamplers(object sender, RoutedEventArgs e) {
            ImportResamplersAsync();
        }

        private async void ImportResamplersAsync() {
            var path = await FilePicker.OpenFolder(this, "welcome.importresamplers", null);
            if (string.IsNullOrEmpty(path)) {
                return;
            }
            try {
                Preferences.Default.AdditionalResamplerPath = path;
                Preferences.Save();
                ResamplersPathText.Text = path;
                RefreshResamplers();
                StatusText.Text = ThemeManager.GetString("welcome.importresamplers.done");
            } catch (Exception ex) {
                await MessageBox.ShowError(this, ex, ThemeManager.GetString("welcome.importresamplers.failed"));
            }
        }

        void OpenUsageGuide(object sender, RoutedEventArgs e) {
            try {
                OS.OpenFolder(PathManager.Inst.DataPath);
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void ThemeSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (ThemeComboBox.SelectedItem is not string themeName ||
                themeName == Preferences.Default.ThemeName) {
                return;
            }
            try {
                Preferences.Default.ThemeName = themeName;
                Preferences.Save();
                App.SetTheme();
                StatusText.Text = ThemeManager.GetString("welcome.theme.done");
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void Skip(object sender, RoutedEventArgs e) {
            Close();
        }

        void Done(object sender, RoutedEventArgs e) {
            Close();
        }

        private static async Task RefreshSingers() {
            await Task.Run(() => {
                SingerManager.Inst.SearchAllSingers();
            });
            DocManager.Inst.ExecuteCmd(new SingersRefreshedNotification());
        }

        private static void RefreshResamplers() {
            ToolsManager.Inst.SearchResamplers();
        }

        private void RefreshThemeSelector() {
            ThemeComboBox.ItemsSource = ThemeManager.GetAvailableThemes();
            ThemeComboBox.SelectedItem = Preferences.Default.ThemeName;
        }

        private void RefreshMainWindowFromPrefs() {
            if (Owner?.DataContext is MainWindowViewModel viewModel) {
                viewModel.RefreshOpenRecent();
            }
        }
    }
}
