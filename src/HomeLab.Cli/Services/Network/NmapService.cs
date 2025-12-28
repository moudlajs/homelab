using System.Diagnostics;
using System.Xml.Linq;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Network;

/// <summary>
/// Real implementation of nmap network scanning service.
/// Executes nmap CLI commands and parses XML output.
/// </summary>
public class NmapService : INmapService
{
    private readonly IHomelabConfigService _configService;
    private const string NmapCommand = "nmap";

    public NmapService(IHomelabConfigService configService)
    {
        _configService = configService;
    }

    public bool IsNmapAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = NmapCommand,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<NetworkDevice>> ScanNetworkAsync(string networkRange, bool quickScan = false)
    {
        if (!IsNmapAvailable())
        {
            throw new InvalidOperationException(
                "nmap is not installed. Install it with: brew install nmap");
        }

        // Build nmap arguments
        // -sn: Ping scan (no port scan) for quick discovery
        // -sT: TCP connect scan (no sudo required)
        // -oX -: Output XML to stdout
        var args = quickScan
            ? $"-sn -oX - {networkRange}"
            : $"-sT -T4 -F -oX - {networkRange}";  // -T4: faster, -F: fast (100 common ports)

        var xmlOutput = await ExecuteNmapAsync(args);
        return ParseNetworkScanXml(xmlOutput);
    }

    public async Task<List<PortScanResult>> ScanPortsAsync(string ipAddress, bool commonPortsOnly = true)
    {
        if (!IsNmapAvailable())
        {
            throw new InvalidOperationException(
                "nmap is not installed. Install it with: brew install nmap");
        }

        // Build nmap arguments
        // -sT: TCP connect scan (no sudo required)
        // -p-: All ports OR -F: Fast (100 common ports)
        // -sV: Version detection
        var portRange = commonPortsOnly ? "-F" : "-p 1-1000";  // Limit to 1-1000 for reasonable time
        var args = $"-sT {portRange} -sV -oX - {ipAddress}";

        var xmlOutput = await ExecuteNmapAsync(args);
        return ParsePortScanXml(xmlOutput, ipAddress);
    }

    private async Task<string> ExecuteNmapAsync(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = NmapCommand,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"nmap failed with exit code {process.ExitCode}: {error}");
        }

        return output;
    }

    private List<NetworkDevice> ParseNetworkScanXml(string xmlOutput)
    {
        var devices = new List<NetworkDevice>();

        try
        {
            var doc = XDocument.Parse(xmlOutput);
            var hosts = doc.Descendants("host");

            foreach (var host in hosts)
            {
                var status = host.Element("status")?.Attribute("state")?.Value;
                if (status != "up")
                {
                    continue;
                }

                var device = new NetworkDevice
                {
                    Status = status ?? "up",
                    ScannedAt = DateTime.Now
                };

                // Get IP address
                var address = host.Element("address");
                if (address?.Attribute("addrtype")?.Value == "ipv4")
                {
                    device.IpAddress = address.Attribute("addr")?.Value ?? "";
                }

                // Get MAC address and vendor
                var macAddress = host.Elements("address")
                    .FirstOrDefault(a => a.Attribute("addrtype")?.Value == "mac");
                if (macAddress != null)
                {
                    device.MacAddress = macAddress.Attribute("addr")?.Value;
                    device.Vendor = macAddress.Attribute("vendor")?.Value;
                }

                // Get hostname
                var hostname = host.Element("hostnames")?.Element("hostname")?.Attribute("name")?.Value;
                device.Hostname = hostname;

                // Get open ports
                var ports = host.Element("ports")?.Elements("port")
                    .Where(p => p.Element("state")?.Attribute("state")?.Value == "open")
                    .Select(p => int.TryParse(p.Attribute("portid")?.Value, out var port) ? port : 0)
                    .Where(p => p > 0)
                    .ToList();

                device.OpenPorts = ports ?? new List<int>();

                // Get OS guess (if available)
                var osMatch = host.Element("os")?.Element("osmatch")?.Attribute("name")?.Value;
                device.OsGuess = osMatch;

                devices.Add(device);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse nmap XML output: {ex.Message}", ex);
        }

        return devices;
    }

    private List<PortScanResult> ParsePortScanXml(string xmlOutput, string ipAddress)
    {
        var results = new List<PortScanResult>();

        try
        {
            var doc = XDocument.Parse(xmlOutput);
            var host = doc.Descendants("host").FirstOrDefault();

            if (host == null)
            {
                return results;
            }

            var ports = host.Element("ports")?.Elements("port");
            if (ports == null)
            {
                return results;
            }

            foreach (var port in ports)
            {
                var state = port.Element("state")?.Attribute("state")?.Value;
                if (state == "closed" || state == "filtered")
                {
                    continue;  // Only show open ports
                }

                var portId = port.Attribute("portid")?.Value;
                var protocol = port.Attribute("protocol")?.Value ?? "tcp";
                var service = port.Element("service");

                var result = new PortScanResult
                {
                    IpAddress = ipAddress,
                    Port = int.TryParse(portId, out var p) ? p : 0,
                    Protocol = protocol,
                    State = state ?? "unknown",
                    Service = service?.Attribute("name")?.Value ?? "unknown",
                    Version = service?.Attribute("version")?.Value,
                    ExtraInfo = service?.Attribute("extrainfo")?.Value
                };

                results.Add(result);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse nmap port scan XML output: {ex.Message}", ex);
        }

        return results;
    }
}
