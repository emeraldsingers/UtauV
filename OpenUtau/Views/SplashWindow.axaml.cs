using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using OpenUtau.App;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using Serilog;

namespace OpenUtau.App.Views {
    public partial class SplashWindow : Window, IDisposable {
        public SplashWindow() {
            InitializeComponent();
            UpdateLogo();
            MessageBus.Current.Listen<ThemeChangedEvent>()
                .Subscribe(_ => UpdateLogo())
                .DisposeWith(disposable);
            this.Cursor = new Cursor(StandardCursorType.AppStarting);
            this.Opened += SplashWindow_Opened;
        }

        private readonly MultipleDisposable disposable = new();

        private void UpdateLogo() {
            LogoTypeDark.IsVisible = ThemeManager.IsDarkMode;
            LogoTypeLight.IsVisible = !ThemeManager.IsDarkMode;
        }

        public void Dispose() {
            disposable.Dispose();
        }

        private void SplashWindow_Opened(object? sender, EventArgs e) {
            if (Screens.Primary == null && Screens.ScreenCount == 0) {
                return;
            }
            CenterOnPrimaryScreen();
            Start();
        }

        private void CenterOnPrimaryScreen() {
            var screen = Screens.Primary;
            if (screen == null) {
                return;
            }
            var area = screen.WorkingArea;
            var x = area.X + (area.Width - (int)Width) / 2;
            var y = area.Y + (area.Height - (int)Height) / 2;
            Position = new PixelPoint(Math.Max(0, x), Math.Max(0, y));
        }

        private void Start() {
            var mainThread = Thread.CurrentThread;
            var mainScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Run(() => {
                Log.Information("Initializing OpenUtau.");
                SetStatus("Loading tools...");
                ToolsManager.Inst.Initialize();
                SetStatus("Loading singers...");
                SingerManager.Inst.Initialize();
                SetStatus("Initializing project manager...");
                DocManager.Inst.Initialize(mainThread, mainScheduler);
                DocManager.Inst.PostOnUIThread = action => Avalonia.Threading.Dispatcher.UIThread.Post(action);
                Log.Information("Initialized OpenUtau.");
                SetStatus("Initializing audio engine...");
                InitAudio();
            }).ContinueWith(t => {
                if (t.IsFaulted) {
                    Log.Error(t.Exception?.Flatten(), "Failed to Start.");
                    MessageBox.ShowError(this, t.Exception, "Failed to Start OpenUtau").ContinueWith(t1 => { Close(); });
                    return;
                }
                if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
                    SetStatus("Opening main window...");
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    desktop.MainWindow = mainWindow;
                    mainWindow.InitProject();
                    LoadingWindow.InitializeLoadingWindow();
                    Close();
                    if (Preferences.FirstRun) {
                        var welcomeDialog = new WelcomeDialog();
                        _ = welcomeDialog.ShowDialog(mainWindow);
                    }
                }
            }, CancellationToken.None, TaskContinuationOptions.None, mainScheduler);
        }

        private void SetStatus(string text) {
            Dispatcher.UIThread.Post(() => {
                StatusText.Text = text;
            });
        }

        private static void InitAudio() {
            Log.Information("Initializing audio.");
            if (!OS.IsWindows() || Core.Util.Preferences.Default.PreferPortAudio) {
                try {
                    PlaybackManager.Inst.AudioOutput = new Audio.MiniAudioOutput();
                } catch (Exception e1) {
                    Log.Error(e1, "Failed to init MiniAudio");
                }
            } else {
                try {
                    PlaybackManager.Inst.AudioOutput = new NAudioOutput();
                } catch (Exception e2) {
                    Log.Error(e2, "Failed to init NAudio");
                }
            }
            Log.Information("Initialized audio.");
        }
    }
}
