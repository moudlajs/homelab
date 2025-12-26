namespace HomeLab.Cli.Models;

/// <summary>
/// Represents information about a Docker container.
/// This is a "DTO" (Data Transfer Object) - just data, no logic.
/// </summary>
public class ContainerInfo
{
    /// <summary>
    /// Container name (e.g., "homelab_adguard").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Container ID (Docker's internal ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Is the container currently running?
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Human-readable uptime (e.g., "3 days").
    /// </summary>
    public string Uptime { get; set; } = "N/A";

    /// <summary>
    /// CPU usage percentage.
    /// </summary>
    public double CpuPercent { get; set; }

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public double MemoryMB { get; set; }
}
