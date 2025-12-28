using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.HomeAssistant;

/// <summary>
/// Client for interacting with Home Assistant REST API
/// </summary>
public interface IHomeAssistantClient
{
    /// <summary>
    /// Get health status of Home Assistant
    /// </summary>
    Task<ServiceHealthInfo> GetHealthInfoAsync();

    /// <summary>
    /// Get all entities from Home Assistant
    /// </summary>
    Task<List<HomeAssistantEntity>> GetAllEntitiesAsync();

    /// <summary>
    /// Get a specific entity by ID
    /// </summary>
    Task<HomeAssistantEntity?> GetEntityAsync(string entityId);

    /// <summary>
    /// Get entities by domain (light, switch, sensor, etc.)
    /// </summary>
    Task<List<HomeAssistantEntity>> GetEntitiesByDomainAsync(string domain);

    /// <summary>
    /// Turn on a device (light, switch, etc.)
    /// </summary>
    Task<bool> TurnOnAsync(string entityId);

    /// <summary>
    /// Turn off a device (light, switch, etc.)
    /// </summary>
    Task<bool> TurnOffAsync(string entityId);

    /// <summary>
    /// Toggle a device state
    /// </summary>
    Task<bool> ToggleAsync(string entityId);

    /// <summary>
    /// Call a service with optional data
    /// </summary>
    Task<bool> CallServiceAsync(string domain, string service, Dictionary<string, object>? data = null);

    /// <summary>
    /// Get Home Assistant configuration
    /// </summary>
    Task<HomeAssistantConfig> GetConfigAsync();

    /// <summary>
    /// Get available services
    /// </summary>
    Task<List<HomeAssistantService>> GetServicesAsync();
}
