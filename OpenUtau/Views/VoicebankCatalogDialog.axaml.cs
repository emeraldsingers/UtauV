using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    public partial class VoicebankCatalogDialog : Window {
        readonly PackageManagerViewModel packageManagerViewModel;
        bool changed;

        public VoicebankCatalogDialog(PackageManagerViewModel packageManagerViewModel, VoicebankGroupViewModel group) {
            this.packageManagerViewModel = packageManagerViewModel;
            DataContext = group;
            InitializeComponent();
        }

        async void OnInstallLatestVariantClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is not Button button ||
                    button.DataContext is not VoicebankVariantViewModel variant) {
                    return;
                }
                var msg = string.Format(
                    ThemeManager.GetString("voicebanks.confirm.install.message"),
                    variant.Id);
                var caption = ThemeManager.GetString("voicebanks.confirm.install.caption");
                var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                if (result != MessageBox.MessageBoxResult.Yes) {
                    return;
                }
                await packageManagerViewModel.InstallVoicebankLatestAsync(variant);
                changed = true;
                await ReloadGroupAsync();
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnInstallVersionClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is not Button button ||
                    button.DataContext is not VoicebankVersionEntryViewModel versionEntry) {
                    return;
                }
                var msg = string.Format(
                    ThemeManager.GetString("voicebanks.confirm.installversion.message"),
                    versionEntry.Variant.Id,
                    versionEntry.Version);
                var caption = ThemeManager.GetString("voicebanks.confirm.installversion.caption");
                var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                if (result != MessageBox.MessageBoxResult.Yes) {
                    return;
                }
                await packageManagerViewModel.InstallVoicebankVersionAsync(versionEntry.Variant, versionEntry.Version);
                changed = true;
                await ReloadGroupAsync();
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnUninstallVariantClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is not Button button ||
                    button.DataContext is not VoicebankVariantViewModel variant) {
                    return;
                }
                var msg = string.Format(
                    ThemeManager.GetString("voicebanks.confirm.uninstall.message"),
                    variant.Id);
                var caption = ThemeManager.GetString("voicebanks.confirm.uninstall.caption");
                var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                if (result != MessageBox.MessageBoxResult.Yes) {
                    return;
                }
                await packageManagerViewModel.UninstallVoicebankAsync(variant);
                changed = true;
                await ReloadGroupAsync();
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnOpenGroupWebsiteClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is VoicebankGroupViewModel group && group.HasWebsite) {
                    OS.OpenWeb(group.WebsiteUrl);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnOpenGroupSingerLinkClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is VoicebankGroupViewModel group && group.HasSingerLink) {
                    OS.OpenWeb(group.SingerLink);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnOpenVariantWebsiteClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is Button button &&
                    button.DataContext is VoicebankVariantViewModel variant &&
                    variant.HasWebsite) {
                    OS.OpenWeb(variant.WebsiteUrl);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnCloseClicked(object sender, RoutedEventArgs e) {
            Close(changed);
        }

        async System.Threading.Tasks.Task ReloadGroupAsync() {
            if (DataContext is not VoicebankGroupViewModel oldGroup) {
                return;
            }
            await packageManagerViewModel.RefreshAsync();
            var updated = packageManagerViewModel.VoicebankGroups
                .SelectMany(team => team.Groups)
                .FirstOrDefault(group => string.Equals(group.GroupId, oldGroup.GroupId, StringComparison.OrdinalIgnoreCase));
            if (updated != null) {
                DataContext = updated;
            } else {
                Close(changed);
            }
        }
    }
}
