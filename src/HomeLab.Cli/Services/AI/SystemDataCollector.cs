using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Network;
using EventLogEntry = HomeLab.Cli.Models.EventLog.EventLogEntry;

namespace HomeLab.Cli.Services.AI;

/// <summary>
/// Collects system, Docker, and network metrics into a structured snapshot.
/// </summary>
public class SystemDataCollector : ISystemDataCollector
{
    private readonly IDockerService _dockerService;
    private readonly IServiceClientFactory _clientFactory;
    private readonly INmapService _nmapService;

    public SystemDataCollector(IDockerService dockerService, IServiceClientFactory clientFactory, INmapService nmapService)
    {
        _dockerService = dockerService;
        _clientFactory = clientFactory;
        _nmapService = nmapService;
    }

    public async Task<HomelabDataSnapshot> CollectAsync()
    {
        var snapshot = new HomelabDataSnapshot();

        // Collect all data sources in parallel
        var systemTask = CollectSystemMetricsAsync(snapshot.Errors);
        var dockerTask = CollectDockerMetricsAsync(snapshot.Errors);
        var networkTask = CollectNetworkMetricsAsync(snapshot.Errors);

        await Task.WhenAll(systemTask, dockerTask, networkTask);

        snapshot.System = await systemTask;
        snapshot.Docker = await dockerTask;
        snapshot.Network = await networkTask;

        return snapshot;
    }

