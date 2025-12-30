using System;
using System.Globalization;
using System.Windows.Data;

namespace Vexa.Converters;

public sealed class TimeSpanStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimeSpan span)
        {
            return span.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
        }

        return "00:00:00.000";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim() ?? "";
        if (TimeSpan.TryParseExact(text, new[] { @"hh\:mm\:ss\.fff", @"h\:mm\:ss\.fff", @"mm\:ss\.fff", @"ss\.fff", @"hh\:mm\:ss" },
            CultureInfo.InvariantCulture, out var span))
        {
            return span;
        }

        return TimeSpan.Zero;
    }
}
