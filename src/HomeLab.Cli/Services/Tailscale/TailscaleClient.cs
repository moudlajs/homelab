using System.Diagnostics;
using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Tailscale;

/// <summary>
/// Tailscale client that shells out to the tailscale CLI binary.
/// </summary>
public class TailscaleClient : ITailscaleClient
{
    private const string TailscaleCommand = "tailscale";

    public string ServiceName => "Tailscale";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var status = await GetStatusAsync();
            return status.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        var isInstalled = await IsTailscaleInstalledAsync();

        if (!isInstalled)
        {
            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = false,
                Status = "Not Installed",
                Message = "Tailscale CLI is not installed. Install with: brew install tailscale"
            };
        }

        try
        {
            var status = await GetStatusAsync();
            var version = await GetVersionAsync();
            var tailscaleIp = status.Self?.PrimaryIP;

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = status.IsConnected,
                Status = status.BackendState,
                Message = status.IsConnected
                    ? $"Connected to {status.TailnetName}"
                    : "Not connected to tailnet",
                Metrics = new Dictionary<string, string>
                {
                    { "Backend State", status.BackendState },
                    { "Tailnet", status.TailnetName ?? "N/A" },
                    { "Tailscale IP", tailscaleIp ?? "N/A" },
                    { "Online Peers", status.Peers.Count(p => p.Online).ToString() },
                    { "Total Peers", status.Peers.Count.ToString() },
                    { "Version", version ?? "Unknown" }
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
                Message = ex.Message
            };
        }
    }

    public async Task<bool> IsTailscaleInstalledAsync()
    {
        try
        {
            await RunCommandAsync("version");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetVersionAsync()
    {
        try
        {
            var output = await RunCommandAsync("version");
            return output?.Split('\n').FirstOrDefault()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task<TailscaleStatus> GetStatusAsync()
    {
        var output = await RunCommandAsync("status --json");

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Failed to get Tailscale status");
        }

        return ParseStatus(output);
    }

    public async Task ConnectAsync()
    {
        await RunCommandAsync("up");
    }

    public async Task DisconnectAsync()
    {
        await RunCommandAsync("down");
    }

    public async Task<string?> GetTailscaleIPAsync()
    {
        try
        {
            var output = await RunCommandAsync("ip");
            return output?.Split('\n').FirstOrDefault()?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> RunCommandAsync(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = TailscaleCommand,
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
                $"tailscale {arguments} failed (exit {process.ExitCode}): {error.Trim()}");
        }

        return output;
    }

    private TailscaleStatus ParseStatus(string jsonOutput)
    {
        using var doc = JsonDocument.Parse(jsonOutput);
        var root = doc.RootElement;

        var status = new TailscaleStatus
        {
            BackendState = root.TryGetProperty("BackendState", out var bs)
                ? bs.GetString() ?? "Unknown"
                : "Unknown",
            MagicDNSSuffix = root.TryGetProperty("MagicDNSSuffix", out var suffix)
                ? suffix.GetString()
                : null
        };

        // Use MagicDNSSuffix as tailnet name (e.g. "tailnet-name.ts.net")
        status.TailnetName = status.MagicDNSSuffix;

        // Parse self node
        if (root.TryGetProperty("Self", out var selfElement))
        {
            status.Self = ParseDevice(selfElement);
        }

        // Parse peers — it's a map of nodeID -> device
        if (root.TryGetProperty("Peer", out var peersElement) &&
            peersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var peerProperty in peersElement.EnumerateObject())
            {
                var peer = ParseDevice(peerProperty.Value);
                peer.Id = peerProperty.Name;
                status.Peers.Add(peer);
            }
        }

        return status;
    }

    private TailscaleDevice ParseDevice(JsonElement element)
    {
        var device = new TailscaleDevice
        {
            HostName = element.TryGetProperty("HostName", out var hn)
                ? hn.GetString() ?? ""
                : "",
            DNSName = element.TryGetProperty("DNSName", out var dns)
                ? dns.GetString() ?? ""
                : "",
            OS = element.TryGetProperty("OS", out var os)
                ? os.GetString() ?? ""
                : "",
            Online = element.TryGetProperty("Online", out var online)
                && online.GetBoolean(),
            ExitNode = element.TryGetProperty("ExitNode", out var exitNode)
                && exitNode.GetBoolean(),
            ExitNodeOption = element.TryGetProperty("ExitNodeOption", out var exitNodeOpt)
                && exitNodeOpt.GetBoolean()
        };

        if (element.TryGetProperty("TailscaleIPs", out var ipsElement) &&
            ipsElement.ValueKind == JsonValueKind.Array)
        {
            device.TailscaleIPs = ipsElement.EnumerateArray()
                .Select(ip => ip.GetString() ?? "")
                .Where(ip => !string.IsNullOrEmpty(ip))
                .ToList();
        }

        if (element.TryGetProperty("LastSeen", out var lastSeen) &&
            lastSeen.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(lastSeen.GetString(), out var dt))
            {
                device.LastSeen = dt;
            }
        }

        return device;
    }
}
