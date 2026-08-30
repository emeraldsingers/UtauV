using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace OpenUtau.App.Controls {
    public partial class NumberSlider : UserControl {
        public static readonly StyledProperty<string?> LabelProperty =
            AvaloniaProperty.Register<NumberSlider, string?>(nameof(Label));
        public static readonly DirectProperty<NumberSlider, double> ValueProperty =
            AvaloniaProperty.RegisterDirect<NumberSlider, double>(
                nameof(Value),
                o => o.Value,
                (o, v) => o.Value = v,
                defaultBindingMode: BindingMode.TwoWay);
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<NumberSlider, double>(nameof(Minimum), 0.0);
        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<NumberSlider, double>(nameof(Maximum), 1.0);
        public static readonly StyledProperty<double> TickFrequencyProperty =
            AvaloniaProperty.Register<NumberSlider, double>(nameof(TickFrequency), 0.1);
        public static readonly StyledProperty<bool> IsSnapToTickEnabledProperty =
            AvaloniaProperty.Register<NumberSlider, bool>(nameof(IsSnapToTickEnabled), true);
        public static readonly StyledProperty<string> FormatProperty =
            AvaloniaProperty.Register<NumberSlider, string>(nameof(Format), "{0:0.###}");
        public static readonly StyledProperty<double?> DefaultProperty =
            AvaloniaProperty.Register<NumberSlider, double?>(nameof(Default));
        public static readonly StyledProperty<bool> IsDirectionReversedProperty =
            AvaloniaProperty.Register<NumberSlider, bool>(nameof(IsDirectionReversed));
        public static readonly StyledProperty<Avalonia.Media.FontWeight> LabelFontWeightProperty =
            AvaloniaProperty.Register<NumberSlider, Avalonia.Media.FontWeight>(
                nameof(LabelFontWeight), Avalonia.Media.FontWeight.Normal);

        public string? Label {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }
        public double Value {
            get => value;
            set => SetAndRaise(ValueProperty, ref this.value, value);
        }
        public double Minimum {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }
        public double Maximum {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }
        public double TickFrequency {
            get => GetValue(TickFrequencyProperty);
            set => SetValue(TickFrequencyProperty, value);
        }
        public bool IsSnapToTickEnabled {
            get => GetValue(IsSnapToTickEnabledProperty);
            set => SetValue(IsSnapToTickEnabledProperty, value);
        }
        public string Format {
            get => GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }
        public double? Default {
            get => GetValue(DefaultProperty);
            set => SetValue(DefaultProperty, value);
        }
        public bool IsDirectionReversed {
            get => GetValue(IsDirectionReversedProperty);
            set => SetValue(IsDirectionReversedProperty, value);
        }
        public Avalonia.Media.FontWeight LabelFontWeight {
            get => GetValue(LabelFontWeightProperty);
            set => SetValue(LabelFontWeightProperty, value);
        }

        public event EventHandler<double>? ValueCommitted;
        public event EventHandler? ResetRequested;

        private double value;

        public NumberSlider() {
            InitializeComponent();
            UpdateValueText();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e) {
            base.OnPropertyChanged(e);
            if (e.Property == ValueProperty) {
                UpdateValueText();
            }
        }

        void UpdateValueText() {
            if (ValueBox != null && !ValueBox.IsFocused) {
                ValueBox.Text = string.Format(Format, Value);
            }
        }

        void ValueBoxKeyDown(object? sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                CommitInput();
                TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
                e.Handled = true;
            } else if (e.Key == Key.Escape) {
                UpdateValueText();
                TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
                e.Handled = true;
            }
        }

        void ValueBoxLostFocus(object? sender, RoutedEventArgs e) {
            CommitInput();
        }

        void CommitInput() {
            if (double.TryParse(ValueBox.Text, out var v)) {
                Value = Math.Clamp(v, Minimum, Maximum);
                ValueCommitted?.Invoke(this, Value);
            }
            UpdateValueText();
        }

        void RootContextRequested(object? sender, ContextRequestedEventArgs e) {
            bool handled = false;
            if (ResetRequested != null) {
                ResetRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
            } else if (Default.HasValue) {
                Value = Math.Clamp(Default.Value, Minimum, Maximum);
                handled = true;
            }
            if (handled) {
                e.Handled = true;
            }
        }
    }
}
