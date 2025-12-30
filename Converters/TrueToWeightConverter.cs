using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Vexa.Converters;

public sealed class TrueToWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool flag && flag ? FontWeights.Bold : FontWeights.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
