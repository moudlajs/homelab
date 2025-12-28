using System.Text.Json.Serialization;

namespace HomeLab.Cli.Services.HomeAssistant;

/// <summary>
/// Represents a Home Assistant entity (device, sensor, light, switch, etc.)
/// </summary>
public class HomeAssistantEntity
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, object> Attributes { get; set; } = new();

    [JsonPropertyName("last_changed")]
    public DateTime LastChanged { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Get the domain (e.g., "light", "switch", "sensor") from entity_id
    /// </summary>
    public string Domain => EntityId.Split('.').FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// Get the friendly name from attributes
    /// </summary>
    public string FriendlyName =>
        Attributes.TryGetValue("friendly_name", out var name) ? name?.ToString() ?? EntityId : EntityId;
}

/// <summary>
/// Represents a service call response from Home Assistant
/// </summary>
public class ServiceCallResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Represents Home Assistant configuration info
/// </summary>
public class HomeAssistantConfig
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("location_name")]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName("time_zone")]
    public string TimeZone { get; set; } = string.Empty;

    [JsonPropertyName("components")]
    public List<string> Components { get; set; } = new();

    [JsonPropertyName("config_dir")]
    public string ConfigDir { get; set; } = string.Empty;
}

/// <summary>
/// Represents a Home Assistant service
/// </summary>
public class HomeAssistantService
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("services")]
    public Dictionary<string, object> Services { get; set; } = new();
}
