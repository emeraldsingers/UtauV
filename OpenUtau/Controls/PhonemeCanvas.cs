﻿using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenUtau.App.ViewModels;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using ReactiveUI;

namespace OpenUtau.App.Controls {
    class PhonemeCanvas : Control {
        public static readonly DirectProperty<PhonemeCanvas, IBrush> BackgroundProperty =
            AvaloniaProperty.RegisterDirect<PhonemeCanvas, IBrush>(
                nameof(Background),
                o => o.Background,
                (o, v) => o.Background = v);
        public static readonly DirectProperty<PhonemeCanvas, double> TickWidthProperty =
            AvaloniaProperty.RegisterDirect<PhonemeCanvas, double>(
                nameof(TickWidth),
                o => o.TickWidth,
                (o, v) => o.TickWidth = v);
        public static readonly DirectProperty<PhonemeCanvas, double> TickOffsetProperty =
            AvaloniaProperty.RegisterDirect<PhonemeCanvas, double>(
                nameof(TickOffset),
                o => o.TickOffset,
                (o, v) => o.TickOffset = v);
        public static readonly DirectProperty<PhonemeCanvas, UVoicePart?> PartProperty =
            AvaloniaProperty.RegisterDirect<PhonemeCanvas, UVoicePart?>(
                nameof(Part),
                o => o.Part,
                (o, v) => o.Part = v);
        public static readonly DirectProperty<PhonemeCanvas, bool> ShowPhonemeProperty =
            AvaloniaProperty.RegisterDirect<PhonemeCanvas, bool>(
                nameof(ShowPhoneme),
                o => o.ShowPhoneme,
                (o, v) => o.ShowPhoneme = v);

        public IBrush Background {
            get => background;
            private set => SetAndRaise(BackgroundProperty, ref background, value);
        }
        public double TickWidth {
            get => tickWidth;
            private set => SetAndRaise(TickWidthProperty, ref tickWidth, value);
        }
        public double TickOffset {
            get => tickOffset;
            private set => SetAndRaise(TickOffsetProperty, ref tickOffset, value);
        }
        public UVoicePart? Part {
            get => part;
            set => SetAndRaise(PartProperty, ref part, value);
        }
        public bool ShowPhoneme {
            get => showPhoneme;
            private set => SetAndRaise(ShowPhonemeProperty, ref showPhoneme, value);
        }

        private IBrush background = Brushes.White;
        private double tickWidth;
        private double tickOffset;
        private UVoicePart? part;
        private bool showPhoneme = true;

        private HashSet<UNote> selectedNotes = new HashSet<UNote>();
        private Geometry pointGeometry;
        private UPhoneme? mouseoverPhoneme;

        public PhonemeCanvas() {
            ClipToBounds = true;
            pointGeometry = new EllipseGeometry(new Rect(-2.5, -2.5, 5, 5));
            MessageBus.Current.Listen<NotesRefreshEvent>()
                .Subscribe(_ => InvalidateVisual());
            MessageBus.Current.Listen<NotesSelectionEvent>()
                .Subscribe(e => {
                    selectedNotes.Clear();
                    selectedNotes.UnionWith(e.selectedNotes);
                    selectedNotes.UnionWith(e.tempSelectedNotes);
                    InvalidateVisual();
                });
            MessageBus.Current.Listen<PhonemeMouseoverEvent>()
                .Subscribe(e => {
                    if (mouseoverPhoneme != e.mouseoverPhoneme) {
                        mouseoverPhoneme = e.mouseoverPhoneme;
                        InvalidateVisual();
                    }
                });
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            InvalidateVisual();
        }

