using System;
using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OpenUtau.App.ViewModels {
    public class StringToSolidColorBrushConverter : IValueConverter {
        public static readonly StringToSolidColorBrushConverter Instance = new StringToSolidColorBrushConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex)) {
                try {
                    var color = Color.Parse(hex.Trim());
                    return new SolidColorBrush(color);
                } catch {
                }
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
    public class CultureNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is CultureInfo cultureInfo) {
                return cultureInfo == CultureInfo.InvariantCulture ? ThemeManager.GetString("languages.invariant") : cultureInfo.NativeName;
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class AutosaveModeConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is string mode) {
                return ThemeManager.GetString($"prefs.advanced.autosave.mode.{mode.ToLower()}");
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class AutosaveIntervalConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is string seconds && int.TryParse(seconds, out int secs)) {
                return secs < 60 ? $"{secs} s" : $"{secs / 60} min";
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EncodingNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as Encoding)?.EncodingName ?? string.Empty;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
