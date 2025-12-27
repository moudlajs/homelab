namespace HomeLab.Cli.Models;

/// <summary>
/// Represents the result of a container health check.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Container name.
    /// </summary>
    public string ContainerName { get; set; } = string.Empty;

    /// <summary>
    /// Is the container healthy?
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Health status message.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Additional details about the health check.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// When was the check performed.
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
