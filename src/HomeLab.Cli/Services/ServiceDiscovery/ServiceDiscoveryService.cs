using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.ServiceDiscovery;

/// <summary>
/// Service that discovers homelab services from docker-compose files.
/// </summary>
public class ServiceDiscoveryService : IServiceDiscoveryService
{
    private readonly IHomelabConfigService _configService;
    private readonly ComposeFileParser _parser;
    private List<ServiceDefinition>? _cachedServices;

    public ServiceDiscoveryService(IHomelabConfigService configService)
    {
        _configService = configService;
        _parser = new ComposeFileParser();
    }

    public async Task<List<ServiceDefinition>> DiscoverServicesAsync()
    {
        if (_cachedServices != null)
        {
            return _cachedServices;
        }

        var composeFilePath = _configService.ComposeFilePath;

        if (!File.Exists(composeFilePath))
        {
            // Return empty list if compose file doesn't exist
            return new List<ServiceDefinition>();
        }

        _cachedServices = await Task.Run(() => _parser.Parse(composeFilePath));
        return _cachedServices;
    }

    public async Task<Dictionary<ServiceType, List<ServiceDefinition>>> DiscoverServicesByTypeAsync()
    {
        var services = await DiscoverServicesAsync();

        return services
            .GroupBy(s => s.Type)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task<ServiceDefinition?> GetServiceAsync(string serviceName)
    {
        var services = await DiscoverServicesAsync();
        return services.FirstOrDefault(s =>
            s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }
}