        public override void Render(DrawingContext context) {
            base.Render(context);
            if (Part == null || !ShowPhoneme) {
                return;
            }
            var viewModel = ((PianoRollViewModel?)DataContext)?.NotesViewModel;
            if (viewModel == null) {
                return;
            }

            var project = DocManager.Inst.Project;
            string singerName = string.Empty;

            if (Part != null && project != null) {
                int trackNo = Part.trackNo;
                if (trackNo >= 0 && trackNo < project.tracks.Count) {
                    singerName = project.tracks[trackNo].Singer?.Name ?? string.Empty;
                }
            }

            context.DrawRectangle(Background, null, Bounds.WithX(0).WithY(0));
            double leftTick = TickOffset - 480;
            double rightTick = TickOffset + Bounds.Width / TickWidth + 480;
            bool raiseText = false;
            double lastTextEndX = double.NegativeInfinity;

            const double y = 35.5;
            const double height = 24;
            if (Part == null) {
                return;
            }
            foreach (var phoneme in Part.phonemes) {
                double leftBound = viewModel.Project.timeAxis.MsPosToTickPos(phoneme.PositionMs - phoneme.preutter) - Part.position;
                double rightBound = phoneme.End;
                if (leftBound > rightTick || rightBound < leftTick || phoneme.Parent.OverlapError) {
                    continue;
                }
                var timeAxis = viewModel.Project.timeAxis;
                double x = Math.Round(viewModel.TickToneToPoint(phoneme.position, 0).X) + 0.5;
                double posMs = phoneme.PositionMs;
                if (!phoneme.Error) {
                    double x0 = viewModel.TickToneToPoint(timeAxis.MsPosToTickPos(posMs + phoneme.envelope.data[0].X) - Part.position, 0).X;
                    double y0 = (1 - phoneme.envelope.data[0].Y / 100) * height;
                    double x1 = viewModel.TickToneToPoint(timeAxis.MsPosToTickPos(posMs + phoneme.envelope.data[1].X) - Part.position, 0).X;
                    double y1 = (1 - phoneme.envelope.data[1].Y / 100) * height;
                    double x2 = viewModel.TickToneToPoint(timeAxis.MsPosToTickPos(posMs + phoneme.envelope.data[2].X) - Part.position, 0).X;
                    double y2 = (1 - phoneme.envelope.data[2].Y / 100) * height;
                    double x3 = viewModel.TickToneToPoint(timeAxis.MsPosToTickPos(posMs + phoneme.envelope.data[3].X) - Part.position, 0).X;
                    double y3 = (1 - phoneme.envelope.data[3].Y / 100) * height;
                    double x4 = viewModel.TickToneToPoint(timeAxis.MsPosToTickPos(posMs + phoneme.envelope.data[4].X) - Part.position, 0).X;
                    double y4 = (1 - phoneme.envelope.data[4].Y / 100) * height;
                    bool isAsoqwer = singerName.IndexOf("Asoqwer", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isKeko = singerName.IndexOf("莠ｬ蟄", StringComparison.OrdinalIgnoreCase) >= 0 || singerName.IndexOf("keko", StringComparison.OrdinalIgnoreCase) >= 0 || singerName.IndexOf("K3K0", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool isAkizora = singerName.IndexOf("Akizora", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isTilke = singerName.IndexOf("Tilke", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isSimon = singerName.IndexOf("Simon", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isMitsuo = singerName.IndexOf("Mitsuo", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool isshiroi = singerName.IndexOf("shiroi", StringComparison.OrdinalIgnoreCase) >= 0;
                    IPen? pen;
                    IBrush? brush;
                    if (isAsoqwer) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AsoqwerPhoneme2 : ThemeManager.AsoqwerPhoneme;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AsoqwerAccentColorSemi : new SolidColorBrush(Color.Parse("#efddc7")) {
                            Opacity = 0.5
                        };

                    } else if (isKeko) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.KekoPhoneme2 : ThemeManager.KekoPhoneme;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.KekoAccentColorSemi : ThemeManager.GetTrackColor("keko").AccentColorLightSemi;

                    } else if (isAkizora) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AkizoraPhoneme2 : ThemeManager.AkizoraPhoneme;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AkizoraAccentColorSemi : ThemeManager.GetTrackColor("akizora").AccentColorLightSemi;

                    } else if (isTilke) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.TilkePhoneme : ThemeManager.TilkePhoneme2;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.GetTrackColor("tilke").AccentColorLightSemi : ThemeManager.TilkeAccentColorSemi;

                    } else if (isshiroi) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.ShiroiPhoneme : ThemeManager.ShiroiPhoneme2;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.ShiroiAccentColorSemi : new SolidColorBrush(Color.Parse("#781818")) {
                            Opacity = 0.5
                        };

                    } else if (isSimon) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.SimonPhoneme : ThemeManager.SimonPhoneme2;
                        var SimonBrush1 = ThemeManager.GetTrackColor("simon").AccentColorCenterKey;
                        SimonBrush1.Opacity = 0.5;
                        brush = selectedNotes.Contains(phoneme.Parent) ? SimonBrush1: ThemeManager.SimonAccentColorSemi;
                    } else if (isMitsuo) {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.MitsuoPhoneme : ThemeManager.MitsuoPhoneme2;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.GetTrackColor("mitsuo").AccentColorLightSemi : ThemeManager.MitsuoAccentColorSemi;
                    } 
                    else {
                        pen = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AccentPen2 : ThemeManager.AccentPen1;
                        brush = selectedNotes.Contains(phoneme.Parent) ? ThemeManager.AccentBrush2Semi : ThemeManager.AccentBrush1Semi;
                    }
                    var point0 = new Point(x0, y + y0);
                    var point1 = new Point(x1, y + y1);
                    var point2 = new Point(x2, y + y2);
                    var point3 = new Point(x3, y + y3);
                    var point4 = new Point(x4, y + y4);
                    var polyline = new PolylineGeometry(new Point[] { point0, point1, point2, point3, point4 }, true);
                    context.DrawGeometry(brush, pen, polyline);

                    brush = phoneme.preutterDelta.HasValue ? pen!.Brush : ThemeManager.BackgroundBrush;
                    using (var state = context.PushTransform(Matrix.CreateTranslation(x0, y + y0 - 1))) {
                        context.DrawGeometry(brush, pen, pointGeometry);
                    }
                    brush = phoneme.overlapDelta.HasValue ? pen!.Brush : ThemeManager.BackgroundBrush;
                    using (var state = context.PushTransform(Matrix.CreateTranslation(point1))) {
                        context.DrawGeometry(brush, pen, pointGeometry);
                    }
                }
                bool isAsoqwer2 = singerName.IndexOf("Asoqwer", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isKeko2 = singerName.IndexOf("莠ｬ蟄", StringComparison.OrdinalIgnoreCase) >= 0 || singerName.IndexOf("keko", StringComparison.OrdinalIgnoreCase) >= 0 || singerName.IndexOf("K3K0", StringComparison.OrdinalIgnoreCase) >= 0;

                bool isAkizora2 = singerName.IndexOf("Akizora", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isTilke2 = singerName.IndexOf("Tilke", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isSimon2 = singerName.IndexOf("Simon", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isMitsuo2 = singerName.IndexOf("Mitsuo", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isShiroi2 = singerName.IndexOf("shiroi", StringComparison.OrdinalIgnoreCase) >= 0;
                IPen? penPos;
                if (isAsoqwer2) {
                    penPos = ThemeManager.AsoqwerAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.AsoqwerAccentPen2Thickness3;
                    }
                } else if (isKeko2) {
                    penPos = ThemeManager.KekoAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.KekoAccentPen2Thickness3;
                    }
                } else if (isAkizora2) {
                    penPos = ThemeManager.AkizoraAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.AkizoraAccentPen2Thickness3;
                    }
                } else if (isTilke2) {
                    penPos = ThemeManager.TilkeAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.TilkeAccentPen2Thickness3;
                    }
                } else if (isSimon2) {
                    penPos = ThemeManager.SimonAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.SimonAccentPen2Thickness3;
                    }
                } else if (isMitsuo2) {
                    penPos = ThemeManager.MitsuoAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.MitsuoAccentPen2Thickness3;
                    }
                } else if (isShiroi2) {
                    penPos = ThemeManager.ShiroiAccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.ShiroiAccentPen2Thickness3;
                    }
                } else {
                    penPos = ThemeManager.AccentPen2;
                    if (phoneme.rawPosition != phoneme.position) {
                        penPos = ThemeManager.AccentPen2Thickness3;
                    }
                }
                context.DrawLine(penPos, new Point(x, y), new Point(x, y + height));

