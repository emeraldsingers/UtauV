using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    public partial class PackageVersionArchiveDialog : Window {
        public PackageVersionArchiveDialog() {
            InitializeComponent();
        }

        async void OnInstallVersionClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is not PackageVersionArchiveViewModel vm ||
                    sender is not Button button ||
                    button.DataContext is not PackageVersionRowViewModel row) {
                    return;
                }
                var msg = string.Format(
                    ThemeManager.GetString("packages.archive.confirm.install.message"),
                    vm.Software.id,
                    row.Version);
                var caption = ThemeManager.GetString("packages.archive.confirm.install.caption");
                var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                if (result != MessageBox.MessageBoxResult.Yes) {
                    return;
                }

                var installingTemplate = ThemeManager.GetString("packages.archive.status.installing");
                var baseStatus = string.Format(installingTemplate, row.Version);
                vm.Status = baseStatus;
                var progress = new Progress<int>(p => {
                    vm.Status = $"{baseStatus} ({p}%)";
                });
                await PackageManager.Inst.InstallVersionAsync(vm.Software, row.Version, progress);
                vm.Status = ThemeManager.GetString("packages.archive.status.installfinished");
                Close(true);
            } catch (Exception ex) {
                if (DataContext is PackageVersionArchiveViewModel vm) {
                    vm.Status = ThemeManager.GetString("packages.archive.status.installfailed");
                }
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnCloseClicked(object sender, RoutedEventArgs e) {
            Close(false);
        }
    }
}
