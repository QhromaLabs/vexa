using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Vexa.Models;

public sealed class TranscriptionSession
{
    [JsonPropertyName("audioFile")]
    public string AudioFile { get; set; } = string.Empty;

    [JsonPropertyName("segments")]
    public ObservableCollection<Segment> Segments { get; set; } = new();
}
