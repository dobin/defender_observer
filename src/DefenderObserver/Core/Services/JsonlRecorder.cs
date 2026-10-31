using System.Text.Encodings.Web;
using System.Text.Json;
using DefenderObserver.Core.Models;

namespace DefenderObserver.Core.Services;

public sealed class JsonlRecorder : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly StreamWriter? _writer;

    public JsonlRecorder(string? outputPath)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var stream = new FileStream(outputPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream) { AutoFlush = true };
        }
    }

    public async Task WriteEventAsync(ObservedEvent evt)
    {
        if (_writer == null) return;

        var json = JsonSerializer.Serialize(evt, JsonOptions);
        await _writer.WriteLineAsync(json);
    }

    public static async IAsyncEnumerable<ObservedEvent> ReadEventsAsync(string inputPath)
    {
        if (!File.Exists(inputPath))
        {
            yield break;
        }

        using var stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            ObservedEvent? evt = null;
            try
            {
                evt = JsonSerializer.Deserialize<ObservedEvent>(line, JsonOptions);
            }
            catch
            {
                // Skip corrupted lines
            }

            if (evt != null)
            {
                yield return evt;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer != null)
        {
            await _writer.FlushAsync();
            await _writer.DisposeAsync();
        }
    }
}
