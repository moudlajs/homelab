namespace HomeLab.Cli.Services.Abstractions;

/// <summary>
/// Base interface for all service clients.
/// Provides common functionality for health checks and connectivity.
/// </summary>
public interface IServiceClient
{
    /// <summary>
    /// The name of the service (e.g., "AdGuard", "WireGuard").
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Checks if the service is reachable and healthy.
    /// </summary>
    /// <returns>True if the service is healthy, false otherwise.</returns>
    Task<bool> IsHealthyAsync();

    /// <summary>
    /// Gets detailed health information about the service.
    /// </summary>
    Task<ServiceHealthInfo> GetHealthInfoAsync();
}

/// <summary>
/// Health information for a service.
/// </summary>
public class ServiceHealthInfo
{
    public string ServiceName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Message { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> Metrics { get; set; } = new();
}
