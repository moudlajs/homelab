using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Network;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of nmap service for testing without actual nmap installation.
/// </summary>
public class MockNmapService : INmapService
{
    public bool IsNmapAvailable()
    {
        return true;  // Always return true in mock mode
    }

    public Task<List<NetworkDevice>> ScanNetworkAsync(string networkRange, bool quickScan = false)
    {
        // Return mock network devices
        var devices = new List<NetworkDevice>
        {
            new()
            {
                IpAddress = "192.168.1.1",
                MacAddress = "00:11:22:33:44:55",
                Hostname = "gateway.local",
                Vendor = "Ubiquiti Networks",
                OpenPorts = new List<int> { 80, 443, 22 },
                OsGuess = "Linux 5.x",
                Status = "up",
                ScannedAt = DateTime.Now
            },
            new()
            {
                IpAddress = "192.168.1.10",
                MacAddress = "AA:BB:CC:DD:EE:FF",
                Hostname = "mac-mini.local",
                Vendor = "Apple, Inc.",
                OpenPorts = new List<int> { 22, 80, 443, 3000, 3001, 9090 },
                OsGuess = "macOS 14.x",
                Status = "up",
                ScannedAt = DateTime.Now
            },
            new()
            {
                IpAddress = "192.168.1.20",
                MacAddress = "11:22:33:44:55:66",
                Hostname = "iphone.local",
                Vendor = "Apple, Inc.",
                OpenPorts = new List<int>(),
                OsGuess = "iOS 17.x",
                Status = "up",
                ScannedAt = DateTime.Now
            },
            new()
            {
                IpAddress = "192.168.1.30",
                MacAddress = "AA:BB:CC:DD:EE:11",
                Hostname = "raspberry-pi.local",
                Vendor = "Raspberry Pi Foundation",
                OpenPorts = new List<int> { 22, 80 },
                OsGuess = "Linux 6.x",
                Status = "up",
                ScannedAt = DateTime.Now
            },
            new()
            {
                IpAddress = "192.168.1.50",
                MacAddress = "FF:EE:DD:CC:BB:AA",
                Hostname = "smart-tv.local",
                Vendor = "Samsung Electronics",
                OpenPorts = new List<int> { 8080 },
                OsGuess = "Linux (embedded)",
                Status = "up",
                ScannedAt = DateTime.Now
            }
        };

        return Task.FromResult(devices);
    }

    public Task<List<PortScanResult>> ScanPortsAsync(string ipAddress, bool commonPortsOnly = true)
    {
        // Return mock port scan results
        var results = new List<PortScanResult>
        {
            new()
            {
                IpAddress = ipAddress,
                Port = 22,
                Protocol = "tcp",
                Service = "ssh",
                State = "open",
                Version = "OpenSSH 9.0",
                ExtraInfo = "protocol 2.0"
            },
            new()
            {
                IpAddress = ipAddress,
                Port = 80,
                Protocol = "tcp",
                Service = "http",
                State = "open",
                Version = "nginx 1.24.0",
                ExtraInfo = null
            },
            new()
            {
                IpAddress = ipAddress,
                Port = 443,
                Protocol = "tcp",
                Service = "https",
                State = "open",
                Version = "nginx 1.24.0",
                ExtraInfo = null
            },
            new()
            {
                IpAddress = ipAddress,
                Port = 3000,
                Protocol = "tcp",
                Service = "http",
                State = "open",
                Version = "AdGuard Home",
                ExtraInfo = null
            },
            new()
            {
                IpAddress = ipAddress,
                Port = 9090,
                Protocol = "tcp",
                Service = "http",
                State = "open",
                Version = "Prometheus",
                ExtraInfo = null
            }
        };

        if (!commonPortsOnly)
        {
            // Add more ports for full scan
            results.AddRange(new[]
            {
                new PortScanResult
                {
                    IpAddress = ipAddress,
                    Port = 8080,
                    Protocol = "tcp",
                    Service = "http-proxy",
                    State = "open"
                },
                new PortScanResult
                {
                    IpAddress = ipAddress,
                    Port = 5432,
                    Protocol = "tcp",
                    Service = "postgresql",
                    State = "open"
                }
            });
        }

        return Task.FromResult(results);
    }
}
