using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of ntopng client for testing without actual ntopng installation.
/// </summary>
public class MockNtopngClient : INtopngClient
{
    public string ServiceName => "ntopng (Mock)";

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        return Task.FromResult(new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = "Running",
            Message = "Mock ntopng service",
            Metrics = new Dictionary<string, string>
            {
                { "Mode", "Mock" },
                { "Devices Tracked", "8" }
            }
        });
    }

    public Task<List<DeviceTraffic>> GetDevicesAsync()
    {
        var devices = new List<DeviceTraffic>
        {
            new()
            {
                DeviceName = "gateway.local",
                IpAddress = "192.168.1.1",
                MacAddress = "00:11:22:33:44:55",
                FirstSeen = DateTime.Now.AddDays(-30),
                LastSeen = DateTime.Now,
                BytesSent = 15_000_000_000,  // 15 GB
                BytesReceived = 25_000_000_000,  // 25 GB
                ThroughputBps = 1_250_000,  // 1.25 MB/s
                Os = "Linux",
                IsActive = true
            },
            new()
            {
                DeviceName = "mac-mini.local",
                IpAddress = "192.168.1.10",
                MacAddress = "AA:BB:CC:DD:EE:FF",
                FirstSeen = DateTime.Now.AddDays(-90),
                LastSeen = DateTime.Now,
                BytesSent = 50_000_000_000,  // 50 GB
                BytesReceived = 100_000_000_000,  // 100 GB
                ThroughputBps = 5_000_000,  // 5 MB/s
                Os = "macOS",
                IsActive = true
            },
            new()
            {
                DeviceName = "iphone.local",
                IpAddress = "192.168.1.20",
                MacAddress = "11:22:33:44:55:66",
                FirstSeen = DateTime.Now.AddDays(-180),
                LastSeen = DateTime.Now.AddMinutes(-5),
                BytesSent = 8_000_000_000,  // 8 GB
                BytesReceived = 12_000_000_000,  // 12 GB
                ThroughputBps = 500_000,  // 500 KB/s
                Os = "iOS",
                IsActive = true
            },
            new()
            {
                DeviceName = "raspberry-pi.local",
                IpAddress = "192.168.1.30",
                MacAddress = "AA:BB:CC:DD:EE:11",
                FirstSeen = DateTime.Now.AddDays(-365),
                LastSeen = DateTime.Now,
                BytesSent = 2_000_000_000,  // 2 GB
                BytesReceived = 5_000_000_000,  // 5 GB
                ThroughputBps = 100_000,  // 100 KB/s
                Os = "Linux",
                IsActive = true
            },
            new()
            {
                DeviceName = "smart-tv.local",
                IpAddress = "192.168.1.50",
                MacAddress = "FF:EE:DD:CC:BB:AA",
                FirstSeen = DateTime.Now.AddDays(-60),
                LastSeen = DateTime.Now.AddHours(-2),
                BytesSent = 1_000_000_000,  // 1 GB
                BytesReceived = 20_000_000_000,  // 20 GB (streaming)
                ThroughputBps = null,
                Os = "Linux (embedded)",
                IsActive = false
            },
            new()
            {
                DeviceName = "laptop.local",
                IpAddress = "192.168.1.25",
                MacAddress = "12:34:56:78:90:AB",
                FirstSeen = DateTime.Now.AddDays(-7),
                LastSeen = DateTime.Now.AddHours(-1),
                BytesSent = 3_000_000_000,  // 3 GB
                BytesReceived = 8_000_000_000,  // 8 GB
                ThroughputBps = 750_000,  // 750 KB/s
                Os = "Windows",
                IsActive = false
            },
            new()
            {
                DeviceName = "printer.local",
                IpAddress = "192.168.1.100",
                MacAddress = "AB:CD:EF:12:34:56",
                FirstSeen = DateTime.Now.AddDays(-200),
                LastSeen = DateTime.Now.AddDays(-1),
                BytesSent = 50_000_000,  // 50 MB
                BytesReceived = 100_000_000,  // 100 MB
                ThroughputBps = null,
                Os = null,
                IsActive = false
            },
            new()
            {
                DeviceName = "security-camera.local",
                IpAddress = "192.168.1.150",
                MacAddress = "CA:FE:BA:BE:00:01",
                FirstSeen = DateTime.Now.AddDays(-120),
                LastSeen = DateTime.Now,
                BytesSent = 30_000_000_000,  // 30 GB (video upload)
                BytesReceived = 500_000_000,  // 500 MB
                ThroughputBps = 2_000_000,  // 2 MB/s
                Os = "Linux (embedded)",
                IsActive = true
            }
        };

        return Task.FromResult(devices);
    }

    public async Task<TrafficStats> GetTrafficStatsAsync(string? deviceIp = null)
    {
        var devices = await GetDevicesAsync();

        if (!string.IsNullOrEmpty(deviceIp))
        {
            devices = devices.Where(d => d.IpAddress == deviceIp).ToList();
        }

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

        var totalBytes = devices.Sum(d => d.BytesSent + d.BytesReceived);

        return new TrafficStats
        {
            TopTalkers = topTalkers,
            TotalBytesTransferred = totalBytes,
            ActiveFlows = devices.Count(d => d.IsActive),
            ProtocolStats = new Dictionary<string, long>
            {
                { "HTTP", totalBytes / 3 },
                { "HTTPS", totalBytes / 2 },
                { "DNS", totalBytes / 20 },
                { "SSH", totalBytes / 50 },
                { "Other", totalBytes - (totalBytes / 3 + totalBytes / 2 + totalBytes / 20 + totalBytes / 50) }
            },
            CollectedAt = DateTime.Now
        };
    }
}
