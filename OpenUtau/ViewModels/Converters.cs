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

    public class EncodingNameConverter : IValueConverter {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => (value as Encoding)?.EncodingName ?? string.Empty;
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
