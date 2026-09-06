using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using static ReactiveUI.Primitives.SubscribeExtensions;
using System.Text.RegularExpressions;
using OpenUtau.Audio;
using OpenUtau.Classic;
using OpenUtau.Core;
using OpenUtau.Core.Util;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using ReactiveUI.Primitives;
using OpenUtau.Core.Render;
using Serilog;

namespace OpenUtau.App.ViewModels {
    public class LyricsHelperOption {
        public readonly Type klass;
        public LyricsHelperOption(Type klass) {
            this.klass = klass;
        }
        public override string ToString() {
            return klass.Name;
        }
    }

    public partial class PreferencesViewModel : ViewModelBase {
        // General
        private CultureInfo? language;
        private CultureInfo? sortingOrder;

        public List<CultureInfo>? Languages { get; }
        public CultureInfo? Language {
            get => language;
            set => this.RaiseAndSetIfChanged(ref language, value);
        }
        public List<CultureInfo>? SortingOrders { get; }
        public CultureInfo? SortingOrder {
            get => sortingOrder;
            set => this.RaiseAndSetIfChanged(ref sortingOrder, value);
        }
        [Reactive] public partial bool Beta { get; set; }

        // Playback
        private List<AudioOutputDevice>? audioOutputDevices;
        private AudioOutputDevice? audioOutputDevice;

        public List<AudioOutputDevice>? AudioOutputDevices {
            get => audioOutputDevices;
            set => this.RaiseAndSetIfChanged(ref audioOutputDevices, value);
        }
        public AudioOutputDevice? AudioOutputDevice {
            get => audioOutputDevice;
            set => this.RaiseAndSetIfChanged(ref audioOutputDevice, value);
        }
        [Reactive] public partial bool UseSystemDefaultDevice { get; set; }
        [Reactive] public partial int PreferPortAudio { get; set; }
        [Reactive] public partial int LockStartTime { get; set; }
        [Reactive] public partial int PlaybackAutoScroll { get; set; }
        [Reactive] public partial double PlaybackVerticalFollowMargin { get; set; }
        [Reactive] public partial double PlaybackVerticalFollowDamping { get; set; }
        [Reactive] public partial double PlaybackHighlightFadeInPerSecond { get; set; }
        [Reactive] public partial double PlaybackHighlightFadeOutPerSecond { get; set; }
        [Reactive] public partial double PlaybackNoteBounceHeight { get; set; }
        [Reactive] public partial double PlaybackNoteBounceDuration { get; set; }
        [Reactive] public partial double PlayPosMarkerMargin { get; set; }
        [Reactive] public partial int MetronomeVolume { get; set; }
        [Reactive] public partial int MetronomeHighFrequency { get; set; }
        [Reactive] public partial int MetronomeLowFrequency { get; set; }

        // Paths
        public string SingerPath => PathManager.Inst.SingersPath;
        public string AdditionalSingersPath => !string.IsNullOrWhiteSpace(PathManager.Inst.AdditionalSingersPath) ? PathManager.Inst.AdditionalSingersPath : "(None)";
        public string AdditionalResamplersPath => !string.IsNullOrWhiteSpace(PathManager.Inst.AdditionalResamplersPath) ? PathManager.Inst.AdditionalResamplersPath : "(None)";
        [Reactive] public partial bool InstallToAdditionalSingersPath { get; set; }
        [Reactive] public partial bool LoadDeepFolders { get; set; }

        // Editing
        public List<LyricsHelperOption> LyricsHelpers { get; } =
            ActiveLyricsHelper.Inst.Available
                .Select(klass => new LyricsHelperOption(klass))
                .ToList();
        [Reactive] public partial LyricsHelperOption? LyricsHelper { get; set; }
        [Reactive] public partial bool LyricsHelperBrackets { get; set; }
        [Reactive] public partial bool PenPlusDefault { get; set; }
        [Reactive] public partial bool ExtendEndingPhonemes { get; set; }

