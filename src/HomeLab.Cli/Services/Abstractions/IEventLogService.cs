using HomeLab.Cli.Models.EventLog;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Reads and writes event log entries to JSONL file storage.
/// </summary>
public interface IEventLogService
{
    /// <summary>
    /// Appends an event entry to the log file.
    /// </summary>
    Task WriteEventAsync(EventLogEntry entry);

    /// <summary>
    /// Reads events from the log file, optionally filtered by time range.
    /// </summary>
    Task<List<EventLogEntry>> ReadEventsAsync(DateTime? since = null, DateTime? until = null);

    /// <summary>
    /// Removes entries older than the retention period (default 7 days).
    /// </summary>
    Task CleanupAsync(int retentionDays = 7);
}
