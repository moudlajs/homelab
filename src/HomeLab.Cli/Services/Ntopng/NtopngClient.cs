using System.Net.Http.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Ntopng;

/// <summary>
/// Real ntopng client that calls the ntopng REST API.
/// Note: ntopng API can be complex - this is a simplified implementation.
/// </summary>
public class NtopngClient : INtopngClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public NtopngClient(IHomelabConfigService configService, HttpClient httpClient)
    {
        _httpClient = httpClient;

        var serviceConfig = configService.GetServiceConfig("ntopng");
        _baseUrl = serviceConfig.Url ?? "http://localhost:3002";
    }

    public string ServiceName => "ntopng";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Try to fetch interfaces to check if ntopng is responding
            var response = await _httpClient.GetAsync($"{_baseUrl}/lua/rest/v2/get/ntopng/interfaces.lua");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/lua/rest/v2/get/ntopng/interfaces.lua");

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

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = true,
                Status = "Running",
                Message = "ntopng is monitoring network traffic",
                Metrics = new Dictionary<string, string>
                {
                    { "Status", "Active" }
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

    public async Task<List<DeviceTraffic>> GetDevicesAsync()
    {
        try
        {
            // ntopng REST API endpoint for hosts
            // This is a simplified version - real ntopng API may require ifid parameter
            var response = await _httpClient.GetAsync($"{_baseUrl}/lua/rest/v2/get/host/active.lua?ifid=0");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ntopng API returned {response.StatusCode}");
            }

            // Parse response - ntopng returns complex nested JSON
            // This is simplified - actual ntopng response structure may vary
            var data = await response.Content.ReadFromJsonAsync<NtopngHostsResponse>();

            if (data?.Hosts == null)
            {
                return new List<DeviceTraffic>();
            }

            var devices = new List<DeviceTraffic>();

            foreach (var host in data.Hosts)
            {
                devices.Add(new DeviceTraffic
                {
                    DeviceName = host.Name ?? host.Ip,
                    IpAddress = host.Ip,
                    MacAddress = host.Mac ?? string.Empty,
                    FirstSeen = DateTimeOffset.FromUnixTimeSeconds(host.FirstSeen).DateTime,
                    LastSeen = DateTimeOffset.FromUnixTimeSeconds(host.LastSeen).DateTime,
                    BytesSent = host.BytesSent,
                    BytesReceived = host.BytesReceived,
                    ThroughputBps = host.Throughput,
                    Os = host.Os,
                    IsActive = host.IsActive
                });
            }

            return devices;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get devices from ntopng: {ex.Message}", ex);
        }
    }

    public async Task<TrafficStats> GetTrafficStatsAsync(string? deviceIp = null)
    {
        try
        {
            // Get all devices for statistics
            var devices = await GetDevicesAsync();

            if (!string.IsNullOrEmpty(deviceIp))
            {
                devices = devices.Where(d => d.IpAddress == deviceIp).ToList();
            }

            // Calculate top talkers
            var topTalkers = devices
                .OrderByDescending(d => d.BytesSent + d.BytesReceived)
                .Take(10)
                .Select(d => new TopTalker
                {
                    IpAddress = d.IpAddress,
                    DeviceName = d.DeviceName,
                    TotalBytes = d.BytesSent + d.BytesReceived,
                    BytesSent = d.BytesSent,
                    BytesReceived = d.BytesReceived
                })
                .ToList();

            // Calculate total bytes
            var totalBytes = devices.Sum(d => d.BytesSent + d.BytesReceived);

            return new TrafficStats
            {
                TopTalkers = topTalkers,
                TotalBytesTransferred = totalBytes,
                ActiveFlows = devices.Count(d => d.IsActive),
                ProtocolStats = new Dictionary<string, long>
                {
                    // ntopng provides detailed protocol stats, but simplified here
                    { "Total", totalBytes }
                },
                CollectedAt = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get traffic stats from ntopng: {ex.Message}", ex);
        }
    }

    // Internal DTOs for ntopng API responses (simplified)
    private class NtopngHostsResponse
    {
        [JsonPropertyName("hosts")]
        public List<NtopngHost>? Hosts { get; set; }
    }

    private class NtopngHost
    {
        [JsonPropertyName("ip")]
        public string Ip { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("mac")]
        public string? Mac { get; set; }

        [JsonPropertyName("bytes.sent")]
        public long BytesSent { get; set; }

        [JsonPropertyName("bytes.rcvd")]
        public long BytesReceived { get; set; }

        [JsonPropertyName("throughput")]
        public long? Throughput { get; set; }

        [JsonPropertyName("os")]
        public string? Os { get; set; }

        [JsonPropertyName("seen.first")]
        public long FirstSeen { get; set; }

        [JsonPropertyName("seen.last")]
        public long LastSeen { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }
    }
}
