using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Vexa.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag ? TrueBrush ?? Brushes.Transparent : FalseBrush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
