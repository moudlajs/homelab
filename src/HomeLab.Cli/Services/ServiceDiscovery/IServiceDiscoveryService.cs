using HomeLab.Cli.Models;

namespace HomeLab.Cli.Services.ServiceDiscovery;

/// <summary>
/// Service for discovering homelab services from docker-compose files.
/// </summary>
public interface IServiceDiscoveryService
{
    /// <summary>
    /// Discovers all services defined in the docker-compose file.
    /// </summary>
    Task<List<ServiceDefinition>> DiscoverServicesAsync();

    /// <summary>
    /// Discovers services and classifies them by type.
    /// </summary>
    Task<Dictionary<ServiceType, List<ServiceDefinition>>> DiscoverServicesByTypeAsync();

    /// <summary>
    /// Gets a specific service by name.
    /// </summary>
    Task<ServiceDefinition?> GetServiceAsync(string serviceName);
}
