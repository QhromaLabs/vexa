using System;

namespace Vexa.Models;

public sealed class WaveformData
{
    public float[] Peaks { get; init; } = Array.Empty<float>();
    public TimeSpan Duration { get; init; }
}
