using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using static ReactiveUI.Primitives.SubscribeExtensions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using OpenUtau.App.ViewModels;
using OpenUtau.App.Roflofic;
using OpenUtau.Core;
using OpenUtau.Core.Render;
using OpenUtau.Core.Ustx;
using OpenUtau.Core.Util;
using ReactiveUI;

namespace OpenUtau.App.Controls {
    class NotesCanvas : Control {
        public static readonly DirectProperty<NotesCanvas, double> TickWidthProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, double>(
                nameof(TickWidth),
                o => o.TickWidth,
                (o, v) => o.TickWidth = v);
        public static readonly DirectProperty<NotesCanvas, double> TrackHeightProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, double>(
                nameof(TrackHeight),
                o => o.TrackHeight,
                (o, v) => o.TrackHeight = v);
        public static readonly DirectProperty<NotesCanvas, double> TickOffsetProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, double>(
                nameof(TickOffset),
                o => o.TickOffset,
                (o, v) => o.TickOffset = v);
        public static readonly DirectProperty<NotesCanvas, double> TrackOffsetProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, double>(
                nameof(TrackOffset),
                o => o.TrackOffset,
                (o, v) => o.TrackOffset = v);
        public static readonly DirectProperty<NotesCanvas, UVoicePart?> PartProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, UVoicePart?>(
                nameof(Part),
                o => o.Part,
                (o, v) => o.Part = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPitchProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(ShowPitch),
                o => o.ShowPitch,
                (o, v) => o.ShowPitch = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowFinalPitchProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(ShowFinalPitch),
                o => o.ShowFinalPitch,
                (o, v) => o.ShowFinalPitch = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowVibratoProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(ShowVibrato),
                o => o.ShowVibrato,
                (o, v) => o.ShowVibrato = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPlaybackNoteHighlightProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(nameof(ShowPlaybackNoteHighlight),
                o => o.ShowPlaybackNoteHighlight, (o, v) => o.ShowPlaybackNoteHighlight = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPlaybackNoteBounceProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(nameof(ShowPlaybackNoteBounce),
                o => o.ShowPlaybackNoteBounce, (o, v) => o.ShowPlaybackNoteBounce = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPlaybackNoteOrbitProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(nameof(ShowPlaybackNoteOrbit),
                o => o.ShowPlaybackNoteOrbit, (o, v) => o.ShowPlaybackNoteOrbit = v);
        public static readonly DirectProperty<NotesCanvas, int> PlayPosTickProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, int>(nameof(PlayPosTick),
                o => o.PlayPosTick, (o, v) => o.PlayPosTick = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPhonemizerTagsProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(ShowPhonemizerTags),
                o => o.ShowPhonemizerTags,
                (o, v) => o.ShowPhonemizerTags = v);
        public static readonly DirectProperty<NotesCanvas, bool> ShowPhonemePanelProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(ShowPhonemePanel),
                o => o.ShowPhonemePanel,
                (o, v) => o.ShowPhonemePanel = v);
        public static readonly DirectProperty<NotesCanvas, bool> PitchEditModeProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, bool>(
                nameof(PitchEditMode),
                o => o.PitchEditMode,
                (o, v) => o.PitchEditMode = v);
        public static readonly DirectProperty<NotesCanvas, double> PitchEditDimProperty =
            AvaloniaProperty.RegisterDirect<NotesCanvas, double>(
                nameof(PitchEditDim),
                o => o.PitchEditDim,
                (o, v) => o.PitchEditDim = v);

        public double TickWidth {
            get => tickWidth;
            private set => SetAndRaise(TickWidthProperty, ref tickWidth, value);
        }
        public double TrackHeight {
            get => trackHeight;
            private set => SetAndRaise(TrackHeightProperty, ref trackHeight, value);
        }
        public double TickOffset {
            get => tickOffset;
            private set => SetAndRaise(TickOffsetProperty, ref tickOffset, value);
        }
        public double TrackOffset {
            get => trackOffset;
            private set => SetAndRaise(TrackOffsetProperty, ref trackOffset, value);
        }
        public UVoicePart? Part {
            get => part;
            set => SetAndRaise(PartProperty, ref part, value);
        }
        public bool ShowPitch {
            get => showPitch;
            private set => SetAndRaise(ShowPitchProperty, ref showPitch, value);
        }
        public bool ShowFinalPitch {
            get => showFinalPitch;
            private set => SetAndRaise(ShowFinalPitchProperty, ref showFinalPitch, value);
        }
        public bool ShowVibrato {
            get => showVibrato;
            private set => SetAndRaise(ShowVibratoProperty, ref showVibrato, value);
        }
        public bool ShowPlaybackNoteHighlight {
            get => showPlaybackNoteHighlight;
            private set => SetAndRaise(ShowPlaybackNoteHighlightProperty, ref showPlaybackNoteHighlight, value);
        }
        public bool ShowPlaybackNoteBounce {
            get => showPlaybackNoteBounce;
            private set => SetAndRaise(ShowPlaybackNoteBounceProperty, ref showPlaybackNoteBounce, value);
        }
        public bool ShowPlaybackNoteOrbit {
            get => showPlaybackNoteOrbit;
            private set => SetAndRaise(ShowPlaybackNoteOrbitProperty, ref showPlaybackNoteOrbit, value);
        }
        public int PlayPosTick {
            get => playPosTick;
            private set => SetAndRaise(PlayPosTickProperty, ref playPosTick, value);
        }
        public bool ShowPhonemizerTags {
            get => showPhonemizerTags;
            private set => SetAndRaise(ShowPhonemizerTagsProperty, ref showPhonemizerTags, value);
        }
        public bool ShowPhonemePanel {
            get => showPhonemePanel;
            private set => SetAndRaise(ShowPhonemePanelProperty, ref showPhonemePanel, value);
        }
        public bool PitchEditMode {
            get => pitchEditMode;
            private set => SetAndRaise(PitchEditModeProperty, ref pitchEditMode, value);
        }
        public double PitchEditDim {
            get => pitchEditDim;
            private set {
                if (SetAndRaise(PitchEditDimProperty, ref pitchEditDim, value)) {
                    pitchEditDimBrush = new ImmutableSolidColorBrush(
                        Color.FromArgb((byte)Math.Clamp(pitchEditDim * 2.55, 0, 255), 0, 0, 0));
                }
            }
        }

        private double tickWidth;
        private double trackHeight;
        private double tickOffset;
        private double trackOffset;
        private UVoicePart? part;
        private bool showPitch = true;
        private bool showFinalPitch = true;
        private bool showVibrato = true;
        private bool showPlaybackNoteHighlight;
        private bool showPlaybackNoteBounce;
        private bool showPlaybackNoteOrbit;
        private int playPosTick = int.MinValue;
        private UNote? activePlaybackNote;
        private UNote? fadingPlaybackNote;
        private float activeHighlight;
        private float fadingHighlight;
        private float activeBounceElapsed;
        private float playbackOrbitElapsed;
        private DateTime highlightLastFrame = DateTime.UtcNow;
        private readonly DispatcherTimer highlightTimer;
        private readonly Dictionary<(Color from, Color to, byte amount), IBrush> highlightBrushes = new();
        private bool showPhonemizerTags = true;
        private bool showPhonemePanel;
        private bool pitchEditMode;
        private double pitchEditDim = 59;

        private IBrush pitchEditDimBrush = new ImmutableSolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        private Pen? pitchEditPen;
        private Points points = new Points();

        private HashSet<UNote> selectedNotes = new HashSet<UNote>();
        private Geometry pointGeometry;

        private bool showGhostNotes = true;
        private List<UPart> otherPartsInView = new List<UPart>();

        private const double HoverGlowDuration = 0.12;
        private UNote? hoverNote;
        private UNote? fadingHoverNote;
        private float hoverGlow;
        private float hoverFadeGlow;
        private DateTime hoverLastFrame = DateTime.UtcNow;
        private readonly DispatcherTimer hoverTimer;
        private Point lastPointerPos;
        private readonly Dictionary<(Color color, byte alpha, int thickness), Pen> glowPens = new();

        public NotesCanvas() {
            ClipToBounds = true;
            pointGeometry = new EllipseGeometry(new Rect(-2.5, -2.5, 5, 5));
            highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30.0) };
            highlightTimer.Tick += (_, _) => UpdatePlaybackHighlight(false);
            RofloficEffects.Changed += InvalidateVisual;
            hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0) };
            hoverTimer.Tick += (_, _) => UpdateHoverGlow();

