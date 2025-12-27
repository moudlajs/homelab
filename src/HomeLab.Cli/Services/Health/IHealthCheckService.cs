using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.Health;

/// <summary>
/// Interface for health check operations.
/// Monitors container health and system resources.
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Performs health checks on all running containers.
    /// </summary>
    Task<List<HealthCheckResult>> CheckAllContainersAsync();

    /// <summary>
    /// Checks health of a specific container.
    /// </summary>
    Task<HealthCheckResult> CheckContainerAsync(string containerName);

    /// <summary>
    /// Gets system resource usage.
    /// </summary>
    Task<SystemResourceInfo> GetSystemResourcesAsync();
}
