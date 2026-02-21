using HomeLab.Cli.Models.AI;
using EventLogEntry = HomeLab.Cli.Models.EventLog.EventLogEntry;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Collects system and Docker data into a structured snapshot.
/// </summary>
public interface ISystemDataCollector
{
    /// <summary>
    /// Collects all available homelab data.
    /// </summary>
    Task<HomelabDataSnapshot> CollectAsync();

    /// <summary>
    /// Formats a data snapshot into a text prompt suitable for an LLM.
    /// Optionally includes event history for incident investigation.
    /// </summary>
    string FormatAsPrompt(HomelabDataSnapshot snapshot, List<EventLogEntry>? eventHistory = null);
}
