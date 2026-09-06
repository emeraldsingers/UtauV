using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OpenUtau.Core;
using OpenUtau.Colors;
using OpenUtau.App.ViewModels;
using ReactiveUI;

namespace OpenUtau.App.Views {
    public class SingerThemeEditorStateChangedEvent { }

    public partial class SingerThemeEditorWindow : Window {
        private static SingerThemeEditorWindow? _instance;
        private SingerThemeManagerViewModel managerVM;
        private bool _saving = false;

        public static bool IsOpen => _instance != null;

        private SingerThemeEditorWindow() {
            InitializeComponent();
            managerVM = new SingerThemeManagerViewModel();
            DataContext = managerVM;

            // Subscribe to initial selection
            if (managerVM.SelectedTheme != null) {
                managerVM.SelectedTheme.PropertyChanged += OnThemePropertyChanged;
                CustomSingerTheme.ActiveEditTheme = managerVM.SelectedTheme.ToTheme();
                Refresh();
            }

            // Handle selection changes
            managerVM.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(SingerThemeManagerViewModel.SelectedTheme)) {
                    // Unsubscribe old theme
                    foreach (var t in managerVM.Themes) {
                        t.PropertyChanged -= OnThemePropertyChanged;
                    }
                    // Subscribe new theme
                    if (managerVM.SelectedTheme != null) {
                        managerVM.SelectedTheme.PropertyChanged += OnThemePropertyChanged;
                    }
                    CustomSingerTheme.ActiveEditTheme = managerVM.SelectedTheme?.ToTheme();
                    Refresh();
                }
            };
        }

        private void OnThemePropertyChanged(object? sender, PropertyChangedEventArgs e) {
            CustomSingerTheme.ActiveEditTheme = managerVM.SelectedTheme?.ToTheme();
            Refresh();
        }

        private void Refresh() {
            MessageBus.Current.SendMessage(new PianorollRefreshEvent("TrackColor"));
            MessageBus.Current.SendMessage(new PianorollRefreshEvent("Part"));
        }

        public static void Open() {
            if (_instance == null) {
                _instance = new SingerThemeEditorWindow();
                _instance.Show();
                MessageBus.Current.SendMessage(new SingerThemeEditorStateChangedEvent());
            } else {
                _instance.Activate();
            }
        }

        private void OnAddTheme(object? sender, RoutedEventArgs e) {
            var newTheme = new SingerThemeConfigViewModel();
            newTheme.LoadFrom(new CustomSingerTheme.SingerThemeYaml {
                Name = "New Singer Theme",
                Singers = "SingerName1",
            });
            managerVM.Themes.Add(newTheme);
            managerVM.SelectedTheme = newTheme;
            SaveThemeToFile(newTheme);
        }

        private async void OnRemoveTheme(object? sender, RoutedEventArgs e) {
            if (managerVM.SelectedTheme == null) return;
            var theme = managerVM.SelectedTheme;
            var result = await MessageBox.Show(this, "Delete this theme?", "Confirm", MessageBox.MessageBoxButtons.YesNo);
            if (result != MessageBox.MessageBoxResult.Yes) return;

            var filePath = GetFilePath(theme.Name);
            if (File.Exists(filePath)) File.Delete(filePath);
            
            managerVM.Themes.Remove(theme);
            managerVM.SelectedTheme = managerVM.Themes.Count > 0 ? managerVM.Themes[0] : null;
            CustomSingerTheme.ListThemes();
            Refresh();
        }

        private void OnSaveAll(object? sender, RoutedEventArgs e) {
            _saving = true;
            foreach (var t in managerVM.Themes) {
                SaveThemeToFile(t);
            }
            CustomSingerTheme.ListThemes();
            CustomSingerTheme.ActiveEditTheme = null;
            Refresh();
            Close();
        }

        private void OnClose(object? sender, RoutedEventArgs e) {
            Close();
        }

        private void SaveThemeToFile(SingerThemeConfigViewModel theme) {
            try {
                Directory.CreateDirectory(PathManager.Inst.SingerThemesPath);
                var yaml = Yaml.DefaultSerializer.Serialize(theme.ToTheme());
                File.WriteAllText(GetFilePath(theme.Name), yaml, Encoding.UTF8);
            } catch (Exception ex) {
                Serilog.Log.Error(ex, "Failed to save singer theme.");
            }
        }

        private string GetFilePath(string name) {
            var safeName = System.Text.RegularExpressions.Regex.Replace(name.ToLower(), @"[^\w]", "-");
            return Path.Join(PathManager.Inst.SingerThemesPath, $"{safeName}.yaml");
        }

        private void WindowClosing(object? sender, WindowClosingEventArgs e) {
            _instance = null;
            if (!_saving) {
                CustomSingerTheme.ActiveEditTheme = null;
                Refresh();
            }
            MessageBus.Current.SendMessage(new SingerThemeEditorStateChangedEvent());
        }
    }

    public class SingerThemeManagerViewModel : ReactiveObject {
        public ObservableCollection<SingerThemeConfigViewModel> Themes { get; } = new ObservableCollection<SingerThemeConfigViewModel>();
        
        private SingerThemeConfigViewModel? selectedTheme;
        public SingerThemeConfigViewModel? SelectedTheme {
            get => selectedTheme;
            set {
                this.RaiseAndSetIfChanged(ref selectedTheme, value);
                this.RaisePropertyChanged(nameof(HasSelectedTheme));
            }
        }
        public bool HasSelectedTheme => SelectedTheme != null;

        public SingerThemeManagerViewModel() {
            // Load from saved themes
            foreach (var kv in CustomSingerTheme.Themes) {
                var vm = new SingerThemeConfigViewModel();
                vm.LoadFrom(kv.Value);
                Themes.Add(vm);
            }
            SelectedTheme = Themes.Count > 0 ? Themes[0] : null;
        }
    }

    public class SingerThemeConfigViewModel : ReactiveObject {
        private string name = "Custom Singer Theme";
        private string singers = "SingerName1,SingerName2";
        private bool hasTrackAccentColor = true;
        private Color trackAccentColor;
        private bool hasTrackAccentColorDark = true;
        private Color trackAccentColorDark;
        private bool hasTrackAccentColorLight = true;
        private Color trackAccentColorLight;
        private bool hasTrackCenterKeyColor = true;
        private Color trackCenterKeyColor;
        private bool hasPhonemeColor = true;
        private Color phonemeColor;
        private bool hasPhonemeColor2 = true;
        private Color phonemeColor2;
        private bool hasAccentPen2Color = true;
        private Color accentPen2Color;
        private bool hasAccentColorSemi = true;
        private Color accentColorSemi;
        private bool hasPitchColor = true;
        private Color pitchColor;
        private bool hasPitchBendColor = true;
        private Color pitchBendColor;
        private bool hasPitchBendBrushColor = true;
        private Color pitchBendBrushColor;

        public string Name { get => name; set => this.RaiseAndSetIfChanged(ref name, value); }
        public string Singers { get => singers; set => this.RaiseAndSetIfChanged(ref singers, value); }
        public bool HasTrackAccentColor { get => hasTrackAccentColor; set => this.RaiseAndSetIfChanged(ref hasTrackAccentColor, value); }
        public Color TrackAccentColor { get => trackAccentColor; set => this.RaiseAndSetIfChanged(ref trackAccentColor, value); }
        public bool HasTrackAccentColorDark { get => hasTrackAccentColorDark; set => this.RaiseAndSetIfChanged(ref hasTrackAccentColorDark, value); }
        public Color TrackAccentColorDark { get => trackAccentColorDark; set => this.RaiseAndSetIfChanged(ref trackAccentColorDark, value); }
        public bool HasTrackAccentColorLight { get => hasTrackAccentColorLight; set => this.RaiseAndSetIfChanged(ref hasTrackAccentColorLight, value); }
        public Color TrackAccentColorLight { get => trackAccentColorLight; set => this.RaiseAndSetIfChanged(ref trackAccentColorLight, value); }
        public bool HasTrackCenterKeyColor { get => hasTrackCenterKeyColor; set => this.RaiseAndSetIfChanged(ref hasTrackCenterKeyColor, value); }
        public Color TrackCenterKeyColor { get => trackCenterKeyColor; set => this.RaiseAndSetIfChanged(ref trackCenterKeyColor, value); }
        public bool HasPhonemeColor { get => hasPhonemeColor; set => this.RaiseAndSetIfChanged(ref hasPhonemeColor, value); }
        public Color PhonemeColor { get => phonemeColor; set => this.RaiseAndSetIfChanged(ref phonemeColor, value); }
        public bool HasPhonemeColor2 { get => hasPhonemeColor2; set => this.RaiseAndSetIfChanged(ref hasPhonemeColor2, value); }
        public Color PhonemeColor2 { get => phonemeColor2; set => this.RaiseAndSetIfChanged(ref phonemeColor2, value); }
        public bool HasAccentPen2Color { get => hasAccentPen2Color; set => this.RaiseAndSetIfChanged(ref hasAccentPen2Color, value); }
        public Color AccentPen2Color { get => accentPen2Color; set => this.RaiseAndSetIfChanged(ref accentPen2Color, value); }
        public bool HasAccentColorSemi { get => hasAccentColorSemi; set => this.RaiseAndSetIfChanged(ref hasAccentColorSemi, value); }
        public Color AccentColorSemi { get => accentColorSemi; set => this.RaiseAndSetIfChanged(ref accentColorSemi, value); }
        public bool HasPitchColor { get => hasPitchColor; set => this.RaiseAndSetIfChanged(ref hasPitchColor, value); }
        public Color PitchColor { get => pitchColor; set => this.RaiseAndSetIfChanged(ref pitchColor, value); }
        public bool HasPitchBendColor { get => hasPitchBendColor; set => this.RaiseAndSetIfChanged(ref hasPitchBendColor, value); }
        public Color PitchBendColor { get => pitchBendColor; set => this.RaiseAndSetIfChanged(ref pitchBendColor, value); }
        public bool HasPitchBendBrushColor { get => hasPitchBendBrushColor; set => this.RaiseAndSetIfChanged(ref hasPitchBendBrushColor, value); }
        public Color PitchBendBrushColor { get => pitchBendBrushColor; set => this.RaiseAndSetIfChanged(ref pitchBendBrushColor, value); }

        public void LoadFrom(CustomSingerTheme.SingerThemeYaml theme) {
            Name = theme.Name;
            Singers = theme.Singers;
            HasTrackAccentColor = theme.HasTrackAccentColor;
            Color.TryParse(theme.TrackAccentColor, out trackAccentColor);
            HasTrackAccentColorDark = theme.HasTrackAccentColorDark;
            Color.TryParse(theme.TrackAccentColorDark, out trackAccentColorDark);
            HasTrackAccentColorLight = theme.HasTrackAccentColorLight;
            Color.TryParse(theme.TrackAccentColorLight, out trackAccentColorLight);
            HasTrackCenterKeyColor = theme.HasTrackCenterKeyColor;
            Color.TryParse(theme.TrackCenterKeyColor, out trackCenterKeyColor);
            HasPhonemeColor = theme.HasPhonemeColor;
            Color.TryParse(theme.PhonemeColor, out phonemeColor);
            HasPhonemeColor2 = theme.HasPhonemeColor2;
            Color.TryParse(theme.PhonemeColor2, out phonemeColor2);
            HasAccentPen2Color = theme.HasAccentPen2Color;
            Color.TryParse(theme.AccentPen2Color, out accentPen2Color);
            HasAccentColorSemi = theme.HasAccentColorSemi;
            Color.TryParse(theme.AccentColorSemi, out accentColorSemi);
            HasPitchColor = theme.HasPitchColor;
            Color.TryParse(theme.PitchColor, out pitchColor);
            HasPitchBendColor = theme.HasPitchBendColor;
            Color.TryParse(theme.PitchBendColor, out pitchBendColor);
            HasPitchBendBrushColor = theme.HasPitchBendBrushColor;
            Color.TryParse(theme.PitchBendBrushColor, out pitchBendBrushColor);
        }

        public CustomSingerTheme.SingerThemeYaml ToTheme() {
            return new CustomSingerTheme.SingerThemeYaml {
                Name = this.Name,
                Singers = this.Singers,
                HasTrackAccentColor = this.HasTrackAccentColor,
                TrackAccentColor = $"#{TrackAccentColor.A:x2}{TrackAccentColor.R:x2}{TrackAccentColor.G:x2}{TrackAccentColor.B:x2}",
                HasTrackAccentColorDark = this.HasTrackAccentColorDark,
                TrackAccentColorDark = $"#{TrackAccentColorDark.A:x2}{TrackAccentColorDark.R:x2}{TrackAccentColorDark.G:x2}{TrackAccentColorDark.B:x2}",
                HasTrackAccentColorLight = this.HasTrackAccentColorLight,
                TrackAccentColorLight = $"#{TrackAccentColorLight.A:x2}{TrackAccentColorLight.R:x2}{TrackAccentColorLight.G:x2}{TrackAccentColorLight.B:x2}",
                HasTrackCenterKeyColor = this.HasTrackCenterKeyColor,
                TrackCenterKeyColor = $"#{TrackCenterKeyColor.A:x2}{TrackCenterKeyColor.R:x2}{TrackCenterKeyColor.G:x2}{TrackCenterKeyColor.B:x2}",
                HasPhonemeColor = this.HasPhonemeColor,
                PhonemeColor = $"#{PhonemeColor.A:x2}{PhonemeColor.R:x2}{PhonemeColor.G:x2}{PhonemeColor.B:x2}",
                HasPhonemeColor2 = this.HasPhonemeColor2,
                PhonemeColor2 = $"#{PhonemeColor2.A:x2}{PhonemeColor2.R:x2}{PhonemeColor2.G:x2}{PhonemeColor2.B:x2}",
                HasAccentPen2Color = this.HasAccentPen2Color,
                AccentPen2Color = $"#{AccentPen2Color.A:x2}{AccentPen2Color.R:x2}{AccentPen2Color.G:x2}{AccentPen2Color.B:x2}",
                HasAccentColorSemi = this.HasAccentColorSemi,
                AccentColorSemi = $"#{AccentColorSemi.A:x2}{AccentColorSemi.R:x2}{AccentColorSemi.G:x2}{AccentColorSemi.B:x2}",
                HasPitchColor = this.HasPitchColor,
                PitchColor = $"#{PitchColor.A:x2}{PitchColor.R:x2}{PitchColor.G:x2}{PitchColor.B:x2}",
                HasPitchBendColor = this.HasPitchBendColor,
                PitchBendColor = $"#{PitchBendColor.A:x2}{PitchBendColor.R:x2}{PitchBendColor.G:x2}{PitchBendColor.B:x2}",
                HasPitchBendBrushColor = this.HasPitchBendBrushColor,
                PitchBendBrushColor = $"#{PitchBendBrushColor.A:x2}{PitchBendBrushColor.R:x2}{PitchBendBrushColor.G:x2}{PitchBendBrushColor.B:x2}"
            };
        }
    }
}
