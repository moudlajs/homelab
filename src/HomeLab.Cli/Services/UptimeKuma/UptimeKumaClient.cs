using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.UptimeKuma;

/// <summary>
/// Client for interacting with Uptime Kuma API.
/// Uptime Kuma is a self-hosted monitoring tool that tracks service uptime.
/// </summary>
public class UptimeKumaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public UptimeKumaClient(HttpClient httpClient, string baseUrl, string? apiKey = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
    }

    /// <summary>
    /// Get health check for Uptime Kuma service.
    /// </summary>
    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/status-page/default");

            return response.IsSuccessStatusCode
                ? new ServiceHealthInfo { IsHealthy = true, Message = "Uptime Kuma is running" }
                : new ServiceHealthInfo { IsHealthy = false, Message = $"HTTP {(int)response.StatusCode}" };
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

    /// <summary>
    /// Get all monitored services.
    /// </summary>
    public async Task<List<UptimeMonitor>> GetMonitorsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UptimeKumaResponse<List<UptimeMonitor>>>(
                $"{_baseUrl}/api/monitors",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return response?.Data ?? new List<UptimeMonitor>();
        }
        catch
        {
            // If real API isn't available, return mock data
            return GetMockMonitors();
        }
    }

    /// <summary>
    /// Get monitor by ID.
    /// </summary>
    public async Task<UptimeMonitor?> GetMonitorAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UptimeKumaResponse<UptimeMonitor>>(
                $"{_baseUrl}/api/monitor/{id}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get uptime statistics for a monitor.
    /// </summary>
    public async Task<UptimeStats?> GetMonitorStatsAsync(int id, int days = 30)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UptimeKumaResponse<UptimeStats>>(
                $"{_baseUrl}/api/monitor/{id}/stats?days={days}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return response?.Data;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get recent alerts/incidents.
    /// </summary>
    public async Task<List<UptimeIncident>> GetIncidentsAsync(int limit = 10)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<UptimeKumaResponse<List<UptimeIncident>>>(
                $"{_baseUrl}/api/incidents?limit={limit}",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return response?.Data ?? new List<UptimeIncident>();
        }
        catch
        {
            return GetMockIncidents();
        }
    }

    /// <summary>
    /// Add a new monitor.
    /// </summary>
    public async Task<bool> AddMonitorAsync(string name, string url, string type = "http")
    {
        try
        {
            var payload = new
            {
                name,
                url,
                type,
                interval = 60 // Check every 60 seconds
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/add-monitor", payload);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Remove a monitor.
    /// </summary>
    public async Task<bool> RemoveMonitorAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/api/monitor/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // Mock data for testing when real API isn't available
    private List<UptimeMonitor> GetMockMonitors()
    {
        return new List<UptimeMonitor>
        {
            new()
            {
                Id = 1,
                Name = "AdGuard Home",
                Url = "http://localhost:3000",
                Type = "http",
                Status = MonitorStatus.Up,
                UptimePercentage = 99.98m,
                AverageResponse = 45
            },
            new()
            {
                Id = 2,
                Name = "WireGuard VPN",
                Url = "http://localhost:51820",
                Type = "port",
                Status = MonitorStatus.Up,
                UptimePercentage = 100m,
                AverageResponse = 12
            },
            new()
            {
                Id = 3,
                Name = "Prometheus",
                Url = "http://localhost:9090",
                Type = "http",
                Status = MonitorStatus.Down,
                UptimePercentage = 85.5m,
                AverageResponse = 0
            }
        };
    }

    private List<UptimeIncident> GetMockIncidents()
    {
        return new List<UptimeIncident>
        {
            new()
            {
                Id = 1,
                MonitorName = "Prometheus",
                Status = "down",
                Message = "Connection refused",
                StartedAt = DateTime.UtcNow.AddHours(-2),
                Duration = TimeSpan.FromHours(2)
            },
            new()
            {
                Id = 2,
                MonitorName = "AdGuard Home",
                Status = "recovered",
                Message = "HTTP 200 OK",
                StartedAt = DateTime.UtcNow.AddDays(-1),
                EndedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
                Duration = TimeSpan.FromMinutes(5)
            }
        };
    }
}

/// <summary>
/// Generic response wrapper for Uptime Kuma API.
/// </summary>
public class UptimeKumaResponse<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("msg")]
    public string? Message { get; set; }
}

/// <summary>
/// Represents a monitored service in Uptime Kuma.
/// </summary>
public class UptimeMonitor
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "http";

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MonitorStatus Status { get; set; }

    [JsonPropertyName("uptime")]
    public decimal UptimePercentage { get; set; }

    [JsonPropertyName("avg_ping")]
    public int AverageResponse { get; set; }
}

/// <summary>
/// Monitor status enum.
/// </summary>
public enum MonitorStatus
{
    Up = 1,
    Down = 0,
    Unknown = -1
}

/// <summary>
/// Uptime statistics for a monitor.
/// </summary>
public class UptimeStats
{
    [JsonPropertyName("uptime_24h")]
    public decimal Uptime24h { get; set; }

    [JsonPropertyName("uptime_7d")]
    public decimal Uptime7d { get; set; }

    [JsonPropertyName("uptime_30d")]
    public decimal Uptime30d { get; set; }

    [JsonPropertyName("total_checks")]
    public int TotalChecks { get; set; }

    [JsonPropertyName("failed_checks")]
    public int FailedChecks { get; set; }
}

/// <summary>
/// Represents an uptime incident/outage.
/// </summary>
public class UptimeIncident
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("monitor_name")]
    public string MonitorName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }
}
