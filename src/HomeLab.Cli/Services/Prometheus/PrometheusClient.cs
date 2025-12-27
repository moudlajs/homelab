using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Prometheus;

/// <summary>
/// Real Prometheus client that calls the Prometheus API.
/// </summary>
public class PrometheusClient : IPrometheusClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public PrometheusClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;

        var serviceConfig = configService.GetServiceConfig("prometheus");
        _baseUrl = serviceConfig.Url ?? "http://localhost:9090";
    }

    public string ServiceName => "Prometheus";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/-/healthy");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/-/healthy");

            if (!response.IsSuccessStatusCode)
            {
                return new ServiceHealthInfo
                {
                    ServiceName = ServiceName,
                    IsHealthy = false,
                    Status = "Unhealthy",
                    Message = $"API returned {response.StatusCode}"
                };
            }

            // Get targets count
            var targets = await GetTargetsAsync();
            var alerts = await GetActiveAlertsAsync();

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = "Running",
                Message = "Prometheus is healthy",
                Metrics = new Dictionary<string, string>
                {
                    { "Targets", targets.Count.ToString() },
                    { "Active Alerts", alerts.Count.ToString() },
                    { "Up Targets", targets.Count(t => t.Health == "up").ToString() }
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = false,
                Status = "Error",
                Message = $"Failed to connect: {ex.Message}"
            };
        }
    }

    public async Task<List<AlertInfo>> GetActiveAlertsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/alerts");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PrometheusAlertsResponse>();

            if (result?.Data?.Alerts == null)
            {
                return new List<AlertInfo>();
            }

            return result.Data.Alerts
                .Where(a => a.State == "firing")
                .Select(a => new AlertInfo
                {
                    Name = a.Labels?.GetValueOrDefault("alertname") ?? "Unknown",
                    State = a.State,
                    Severity = a.Labels?.GetValueOrDefault("severity") ?? "unknown",
                    Summary = a.Annotations?.GetValueOrDefault("summary") ?? string.Empty,
                    Description = a.Annotations?.GetValueOrDefault("description") ?? string.Empty,
                    ActiveAt = a.ActiveAt,
                    Labels = a.Labels ?? new Dictionary<string, string>()
                })
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get alerts: {ex.Message}", ex);
        }
    }

    public async Task<List<TargetInfo>> GetTargetsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/targets");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PrometheusTargetsResponse>();

            if (result?.Data?.ActiveTargets == null)
            {
                return new List<TargetInfo>();
            }

            return result.Data.ActiveTargets.Select(t => new TargetInfo
            {
                Job = t.Labels?.GetValueOrDefault("job") ?? "unknown",
                Instance = t.Labels?.GetValueOrDefault("instance") ?? "unknown",
                Health = t.Health,
                LastScrape = t.LastScrape,
                ScrapeDuration = t.ScrapeDuration,
                Error = t.LastError
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get targets: {ex.Message}", ex);
        }
    }

    public async Task<string> QueryAsync(string query)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/query?query={Uri.EscapeDataString(query)}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return content;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to execute query: {ex.Message}", ex);
        }
    }

    // Prometheus API response models
    private class PrometheusAlertsResponse
    {
        [JsonPropertyName("data")]
        public AlertsData? Data { get; set; }
    }

    private class AlertsData
    {
        [JsonPropertyName("alerts")]
        public List<PrometheusAlert>? Alerts { get; set; }
    }

    private class PrometheusAlert
    {
        [JsonPropertyName("labels")]
        public Dictionary<string, string>? Labels { get; set; }

        [JsonPropertyName("annotations")]
        public Dictionary<string, string>? Annotations { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("activeAt")]
        public DateTime ActiveAt { get; set; }
    }

    private class PrometheusTargetsResponse
    {
        [JsonPropertyName("data")]
        public TargetsData? Data { get; set; }
    }

    private class TargetsData
    {
        [JsonPropertyName("activeTargets")]
        public List<PrometheusTarget>? ActiveTargets { get; set; }
    }

    private class PrometheusTarget
    {
        [JsonPropertyName("labels")]
        public Dictionary<string, string>? Labels { get; set; }

        [JsonPropertyName("health")]
        public string Health { get; set; } = string.Empty;

        [JsonPropertyName("lastScrape")]
        public DateTime? LastScrape { get; set; }

        [JsonPropertyName("scrapePool")]
        public string? ScrapePool { get; set; }

        [JsonPropertyName("scrapeDuration")]
        public double ScrapeDuration { get; set; }

        [JsonPropertyName("lastError")]
        public string? LastError { get; set; }
    }
}
