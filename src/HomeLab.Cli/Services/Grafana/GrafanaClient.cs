using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Grafana;

/// <summary>
/// Real Grafana client that calls the Grafana API.
/// </summary>
public class GrafanaClient : IGrafanaClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _username;
    private readonly string? _password;

    public GrafanaClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;

        var serviceConfig = configService.GetServiceConfig("grafana");
        _baseUrl = serviceConfig.Url ?? "http://localhost:3001";
        _username = serviceConfig.Username;
        _password = serviceConfig.Password;

        // Setup basic auth if credentials provided
        if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            var authBytes = System.Text.Encoding.UTF8.GetBytes($"{_username}:{_password}");
            var authHeader = Convert.ToBase64String(authBytes);
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);
        }
    }

    public string ServiceName => "Grafana";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/health");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/health");

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

            var dashboards = await GetDashboardsAsync();

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = "Running",
                Message = "Grafana is healthy",
                Metrics = new Dictionary<string, string>
                {
                    { "Dashboards", dashboards.Count.ToString() },
                    { "URL", _baseUrl }
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

    public async Task<List<DashboardInfo>> GetDashboardsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/search?type=dash-db");
            response.EnsureSuccessStatusCode();

            var dashboards = await response.Content.ReadFromJsonAsync<List<GrafanaDashboard>>();

            if (dashboards == null)
            {
                return new List<DashboardInfo>();
            }

            return dashboards.Select(d => new DashboardInfo
            {
                Uid = d.Uid ?? string.Empty,
                Title = d.Title ?? string.Empty,
                Uri = d.Uri ?? string.Empty,
                Url = d.Url ?? string.Empty,
                Tags = d.Tags ?? new List<string>(),
                IsStarred = d.IsStarred
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get dashboards: {ex.Message}", ex);
        }
    }

    public async Task OpenDashboardAsync(string uid)
    {
        var url = GetDashboardUrl(uid);

        await Task.Run(() =>
        {
            try
            {
                // Try to open in browser (macOS)
                System.Diagnostics.Process.Start("open", url);
            }
            catch
            {
                // Silently fail - user can copy the URL
            }
        });
    }

    public string GetDashboardUrl(string uid = "")
    {
        return string.IsNullOrEmpty(uid) ? _baseUrl : $"{_baseUrl}/d/{uid}";
    }

    // Grafana API response models
    private class GrafanaDashboard
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("uid")]
        public string? Uid { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("isStarred")]
        public bool IsStarred { get; set; }
    }
}