        // Render
        [Reactive] public partial bool PreRender { get; set; }
        [Reactive] public partial int NumRenderThreads { get; set; }
        public int LogicalCoreCount {
            get => Environment.ProcessorCount;
        }
        [Reactive] public partial bool HighThreads { get; set; }
        public int SafeMaxThreadCount {
            get => Math.Min(8, LogicalCoreCount / 2);
        }
        [Reactive] public partial bool SkipRenderingMutedTracks { get; set; }
        [Reactive] public partial bool ClearCacheOnQuit { get; set; }
        public List<string> OnnxRunnerOptions { get; set; }
        [Reactive] public partial string OnnxRunner { get; set; }
        public List<GpuInfo> OnnxGpuOptions { get; set; }
        [Reactive] public partial GpuInfo? OnnxGpu { get; set; }
        [Reactive] public partial bool ShowOnnxGpu { get; set; }

        // GAME backend (onnx / ggml)
        public List<string> GameBackendOptions { get; } = new() { "ONNX", "GGML" };
        [Reactive] public partial string GameBackend { get; set; }

        // Appearance
        [Reactive] public partial string ThemeName { get; set; }
        [Reactive] public partial string UiFontPath { get; set; }
        [Reactive] public partial string UiFontFamily { get; set; }
        [Reactive] public partial bool UseUiFontForNotes { get; set; }
        public List<string> UiFontFamilies { get; private set; } = new();
        [Reactive] public partial double ButtonCornerRadius { get; set; }
        [Reactive] public partial double NoteCornerRadius { get; set; }
        [Reactive] public partial double NoteOpacity { get; set; }
        [Reactive] public partial double NoteHighlightThickness { get; set; }
        [Reactive] public partial double UiCornerRadius { get; set; }
        [Reactive] public partial int DegreeStyle { get; set; }
        [Reactive] public partial bool UseTrackColor { get; set; }
        [Reactive] public partial bool ShowPortrait { get; set; }
        [Reactive] public partial bool ShowIcon { get; set; }
        [Reactive] public partial bool ShowGhostNotes { get; set; }
        [Reactive] public partial bool NoteHoverGlow { get; set; }
        [Reactive] public partial bool DiffSingerBarStyle { get; set; }
        [Reactive] public partial bool PitchEditMode { get; set; }
        [Reactive] public partial double PitchEditDim { get; set; }
        [Reactive] public partial bool ThemeEditable { get; set; }
        public List<string> ThemeItems => ThemeManager.GetAvailableThemes();
        public bool IsThemeEditorOpen => Views.ThemeEditorWindow.IsOpen;

        // UTAU
        public List<string> DefaultRendererOptions { get; set; }
        [Reactive] public partial string DefaultRenderer { get; set; }
        [Reactive] public partial int OtoEditor { get; set; }
        public string VLabelerPath => Preferences.Default.VLabelerPath;
        public string SetParamPath => Preferences.Default.SetParamPath;

