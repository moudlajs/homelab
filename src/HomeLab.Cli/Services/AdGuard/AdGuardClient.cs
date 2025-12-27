using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.AdGuard;

/// <summary>
/// Real AdGuard Home client that calls the AdGuard Home API.
/// </summary>
public class AdGuardClient : IAdGuardClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _username;
    private readonly string? _password;

    public AdGuardClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;

        var serviceConfig = configService.GetServiceConfig("adguard");
        _baseUrl = serviceConfig.Url ?? "http://localhost:3000";
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

    public string ServiceName => "AdGuard Home";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/control/status");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/control/status");

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

            var status = await response.Content.ReadFromJsonAsync<AdGuardStatus>();

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = "Running",
                Message = status?.Running == true ? "Service is running" : "Service is not running",
                Metrics = new Dictionary<string, string>
                {
                    { "Version", status?.Version ?? "Unknown" },
                    { "Protection", status?.ProtectionEnabled == true ? "Enabled" : "Disabled" },
                    { "DNS Port", status?.DnsPort.ToString() ?? "53" }
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

    public async Task<DnsStats> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/control/stats");
            response.EnsureSuccessStatusCode();

            var stats = await response.Content.ReadFromJsonAsync<AdGuardStatsResponse>();

            if (stats == null)
            {
                throw new InvalidOperationException("Failed to parse stats response");
            }

            var totalQueries = stats.NumDnsQueries;
            var blockedQueries = stats.NumBlockedFiltering;
            var blockedPercentage = totalQueries > 0
                ? Math.Round((double)blockedQueries / totalQueries * 100, 2)
                : 0;

            return new DnsStats
            {
                TotalQueries = totalQueries,
                BlockedQueries = blockedQueries,
                BlockedPercentage = blockedPercentage,
                SafeBrowsingBlocks = stats.NumReplacedSafebrowsing,
                ParentalBlocks = stats.NumReplacedParental,
                UpdatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get DNS stats: {ex.Message}", ex);
        }
    }

    public async Task<List<BlockedDomain>> GetTopBlockedDomainsAsync(int limit = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/control/stats");
            response.EnsureSuccessStatusCode();

            var stats = await response.Content.ReadFromJsonAsync<AdGuardStatsResponse>();

            if (stats?.BlockedFiltering == null)
            {
                return new List<BlockedDomain>();
            }

            return stats.BlockedFiltering
                .OrderByDescending(kvp => kvp.Value)
                .Take(limit)
                .Select(kvp => new BlockedDomain
                {
                    Domain = kvp.Key,
                    Count = kvp.Value
                })
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get blocked domains: {ex.Message}", ex);
        }
    }

    public async Task UpdateFiltersAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/control/filtering/refresh", null);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to update filters: {ex.Message}", ex);
        }
    }

    // AdGuard API response models
    private class AdGuardStatus
    {
        [JsonPropertyName("running")]
        public bool Running { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("protection_enabled")]
        public bool ProtectionEnabled { get; set; }

        [JsonPropertyName("dns_port")]
        public int DnsPort { get; set; }
    }

    private class AdGuardStatsResponse
    {
        [JsonPropertyName("num_dns_queries")]
        public long NumDnsQueries { get; set; }

        [JsonPropertyName("num_blocked_filtering")]
        public long NumBlockedFiltering { get; set; }

        [JsonPropertyName("num_replaced_safebrowsing")]
        public long NumReplacedSafebrowsing { get; set; }

        [JsonPropertyName("num_replaced_parental")]
        public long NumReplacedParental { get; set; }

        [JsonPropertyName("blocked_filtering")]
        public Dictionary<string, long>? BlockedFiltering { get; set; }
    }
}
