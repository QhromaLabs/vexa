using System.IO;
using System.Text.Json;
using Vexa.Models;

namespace Vexa.Services;

public sealed class JsonSessionService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public TranscriptionSession Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TranscriptionSession>(json, Options) ?? new TranscriptionSession();
    }

    public void Save(string path, TranscriptionSession session)
    {
        var json = JsonSerializer.Serialize(session, Options);
        File.WriteAllText(path, json);
    }
}
