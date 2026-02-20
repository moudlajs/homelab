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
            // Discover the first non-loopback interface
            var ifid = await GetActiveInterfaceIdAsync();

            var response = await _httpClient.GetAsync($"{_baseUrl}/lua/rest/v2/get/host/active.lua?ifid={ifid}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ntopng API returned {response.StatusCode}");
            }

            var data = await response.Content.ReadFromJsonAsync<NtopngApiResponse<NtopngHostsPage>>();
            var hosts = data?.Rsp?.Data;

            if (hosts == null || hosts.Count == 0)
            {
                return new List<DeviceTraffic>();
            }

            return hosts.Select(host => new DeviceTraffic
            {
                DeviceName = host.Name ?? host.Ip,
                IpAddress = host.Ip,
                MacAddress = host.Mac ?? string.Empty,
                FirstSeen = DateTimeOffset.FromUnixTimeSeconds(host.FirstSeen).DateTime,
                LastSeen = DateTimeOffset.FromUnixTimeSeconds(host.LastSeen).DateTime,
                BytesSent = host.Bytes?.Sent ?? 0,
                BytesReceived = host.Bytes?.Recvd ?? 0,
                ThroughputBps = (long?)(host.Thpt?.Bps),
                Os = host.Os?.ToString(),
                IsActive = host.NumFlows?.Total > 0
            }).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to get devices from ntopng: {ex.Message}", ex);
        }
    }

    private async Task<int> GetActiveInterfaceIdAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/lua/rest/v2/get/ntopng/interfaces.lua");
            if (!response.IsSuccessStatusCode)
            {
                return 2;
            }

            var data = await response.Content.ReadFromJsonAsync<NtopngApiResponse<List<NtopngInterface>>>();
            // Pick first non-loopback interface
            var iface = data?.Rsp?.FirstOrDefault(i => i.Name != "lo");
            return iface?.Ifid ?? 2;
        }
        catch
        {
            return 2;
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

    // Internal DTOs matching actual ntopng REST v2 response format

    private class NtopngApiResponse<T>
    {
        [JsonPropertyName("rc")] public int Rc { get; set; }
        [JsonPropertyName("rc_str")] public string? RcStr { get; set; }
        [JsonPropertyName("rsp")] public T? Rsp { get; set; }
    }

    private class NtopngHostsPage
    {
        [JsonPropertyName("data")] public List<NtopngHost>? Data { get; set; }
    }

    private class NtopngInterface
    {
        [JsonPropertyName("ifid")] public int Ifid { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private class NtopngHost
    {
        [JsonPropertyName("ip")] public string Ip { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("mac")] public string? Mac { get; set; }
        [JsonPropertyName("bytes")] public NtopngBytes? Bytes { get; set; }
        [JsonPropertyName("thpt")] public NtopngThpt? Thpt { get; set; }
        [JsonPropertyName("os")] public object? Os { get; set; }
        [JsonPropertyName("first_seen")] public long FirstSeen { get; set; }
        [JsonPropertyName("last_seen")] public long LastSeen { get; set; }
        [JsonPropertyName("num_flows")] public NtopngFlows? NumFlows { get; set; }
    }

    private class NtopngBytes
    {
        [JsonPropertyName("sent")] public long Sent { get; set; }
        [JsonPropertyName("recvd")] public long Recvd { get; set; }
        [JsonPropertyName("total")] public long Total { get; set; }
    }

    private class NtopngThpt
    {
        [JsonPropertyName("bps")] public double Bps { get; set; }
    }

    private class NtopngFlows
    {
        [JsonPropertyName("total")] public int Total { get; set; }
    }
}
