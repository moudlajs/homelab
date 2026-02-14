using HomeLab.Cli.Models.EventLog;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Collects a lightweight event snapshot from all system sources.
/// </summary>
public interface IEventCollector
{
    /// <summary>
    /// Collects current state from all sources and returns an event log entry.
    /// </summary>
    Task<EventLogEntry> CollectEventAsync();
}
