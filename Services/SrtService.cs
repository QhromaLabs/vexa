using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Vexa.Models;

namespace Vexa.Services;

public sealed class SrtService
{
    public List<Segment> Load(string path)
    {
        var lines = File.ReadAllLines(path);
        var segments = new List<Segment>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (int.TryParse(line, out _))
            {
                index++;
            }

            if (index >= lines.Length)
            {
                break;
            }

            var timeLine = lines[index].Trim();
            index++;
            var arrow = timeLine.Split("-->", StringSplitOptions.TrimEntries);
            if (arrow.Length != 2)
            {
                continue;
            }

            var start = ParseTimestamp(arrow[0]);
            var end = ParseTimestamp(arrow[1]);
            var text = new StringBuilder();
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                if (text.Length > 0)
                {
                    text.AppendLine();
                }
                text.Append(lines[index]);
                index++;
            }

            segments.Add(new Segment
            {
                StartSeconds = start.TotalSeconds,
                EndSeconds = end.TotalSeconds,
                Text = text.ToString()
            });
        }

        return segments;
    }

    public string Build(IEnumerable<Segment> segments)
    {
        var builder = new StringBuilder();
        var index = 1;
        foreach (var segment in segments)
        {
            builder.AppendLine(index.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine($"{FormatTimestamp(segment.StartTime)} --> {FormatTimestamp(segment.EndTime)}");
            builder.AppendLine(NormalizeText(segment.Text));
            builder.AppendLine();
            index++;
        }

        return builder.ToString();
    }

    private static TimeSpan ParseTimestamp(string value)
    {
        var trimmed = value.Trim().Replace(',', '.');
        if (TimeSpan.TryParseExact(trimmed, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out var span))
        {
            return span;
        }

        return TimeSpan.Zero;
    }

    private static string FormatTimestamp(TimeSpan span)
    {
        return span.ToString(@"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);
    }

    private static string NormalizeText(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }
}
