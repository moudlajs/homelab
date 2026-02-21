using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.ServiceDiscovery;

namespace HomeLab.Cli.Services.Health;

/// <summary>
/// Orchestrates health checks for all homelab services.
/// </summary>
public class ServiceHealthCheckService : IServiceHealthCheckService
{
    private readonly IServiceDiscoveryService _discoveryService;
    private readonly IDockerService _dockerService;
    private readonly IServiceClientFactory _clientFactory;

    public ServiceHealthCheckService(
        IServiceDiscoveryService discoveryService,
        IDockerService dockerService,
        IServiceClientFactory clientFactory)
    {
        _discoveryService = discoveryService;
        _dockerService = dockerService;
        _clientFactory = clientFactory;
    }

    public async Task<List<ServiceHealthResult>> CheckAllServicesAsync()
    {
        var services = await _discoveryService.DiscoverServicesAsync();
        var containers = await _dockerService.ListContainersAsync(onlyHomelab: true);

        var healthChecks = new List<ServiceHealthResult>();

        foreach (var service in services)
        {
            var result = await CheckServiceAsync(service);
            healthChecks.Add(result);
        }

        return healthChecks;
    }

    public async Task<ServiceHealthResult> CheckServiceAsync(ServiceDefinition service)
    {
        var result = new ServiceHealthResult
        {
            ServiceName = service.Name,
            ServiceType = service.Type,
            CheckedAt = DateTime.UtcNow
        };

        // Check if container is running via Docker
        try
        {
            var containers = await _dockerService.ListContainersAsync(onlyHomelab: true);
            var container = containers.FirstOrDefault(c =>
                c.Name.Contains(service.Name, StringComparison.OrdinalIgnoreCase));

            result.IsRunning = container?.IsRunning ?? false;
            result.Status = container?.IsRunning == true ? "running" : (container != null ? "stopped" : "not found");
        }
        catch (Exception ex)
        {
            result.IsRunning = false;
            result.Status = "error";
            result.Message = $"Docker check failed: {ex.Message}";
        }

        // Perform service-specific health check
        if (result.IsRunning)
        {
            try
            {
                var serviceHealth = await CheckServiceSpecificHealthAsync(service);
                result.ServiceHealth = serviceHealth;
                result.IsHealthy = serviceHealth?.IsHealthy ?? false;

                if (serviceHealth != null)
                {
                    result.Metrics = serviceHealth.Metrics;
                    if (!string.IsNullOrEmpty(serviceHealth.Message))
                    {
                        result.Message = serviceHealth.Message;
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsHealthy = false;
                result.Message = $"Health check failed: {ex.Message}";
            }
        }
        else
        {
            result.IsHealthy = false;
        }

        return result;
    }

    /// <summary>
    /// Performs service-specific health checks using the appropriate client.
    /// </summary>
    private async Task<ServiceHealthInfo?> CheckServiceSpecificHealthAsync(ServiceDefinition service)
    {
        IServiceClient? client = service.Type switch
        {
            ServiceType.Dns => _clientFactory.CreateAdGuardClient(),
            ServiceType.Vpn => _clientFactory.CreateTailscaleClient(),
            _ => null
        };

        if (client == null)
        {
            return null;
        }

        return await client.GetHealthInfoAsync();
    }
}
