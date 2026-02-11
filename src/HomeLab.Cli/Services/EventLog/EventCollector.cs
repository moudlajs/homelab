using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Network;
using EventLogEntry = HomeLab.Cli.Models.EventLog.EventLogEntry;

namespace HomeLab.Cli.Services.EventLog;

/// <summary>
/// Collects lightweight event snapshots from all system sources.
/// Runs everything in parallel, targeting < 10 seconds total.
/// </summary>
public class EventCollector : IEventCollector
{
    private readonly IDockerService _dockerService;
    private readonly IServiceClientFactory _clientFactory;
    private readonly INmapService _nmapService;
    private readonly IServiceHealthCheckService _healthCheckService;

    public EventCollector(
        IDockerService dockerService,
        IServiceClientFactory clientFactory,
        INmapService nmapService,
        IServiceHealthCheckService healthCheckService)
    {
        _dockerService = dockerService;
        _clientFactory = clientFactory;
        _nmapService = nmapService;
        _healthCheckService = healthCheckService;
    }

    public async Task<EventLogEntry> CollectEventAsync()
    {
        var entry = new EventLogEntry();

        var systemTask = CollectSystemAsync(entry.Errors);
        var powerTask = CollectPowerEventsAsync(entry.Errors);
        var tailscaleTask = CollectTailscaleAsync(entry.Errors);
        var dockerTask = CollectDockerAsync(entry.Errors);
        var networkTask = CollectNetworkAsync(entry.Errors);
        var healthTask = CollectServiceHealthAsync(entry.Errors);

        await Task.WhenAll(systemTask, powerTask, tailscaleTask, dockerTask, networkTask, healthTask);

        entry.System = await systemTask;
        entry.Power = await powerTask;
        entry.Tailscale = await tailscaleTask;
        entry.Docker = await dockerTask;
        entry.Network = await networkTask;
        entry.Services = await healthTask;

        return entry;
    }

