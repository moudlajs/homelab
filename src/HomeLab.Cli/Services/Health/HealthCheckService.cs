using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Docker;

namespace HomeLab.Cli.Services.Health;

/// <summary>
/// Implementation of IHealthCheckService.
/// Monitors container and system health.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IDockerService _dockerService;

    public HealthCheckService(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<List<HealthCheckResult>> CheckAllContainersAsync()
    {
        var containers = await _dockerService.ListContainersAsync(onlyHomelab: true);
        var results = new List<HealthCheckResult>();

        foreach (var container in containers)
        {
            results.Add(new HealthCheckResult
            {
                ContainerName = container.Name,
                IsHealthy = container.IsRunning,
                Status = container.IsRunning ? "Running" : "Stopped",
                Details = container.IsRunning ? $"Uptime: {container.Uptime}" : null,
                CheckedAt = DateTime.UtcNow
            });
        }

        return results;
    }

    public async Task<HealthCheckResult> CheckContainerAsync(string containerName)
    {
        var containers = await _dockerService.ListContainersAsync(onlyHomelab: false);
        var container = containers.FirstOrDefault(c =>
            c.Name.Contains(containerName, StringComparison.OrdinalIgnoreCase));

        if (container == null)
        {
            return new HealthCheckResult
            {
                ContainerName = containerName,
                IsHealthy = false,
                Status = "Not Found",
                Details = "Container does not exist"
            };
        }

        return new HealthCheckResult
        {
            ContainerName = container.Name,
            IsHealthy = container.IsRunning,
            Status = container.IsRunning ? "Running" : "Stopped",
            Details = container.IsRunning ? $"Uptime: {container.Uptime}" : null,
            CheckedAt = DateTime.UtcNow
        };
    }

    public async Task<SystemResourceInfo> GetSystemResourcesAsync()
    {
        var containers = await _dockerService.ListContainersAsync(onlyHomelab: false);

        // Basic system info - in a real implementation, this would query actual system metrics
        return new SystemResourceInfo
        {
            RunningContainers = containers.Count(c => c.IsRunning),
            TotalContainers = containers.Count,
            // Placeholder values - would need proper system monitoring in production
            CpuUsagePercent = 0,
            TotalMemoryGB = 0,
            UsedMemoryGB = 0,
            MemoryUsagePercent = 0,
            TotalDiskGB = 0,
            UsedDiskGB = 0,
            DiskUsagePercent = 0
        };
    }
}