                // FIXME: Changing code below may break `HitTestAlias`.
                if (viewModel.TickWidth > ViewConstants.PianoRollTickWidthShowDetails) {
                    string phonemeText = !string.IsNullOrEmpty(phoneme.phonemeMapped) ? phoneme.phonemeMapped : phoneme.phoneme;
                    if (!string.IsNullOrEmpty(phonemeText)) {
                        var bold = phoneme.rawPhoneme != phoneme.phoneme;
                        var textLayout = TextLayoutCache.Get(phonemeText, ThemeManager.ForegroundBrush!, 12, bold);
                        if (x < lastTextEndX) {
                            raiseText = !raiseText;
                        } else {
                            raiseText = false;
                        }
                        double textY = raiseText ? 2 : 18;
                        var size = new Size(textLayout.Width + 4, textLayout.Height - 2);
                        using (var state = context.PushTransform(Matrix.CreateTranslation(x + 2, textY))) {
                            var pen = mouseoverPhoneme == phoneme ? ThemeManager.AccentPen1Thickness2 : ThemeManager.NeutralAccentPenSemi;
                            context.DrawRectangle(ThemeManager.BackgroundBrush, pen, new Rect(new Point(-2, 1.5), size), 4, 4);
                            textLayout.Draw(context, new Point());
                        }
                        lastTextEndX = x + size.Width;
                    }
                }
            }
        }
    }
}
