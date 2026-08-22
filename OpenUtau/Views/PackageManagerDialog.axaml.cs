using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;

namespace OpenUtau.App.Views {
    public partial class PackageManagerDialog : Window {
        public PackageManagerDialog() {
            InitializeComponent();
        }

        async void OnPrimaryActionClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm) {
                    if (sender is Button b && b.DataContext is PackageRowViewModel row) {
                        if (row.IsInstalled && !row.IsUpToDate) {
                            var msg = string.Format(ThemeManager.GetString("packages.confirm.update.message"), row.Id, row.Version);
                            var caption = ThemeManager.GetString("packages.confirm.update.caption");
                            var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                            if (result != MessageBox.MessageBoxResult.Yes) return;
                        } else {
                            var msg = string.Format(ThemeManager.GetString("packages.confirm.install.message"), row.Id);
                            var caption = ThemeManager.GetString("packages.confirm.install.caption");
                            var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                            if (result != MessageBox.MessageBoxResult.Yes) return;
                        }
                        await vm.InstallAsync(row);
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnUninstallClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm) {
                    if (sender is Button b && b.DataContext is PackageRowViewModel row) {
                        var msg = string.Format(ThemeManager.GetString("packages.confirm.uninstall.message"), row.Id);
                        var caption = ThemeManager.GetString("packages.confirm.uninstall.caption");
                        var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                        if (result != MessageBox.MessageBoxResult.Yes) return;
                        await vm.UninstallAsync(row);
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnInstallFromFile(object sender, RoutedEventArgs e) {
            try {
                var file = await FilePicker.OpenFile(
                    this, "menu.tools.dependency.install", FilePicker.OUDEP);
                if (file == null) return;
                if (file.EndsWith(PackageManager.OudepExt)) {
                    await PackageManager.Inst.InstallFromFileAsync(file);
                    if (DataContext is PackageManagerViewModel vm) {
                        await vm.RefreshAsync();
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnVersionArchiveClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is PackageRowViewModel row &&
                    row.Software != null &&
                    row.HasVersionArchive) {
                    var dialog = new PackageVersionArchiveDialog {
                        DataContext = new PackageVersionArchiveViewModel(
                            row.Software,
                            row.IsInstalled ? row.InstalledVersion : string.Empty),
                    };
                    var changed = await dialog.ShowDialog<bool>(this);
                    if (changed) {
                        await vm.RefreshAsync();
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnOpenVoicebankCatalogClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is VoicebankGroupViewModel group) {
                    var dialog = new VoicebankCatalogDialog(vm, group);
                    var changed = await dialog.ShowDialog<bool>(this);
                    if (changed) {
                        await vm.RefreshAsync();
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnVoicebankWebsiteClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is Button b &&
                    b.DataContext is VoicebankGroupViewModel row &&
                    row.HasWebsite) {
                    OS.OpenWeb(row.WebsiteUrl);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnVoicebankSingerLinkClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is Button b &&
                    b.DataContext is VoicebankGroupViewModel row &&
                    row.HasSingerLink) {
                    OS.OpenWeb(row.SingerLink);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OpenLocation(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm) {
                    if (vm.IsVoicebanksSection) {
                        Directory.CreateDirectory(PathManager.Inst.SingersInstallPath);
                        OS.OpenFolder(PathManager.Inst.SingersInstallPath);
                    } else if (vm.IsPluginsSection) {
                        Directory.CreateDirectory(PathManager.Inst.PluginsPath);
                        OS.OpenFolder(PathManager.Inst.PluginsPath);
                    } else if (vm.IsThemesSection) {
                        Directory.CreateDirectory(PathManager.Inst.ThemesPath);
                        OS.OpenFolder(PathManager.Inst.ThemesPath);
                    } else {
                        Directory.CreateDirectory(PathManager.Inst.DependencyPath);
                        OS.OpenFolder(PathManager.Inst.DependencyPath);
                    }
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnInstallPluginClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is PluginRowViewModel row) {
                    await vm.InstallPluginAsync(row);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnUninstallPluginClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is PluginRowViewModel row) {
                    var msg = string.Format(ThemeManager.GetString("packages.confirm.uninstall.message"), row.Id);
                    var caption = ThemeManager.GetString("packages.confirm.uninstall.caption");
                    var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                    if (result != MessageBox.MessageBoxResult.Yes) return;
                    await vm.UninstallPluginAsync(row);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnPluginWebsiteClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is Button b && b.DataContext is PluginRowViewModel row && !string.IsNullOrWhiteSpace(row.RepoUrl)) {
                    OS.OpenWeb(row.RepoUrl);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }


        void OnThemeCardPointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Avalonia.Controls.Border border &&
                    border.DataContext is ThemeRowViewModel theme) {
                    vm.SelectedTheme = theme;
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnThemeTypeFilterChanged(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Avalonia.Controls.RadioButton rb &&
                    rb.Tag is string tag) {
                    vm.SelectedThemeTypeFilter = tag;
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnInstallThemeClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is ThemeRowViewModel row) {
                    var msg = string.Format(ThemeManager.GetString("packages.confirm.install.message"), row.Id);
                    var caption = ThemeManager.GetString("packages.confirm.install.caption");
                    var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                    if (result != MessageBox.MessageBoxResult.Yes) return;
                    await vm.InstallThemeAsync(row);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        async void OnUninstallThemeClick(object sender, RoutedEventArgs e) {
            try {
                if (DataContext is PackageManagerViewModel vm &&
                    sender is Button b &&
                    b.DataContext is ThemeRowViewModel row) {
                    var msg = string.Format(ThemeManager.GetString("packages.confirm.uninstall.message"), row.Id);
                    var caption = ThemeManager.GetString("packages.confirm.uninstall.caption");
                    var result = await MessageBox.Show(this, msg, caption, MessageBox.MessageBoxButtons.YesNo);
                    if (result != MessageBox.MessageBoxResult.Yes) return;
                    await vm.UninstallThemeAsync(row);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnThemeHomepageClick(object sender, RoutedEventArgs e) {
            try {
                if (sender is Button b &&
                    b.DataContext is ThemeRowViewModel row &&
                    row.HasHomepage) {
                    OS.OpenWeb(row.HomepageUrl);
                }
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OpenThemesLocation(object sender, RoutedEventArgs e) {
            try {
                Directory.CreateDirectory(PathManager.Inst.ThemesPath);
                OS.OpenFolder(PathManager.Inst.ThemesPath);
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

        void OnCreateThemePrClick(object sender, RoutedEventArgs e) {
            try {
                OS.OpenWeb("https://github.com/emeraldsingers/UtauV_Packages/compare");
            } catch (Exception ex) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(ex));
            }
        }

    }
}
