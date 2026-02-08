using HomeLab.Cli.Models.AI;

namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Collects system, Docker, and Prometheus data into a structured snapshot.
/// </summary>
public interface ISystemDataCollector
{
    /// <summary>
    /// Collects all available homelab data.
    /// </summary>
    Task<HomelabDataSnapshot> CollectAsync();

    /// <summary>
    /// Formats a data snapshot into a text prompt suitable for an LLM.
    /// </summary>
    string FormatAsPrompt(HomelabDataSnapshot snapshot);
}
