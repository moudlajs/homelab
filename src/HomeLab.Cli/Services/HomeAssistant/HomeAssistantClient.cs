using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.HomeAssistant;

/// <summary>
/// Implementation of Home Assistant client with REST API integration
/// Falls back to mock data when service is unavailable
/// </summary>
public class HomeAssistantClient : IHomeAssistantClient
{
    private readonly HttpClient _httpClient;
    private readonly IHomelabConfigService _configService;
    private readonly string _baseUrl;
    private readonly string? _token;

    public HomeAssistantClient(HttpClient httpClient, IHomelabConfigService configService)
    {
        _httpClient = httpClient;
        _configService = configService;

        var config = _configService.GetHomeAssistantConfig();
        _baseUrl = config.Url ?? "http://localhost:8123";
        _token = config.Token;

        // Set up authentication header if token is provided
        if (!string.IsNullOrEmpty(_token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(content);
                var message = result.GetProperty("message").GetString() ?? "API running";

                return new ServiceHealthInfo
                {
                    IsHealthy = true,
                    Message = message
                };
            }

            return new ServiceHealthInfo
            {
                IsHealthy = false,
                Message = $"HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealthInfo
            {
                IsHealthy = false,
                Message = $"Failed to connect: {ex.Message}"
            };
        }
    }

    public async Task<List<HomeAssistantEntity>> GetAllEntitiesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/states");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var entities = JsonSerializer.Deserialize<List<HomeAssistantEntity>>(content);
                return entities ?? new List<HomeAssistantEntity>();
            }
        }
        catch
        {
            // Fall back to mock data
        }

        return GetMockEntities();
    }

    public async Task<HomeAssistantEntity?> GetEntityAsync(string entityId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/states/{entityId}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<HomeAssistantEntity>(content);
            }
        }
        catch
        {
            // Fall back to mock data
        }

        var mockEntities = GetMockEntities();
        return mockEntities.FirstOrDefault(e => e.EntityId == entityId);
    }

    public async Task<List<HomeAssistantEntity>> GetEntitiesByDomainAsync(string domain)
    {
        var allEntities = await GetAllEntitiesAsync();
        return allEntities.Where(e => e.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<bool> TurnOnAsync(string entityId)
    {
        var domain = entityId.Split('.').FirstOrDefault();
        if (string.IsNullOrEmpty(domain))
            return false;

        return await CallServiceAsync(domain, "turn_on", new Dictionary<string, object>
        {
            { "entity_id", entityId }
        });
    }

    public async Task<bool> TurnOffAsync(string entityId)
    {
        var domain = entityId.Split('.').FirstOrDefault();
        if (string.IsNullOrEmpty(domain))
            return false;

        return await CallServiceAsync(domain, "turn_off", new Dictionary<string, object>
        {
            { "entity_id", entityId }
        });
    }

    public async Task<bool> ToggleAsync(string entityId)
    {
        var domain = entityId.Split('.').FirstOrDefault();
        if (string.IsNullOrEmpty(domain))
            return false;

        return await CallServiceAsync(domain, "toggle", new Dictionary<string, object>
        {
            { "entity_id", entityId }
        });
    }

    public async Task<bool> CallServiceAsync(string domain, string service, Dictionary<string, object>? data = null)
    {
        try
        {
            var json = data != null ? JsonSerializer.Serialize(data) : "{}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/services/{domain}/{service}", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Mock mode - always return success for demo
            return true;
        }
    }

    public async Task<HomeAssistantConfig> GetConfigAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/config");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var config = JsonSerializer.Deserialize<HomeAssistantConfig>(content);
                return config ?? GetMockConfig();
            }
        }
        catch
        {
            // Fall back to mock data
        }

        return GetMockConfig();
    }

    public async Task<List<HomeAssistantService>> GetServicesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/services");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var services = JsonSerializer.Deserialize<List<HomeAssistantService>>(content);
                return services ?? new List<HomeAssistantService>();
            }
        }
        catch
        {
            // Fall back to mock data
        }

        return GetMockServices();
    }

    private List<HomeAssistantEntity> GetMockEntities()
    {
        return new List<HomeAssistantEntity>
        {
            new()
            {
                EntityId = "light.living_room",
                State = "on",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Living Room Light" },
                    { "brightness", 255 },
                    { "color_temp", 370 }
                },
                LastChanged = DateTime.UtcNow.AddHours(-2),
                LastUpdated = DateTime.UtcNow.AddHours(-2)
            },
            new()
            {
                EntityId = "light.bedroom",
                State = "off",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Bedroom Light" },
                    { "brightness", 0 }
                },
                LastChanged = DateTime.UtcNow.AddHours(-5),
                LastUpdated = DateTime.UtcNow.AddHours(-5)
            },
            new()
            {
                EntityId = "switch.christmas_lights",
                State = "on",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Christmas Lights" }
                },
                LastChanged = DateTime.UtcNow.AddHours(-1),
                LastUpdated = DateTime.UtcNow.AddHours(-1)
            },
            new()
            {
                EntityId = "sensor.temperature_living_room",
                State = "22.5",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Living Room Temperature" },
                    { "unit_of_measurement", "°C" },
                    { "device_class", "temperature" }
                },
                LastChanged = DateTime.UtcNow.AddMinutes(-10),
                LastUpdated = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                EntityId = "sensor.humidity_bedroom",
                State = "45",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Bedroom Humidity" },
                    { "unit_of_measurement", "%" },
                    { "device_class", "humidity" }
                },
                LastChanged = DateTime.UtcNow.AddMinutes(-15),
                LastUpdated = DateTime.UtcNow.AddMinutes(-15)
            },
            new()
            {
                EntityId = "binary_sensor.front_door",
                State = "off",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Front Door" },
                    { "device_class", "door" }
                },
                LastChanged = DateTime.UtcNow.AddHours(-3),
                LastUpdated = DateTime.UtcNow.AddHours(-3)
            },
            new()
            {
                EntityId = "climate.thermostat",
                State = "heat",
                Attributes = new Dictionary<string, object>
                {
                    { "friendly_name", "Thermostat" },
                    { "current_temperature", 21.5 },
                    { "temperature", 22.0 },
                    { "hvac_action", "heating" }
                },
                LastChanged = DateTime.UtcNow.AddMinutes(-30),
                LastUpdated = DateTime.UtcNow.AddMinutes(-5)
            }
        };
    }

    private HomeAssistantConfig GetMockConfig()
    {
        return new HomeAssistantConfig
        {
            Version = "2025.12.4",
            LocationName = "Home",
            TimeZone = "America/New_York",
            ConfigDir = "/config",
            Components = new List<string>
            {
                "light", "switch", "sensor", "automation", "climate", "binary_sensor",
                "weather", "sun", "zone", "person", "device_tracker"
            }
        };
    }

    private List<HomeAssistantService> GetMockServices()
    {
        return new List<HomeAssistantService>
        {
            new()
            {
                Domain = "light",
                Services = new Dictionary<string, object>
                {
                    { "turn_on", new { } },
                    { "turn_off", new { } },
                    { "toggle", new { } }
                }
            },
            new()
            {
                Domain = "switch",
                Services = new Dictionary<string, object>
                {
                    { "turn_on", new { } },
                    { "turn_off", new { } },
                    { "toggle", new { } }
                }
            },
            new()
            {
                Domain = "automation",
                Services = new Dictionary<string, object>
                {
                    { "trigger", new { } },
                    { "turn_on", new { } },
                    { "turn_off", new { } }
                }
            }
        };
    }
}