    public string FormatAsPrompt(HomelabDataSnapshot snapshot, List<EventLogEntry>? eventHistory = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== HOMELAB STATUS ===");
        sb.AppendLine($"Collected: {snapshot.CollectedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // System metrics
        if (snapshot.System != null)
        {
            sb.AppendLine("--- SYSTEM ---");
            sb.AppendLine($"CPU: {snapshot.System.CpuCount} cores, {snapshot.System.CpuUsagePercent:F1}% usage");
            sb.AppendLine($"Memory: {snapshot.System.UsedMemoryGB:F1}/{snapshot.System.TotalMemoryGB:F1} GB ({snapshot.System.MemoryUsagePercent:F0}%)");
            sb.AppendLine($"Disk: {snapshot.System.DiskUsed}/{snapshot.System.DiskTotal} ({snapshot.System.DiskUsagePercent}%)");
            sb.AppendLine($"Available disk: {snapshot.System.DiskAvailable}");
            sb.AppendLine($"Uptime: {snapshot.System.Uptime}");
            sb.AppendLine();
        }

        // Docker metrics
        if (snapshot.Docker != null)
        {
            if (snapshot.Docker.Available)
            {
                sb.AppendLine($"--- DOCKER ({snapshot.Docker.TotalContainers} containers) ---");
                var running = snapshot.Docker.Containers.Where(c => c.IsRunning).ToList();
                var stopped = snapshot.Docker.Containers.Where(c => !c.IsRunning).ToList();

                if (running.Count > 0)
                {
                    sb.AppendLine($"Running ({running.Count}): {string.Join(", ", running.Select(c => c.Name))}");
                }

                if (stopped.Count > 0)
                {
                    sb.AppendLine($"Stopped ({stopped.Count}): {string.Join(", ", stopped.Select(c => c.Name))}");
                }
            }
            else
            {
                sb.AppendLine("--- DOCKER ---");
                sb.AppendLine("Not available");
            }

            sb.AppendLine();
        }

        // Network metrics
        if (snapshot.Network != null)
        {
            sb.AppendLine("--- NETWORK ---");

            if (snapshot.Network.NmapAvailable && snapshot.Network.Devices.Count > 0)
            {
                sb.AppendLine($"Devices on network: {snapshot.Network.DevicesFound}");
                foreach (var device in snapshot.Network.Devices)
                {
                    var info = device.Ip;
                    if (!string.IsNullOrEmpty(device.Hostname))
                    {
                        info += $" ({device.Hostname})";
                    }

                    if (!string.IsNullOrEmpty(device.Vendor))
                    {
                        info += $" - {device.Vendor}";
                    }

                    if (device.OpenPorts.Count > 0)
                    {
                        info += $" [ports: {string.Join(",", device.OpenPorts)}]";
                    }

                    sb.AppendLine($"  - {info}");
                }
            }
            else if (!snapshot.Network.NmapAvailable)
            {
                sb.AppendLine("Device scanning: nmap not available");
            }

            if (snapshot.Network.NtopngAvailable)
            {
                sb.AppendLine($"Traffic: {FormatBytes(snapshot.Network.TotalBytesTransferred)} total, {snapshot.Network.ActiveFlows} active flows");
                if (snapshot.Network.TopTalkers.Count > 0)
                {
                    sb.AppendLine($"Top talkers: {string.Join(", ", snapshot.Network.TopTalkers)}");
                }
            }

            if (snapshot.Network.SuricataAvailable)
            {
                sb.AppendLine($"Security alerts: {snapshot.Network.SecurityAlerts} total ({snapshot.Network.CriticalAlerts} critical, {snapshot.Network.HighAlerts} high)");
                foreach (var alert in snapshot.Network.AlertSummaries.Take(10))
                {
                    sb.AppendLine($"  - {alert}");
                }
            }
            else
            {
                sb.AppendLine("Intrusion detection: Suricata not available");
            }

            sb.AppendLine();
        }

        // Collection notes
        if (snapshot.Errors.Count > 0)
        {
            sb.AppendLine("--- NOTES ---");
            foreach (var error in snapshot.Errors)
            {
                sb.AppendLine($"- {error}");
            }
        }

        // Event history for incident investigation
        if (eventHistory != null && eventHistory.Count > 0)
        {
            sb.AppendLine();
            AppendEventHistory(sb, eventHistory);
        }

        return sb.ToString();
    }

    private static void AppendEventHistory(StringBuilder sb, List<EventLogEntry> events)
    {
        var first = events.First();
        var last = events.Last();

        sb.AppendLine("=== EVENT HISTORY ===");
        sb.AppendLine($"Total snapshots: {events.Count}");
        sb.AppendLine($"Period: {first.Timestamp:yyyy-MM-dd HH:mm} to {last.Timestamp:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        // Detect gaps (> 10 min between entries)
        var gaps = new List<(DateTime start, DateTime end)>();
        for (var i = 1; i < events.Count; i++)
        {
            var gap = events[i].Timestamp - events[i - 1].Timestamp;
            if (gap.TotalMinutes > 10)
            {
                gaps.Add((events[i - 1].Timestamp, events[i].Timestamp));
            }
        }

        if (gaps.Count > 0)
        {
            sb.AppendLine($"Gaps detected ({gaps.Count}):");
            foreach (var (start, end) in gaps.Take(10))
            {
                var duration = end - start;
                sb.AppendLine($"  - {start:HH:mm} to {end:HH:mm} ({(int)duration.TotalMinutes} min) — possible sleep/outage");
            }
            sb.AppendLine();
        }

        // Power events
        var powerEvents = events
            .Where(e => e.Power?.RecentEvents.Count > 0)
            .SelectMany(e => e.Power!.RecentEvents)
            .OrderBy(e => e.Timestamp)
            .ToList();

        if (powerEvents.Count > 0)
        {
            sb.AppendLine($"Power events ({powerEvents.Count}):");
            foreach (var pe in powerEvents.Take(20))
            {
                sb.AppendLine($"  - {pe.Timestamp:HH:mm} {pe.Type}");
            }
            sb.AppendLine();
        }

        // Tailscale connectivity summary
        var tsConnected = events.Count(e => e.Tailscale?.IsConnected == true);
        var tsTotal = events.Count(e => e.Tailscale != null);
        if (tsTotal > 0)
        {
            var pct = (int)(100.0 * tsConnected / tsTotal);
            var drops = 0;
            for (var i = 1; i < events.Count; i++)
            {
                if (events[i - 1].Tailscale?.IsConnected == true && events[i].Tailscale?.IsConnected == false)
                {
                    drops++;
                }
            }
            sb.AppendLine($"Tailscale: connected {pct}% of time, {drops} disconnection(s)");
        }

        // Docker changes
        var dockerChanges = new List<string>();
        for (var i = 1; i < events.Count; i++)
        {
            var prev = events[i - 1].Docker;
            var curr = events[i].Docker;
            if (prev?.Available != true || curr?.Available != true)
            {
                continue;
            }

            var prevNames = prev.Containers.Where(c => c.IsRunning).Select(c => c.Name).ToHashSet();
            var currNames = curr.Containers.Where(c => c.IsRunning).Select(c => c.Name).ToHashSet();

            var stopped = prevNames.Except(currNames).ToList();
            var started = currNames.Except(prevNames).ToList();

            foreach (var name in stopped)
            {
                dockerChanges.Add($"{events[i].Timestamp:HH:mm} {name} stopped");
            }

            foreach (var name in started)
            {
                dockerChanges.Add($"{events[i].Timestamp:HH:mm} {name} started");
            }
        }

        if (dockerChanges.Count > 0)
        {
            sb.AppendLine($"Docker changes ({dockerChanges.Count}):");
            foreach (var change in dockerChanges.Take(20))
            {
                sb.AppendLine($"  - {change}");
            }
        }

        // Service health changes
        var healthChanges = new List<string>();
        for (var i = 1; i < events.Count; i++)
        {
            var prevServices = events[i - 1].Services.ToDictionary(s => s.Name, s => s.IsHealthy);
            foreach (var svc in events[i].Services)
            {
                if (prevServices.TryGetValue(svc.Name, out var wasHealthy) && wasHealthy != svc.IsHealthy)
                {
                    var status = svc.IsHealthy ? "recovered" : "down";
                    healthChanges.Add($"{events[i].Timestamp:HH:mm} {svc.Name} {status}");
                }
            }
        }

        if (healthChanges.Count > 0)
        {
            sb.AppendLine($"Service health changes ({healthChanges.Count}):");
            foreach (var change in healthChanges.Take(20))
            {
                sb.AppendLine($"  - {change}");
            }
        }

        // Network device changes
        var networkChanges = new List<string>();
        for (var i = 1; i < events.Count; i++)
        {
            var prevDevices = events[i - 1].Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();
            var currDevices = events[i].Network?.Devices ?? new List<Models.EventLog.DeviceBrief>();

            foreach (var device in currDevices)
            {
                if (!prevDevices.Contains(device.Ip))
                {
                    var info = device.Hostname ?? device.Vendor ?? "unknown";
                    networkChanges.Add($"{events[i].Timestamp:HH:mm} new device: {device.Ip} ({info})");
                }
            }

            var currIps = currDevices.Select(d => d.Ip).ToHashSet();
            foreach (var ip in prevDevices.Except(currIps))
            {
                networkChanges.Add($"{events[i].Timestamp:HH:mm} device left: {ip}");
            }
        }

        if (networkChanges.Count > 0)
        {
            sb.AppendLine($"Network device changes ({networkChanges.Count}):");
            foreach (var change in networkChanges.Take(20))
            {
                sb.AppendLine($"  - {change}");
            }

            sb.AppendLine();
        }

        // Security events
        var securityEvents = new List<string>();
        foreach (var evt in events)
        {
            var sec = evt.Network?.Security;
            if (sec == null || (sec.CriticalCount == 0 && sec.HighCount == 0))
            {
                continue;
            }

            securityEvents.Add($"{evt.Timestamp:HH:mm} {sec.CriticalCount}c/{sec.HighCount}h alerts");
            foreach (var alert in sec.RecentAlerts.Take(3))
            {
                securityEvents.Add($"  [{alert.Severity}] {alert.Signature}: {alert.SourceIp} -> {alert.DestinationIp}");
            }
        }

        if (securityEvents.Count > 0)
        {
            sb.AppendLine($"Security events:");
            foreach (var evt in securityEvents.Take(20))
            {
                sb.AppendLine($"  - {evt}");
            }

            sb.AppendLine();
        }

        // Traffic trend
        var trafficEntries = events
            .Where(e => e.Network?.Traffic != null)
            .Select(e => e.Network!.Traffic!.TotalBytes)
            .ToList();
        if (trafficEntries.Count > 0)
        {
            sb.AppendLine($"Traffic trend: min={FormatBytes(trafficEntries.Min())}, max={FormatBytes(trafficEntries.Max())}, avg={FormatBytes((long)trafficEntries.Average())}");
        }
    }

    private async Task<SystemMetrics> CollectSystemMetricsAsync(List<string> errors)
    {
        var metrics = new SystemMetrics();

        try
        {
            // CPU count
            var cpuCountOutput = await RunCommandAsync("sysctl", "-n hw.ncpu");
            if (int.TryParse(cpuCountOutput.Trim(), out var cpuCount))
            {
                metrics.CpuCount = cpuCount;
            }

            // CPU usage from top
            var topOutput = await RunCommandAsync("top", "-l 1 -s 0 -n 0");
            var cpuMatch = Regex.Match(topOutput, @"CPU usage:\s+([\d.]+)%\s+user,\s+([\d.]+)%\s+sys,\s+([\d.]+)%\s+idle");
            if (cpuMatch.Success)
            {
                var idle = double.Parse(cpuMatch.Groups[3].Value, CultureInfo.InvariantCulture);
                metrics.CpuUsagePercent = 100.0 - idle;
            }

            // Total memory
            var memOutput = await RunCommandAsync("sysctl", "-n hw.memsize");
            if (long.TryParse(memOutput.Trim(), out var memBytes))
            {
                metrics.TotalMemoryGB = memBytes / (1024.0 * 1024.0 * 1024.0);
            }

            // Memory usage from vm_stat
            var vmStatOutput = await RunCommandAsync("vm_stat", "");
            var pageSize = 16384L; // Apple Silicon default page size
            var pageSizeMatch = Regex.Match(vmStatOutput, @"page size of (\d+) bytes");
            if (pageSizeMatch.Success)
            {
                pageSize = long.Parse(pageSizeMatch.Groups[1].Value);
            }

            long activePages = 0, wiredPages = 0, compressedPages = 0;
            var activeMatch = Regex.Match(vmStatOutput, @"Pages active:\s+(\d+)");
            var wiredMatch = Regex.Match(vmStatOutput, @"Pages wired down:\s+(\d+)");
            var compressedMatch = Regex.Match(vmStatOutput, @"Pages occupied by compressor:\s+(\d+)");

            if (activeMatch.Success)
            {
                activePages = long.Parse(activeMatch.Groups[1].Value);
            }

            if (wiredMatch.Success)
            {
                wiredPages = long.Parse(wiredMatch.Groups[1].Value);
            }

            if (compressedMatch.Success)
            {
                compressedPages = long.Parse(compressedMatch.Groups[1].Value);
            }

            var usedBytes = (activePages + wiredPages + compressedPages) * pageSize;
            metrics.UsedMemoryGB = usedBytes / (1024.0 * 1024.0 * 1024.0);
            if (metrics.TotalMemoryGB > 0)
            {
                metrics.MemoryUsagePercent = (metrics.UsedMemoryGB / metrics.TotalMemoryGB) * 100.0;
            }

            // Disk usage
            var dfOutput = await RunCommandAsync("df", "-h /");
            var dfLines = dfOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (dfLines.Length >= 2)
            {
                var parts = Regex.Split(dfLines[1].Trim(), @"\s+");
                if (parts.Length >= 5)
                {
                    metrics.DiskTotal = parts[1];
                    metrics.DiskUsed = parts[2];
                    metrics.DiskAvailable = parts[3];
                    if (int.TryParse(parts[4].TrimEnd('%'), out var diskPercent))
                    {
                        metrics.DiskUsagePercent = diskPercent;
                    }
                }
            }

            // Uptime
            var uptimeOutput = await RunCommandAsync("uptime", "");
            var uptimeMatch = Regex.Match(uptimeOutput, @"up\s+(.+?),\s+\d+\s+user");
            if (uptimeMatch.Success)
            {
                metrics.Uptime = uptimeMatch.Groups[1].Value.Trim();
            }
            else
            {
                metrics.Uptime = uptimeOutput.Trim();
            }
        }
        catch (Exception ex)
        {
            errors.Add($"System metrics partially failed: {ex.Message}");
        }

        return metrics;
    }

    private async Task<DockerMetrics> CollectDockerMetricsAsync(List<string> errors)
    {
        var metrics = new DockerMetrics();

        try
        {
            if (!await _dockerService.IsDockerAvailableAsync())
            {
                metrics.Available = false;
                errors.Add("Docker is not available");
                return metrics;
            }

            metrics.Available = true;
            var containers = await _dockerService.ListContainersAsync(onlyHomelab: false);

            metrics.TotalContainers = containers.Count;
            metrics.RunningContainers = containers.Count(c => c.IsRunning);
            metrics.StoppedContainers = containers.Count(c => !c.IsRunning);

            metrics.Containers = containers.Select(c => new ContainerSnapshot
            {
                Name = c.Name,
                IsRunning = c.IsRunning,
                Status = c.IsRunning ? $"Up {c.Uptime}" : "Stopped"
            }).ToList();
        }
        catch (Exception ex)
        {
            metrics.Available = false;
            errors.Add($"Docker data collection failed: {ex.Message}");
        }

        return metrics;
    }

    private async Task<NetworkMetrics> CollectNetworkMetricsAsync(List<string> errors)
    {
        var metrics = new NetworkMetrics();

        // Nmap quick scan (ping only — fast, ~5 seconds)
        try
        {
            if (_nmapService.IsNmapAvailable())
            {
                metrics.NmapAvailable = true;
                var devices = await _nmapService.ScanNetworkAsync("192.168.1.0/24", quickScan: true);
                metrics.DevicesFound = devices.Count;
                metrics.Devices = devices.Select(d => new DiscoveredDevice
                {
                    Ip = d.IpAddress,
                    Mac = d.MacAddress ?? string.Empty,
                    Hostname = d.Hostname ?? string.Empty,
                    Vendor = d.Vendor ?? string.Empty,
                    OpenPorts = d.OpenPorts
                }).ToList();
            }
            else
            {
                metrics.NmapAvailable = false;
                errors.Add("nmap not installed — install with: brew install nmap");
            }
        }
        catch (Exception ex)
        {
            metrics.NmapAvailable = false;
            errors.Add($"Network scan failed: {ex.Message}");
        }

        // Ntopng traffic data (when container is running)
        try
        {
            var ntopng = _clientFactory.CreateNtopngClient();
            if (await ntopng.IsHealthyAsync())
            {
                metrics.NtopngAvailable = true;
                var stats = await ntopng.GetTrafficStatsAsync();
                metrics.TotalBytesTransferred = stats.TotalBytesTransferred;
                metrics.ActiveFlows = stats.ActiveFlows;
                metrics.TopTalkers = stats.TopTalkers
                    .Take(5)
                    .Select(t => $"{t.DeviceName ?? t.IpAddress} ({FormatBytes(t.TotalBytes)})")
                    .ToList();
            }
        }
        catch
        {
            metrics.NtopngAvailable = false;
        }

        // Suricata IDS alerts (when container is running)
        try
        {
            var suricata = _clientFactory.CreateSuricataClient();
            if (await suricata.IsHealthyAsync())
            {
                metrics.SuricataAvailable = true;
                var alerts = await suricata.GetAlertsAsync(limit: 20);
                metrics.SecurityAlerts = alerts.Count;
                metrics.CriticalAlerts = alerts.Count(a => a.Severity == "critical");
                metrics.HighAlerts = alerts.Count(a => a.Severity == "high");
                metrics.AlertSummaries = alerts
                    .Take(10)
                    .Select(a => $"[{a.Severity.ToUpperInvariant()}] {a.Signature} ({a.SourceIp} → {a.DestinationIp})")
                    .ToList();
            }
        }
        catch
        {
            metrics.SuricataAvailable = false;
        }

        return metrics;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.#} {sizes[order]}";
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