    private async Task<SystemSnapshot> CollectSystemAsync(List<string> errors)
    {
        var snapshot = new SystemSnapshot();

        try
        {
            // CPU usage from top
            var topOutput = await RunCommandAsync("top", "-l 1 -s 0 -n 0");
            var cpuMatch = Regex.Match(topOutput, @"CPU usage:\s+([\d.]+)%\s+user,\s+([\d.]+)%\s+sys,\s+([\d.]+)%\s+idle");
            if (cpuMatch.Success)
            {
                var idle = double.Parse(cpuMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                snapshot.CpuPercent = Math.Round(100.0 - idle, 1);
            }

            // Memory from vm_stat
            var memTotal = await RunCommandAsync("sysctl", "-n hw.memsize");
            var vmStat = await RunCommandAsync("vm_stat", "");
            if (long.TryParse(memTotal.Trim(), out var memBytes) && memBytes > 0)
            {
                var pageSize = 16384L;
                var pageSizeMatch = Regex.Match(vmStat, @"page size of (\d+) bytes");
                if (pageSizeMatch.Success)
                {
                    pageSize = long.Parse(pageSizeMatch.Groups[1].Value);
                }

                long active = 0, wired = 0, compressed = 0;
                var m = Regex.Match(vmStat, @"Pages active:\s+(\d+)");
                if (m.Success)
                {
                    active = long.Parse(m.Groups[1].Value);
                }

                m = Regex.Match(vmStat, @"Pages wired down:\s+(\d+)");
                if (m.Success)
                {
                    wired = long.Parse(m.Groups[1].Value);
                }

                m = Regex.Match(vmStat, @"Pages occupied by compressor:\s+(\d+)");
                if (m.Success)
                {
                    compressed = long.Parse(m.Groups[1].Value);
                }

                var usedBytes = (active + wired + compressed) * pageSize;
                snapshot.MemoryPercent = Math.Round((double)usedBytes / memBytes * 100.0, 0);
            }

            // Disk
            var dfOutput = await RunCommandAsync("df", "-h /");
            var dfLines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (dfLines.Length >= 2)
            {
                var parts = Regex.Split(dfLines[1].Trim(), @"\s+");
                if (parts.Length >= 5 && int.TryParse(parts[4].TrimEnd('%'), out var diskPct))
                {
                    snapshot.DiskPercent = diskPct;
                }
            }

            // Uptime
            var uptimeOutput = await RunCommandAsync("uptime", "");
            var uptimeMatch = Regex.Match(uptimeOutput, @"up\s+(.+?),\s+\d+\s+user");
            snapshot.Uptime = uptimeMatch.Success ? uptimeMatch.Groups[1].Value.Trim() : "unknown";
        }
        catch (Exception ex)
        {
            errors.Add($"System: {ex.Message}");
        }

        return snapshot;
    }

    private async Task<PowerSnapshot> CollectPowerEventsAsync(List<string> errors)
    {
        var snapshot = new PowerSnapshot();

        try
        {
            // Get power events from the last 10 minutes (between collection intervals)
            var output = await RunCommandAsync("pmset", "-g log");

            // Parse sleep/wake events
            var lines = output.Split('\n');
            var cutoff = DateTime.UtcNow.AddMinutes(-10);

            foreach (var line in lines)
            {
                // Match patterns like: "2026-02-10 14:30:00 +0100 Sleep" or "Wake" or "DarkWake"
                var match = Regex.Match(line, @"(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s+[+-]\d{4}\s+.*(Sleep|Wake|DarkWake)\s");
                if (!match.Success)
                {
                    continue;
                }

                if (DateTime.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var eventTime))
                {
                    if (eventTime.ToUniversalTime() >= cutoff)
                    {
                        snapshot.RecentEvents.Add(new PowerEvent
                        {
                            Timestamp = eventTime.ToUniversalTime(),
                            Type = match.Groups[2].Value
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Power: {ex.Message}");
        }

        return snapshot;
    }

    private async Task<TailscaleSnapshot> CollectTailscaleAsync(List<string> errors)
    {
        var snapshot = new TailscaleSnapshot();

        try
        {
            var client = _clientFactory.CreateTailscaleClient();
            if (!await client.IsTailscaleInstalledAsync())
            {
                errors.Add("Tailscale not installed");
                return snapshot;
            }

            var status = await client.GetStatusAsync();
            snapshot.IsConnected = status.IsConnected;
            snapshot.BackendState = status.BackendState;
            snapshot.SelfIp = status.Self?.PrimaryIP;
            snapshot.PeerCount = status.Peers.Count;
            snapshot.OnlinePeerCount = status.Peers.Count(p => p.Online);
        }
        catch (Exception ex)
        {
            errors.Add($"Tailscale: {ex.Message}");
        }

        return snapshot;
    }

    private async Task<DockerSnapshot> CollectDockerAsync(List<string> errors)
    {
        var snapshot = new DockerSnapshot();

        try
        {
            if (!await _dockerService.IsDockerAvailableAsync())
            {
                errors.Add("Docker not available");
                return snapshot;
            }

            snapshot.Available = true;
            var containers = await _dockerService.ListContainersAsync(onlyHomelab: false);
            snapshot.TotalCount = containers.Count;
            snapshot.RunningCount = containers.Count(c => c.IsRunning);
            snapshot.Containers = containers.Select(c => new ContainerBrief
            {
                Name = c.Name,
                IsRunning = c.IsRunning
            }).ToList();
        }
        catch (Exception ex)
        {
            errors.Add($"Docker: {ex.Message}");
        }

        return snapshot;
    }

    private async Task<NetworkSnapshot> CollectNetworkAsync(List<string> errors)
    {
        var snapshot = new NetworkSnapshot();

        try
        {
            if (_nmapService.IsNmapAvailable())
            {
                var devices = await _nmapService.ScanNetworkAsync("192.168.1.0/24", quickScan: true);
                snapshot.DeviceCount = devices.Count;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Network: {ex.Message}");
        }

        return snapshot;
    }

    private async Task<List<ServiceHealthEntry>> CollectServiceHealthAsync(List<string> errors)
    {
        var entries = new List<ServiceHealthEntry>();

        try
        {
            var results = await _healthCheckService.CheckAllServicesAsync();
            entries = results.Select(r => new ServiceHealthEntry
            {
                Name = r.ServiceName,
                IsHealthy = r.IsHealthy
            }).ToList();
        }
        catch (Exception ex)
        {
            errors.Add($"Health: {ex.Message}");
        }

        return entries;
    }

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(10);

    private static async Task<string> RunCommandAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();

            using var cts = new CancellationTokenSource(CommandTimeout);
            var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