            MessageBus.Current.Listen<NotesRefreshEvent>()
                .Subscribe(_ => InvalidateVisual());
            MessageBus.Current.Listen<NotesSelectionEvent>()
                .Subscribe(e => {
                    selectedNotes.Clear();
                    selectedNotes.UnionWith(e.selectedNotes);
                    selectedNotes.UnionWith(e.tempSelectedNotes);
                    InvalidateVisual();
                });
            MessageBus.Current.Listen<PartRefreshEvent>()
                .Subscribe(_ => RefreshGhostNotes());
            this.WhenAnyValue(x => x.Part)
                .Subscribe(_ => {
                    RefreshGhostNotes();
                    hoverNote = null;
                    fadingHoverNote = null;
                    hoverGlow = 0;
                    hoverFadeGlow = 0;
                    hoverTimer.Stop();
                });
        }

        void RefreshGhostNotes() {
            showGhostNotes = Convert.ToBoolean(Preferences.Default.ShowGhostNotes);
            if (Part == null || !showGhostNotes) {
                return;
            }
            otherPartsInView = DocManager.Inst.Project.parts
                .Where(other => other.trackNo != Part.trackNo &&
                    other.position < Part.End &&
                    Part.position < other.End)
                .ToList();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == PlayPosTickProperty) {
                if (!ShowPlaybackNoteHighlight &&
                    !ShowPlaybackNoteBounce && !ShowPlaybackNoteOrbit) {
                    return;
                }
                playbackSeekPending = true;
                UpdatePlaybackHighlight(true);
                return;
            }
            if (change.Property == ShowPlaybackNoteHighlightProperty ||
                change.Property == ShowPlaybackNoteBounceProperty ||
                change.Property == ShowPlaybackNoteOrbitProperty) {
                playbackSeekPending = true;
                UpdatePlaybackHighlight(false);
                InvalidateVisual();
                return;
            }
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e) {
            base.OnPointerMoved(e);
            lastPointerPos = e.GetPosition(this);
            UpdateHoveredNote();
        }

        protected override void OnPointerExited(PointerEventArgs e) {
            base.OnPointerExited(e);
            SetHoveredNote(null);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e) {
            base.OnPointerPressed(e);
            SetHoveredNote(null);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) {
            base.OnPointerReleased(e);
            UpdateHoveredNote();
        }

        void UpdateHoveredNote() {
            if (!Preferences.Default.NoteHoverGlow || Part == null) {
                SetHoveredNote(null);
                return;
            }
            var viewModel = ((PianoRollViewModel?)DataContext)?.NotesViewModel;
            if (viewModel == null) {
                SetHoveredNote(null);
                return;
            }
            double tick = viewModel.PointToTick(lastPointerPos);
            int tone = viewModel.PointToTone(lastPointerPos);
            UNote? found = null;
            foreach (var note in Part.notes) {
                if (note.position > tick && note.LeftBound > tick) {
                    break;
                }
                if (note.LeftBound <= tick && tick < note.RightBound && note.AdjustedTone == tone) {
                    found = note;
                    break;
                }
            }
            SetHoveredNote(found);
        }

        void SetHoveredNote(UNote? note) {
            if (!Preferences.Default.NoteHoverGlow) {
                note = null;
            }
            if (ReferenceEquals(note, hoverNote)) {
                return;
            }
            if (hoverNote != null && hoverGlow > 0.001f) {
                fadingHoverNote = hoverNote;
                hoverFadeGlow = hoverGlow;
            } else if (hoverNote != null) {
                fadingHoverNote = null;
                hoverFadeGlow = 0;
            }
            hoverNote = note;
            hoverGlow = 0;
            hoverLastFrame = DateTime.UtcNow;
            hoverTimer.Start();
        }

        void UpdateHoverGlow() {
            var now = DateTime.UtcNow;
            float dt = (float)Math.Clamp((now - hoverLastFrame).TotalSeconds, 0, 0.1);
            hoverLastFrame = now;
            float step = dt / (float)HoverGlowDuration;
            bool changed = false;
            float newActive = MoveTowards(hoverGlow, hoverNote == null ? 0f : 1f, step);
            if (newActive != hoverGlow) {
                hoverGlow = newActive;
                changed = true;
            }
            float newFade = MoveTowards(hoverFadeGlow, 0f, step);
            if (newFade != hoverFadeGlow) {
                hoverFadeGlow = newFade;
                changed = true;
            }
            if (hoverFadeGlow <= 0.001f) {
                fadingHoverNote = null;
                hoverFadeGlow = 0;
            }
            bool settled = (hoverNote == null ? hoverGlow == 0f : hoverGlow == 1f) && fadingHoverNote == null;
            if (!changed && settled) {
                hoverTimer.Stop();
                return;
            }
            InvalidateVisual();
        }

        float GetHoverGlow(UNote note) {
            if (note == hoverNote) {
                return hoverGlow;
            }
            if (note == fadingHoverNote) {
                return hoverFadeGlow;
            }
            return 0f;
        }

        Pen GetGlowPen(Color color, byte alpha, int thickness) {
            var key = (color, alpha, thickness);
            if (!glowPens.TryGetValue(key, out var pen)) {
                pen = new Pen(new ImmutableSolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), thickness) {
                    LineJoin = PenLineJoin.Round,
                };
                glowPens[key] = pen;
            }
            return pen;
        }

        void DrawHoverGlow(DrawingContext context, Point leftTop, Size size, double radius, float glow) {
            if (glow <= 0.01f || !(ThemeManager.AccentBrush2 is ISolidColorBrush solid)) {
                return;
            }
            byte alpha = (byte)Math.Clamp((int)Math.Round(glow * 100), 0, 255);
            context.DrawRectangle(null, GetGlowPen(solid.Color, alpha, 2),
                Inflate(leftTop, size, 1), radius + 1, radius + 1);
            context.DrawRectangle(null, GetGlowPen(solid.Color, (byte)(alpha * 2 / 5), 3),
                Inflate(leftTop, size, 2.5), radius + 2.5, radius + 2.5);
        }

        static Rect Inflate(Point leftTop, Size size, double d) =>
            new Rect(leftTop.X - d, leftTop.Y - d, size.Width + d * 2, size.Height + d * 2);

        public override void Render(DrawingContext context) {
            base.Render(context);
            if (Part == null) {
                return;
            }
            var viewModel = ((PianoRollViewModel?)DataContext)?.NotesViewModel;
            if (viewModel == null) {
                return;
            }
            renderPassActive = true;
            try {
                DrawBackgroundForHitTest(context);
                double leftTick = TickOffset - 480;
                double rightTick = TickOffset + Bounds.Width / TickWidth + 480;
                bool hidePitch = viewModel.TickWidth <= ViewConstants.PianoRollTickWidthShowDetails * 0.5;
                bool seek = playbackSeekPending;
                playbackSeekPending = false;
                UpdatePlaybackHighlight(seek);
                PrepareNoteRenderState();

                if (showGhostNotes) {
                    foreach (UPart otherPart in otherPartsInView) {
                        if (otherPart is UVoicePart otherVoicePart) {
                            var xOffset = otherVoicePart.position - Part.position;
                            var brush = ThemeManager.NeutralAccentBrushSemi;
                            if (otherVoicePart.trackNo >= 0) {
                                var track = DocManager.Inst.Project.tracks[otherVoicePart.trackNo];
                                brush = ThemeManager.GetTrackColor(track.TrackColor).AccentColorLightSemi;
                            }

                            foreach (var note in otherVoicePart.notes) {
                                if (note.LeftBound + xOffset >= rightTick || note.RightBound + xOffset <= leftTick) {
                                    continue;
                                }
                                RenderGhostNote(note, viewModel, context, xOffset, brush);
                            }
                        }
                    }
                }

                foreach (var note in Part.notes) {
                    if (note.LeftBound >= rightTick || note.RightBound <= leftTick) {
                        continue;
                    }
                    RenderNoteBody(note, viewModel, context);
                }
                if (ShowPhonemePanel && viewModel.ShowPhonemePanelButton) {
                    RenderPhonemePanels(viewModel, context, leftTick, rightTick);
                }
                RenderDiffSingerPhraseBoundaries(leftTick, rightTick, viewModel, context);
                if (ShowFinalPitch && !hidePitch) {
                    RenderFinalPitch(leftTick, rightTick, viewModel, context);
                }
                foreach (var note in Part.notes) {
                    if (note.LeftBound >= rightTick || note.RightBound <= leftTick) {
                        continue;
                    }
                    if (ShowPitch && !hidePitch) {
                        RenderPitchBend(note, viewModel, context);
                    }
                    if ((ShowPitch || ShowVibrato) && !hidePitch) {
                        RenderVibrato(note, viewModel, context);
                    }
                    if (ShowVibrato && !note.Error && !hidePitch) {
                        RenderVibratoToggle(note, viewModel, context);
                        RenderVibratoControl(note, viewModel, context);
                    }
                }
                if (PitchEditMode) {
                    UpdatePitchEditVisuals();
                    context.FillRectangle(pitchEditDimBrush, Bounds.WithX(0).WithY(0));
                    RenderFinalPitch(leftTick, rightTick, viewModel, context, bright: true);
                }
            } finally {
                renderPassActive = false;
            }
        }

        private void UpdatePitchEditVisuals() {
            const double blend = 0.6;
            var source = ThemeManager.FinalPitchPen?.Brush as ISolidColorBrush;
            if (source == null && ThemeManager.AccentBrush3 is ISolidColorBrush accent) {
                source = accent;
            }
            if (source != null) {
                var c = source.Color;
                var bright = Color.FromRgb(
                    (byte)(c.R + (255 - c.R) * blend),
                    (byte)(c.G + (255 - c.G) * blend),
                    (byte)(c.B + (255 - c.B) * blend));
                var brush = new ImmutableSolidColorBrush(bright);
                pitchEditPen = new Pen(brush, 2.5);
            } else {
                pitchEditPen = new Pen(Brushes.White, 2.5);
            }
        }

        private void DrawBackgroundForHitTest(DrawingContext context) {
            context.DrawRectangle(Brushes.Transparent, null, Bounds.WithX(0).WithY(0));
        }

        private void RenderPhonemePanels(NotesViewModel viewModel, DrawingContext context, double leftTick, double rightTick) {
            if (Part == null) {
                return;
            }
            string langCode = PhonemeUIRender.getLangCode(Part);
            var phonemesByParent = PhonemePanelLayout.GetPhonemesByParent(Part);
            foreach (var note in Part.notes) {
                if (note.LeftBound >= rightTick || note.RightBound <= leftTick) {
                    continue;
                }
                var size = viewModel.TickToneToSize(note.duration, 1);
                var layout = PhonemePanelLayout.Build(note, langCode, size.Width, phonemesByParent);
                if (layout == null) {
                    continue;
                }
                var bounds = PhonemePanelLayout.GetPanelBounds(viewModel, note, layout);
                foreach (var token in layout.tokens) {
                    using (var state = context.PushTransform(Matrix.CreateTranslation(bounds.X + token.x, bounds.Y))) {
                        token.layout.Draw(context, new Point());
                    }
                }
            }
        }

        private readonly HashSet<UNote> renderErroredParents = new();
        private readonly HashSet<UNote> renderPhonemeParents = new();
        private Pen? renderHighlightPen;

        private void PrepareNoteRenderState() {
            renderErroredParents.Clear();
            renderPhonemeParents.Clear();
            var phonemes = Part?.phonemes;
            if (phonemes != null) {
                foreach (var phoneme in phonemes) {
                    if (phoneme.Parent == null) {
                        continue;
                    }
                    renderPhonemeParents.Add(phoneme.Parent);
                    if (phoneme.Error) {
                        renderErroredParents.Add(phoneme.Parent);
                    }
                }
            }
            renderHighlightPen = new Pen(ThemeManager.AccentBrush2, Preferences.Default.NoteHighlightThickness);
        }

        private sealed class NullDisposable : IDisposable {
            public static readonly NullDisposable Instance = new NullDisposable();
            public void Dispose() { }
        }

        private void RenderNoteBody(UNote note, NotesViewModel viewModel, DrawingContext context) {
            Point leftTop = viewModel.TickToneToPoint(note.position, note.AdjustedTone);
            leftTop = leftTop.WithX(leftTop.X + 1).WithY(Math.Round(leftTop.Y + 1));
            Vector playbackOffset = GetPlaybackAnimationOffset(note);
            leftTop += playbackOffset;
            Size size = viewModel.TickToneToSize(note.duration, 1);
            size = size.WithWidth(size.Width - 1).WithHeight(Math.Floor(size.Height - 2));
            Point rightBottom = new Point(leftTop.X + size.Width, leftTop.Y + size.Height);

            bool hasError = note.Error ||
                renderErroredParents.Contains(note) ||
                (!note.lyric.StartsWith("+") && !note.lyric.StartsWith("-") &&
                 !renderPhonemeParents.Contains(note));

            Matrix rotationMatrix = GetPlaybackRotation(note, new Point(
                leftTop.X + size.Width / 2, leftTop.Y + size.Height / 2));
            using IDisposable rotation = rotationMatrix == Matrix.Identity
                ? (IDisposable)NullDisposable.Instance
                : context.PushTransform(rotationMatrix);
            var brush = selectedNotes.Contains(note)
                ? (hasError ? ThemeManager.AccentBrush3Semi : ThemeManager.AccentBrush2)
                : (hasError ? ThemeManager.NeutralAccentBrushSemi : ThemeManager.AccentBrush1);
            if (RofloficEffects.RainbowEnabled) {
                brush = RofloficEffects.Gradient(note.position * 0.002, 230);
            }
            if (!selectedNotes.Contains(note)) {
                float highlight = ShowPlaybackNoteHighlight
                    ? (note == activePlaybackNote ? activeHighlight : note == fadingPlaybackNote ? fadingHighlight : 0)
                    : 0;
                brush = BlendBrush(brush, hasError ? ThemeManager.AccentBrush3Semi : ThemeManager.AccentBrush2, highlight);
            }
            brush = ApplyNoteOpacity(brush);
            double radius = GetNoteCornerRadius(size);
            context.DrawRectangle(brush, null, new Rect(leftTop, rightBottom), radius, radius);
            if (!selectedNotes.Contains(note)) {
                context.DrawRectangle(null, renderHighlightPen, new Rect(leftTop, rightBottom), radius, radius);
            }
            if (Preferences.Default.NoteHoverGlow) {
                DrawHoverGlow(context, leftTop, size, radius, GetHoverGlow(note));
            }
            if (TrackHeight < 10 || note.lyric.Length == 0) {
                return;
            }
            // grey out the Phonemizer Transition Badges
            if (ShowPhonemizerTags && TrackHeight >= 20) {
                string currentOver = note.PhonemizerOverride ?? "";
                bool isCurrentDefault = string.IsNullOrEmpty(currentOver) || currentOver.Equals("Default", StringComparison.OrdinalIgnoreCase);
                string currentPh = isCurrentDefault ? "Default" : currentOver;
                string prevPh = "Default"; 
                if (note.Prev != null) {
                    string prevOver = note.Prev.PhonemizerOverride ?? "";
                    bool isPrevDefault = string.IsNullOrEmpty(prevOver) || prevOver.Equals("Default", StringComparison.OrdinalIgnoreCase);
                    prevPh = isPrevDefault ? "Default" : prevOver;
                }
                bool isContinuation = note.lyric.StartsWith("+");
                bool isTransition = !isContinuation && ((note.Prev == null && !isCurrentDefault) || (note.Prev != null && currentPh != prevPh));
                
                if (isTransition) {
                    // Badge Background utilizes the same hasError flag
                    var badgeBrush = selectedNotes.Contains(note)
                        ? (hasError ? ThemeManager.AccentBrush3Semi : ThemeManager.AccentBrush2)
                        : (hasError ? ThemeManager.NeutralAccentBrushSemi : ThemeManager.AccentBrush1);

                    if (isCurrentDefault) {
                        double boxWidth = 16; 
                        double boxHeight = 16;
                        double dotRadius = 3;
                        Avalonia.Rect boxRect = new Avalonia.Rect(
                            leftTop.X + 2, 
                            leftTop.Y - boxHeight - 4, 
                            boxWidth, 
                            boxHeight
                        );
                        Avalonia.Point center = new Avalonia.Point(
                            boxRect.X + boxWidth / 2, 
                            boxRect.Y + boxHeight / 2
                        );
                        context.DrawRectangle(badgeBrush, null, boxRect, 3, 3);
                        context.DrawEllipse(Brushes.White, null, center, dotRadius, dotRadius);
                        
                    } else {
                        var factory = GetPhonemizerFactoryCached(currentPh);
                        string displayLang = factory?.language ?? "";
                        if (string.IsNullOrEmpty(displayLang) && !string.IsNullOrEmpty(factory?.tag)) {
                            displayLang = factory.tag.Split(' ')[0]; 
                        }
                        if (string.IsNullOrEmpty(displayLang)) {
                            string rawName = currentPh.Split('.').Last().Replace("Phonemizer", "");
                            displayLang = System.Text.RegularExpressions.Regex.Replace(rawName, "([A-Z])", " $1").Trim();
                            if (displayLang.Length > 5) {
                                displayLang = displayLang.Substring(0, 5);
                            }
                        }
                        if (!string.IsNullOrEmpty(displayLang)) {
                            var langLayout = TextLayoutCache.Get(displayLang, Avalonia.Media.Brushes.White, 10,
                                useUiFont: Preferences.Default.UseUiFontForNotes);
                            double paddingX = 3;
                            double paddingY = 1.5;
                            Avalonia.Rect badgeRect = new Avalonia.Rect(
                                leftTop.X + 2, 
                                leftTop.Y - langLayout.Height - (paddingY * 2) - 4, 
                                langLayout.Width + (paddingX * 2), 
                                langLayout.Height + (paddingY * 2)
                            );
                            context.DrawRectangle(badgeBrush, null, badgeRect, 3, 3);
                            Avalonia.Point textPos = new Avalonia.Point(badgeRect.X + paddingX, badgeRect.Y + paddingY);
                            using (var state = context.PushTransform(Avalonia.Matrix.CreateTranslation(textPos.X, textPos.Y))) {
                                langLayout?.Draw(context, new Avalonia.Point());
                            }
                        }
                    }
                }
            }
            string displayLyric = note.lyric;
            if (ShowPhonemePanel && viewModel.ShowPhonemePanelButton) {
                int bracketIndex = displayLyric.IndexOf('[');
                if (bracketIndex >= 0) {
                    displayLyric = displayLyric.Substring(0, bracketIndex).TrimEnd();
                }
            }
            if (displayLyric.Length == 0) {
                return;
            }
            int txtsize = 12;
            var textLayout = TextLayoutCache.Get(displayLyric, Brushes.White, txtsize,
                useUiFont: Preferences.Default.UseUiFontForNotes);
            if (txtsize > size.Height) {
                return;
            }
            if (textLayout.Height + 5 < size.Height) {
                txtsize = (int)(12 * (size.Height / textLayout.Height));
                textLayout = TextLayoutCache.Get(displayLyric, Brushes.White, txtsize,
                    useUiFont: Preferences.Default.UseUiFontForNotes);
            }
            if (textLayout.Width + 5 > size.Width) {
                displayLyric = displayLyric[0] + "..";
                textLayout = TextLayoutCache.Get(displayLyric, Brushes.White, txtsize,
                    useUiFont: Preferences.Default.UseUiFontForNotes);
                if (textLayout.Width + 5 > size.Width) {
                    return;
                }
            }
            Point textPosition = leftTop.WithX(leftTop.X + 5)
                .WithY(Math.Round(leftTop.Y + (size.Height - textLayout.Height) / 2));
            using (var state = context.PushTransform(Matrix.CreateTranslation(textPosition.X, textPosition.Y))) {
                textLayout.Draw(context, new Point());
            }
        }

        private void RenderGhostNote(UNote note, NotesViewModel viewModel, DrawingContext context, int partOffset, IBrush brush) {
            // REVIEW should ghost note be smaller?
            double relativeSize = 0.5d;
            double height = TrackHeight * relativeSize;
            double yOffset = Math.Floor(height * 0.5f);
            Point leftTop = viewModel.TickToneToPoint(partOffset + note.position, note.AdjustedTone);
            leftTop = leftTop.WithX(leftTop.X + 1).WithY(Math.Round(leftTop.Y + 1 + yOffset));

            Size size = viewModel.TickToneToSize(note.duration, relativeSize);
            size = size.WithWidth(size.Width - 1).WithHeight(Math.Floor(size.Height - 2));

            Point rightBottom = new Point(leftTop.X + size.Width, leftTop.Y + size.Height);

            double radius = GetNoteCornerRadius(size);
            context.DrawRectangle(brush, null, new Rect(leftTop, rightBottom), radius, radius);
        }

        private static double GetNoteCornerRadius(Size size) {
            double maxRadius = Math.Max(0, Math.Min(size.Width, size.Height) / 2);
            return Math.Clamp(Preferences.Default.NoteCornerRadius, 0, Math.Min(10, maxRadius));
        }

        private readonly Dictionary<(Color color, byte alpha), IBrush> opacityBrushCache = new();

        private IBrush ApplyNoteOpacity(IBrush brush) {
            if (brush is ISolidColorBrush solidBrush) {
                byte alpha = (byte)Math.Clamp(
                    (int)Math.Round(solidBrush.Color.A * solidBrush.Opacity * Preferences.Default.NoteOpacity), 0, 255);
                var key = (solidBrush.Color, alpha);
                if (!opacityBrushCache.TryGetValue(key, out var cached)) {
                    cached = new ImmutableSolidColorBrush(Color.FromArgb(
                        alpha, solidBrush.Color.R, solidBrush.Color.G, solidBrush.Color.B));
                    if (opacityBrushCache.Count > 4096) {
                        opacityBrushCache.Clear();
                    }
                    opacityBrushCache[key] = cached;
                }
                return cached;
            }
            return brush;
        }

        private bool playbackSeekPending = true;
        private bool renderPassActive;
        private bool invalidatePending;

        private void UpdatePlaybackHighlight(bool seek) {
            var now = DateTime.UtcNow;
            float dt = (float)Math.Clamp((now - highlightLastFrame).TotalSeconds, 0, 0.1);
            highlightLastFrame = now;
            bool anyEffect = ShowPlaybackNoteHighlight || ShowPlaybackNoteBounce || ShowPlaybackNoteOrbit;
            var target = (!anyEffect || !PlaybackManager.Inst.PlayingMaster)
                ? null
                : seek || activePlaybackNote == null ? FindPlaybackNote() : activePlaybackNote;
            bool changed = false;
            if (target != activePlaybackNote) {
                if (activePlaybackNote != null && activeHighlight > 0.001f) {
                    fadingPlaybackNote = activePlaybackNote;
                    fadingHighlight = activeHighlight;
                }
                activePlaybackNote = target;
                activeHighlight = 0;
                activeBounceElapsed = 0;
                changed = true;
            }
            float newActive = MoveTowards(activeHighlight, !ShowPlaybackNoteHighlight || activePlaybackNote == null ? 0 : 1,
                (float)Math.Clamp(Preferences.Default.PlaybackHighlightFadeInPerSecond, 0.1, 30.0) * dt);
            if (newActive != activeHighlight) {
                activeHighlight = newActive;
                changed = true;
            }
            float newFading = MoveTowards(fadingHighlight, 0,
                (float)Math.Clamp(Preferences.Default.PlaybackHighlightFadeOutPerSecond, 0.1, 30.0) * dt);
            if (newFading != fadingHighlight) {
                fadingHighlight = newFading;
                changed = true;
            }
            if (fadingHighlight <= 0.001f) {
                fadingPlaybackNote = null;
                fadingHighlight = 0;
            }
            bool bouncing = ShowPlaybackNoteBounce && activePlaybackNote != null &&
                activeBounceElapsed < Math.Clamp(Preferences.Default.PlaybackNoteBounceDuration, 0.05, 2.0);
            if (bouncing) {
                activeBounceElapsed += dt;
                changed = true;
            }
            if (ShowPlaybackNoteOrbit && PlaybackManager.Inst.PlayingMaster) {
                playbackOrbitElapsed += dt;
                changed = true;
            }
            bool orbiting = ShowPlaybackNoteOrbit && PlaybackManager.Inst.PlayingMaster;
            bool needed = activeHighlight > 0.001f || fadingHighlight > 0.001f || bouncing || orbiting;
            if (needed || (anyEffect && PlaybackManager.Inst.PlayingMaster)) {
                if (!highlightTimer.IsEnabled) {
                    highlightTimer.Start();
                }
            } else if (highlightTimer.IsEnabled) {
                highlightTimer.Stop();
            }
            if (changed) {
                if (renderPassActive) {
                    if (!invalidatePending) {
                        invalidatePending = true;
                        Dispatcher.UIThread.Post(() => {
                            invalidatePending = false;
                            InvalidateVisual();
                        }, DispatcherPriority.Background);
                    }
                } else {
                    InvalidateVisual();
                }
            }
        }

        private Vector GetPlaybackAnimationOffset(UNote note) {
            if (!PlaybackManager.Inst.PlayingMaster ||
                (!ShowPlaybackNoteOrbit && (!ShowPlaybackNoteBounce || note != activePlaybackNote))) {
                return default;
            }
            double x = 0;
            double y = 0;
            if (ShowPlaybackNoteBounce && note == activePlaybackNote) {
                double duration = Math.Clamp(Preferences.Default.PlaybackNoteBounceDuration, 0.05, 2.0);
                double progress = Math.Clamp(activeBounceElapsed / duration, 0, 1);
                double height = Math.Min(Math.Clamp(Preferences.Default.PlaybackNoteBounceHeight, 1, 40), TrackHeight * 0.4);
                y -= Math.Sin(progress * Math.PI) * height;
            }
            if (ShowPlaybackNoteOrbit) {
                var orbit = RofloficEffects.OrbitOffset(note.position, playbackOrbitElapsed, TrackHeight, true);
                x += orbit.X;
                y += orbit.Y;
            }
            return new Vector(x, y);
        }

        private Matrix GetPlaybackRotation(UNote note, Point center) {
            if (!ShowPlaybackNoteOrbit || !PlaybackManager.Inst.PlayingMaster) {
                return Matrix.Identity;
            }
            return RofloficEffects.OrbitRotation(note.position, playbackOrbitElapsed, center, true);
        }

        private UNote? FindPlaybackNote() {
            var viewModel = ((PianoRollViewModel?)DataContext)?.NotesViewModel;
            return viewModel?.FindVoiceNoteAtTick(PlayPosTick);
        }

        private IBrush BlendBrush(IBrush from, IBrush to, float amount) {
            if (amount <= 0.001f || from is not ISolidColorBrush fromSolid || to is not ISolidColorBrush toSolid) return from;
            byte quantizedAmount = (byte)Math.Clamp((int)Math.Round(amount * 255), 0, 255);
            var key = (fromSolid.Color, toSolid.Color, quantizedAmount);
            if (!highlightBrushes.TryGetValue(key, out var brush)) {
                float t = quantizedAmount / 255f;
                var a = fromSolid.Color;
                var b = toSolid.Color;
                brush = new SolidColorBrush(Color.FromArgb(
                    (byte)(a.A + (b.A - a.A) * t),
                    (byte)(a.R + (b.R - a.R) * t),
                    (byte)(a.G + (b.G - a.G) * t),
                    (byte)(a.B + (b.B - a.B) * t)));
                highlightBrushes[key] = brush;
            }
            return brush;
        }

        private static float MoveTowards(float value, float target, float delta) =>
            Math.Abs(target - value) <= delta ? target : value + Math.Sign(target - value) * delta;



        private static readonly IDashStyle PhraseBoundaryDashStyle = new ImmutableDashStyle(new double[] { 4, 2, 1, 2 }, 0);
        private static readonly IBrush PhraseOverlapBrush = new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));

        private void RenderDiffSingerPhraseBoundaries(double viewLeftTick, double viewRightTick, NotesViewModel viewModel, DrawingContext context) {
            if (!Preferences.Default.DiffSingerShowRenderPhraseBoundaries) {
                return;
            }
            if (!TryGetDiffSingerRenderer(viewModel, out var renderer)) {
                return;
            }
            var accent = ThemeManager.AccentBrush3;
            var boundaryPen = new Pen(accent, 1) { DashStyle = PhraseBoundaryDashStyle };
            var railPen = new Pen(accent, 2);
            var overlapRailPen = new Pen(PhraseOverlapBrush, 2);
            RenderPhrase[] phrases;
            lock (Part!) {
                phrases = Part!.renderPhrases.ToArray();
            }
            var visible = new List<(double startTick, double endTick)>(phrases.Length);
            foreach (var phrase in phrases) {
                var (startTick, endTick) = GetRenderedPhraseTickBounds(phrase, renderer);
                if (startTick >= viewRightTick || endTick <= viewLeftTick) {
                    continue;
                }
                visible.Add((startTick, endTick));
            }
            foreach (var (startTick, endTick) in visible) {
                DrawPhraseBoundaryLine(context, boundaryPen, viewModel.TickToneToPoint(startTick, 0).X);
                DrawPhraseBoundaryLine(context, boundaryPen, viewModel.TickToneToPoint(endTick, 0).X);
            }
            var events = new List<(double tick, int delta)>(visible.Count * 2);
            foreach (var (startTick, endTick) in visible) {
                events.Add((startTick, +1));
                events.Add((endTick, -1));
            }
            events.Sort((a, b) => a.tick.CompareTo(b.tick));
            int coverage = 0;
            double? segStart = null;
            int i = 0;
            while (i < events.Count) {
                double tick = events[i].tick;
                if (segStart.HasValue && coverage > 0 && tick > segStart.Value) {
                    double startX = Math.Clamp(viewModel.TickToneToPoint(segStart.Value, 0).X, 0, Bounds.Width);
                    double endX = Math.Clamp(viewModel.TickToneToPoint(tick, 0).X, 0, Bounds.Width);
                    if (endX > startX) {
                        var pen = coverage >= 2 ? overlapRailPen : railPen;
                        context.DrawLine(pen, new Point(startX, 3.5), new Point(endX, 3.5));
                    }
                }
                while (i < events.Count && events[i].tick == tick) {
                    coverage += events[i].delta;
                    i++;
                }
                segStart = tick;
            }
        }

        private static readonly Dictionary<string, OpenUtau.Api.PhonemizerFactory?> phonemizerFactoryCache = new();

        private static OpenUtau.Api.PhonemizerFactory? GetPhonemizerFactoryCached(string name) {
            if (!phonemizerFactoryCache.TryGetValue(name, out var factory)) {
                factory = OpenUtau.Api.PhonemizerFactory.Get(name)
                    ?? OpenUtau.Api.PhonemizerFactory.GetAll().FirstOrDefault(
                        f => f.name == name || (name.Length > 0 && f.name.EndsWith(name)));
                phonemizerFactoryCache[name] = factory;
            }
            return factory;
        }

        private void DrawPhraseBoundaryLine(DrawingContext context, IPen pen, double x) {
            if (Bounds.Width < 1 || x < 0 || x > Bounds.Width) {
                return;
            }
            double crispX = Math.Clamp(Math.Round(x) + 0.5, 0.5, Bounds.Width - 0.5);
            context.DrawLine(pen, new Point(crispX, 0), new Point(crispX, Bounds.Height));
        }

        private bool TryGetDiffSingerRenderer(NotesViewModel viewModel, out IRenderer? renderer) {
            renderer = null;
            if (Part == null || viewModel.Project == null || Part.trackNo < 0 || Part.trackNo >= viewModel.Project.tracks.Count) {
                return false;
            }
            var settings = viewModel.Project.tracks[Part.trackNo].RendererSettings;
            renderer = settings?.Renderer;
            return string.Equals(renderer?.ToString(), Renderers.DIFFSINGER, StringComparison.OrdinalIgnoreCase)
                || string.Equals(settings?.renderer, Renderers.DIFFSINGER, StringComparison.OrdinalIgnoreCase);
        }

        private (double startTick, double endTick) GetRenderedPhraseTickBounds(RenderPhrase phrase, IRenderer? renderer) {
            if (Part == null) {
                return (0, 0);
            }
            try {
                var layout = renderer?.Layout(phrase);
                if (layout != null) {
                    double startMs = layout.positionMs - layout.leadingMs;
                    double endMs = startMs + layout.estimatedLengthMs;
                    return (
                        phrase.timeAxis.MsPosToTickPos(startMs) - Part.position,
                        phrase.timeAxis.MsPosToTickPos(endMs) - Part.position);
                }
            } catch {
                // Rendering invalid singers should not break piano roll painting.
            }
            return (phrase.position - phrase.leading - Part.position, phrase.end - Part.position);
        }

        private void RenderPitchBend(UNote note, NotesViewModel viewModel, DrawingContext context) {
            var pitchExp = note.pitch;
            var pts = pitchExp.data;
            if (pts.Count < 2 || viewModel.Part == null) return;

            var project = viewModel.Project;
            double p0Tick = project.timeAxis.MsPosToTickPos(note.PositionMs + pts[0].X) - viewModel.Part.position;
            double p0Tone = note.AdjustedTone + pts[0].Y / 10.0;
            Point p0 = viewModel.TickToneToPoint(p0Tick, p0Tone - 0.5);
            Point p_1 = p0;
            points.Clear();
            points.Add(p0);

            var brush = note.pitch.snapFirst ? ThemeManager.AccentBrush3 : null;
            var pen = ThemeManager.AccentPen3;
            using (var state = context.PushTransform(Matrix.CreateTranslation(p0.X, p0.Y))) {
                context.DrawGeometry(brush, pen, pointGeometry);
            }

            for (int i = 1; i < pts.Count; i++) {
                double p1Tick = project.timeAxis.MsPosToTickPos(note.PositionMs + pts[i].X) - viewModel.Part.position;
                double p1Tone = note.AdjustedTone + pts[i].Y / 10.0;
                Point p1 = viewModel.TickToneToPoint(p1Tick, p1Tone - 0.5);
                CubicSplineSegment? curve = null;

                if (pts.Count > 2 && pts[i - 1].shape == PitchPointShape.sp) {
                    var p2 = p1;
                    if (i == 1) {
                        if (note.pitch.data[0].X > 0) {
                            p_1 = viewModel.TickToneToPoint(note.position, p0Tone - 0.5);
                        }
                    }
                    if (i < pts.Count - 1) {
                        double p2Tick = project.timeAxis.MsPosToTickPos(note.PositionMs + pts[i + 1].X) - viewModel.Part.position;
                        double p2Tone = note.AdjustedTone + pts[i + 1].Y / 10.0;
                        p2 = viewModel.TickToneToPoint(p2Tick, p2Tone - 0.5);
                    } else if (pts[i].X < note.DurationMs) {
                        p2 = viewModel.TickToneToPoint(note.End, note.AdjustedTone - 0.5);
                    }
                    curve = new CubicSplineSegment(
                                p_1.X, p_1.Y,
                                p0.X, p0.Y,
                                p1.X, p1.Y,
                                p2.X, p2.Y);
                }
                // Draw arc
                double x0 = p0.X;
                double y0 = p0.Y;
                double x1 = p0.X;
                double y1 = p0.Y;
                if (p1.X - p0.X < 5) {
                    points.Add(p1);
                } else {
                    points.Add(new Point(x0, y0));
                    while (x0 < p1.X) {
                        x1 = Math.Min(x1 + 4, p1.X);
                        y1 = curve?.GetY(x1) ?? MusicMath.InterpolateShape(p0.X, p1.X, p0.Y, p1.Y, x1, pts[i - 1].shape);
                        points.Add(new Point(x1, y1));
                        x0 = x1;
                        y0 = y1;
                    }
                }
                p_1 = p0;
                p0 = p1;
                using (var state = context.PushTransform(Matrix.CreateTranslation(p0.X, p0.Y))) {
                    context.DrawGeometry(null, pen, pointGeometry);
                }
            }
            DrawPolyline(context, pen);
        }

        private void RenderVibrato(UNote note, NotesViewModel viewModel, DrawingContext context) {
            var vibrato = note.vibrato;
            if (vibrato == null || vibrato.length == 0) {
                return;
            }

            var pen = ThemeManager.AccentPen3;
            float nPeriod = (float)viewModel.Project.timeAxis.TicksBetweenMsPos(note.PositionMs, note.PositionMs + vibrato.period) / note.duration;
            float nPos = vibrato.NormalizedStart;
            var point = vibrato.Evaluate(nPos, nPeriod, note);
            points.Clear();
            points.Add(viewModel.TickToneToPoint(point.X, point.Y - 0.5));
            while (nPos < 1) {
                nPos = Math.Min(1, nPos + nPeriod / 16);
                point = vibrato.Evaluate(nPos, nPeriod, note);
                points.Add(viewModel.TickToneToPoint(point.X, point.Y - 0.5));
            }
            DrawPolyline(context, pen);
        }

        private readonly Geometry vibratoIcon = Geometry.Parse("M-6.5 1 L-6 1.5 L-4.5 0 L-2 2.5 L0.5 0 L3 2.5 L6.5 -1 L6 -1.5 L4.5 0 L2 -2.5 L-0.5 0 L-3 -2.5 Z");
        private void RenderVibratoToggle(UNote note, NotesViewModel viewModel, DrawingContext context) {
            var vibrato = note.vibrato;
            var togglePos = vibrato.GetToggle(note);
            Point icon = viewModel.TickToneToPoint(togglePos.X, togglePos.Y);
            var pen = ThemeManager.BarNumberPen;
            using (var state = context.PushTransform(Matrix.CreateTranslation(icon.X - 10, icon.Y))) {
                context.DrawGeometry(vibrato.length == 0 ? null : pen.Brush, pen, vibratoIcon);
            }
        }

        private void RenderVibratoControl(UNote note, NotesViewModel viewModel, DrawingContext context) {
            var vibrato = note.vibrato;
            if (vibrato.length == 0) {
                return;
            }
            var pen = ThemeManager.BarNumberPen!;
            Point start = viewModel.TickToneToPoint(vibrato.GetEnvelopeStart(note));
            Point fadeIn = viewModel.TickToneToPoint(vibrato.GetEnvelopeFadeIn(note));
            Point fadeOut = viewModel.TickToneToPoint(vibrato.GetEnvelopeFadeOut(note));
            Point end = viewModel.TickToneToPoint(vibrato.GetEnvelopeEnd(note));
            context.DrawLine(pen, start, fadeIn);
            context.DrawLine(pen, fadeIn, fadeOut);
            context.DrawLine(pen, fadeOut, end);
            using (var state = context.PushTransform(Matrix.CreateTranslation(start))) {
                context.DrawGeometry(pen.Brush, pen, pointGeometry);
            }
            using (var state = context.PushTransform(Matrix.CreateTranslation(fadeIn))) {
                context.DrawGeometry(pen.Brush, pen, pointGeometry);
            }
            using (var state = context.PushTransform(Matrix.CreateTranslation(fadeOut))) {
                context.DrawGeometry(pen.Brush, pen, pointGeometry);
            }
            vibrato.GetPeriodStartEnd(DocManager.Inst.Project, note, out var periodStartPos, out var periodEndPos);
            Point periodStart = viewModel.TickToneToPoint(periodStartPos);
            Point periodEnd = viewModel.TickToneToPoint(periodEndPos);
            float height = (float)TrackHeight / 3;
            periodStart = periodStart.WithY(periodStart.Y - height / 2 - 0.5f);
            double width = periodEnd.X - periodStart.X;
            periodEnd = periodEnd.WithX(periodEnd.X - 2).WithY(periodEnd.Y - height / 2 - 0.5f);
            context.DrawRectangle(null, pen, new Rect(periodStart, new Size(width, height)), 1, 1);
            context.DrawLine(pen, periodEnd, periodEnd + new Vector(0, height));
        }

        private void RenderFinalPitch(double leftTick, double rightTick, NotesViewModel viewModel, DrawingContext context, bool bright = false) {
            var pen = bright ? pitchEditPen : ThemeManager.FinalPitchPen!;
            lock (Part!) {
                foreach (var phrase in Part!.renderPhrases) {
                    if (phrase.position - Part.position > rightTick || phrase.end - Part.position < leftTick) {
                        continue;
                    }
                    int pitchStart = phrase.position - phrase.leading - Part.position;
                    int startIdx = (int)Math.Max(0, (leftTick - pitchStart) / 5);
                    int endIdx = (int)Math.Min(phrase.pitches.Length, (rightTick - pitchStart) / 5 + 1);
                    points.Clear();
                    for (int i = startIdx; i < endIdx; ++i) {
                        int t = pitchStart + i * 5;
                        float p = phrase.pitches[i];
                        points.Add(viewModel.TickToneToPoint(t, p / 100 - 0.5));
                    }
                    DrawPolyline(context, pen);
                }
            }
        }

        private void DrawPolyline(DrawingContext context, IPen? pen) {
            // Drawing is deferred; use an immutable snapshot so later point edits cannot
            // mutate geometry already queued for the current render pass.
            context.DrawGeometry(null, pen, new PolylineGeometry(points.ToArray(), false));
        }
    }
}
