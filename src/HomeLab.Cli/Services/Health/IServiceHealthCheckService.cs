using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Health;

/// <summary>
/// Service for performing health checks on homelab services.
/// </summary>
public interface IServiceHealthCheckService
{
    /// <summary>
    /// Performs health checks on all discovered services.
    /// </summary>
    Task<List<ServiceHealthResult>> CheckAllServicesAsync();

    /// <summary>
    /// Performs a health check on a specific service.
    /// </summary>
    Task<ServiceHealthResult> CheckServiceAsync(ServiceDefinition service);
}

/// <summary>
/// Result of a service health check.
/// </summary>
public class ServiceHealthResult
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceType ServiceType { get; set; }
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = "Unknown";
    public string? Message { get; set; }
    public Dictionary<string, string> Metrics { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Container is running (from Docker).
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// Service-specific health (from service client).
    /// </summary>
    public ServiceHealthInfo? ServiceHealth { get; set; }
}