        // Diffsinger
        public List<int> DiffSingerStepsOptions { get; } = new List<int> { 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
        public List<int> DiffSingerStepsVarianceOptions { get; } = new List<int> { 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
        public List<int> DiffSingerStepsPitchOptions { get; } = new List<int> { 2, 5, 10, 20, 50, 100, 200, 500, 1000 };
        [Reactive] public partial int DiffSingerSteps { get; set; }
        [Reactive] public partial int DiffSingerStepsVariance { get; set; }
        [Reactive] public partial int DiffSingerStepsPitch { get; set; }
        [Reactive] public partial double DiffSingerDepth { get; set; }
        [Reactive] public partial bool DiffSingerTensorCache { get; set; }
        [Reactive] public partial bool DiffSingerVarianceLocalPitchPatch { get; set; }
        [Reactive] public partial bool DiffSingerLangCodeHide { get; set; }
        [Reactive] public partial bool DiffSingerAutoSP { get; set; }
        [Reactive] public partial int DiffSingerAutoSPMs { get; set; }
        [Reactive] public partial bool DiffSingerLocalRetaking { get; set; }
        [Reactive] public partial bool DiffSingerShowRenderPhraseBoundaries { get; set; }
        [Reactive] public partial bool ShowWaveformPhraseBoundaries { get; set; }

        // Advanced
        [Reactive] public partial bool RememberMid { get; set; }
        [Reactive] public partial bool RememberUst { get; set; }
        [Reactive] public partial bool RememberVsqx { get; set; }
        public string WinePath => Preferences.Default.WinePath;

        public PreferencesViewModel() {
            var audioOutput = PlaybackManager.Inst.AudioOutput;
            if (audioOutput != null) {
                AudioOutputDevices = audioOutput.GetOutputDevices();
                int deviceNumber = audioOutput.DeviceNumber;
                var device = AudioOutputDevices.FirstOrDefault(d => d.deviceNumber == deviceNumber);
                if (device != null) {
                    AudioOutputDevice = device;
                }
                // Subscribe to device list changes to refresh UI when devices are plugged/unplugged.
                try {
                    audioOutput.DevicesChanged += (s, e) => {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                            try {
                                AudioOutputDevices = PlaybackManager.Inst.AudioOutput.GetOutputDevices();
                                int curDeviceNumber = PlaybackManager.Inst.AudioOutput.DeviceNumber;
                                var cur = AudioOutputDevices.FirstOrDefault(d => d.deviceNumber == curDeviceNumber);
                                if (cur != null) {
                                    AudioOutputDevice = cur;
                                }
                            } catch (Exception ex) {
                                Log.Warning(ex, "Failed to update audio device list on DevicesChanged");
                            }
                        });
                    };
                } catch { }
            }
            UseSystemDefaultDevice = Preferences.Default.UseSystemDefaultAudioDevice;
            PreferPortAudio = Preferences.Default.PreferPortAudio == true ? 1 : 0;
            PlaybackAutoScroll = Preferences.Default.PlaybackAutoScroll;
            PlaybackVerticalFollowMargin = Preferences.Default.PlaybackVerticalFollowMargin;
            PlaybackVerticalFollowDamping = Preferences.Default.PlaybackVerticalFollowDamping;
            PlaybackHighlightFadeInPerSecond = Preferences.Default.PlaybackHighlightFadeInPerSecond;
            PlaybackHighlightFadeOutPerSecond = Preferences.Default.PlaybackHighlightFadeOutPerSecond;
            PlaybackNoteBounceHeight = Preferences.Default.PlaybackNoteBounceHeight;
            PlaybackNoteBounceDuration = Preferences.Default.PlaybackNoteBounceDuration;
            PlayPosMarkerMargin = Preferences.Default.PlayPosMarkerMargin;
            MetronomeVolume = Preferences.Default.MetronomeVolume;
            MetronomeHighFrequency = Preferences.Default.MetronomeHighFrequency;
            MetronomeLowFrequency = Preferences.Default.MetronomeLowFrequency;
            LockStartTime = Preferences.Default.LockStartTime;
            InstallToAdditionalSingersPath = Preferences.Default.InstallToAdditionalSingersPath;
            LoadDeepFolders = Preferences.Default.LoadDeepFolderSinger;
            ToolsManager.Inst.Initialize();
            var pattern = new Regex(@"Strings\.([\w-]+)\.axaml");
            Languages = App.GetLanguages().Keys
                .Select(lang => CultureInfo.GetCultureInfo(lang))
                .ToList();
            Language = string.IsNullOrEmpty(Preferences.Default.Language)
                ? null
                : CultureInfo.GetCultureInfo(Preferences.Default.Language);
            SortingOrders = Languages.ToList();
            SortingOrders.Insert(0, CultureInfo.InvariantCulture);
            SortingOrder = Preferences.Default.SortingOrder == null ? Language
                : string.IsNullOrEmpty(Preferences.Default.SortingOrder) ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(Preferences.Default.SortingOrder);
            PreRender = Preferences.Default.PreRender;
            DefaultRendererOptions = Renderers.getRendererOptions();
            DefaultRenderer = String.IsNullOrEmpty(Preferences.Default.DefaultRenderer) ?
               DefaultRendererOptions[0] : Preferences.Default.DefaultRenderer;
            NumRenderThreads = Preferences.Default.NumRenderThreads;
            OnnxRunnerOptions = Onnx.getRunnerOptions();
            OnnxRunner = String.IsNullOrEmpty(Preferences.Default.OnnxRunner) ?
               OnnxRunnerOptions[0] : Preferences.Default.OnnxRunner;
            if (!OnnxRunnerOptions.Contains(OnnxRunner)) {
                OnnxRunner = OnnxRunnerOptions[0];
            }
            OnnxGpuOptions = Onnx.getGpuInfo();
            OnnxGpu = OnnxGpuOptions.Count > 0
                ? OnnxGpuOptions.FirstOrDefault(x => x.deviceId == Preferences.Default.OnnxGpu, OnnxGpuOptions[0])
                : null;
            ShowOnnxGpu = (OnnxRunner == "DirectML" || OnnxRunner == "CUDA");
            // GAME backend: ONNX is the default, GGML is available when installed.
            // The options list always includes both so the ComboBox UX is stable.
            GameBackend = Preferences.Default.GameBackend switch {
                "ggml" => "GGML",
                _ => "ONNX",  // default / empty / unrecognized all map to ONNX
            };
            DiffSingerDepth = Preferences.Default.DiffSingerDepth * 100;
            DiffSingerSteps = Preferences.Default.DiffSingerSteps;
            DiffSingerStepsVariance = Preferences.Default.DiffSingerStepsVariance;
            DiffSingerStepsPitch = Preferences.Default.DiffSingerStepsPitch;
            DiffSingerTensorCache = Preferences.Default.DiffSingerTensorCache;
            DiffSingerVarianceLocalPitchPatch = Preferences.Default.DiffSingerVarianceLocalPitchPatch;
            DiffSingerLangCodeHide = Preferences.Default.DiffSingerLangCodeHide;
            DiffSingerAutoSP = Preferences.Default.DiffSingerAutoSP;
            DiffSingerAutoSPMs = Math.Clamp(Preferences.Default.DiffSingerAutoSPMs, 10, 200);
            DiffSingerLocalRetaking = Preferences.Default.DiffSingerLocalRetaking;
            DiffSingerShowRenderPhraseBoundaries = Preferences.Default.DiffSingerShowRenderPhraseBoundaries;
            ShowWaveformPhraseBoundaries = Preferences.Default.ShowWaveformPhraseBoundaries;
            SkipRenderingMutedTracks = Preferences.Default.SkipRenderingMutedTracks;
            ThemeName = Preferences.Default.ThemeName;
            UiFontPath = Preferences.Default.UiFontPath;
            UiFontFamily = Preferences.Default.UiFontFamily;
            UseUiFontForNotes = Preferences.Default.UseUiFontForNotes;
            RefreshUiFontFamilies();
            ButtonCornerRadius = Preferences.Default.ButtonCornerRadius;
            NoteCornerRadius = Preferences.Default.NoteCornerRadius;
            NoteOpacity = Preferences.Default.NoteOpacity * 100;
            NoteHighlightThickness = Math.Max(0.5, Preferences.Default.NoteHighlightThickness);
            UiCornerRadius = Preferences.Default.UiCornerRadius;
            DegreeStyle = Preferences.Default.DegreeStyle;
            UseTrackColor = Preferences.Default.UseTrackColor;
            ShowPortrait = Preferences.Default.ShowPortrait;
            ShowIcon = Preferences.Default.ShowIcon;
            ShowGhostNotes = Preferences.Default.ShowGhostNotes;
            NoteHoverGlow = Preferences.Default.NoteHoverGlow;
            DiffSingerBarStyle = Preferences.Default.DiffSingerBarStyle;
            PitchEditMode = Preferences.Default.PitchEditMode;
            PitchEditDim = Preferences.Default.PitchEditDim;
            Beta = Preferences.Default.Beta;
            LyricsHelper = LyricsHelpers.FirstOrDefault(option => option.klass.Equals(ActiveLyricsHelper.Inst.GetPreferred()));
            LyricsHelperBrackets = Preferences.Default.LyricsHelperBrackets;
            OtoEditor = Preferences.Default.OtoEditor;
            RememberMid = Preferences.Default.RememberMid;
            RememberUst = Preferences.Default.RememberUst;
            RememberVsqx = Preferences.Default.RememberVsqx;
            ClearCacheOnQuit = Preferences.Default.ClearCacheOnQuit;
            ExtendEndingPhonemes = Preferences.Default.ExtendEndingPhonemes;

            MessageBus.Current.Listen<ThemeEditorStateChangedEvent>()
                .Subscribe(_ => this.RaisePropertyChanged(nameof(IsThemeEditorOpen)));
            
            this.WhenAnyValue(vm => vm.UseSystemDefaultDevice)
                .Subscribe(useDefault => {
                    Preferences.Default.UseSystemDefaultAudioDevice = useDefault;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.AudioOutputDevice)
                .Where(x => x != null).Select(x => x!)
                .Subscribe(device => {
                    if (UseSystemDefaultDevice) {
                        return;
                    }
                    if (PlaybackManager.Inst.AudioOutput != null) {
                        try {
                            PlaybackManager.Inst.AudioOutput.SelectDevice(device.guid, device.deviceNumber);
                        } catch (Exception e) {
                            DocManager.Inst.ExecuteCmd(new ErrorMessageNotification($"Failed to select device {device.name}", e));
                        }
                    }
                });
            this.WhenAnyValue(vm => vm.PreferPortAudio)
                .Subscribe(index => {
                    Preferences.Default.PreferPortAudio = index > 0;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackAutoScroll)
                .Subscribe(autoScroll => {
                    Preferences.Default.PlaybackAutoScroll = autoScroll;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackVerticalFollowMargin)
                .Subscribe(margin => {
                    Preferences.Default.PlaybackVerticalFollowMargin = Math.Clamp(margin, 0.0, 10.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackVerticalFollowDamping)
                .Subscribe(damping => {
                    Preferences.Default.PlaybackVerticalFollowDamping = Math.Clamp(damping, 1.0, 20.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackHighlightFadeInPerSecond)
                .Subscribe(value => {
                    Preferences.Default.PlaybackHighlightFadeInPerSecond = Math.Clamp(value, 0.1, 30.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackHighlightFadeOutPerSecond)
                .Subscribe(value => {
                    Preferences.Default.PlaybackHighlightFadeOutPerSecond = Math.Clamp(value, 0.1, 30.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackNoteBounceHeight)
                .Subscribe(value => {
                    Preferences.Default.PlaybackNoteBounceHeight = Math.Clamp(value, 1.0, 40.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlaybackNoteBounceDuration)
                .Subscribe(value => {
                    Preferences.Default.PlaybackNoteBounceDuration = Math.Clamp(value, 0.05, 2.0);
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PlayPosMarkerMargin)
                .Subscribe(playPosMarkerMargin => {
                    Preferences.Default.PlayPosMarkerMargin = playPosMarkerMargin;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.MetronomeVolume)
                .Subscribe(metronomeVolume => {
                    Preferences.Default.MetronomeVolume = metronomeVolume;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.MetronomeHighFrequency)
                .Subscribe(metronomeHighFrequency => {
                    Preferences.Default.MetronomeHighFrequency = metronomeHighFrequency;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.MetronomeLowFrequency)
                .Subscribe(metronomeLowFrequency => {
                    Preferences.Default.MetronomeLowFrequency = metronomeLowFrequency;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.LockStartTime)
                .Subscribe(lockStartTime => {
                    Preferences.Default.LockStartTime = lockStartTime;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.InstallToAdditionalSingersPath)
                .Subscribe(additionalSingersPath => {
                    Preferences.Default.InstallToAdditionalSingersPath = additionalSingersPath;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.LoadDeepFolders)
                .Subscribe(loadDeepFolders => {
                    Preferences.Default.LoadDeepFolderSinger = loadDeepFolders;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.PreRender)
                .Subscribe(preRender => {
                    Preferences.Default.PreRender = preRender;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.Language)
                .Subscribe(lang => {
                    Preferences.Default.Language = lang?.Name ?? string.Empty;
                    Preferences.Save();
                    App.SetLanguage(Preferences.Default.Language);
                });
            this.WhenAnyValue(vm => vm.SortingOrder)
                .Subscribe(so => {
                    Preferences.Default.SortingOrder = so?.Name ?? null;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.ThemeName)
                .Subscribe(themeName => {
                    ThemeEditable = !ThemeManager.IsBuiltInTheme(themeName) && !Colors.CustomTheme.IsPackageTheme(themeName);
                    if (!IsThemeEditorOpen) {
                        Preferences.Default.ThemeName = themeName;
                        Preferences.Save();
                        App.SetTheme();
                    }
                });
            this.WhenAnyValue(vm => vm.UiFontPath)
                .Subscribe(path => {
                    Preferences.Default.UiFontPath = UiFontManager.IsSupportedFontPath(path) ? path : string.Empty;
                    Preferences.Save();
                    UiFontManager.Apply();
                    RefreshUiFontFamilies();
                });
            this.WhenAnyValue(vm => vm.UiFontFamily)
                .Subscribe(family => {
                    Preferences.Default.UiFontFamily = family?.Trim() ?? string.Empty;
                    Preferences.Save();
                    UiFontManager.Apply();
                });
            this.WhenAnyValue(vm => vm.UseUiFontForNotes)
                .Subscribe(useUiFont => {
                    Preferences.Default.UseUiFontForNotes = useUiFont;
                    Preferences.Save();
                    OpenUtau.App.Controls.TextLayoutCache.Clear();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.ButtonCornerRadius)
                .Subscribe(radius => {
                    Preferences.Default.ButtonCornerRadius = Math.Clamp(radius, 0, 10);
                    Preferences.Save();
                    App.SetUiCornerRadii();
                });
            this.WhenAnyValue(vm => vm.NoteCornerRadius)
                .Subscribe(radius => {
                    Preferences.Default.NoteCornerRadius = Math.Clamp(radius, 0, 10);
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.NoteOpacity)
                .Subscribe(opacity => {
                    Preferences.Default.NoteOpacity = Math.Clamp(opacity / 100, 0, 1);
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.NoteHighlightThickness)
                .Subscribe(thickness => {
                    Preferences.Default.NoteHighlightThickness = Math.Clamp(thickness, 0.5, 6);
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.UiCornerRadius)
                .Subscribe(radius => {
                    Preferences.Default.UiCornerRadius = Math.Clamp(radius, 0, 10);
                    Preferences.Save();
                    App.SetUiCornerRadii();
                });
            this.WhenAnyValue(vm => vm.DegreeStyle)
                .Subscribe(degreeStyle => {
                    Preferences.Default.DegreeStyle = degreeStyle;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PianorollRefreshEvent("Part"));
                });
            this.WhenAnyValue(vm => vm.UseTrackColor)
                .Subscribe(trackColor => {
                    Preferences.Default.UseTrackColor = trackColor;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PianorollRefreshEvent("TrackColor"));
                    MessageBus.Current.SendMessage(new TracksRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.ShowPortrait)
                .Subscribe(showPortrait => {
                    Preferences.Default.ShowPortrait = showPortrait;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PianorollRefreshEvent("Portrait"));
                });
            this.WhenAnyValue(vm => vm.ShowIcon)
                .Subscribe(showIcon => {
                    Preferences.Default.ShowIcon = showIcon;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PianorollRefreshEvent("Portrait"));
                });
            this.WhenAnyValue(vm => vm.ShowGhostNotes)
                .Subscribe(showGhostNotes => {
                    Preferences.Default.ShowGhostNotes = showGhostNotes;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PianorollRefreshEvent("Part"));
                });
            this.WhenAnyValue(vm => vm.NoteHoverGlow)
                .Subscribe(noteHoverGlow => {
                    Preferences.Default.NoteHoverGlow = noteHoverGlow;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.DiffSingerBarStyle)
                .Subscribe(value => {
                    Preferences.Default.DiffSingerBarStyle = value;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.PitchEditMode)
                .Subscribe(pitchEditMode => {
                    Preferences.Default.PitchEditMode = pitchEditMode;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PitchEditModePrefChangedEvent());
                });
            this.WhenAnyValue(vm => vm.PitchEditDim)
                .Subscribe(pitchEditDim => {
                    Preferences.Default.PitchEditDim = pitchEditDim;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new PitchEditModePrefChangedEvent());
                });
            this.WhenAnyValue(vm => vm.Beta)
                .Subscribe(beta => {
                    Preferences.Default.Beta = beta;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.LyricsHelper)
                .Subscribe(option => {
                    ActiveLyricsHelper.Inst.Set(option?.klass);
                    Preferences.Default.LyricHelper = option?.klass?.Name ?? string.Empty;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.LyricsHelperBrackets)
                .Subscribe(brackets => {
                    Preferences.Default.LyricsHelperBrackets = brackets;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.OtoEditor)
                .Subscribe(index => {
                    Preferences.Default.OtoEditor = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.NumRenderThreads)
                .Subscribe(index => {
                    Preferences.Default.NumRenderThreads = index;
                    HighThreads = index > SafeMaxThreadCount ? true : false;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DefaultRenderer)
                .Subscribe(index => {
                    Preferences.Default.DefaultRenderer = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.OnnxRunner)
                .Subscribe(index => {
                    Preferences.Default.OnnxRunner = index;
                    Preferences.Save();
                    ToggleOnnxGpuDisplay(index == "DirectML" || index == "CUDA");
                });
            this.WhenAnyValue(vm => vm.OnnxGpu)
                .Where(x => x != null).Select(x => x!)
                .Subscribe(index => {
                    Preferences.Default.OnnxGpu = index.deviceId;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.GameBackend)
                .Subscribe(index => {
                    Preferences.Default.GameBackend = index == "GGML" ? "ggml" : "onnx";
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.RememberMid)
                .Subscribe(index => {
                    Preferences.Default.RememberMid = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.RememberUst)
                .Subscribe(index => {
                    Preferences.Default.RememberUst = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.RememberVsqx)
                .Subscribe(index => {
                    Preferences.Default.RememberVsqx = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.ClearCacheOnQuit)
                .Subscribe(index => {
                    Preferences.Default.ClearCacheOnQuit = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerSteps)
                .Subscribe(index => {
                    Preferences.Default.DiffSingerSteps = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerStepsVariance)
                 .Subscribe(index => {
                     Preferences.Default.DiffSingerStepsVariance = index;
                     Preferences.Save();
                 });
            this.WhenAnyValue(vm => vm.DiffSingerStepsPitch)
                .Subscribe(index => {
                    Preferences.Default.DiffSingerStepsPitch = index;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerDepth)
                .Subscribe(index => {
                    Preferences.Default.DiffSingerDepth = index / 100;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerTensorCache)
                .Subscribe(useCache => {
                    Preferences.Default.DiffSingerTensorCache = useCache;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerVarianceLocalPitchPatch)
                .Subscribe(useLocalPatch => {
                    Preferences.Default.DiffSingerVarianceLocalPitchPatch = useLocalPatch;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerLangCodeHide)
                .Subscribe(useCache => {
                    Preferences.Default.DiffSingerLangCodeHide = useCache;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerAutoSP)
                .Skip(1)
                .Subscribe(enabled => {
                    Preferences.Default.DiffSingerAutoSP = enabled;
                    Preferences.Save();
                    RePredictDiffSingerDurations();
                });
            this.WhenAnyValue(vm => vm.DiffSingerAutoSPMs)
                .Skip(1)
                .Throttle(TimeSpan.FromMilliseconds(400))
                .Subscribe(ms => {
                    Preferences.Default.DiffSingerAutoSPMs = Math.Clamp(ms, 10, 200);
                    Preferences.Save();
                    RePredictDiffSingerDurations();
                });
            this.WhenAnyValue(vm => vm.DiffSingerLocalRetaking)
                .Skip(1)
                .Subscribe(value => {
                    Preferences.Default.DiffSingerLocalRetaking = value;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.DiffSingerShowRenderPhraseBoundaries)
                .Subscribe(showBoundaries => {
                    Preferences.Default.DiffSingerShowRenderPhraseBoundaries = showBoundaries;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.ShowWaveformPhraseBoundaries)
                .Subscribe(showBoundaries => {
                    Preferences.Default.ShowWaveformPhraseBoundaries = showBoundaries;
                    Preferences.Save();
                    MessageBus.Current.SendMessage(new NotesRefreshEvent());
                });
            this.WhenAnyValue(vm => vm.SkipRenderingMutedTracks)
                .Subscribe(skipRenderingMutedTracks => {
                    Preferences.Default.SkipRenderingMutedTracks = skipRenderingMutedTracks;
                    Preferences.Save();
                });
            this.WhenAnyValue(vm => vm.ExtendEndingPhonemes)
                .Subscribe(extend => {
                    Preferences.Default.ExtendEndingPhonemes = extend;
                    Preferences.Save();
                    if (DocManager.Inst.Project != null) {
                        DocManager.Inst.Project.ValidateFull();
                        DocManager.Inst.ExecuteCmd(new ValidateProjectNotification());
                    }
                });
        }

        public void TestAudioOutputDevice() {
            try {
                PlaybackManager.Inst.PlayTestSound();
            } catch (Exception e) {
                Log.Error(e, "Failed to play test sound.");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification("Failed to play test sound.", e));
            }
        }
        public void TestMetronome() {
            try {
                PlaybackManager.Inst.PlayMetronomeClick();
            } catch (Exception e) {
                Log.Error(e, "Failed to play metronome preview.");
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification("Failed to play metronome preview.", e));
            }
        }

        public void ResetMetronomeVolume() {
            MetronomeVolume = new Preferences.SerializablePreferences().MetronomeVolume;
        }

        public void ResetMetronomeHighFrequency() {
            MetronomeHighFrequency = new Preferences.SerializablePreferences().MetronomeHighFrequency;
        }

        public void ResetMetronomeLowFrequency() {
            MetronomeLowFrequency = new Preferences.SerializablePreferences().MetronomeLowFrequency;
        }

        public void OpenResamplerLocation() {
            try {
                string path = PathManager.Inst.ResamplersPath;
                Directory.CreateDirectory(path);
                OS.OpenFolder(path);
            } catch (Exception e) {
                DocManager.Inst.ExecuteCmd(new ErrorMessageNotification(e));
            }
        }

        public void SetAddlSingersPath(string path) {
            Preferences.Default.AdditionalSingerPath = path;
            Preferences.Save();
            this.RaisePropertyChanged(nameof(AdditionalSingersPath));
        }

        public void SetAddlResamplersPath(string path) {
            Preferences.Default.AdditionalResamplerPath = path;
            Preferences.Save();
            ToolsManager.Inst.SearchResamplers();
            this.RaisePropertyChanged(nameof(AdditionalResamplersPath));
        }

        public void SetVLabelerPath(string path) {
            Preferences.Default.VLabelerPath = path;
            Preferences.Save();
            this.RaisePropertyChanged(nameof(VLabelerPath));
        }

        public void SetSetParamPath(string path) {
            Preferences.Default.SetParamPath = path;
            Preferences.Save();
            this.RaisePropertyChanged(nameof(SetParamPath));
        }

        public void SetUiFontPath(string path) {
            UiFontPath = path;
            UiFontFamily = UiFontManager.GetSuggestedFamilyName(path);
        }

        public void ResetUiFont() {
            UiFontPath = string.Empty;
            UiFontFamily = string.Empty;
        }

        private void RefreshUiFontFamilies() {
            UiFontFamilies = UiFontManager.GetAvailableFontFamilies();
            if (!string.IsNullOrWhiteSpace(UiFontFamily)
                && !UiFontFamilies.Contains(UiFontFamily, StringComparer.OrdinalIgnoreCase)) {
                UiFontFamilies.Insert(0, UiFontFamily);
            }
            this.RaisePropertyChanged(nameof(UiFontFamilies));
        }

        public void SetWinePath(string path) {
            Preferences.Default.WinePath = path;
            Preferences.Save();
            ToolsManager.Inst.Initialize();
            this.RaisePropertyChanged(nameof(WinePath));
        }

        private void RePredictDiffSingerDurations() {
            if (DocManager.Inst.Project == null) {
                return;
            }
            DocManager.Inst.ExecuteCmd(new ValidateProjectNotification());
            DocManager.Inst.ExecuteCmd(new PreRenderNotification());
        }

        public void RefreshThemes() {
            Colors.CustomTheme.ListThemes();
            _ = OudepLoaderRegistry.LoadAllAsync();
            this.RaisePropertyChanged(nameof(ThemeItems));
        }

        public void ToggleOnnxGpuDisplay(bool show) {
            ShowOnnxGpu = show;
        }
    }
}
