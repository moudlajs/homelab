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
        var client = CreateClientFor(service);

        if (client == null)
        {
            return null;
        }

        try
        {
            return await client.GetHealthInfoAsync();
        }
        finally
        {
            // Only clients that own a connection implement IDisposable; the rest
            // share the injected HttpClient and must not be disposed here.
            (client as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Resolves the health client for a service.
    /// Dispatch is by compose service name first: ntopng, suricata and uptime-kuma
    /// all classify as <see cref="ServiceType.Application"/>, so type alone cannot
    /// tell them apart and they previously fell through to no client at all.
    /// </summary>
    private IServiceClient? CreateClientFor(ServiceDefinition service)
    {
        switch (service.Name.ToLowerInvariant())
        {
            case "ntopng":
                return _clientFactory.CreateNtopngClient();
            case "suricata":
                return _clientFactory.CreateSuricataClient();
            case "traefik":
                return _clientFactory.CreateTraefikClient();
            case "uptime-kuma":
            case "uptime_kuma":
                return _clientFactory.CreateUptimeKumaClient();
        }

        return service.Type switch
        {
            ServiceType.Dns => _clientFactory.CreateAdGuardClient(),
            ServiceType.Vpn => _clientFactory.CreateTailscaleClient(),
            _ => null
        };
    }
}
