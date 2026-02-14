using System.Text.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.EventLog;

/// <summary>
/// JSONL file-based event log storage at ~/.homelab/events.jsonl.
/// Each line is a complete JSON object for append-only, crash-safe writes.
/// </summary>
public class EventLogService : IEventLogService
{
    private static readonly string DefaultLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".homelab", "events.jsonl");

    private readonly string _logPath;

    public EventLogService() : this(DefaultLogPath) { }

    public EventLogService(string logPath)
    {
        _logPath = logPath;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public async Task WriteEventAsync(EventLogEntry entry)
    {
        var dir = Path.GetDirectoryName(_logPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await File.AppendAllTextAsync(_logPath, json + Environment.NewLine);
    }

    public async Task<List<EventLogEntry>> ReadEventsAsync(DateTime? since = null, DateTime? until = null)
    {
        var events = new List<EventLogEntry>();

        if (!File.Exists(_logPath))
        {
            return events;
        }

        var lines = await File.ReadAllLinesAsync(_logPath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<EventLogEntry>(line, JsonOptions);
                if (entry == null)
                {
                    continue;
                }

                if (since.HasValue && entry.Timestamp < since.Value)
                {
                    continue;
                }

                if (until.HasValue && entry.Timestamp > until.Value)
                {
                    continue;
                }

                events.Add(entry);
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return events;
    }

    public async Task CleanupAsync(int retentionDays = 7)
    {
        if (!File.Exists(_logPath))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var lines = await File.ReadAllLinesAsync(_logPath);
        var kept = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<EventLogEntry>(line, JsonOptions);
                if (entry != null && entry.Timestamp >= cutoff)
                {
                    kept.Add(line);
                }
            }
            catch (JsonException)
            {
                // Drop malformed lines during cleanup
            }
        }

        await File.WriteAllLinesAsync(_logPath, kept);
    }
}
