using System.ComponentModel;
using HomeLab.Cli.Models;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Network;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Live terminal dashboard for your homelab.
/// Shows system metrics, services, network, VPN, containers, and anomalies.
/// </summary>
public class TuiCommand : AsyncCommand<TuiCommand.Settings>
{
    private readonly IDockerService _dockerService;
    private readonly IServiceHealthCheckService _healthCheckService;
    private readonly IEventLogService _eventLogService;
    private readonly INetworkAnomalyDetector _anomalyDetector;
    private bool _shouldExit;

    // Anomaly cache — refresh every 60s, not every 2s cycle
    private List<NetworkAnomaly>? _cachedAnomalies;
    private DateTime _lastAnomalyCheck = DateTime.MinValue;

    public class Settings : CommandSettings
    {
        [CommandOption("--refresh <SECONDS>")]
        [Description("Refresh interval in seconds (default: 2)")]
        [DefaultValue(2)]
        public int RefreshInterval { get; set; } = 2;
    }

    public TuiCommand(
        IDockerService dockerService,
        IServiceHealthCheckService healthCheckService,
        IEventLogService eventLogService,
        INetworkAnomalyDetector anomalyDetector)
    {
        _dockerService = dockerService;
        _healthCheckService = healthCheckService;
        _eventLogService = eventLogService;
        _anomalyDetector = anomalyDetector;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _shouldExit = true;
        };

        await AnsiConsole.Live(CreateEmptyLayout())
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                while (!_shouldExit)
                {
                    try
                    {
                        var layout = await CreateDashboard(settings.RefreshInterval);
                        ctx.UpdateTarget(layout);
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                }
            });

        AnsiConsole.MarkupLine("\n[yellow]Dashboard stopped.[/]");
        return 0;
    }

    private static Layout CreateEmptyLayout()
    {
        return new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Body"),
                new Layout("Bottom").Size(5),
                new Layout("Footer").Size(3));
    }

    private async Task<Layout> CreateDashboard(int refreshInterval)
    {
        // Fetch all data in parallel
        var healthTask = _healthCheckService.CheckAllServicesAsync();
        var containersTask = SafeGetContainers();
        var latestEventTask = GetLatestEventAsync();
        var anomaliesTask = GetCachedAnomaliesAsync();

        await Task.WhenAll(healthTask, containersTask, latestEventTask, anomaliesTask);

        var healthResults = await healthTask;
        var containers = await containersTask;
        var latest = await latestEventTask;
        var anomalies = await anomaliesTask;

        // Build all panels
        var header = BuildHeaderPanel(latest);
        var servicesTable = BuildServicesTable(healthResults, containers);
        var systemPanel = BuildSystemGaugesPanel(latest);
        var networkPanel = BuildNetworkVpnPanel(latest);
        var containerPanel = BuildContainersPanel(containers);
        var summaryPanel = BuildSummaryPanel(healthResults, latest);
        var anomalyPanel = BuildAnomaliesPanel(anomalies);
        var footer = BuildFooterPanel(latest, refreshInterval);

        return new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3).Update(header),
                new Layout("Body").SplitColumns(
                    new Layout("Left").Update(servicesTable),
                    new Layout("Right").Size(45).SplitRows(
                        new Layout("SystemGauges").Size(7).Update(systemPanel),
                        new Layout("NetworkVpn").Size(8).Update(networkPanel),
                        new Layout("Containers").Update(containerPanel)
                    )
                ),
                new Layout("Bottom").Size(5).SplitColumns(
                    new Layout("Summary").Update(summaryPanel),
                    new Layout("Anomalies").Update(anomalyPanel)
                ),
                new Layout("Footer").Size(3).Update(footer)
            );
    }

    private async Task<EventLogEntry?> GetLatestEventAsync()
    {
        try
        {
            var recent = await _eventLogService.ReadEventsAsync(
                since: DateTime.UtcNow.AddMinutes(-60));
            return recent.LastOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<NetworkAnomaly>> GetCachedAnomaliesAsync()
    {
        if (_cachedAnomalies != null &&
            DateTime.UtcNow - _lastAnomalyCheck < TimeSpan.FromSeconds(60))
        {
            return _cachedAnomalies;
        }

        try
        {
            var events = await _eventLogService.ReadEventsAsync(
                since: DateTime.UtcNow.AddHours(-1));
            _cachedAnomalies = _anomalyDetector.DetectAnomalies(events);
            _lastAnomalyCheck = DateTime.UtcNow;
            return _cachedAnomalies;
        }
        catch
        {
            return _cachedAnomalies ?? new List<NetworkAnomaly>();
        }
    }

    private async Task<List<ContainerInfo>> SafeGetContainers()
    {
        try
        {
            return await _dockerService.ListContainersAsync(onlyHomelab: false);
        }
        catch
        {
            return new List<ContainerInfo>();
        }
    }

    // --- Panel Builders ---

    private static Panel BuildHeaderPanel(EventLogEntry? latest)
    {
        var uptime = latest?.System?.Uptime;
        var uptimeText = !string.IsNullOrEmpty(uptime) ? $" [dim]|[/] up {uptime}" : "";

        return new Panel(
            Align.Center(
                new Markup($"[bold green]HomeLab Dashboard[/] [dim]|[/] {DateTime.Now:HH:mm:ss}{uptimeText} [dim]|[/] [yellow]Ctrl+C[/] to exit"),
                VerticalAlignment.Middle))
            .BorderColor(Color.Green)
            .Padding(0, 0);
    }

    private static Table BuildServicesTable(
        List<ServiceHealthResult> healthResults,
        List<ContainerInfo> containers)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[yellow]Service[/]"))
            .AddColumn(new TableColumn("[yellow]Docker[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Health[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Uptime[/]").Centered());

        // Build container lookup for uptime
        var containerByName = containers
            .ToDictionary(c => c.Name.TrimStart('/').ToLowerInvariant(), c => c);

        foreach (var result in healthResults)
        {
            var dockerStatus = result.IsRunning
                ? "[green]Running[/]"
                : "[red]Stopped[/]";

            var healthStatus = result.IsHealthy
                ? "[green]Healthy[/]"
                : "[red]Down[/]";

            // Try to find container uptime by matching names
            var uptime = "-";
            var key = result.ServiceName.ToLowerInvariant();
            if (containerByName.TryGetValue(key, out var container) ||
                containerByName.TryGetValue($"homelab_{key}", out container))
            {
                if (container.IsRunning && container.Uptime != "N/A")
                {
                    uptime = container.Uptime;
                }
            }

            table.AddRow(
                Markup.Escape(result.ServiceName),
                dockerStatus,
                healthStatus,
                uptime);
        }

        return table;
    }

    private static Panel BuildSystemGaugesPanel(EventLogEntry? latest)
    {
        if (latest?.System == null)
        {
            return new Panel(new Markup("[dim]No event data — run monitor collect[/]"))
                .Header("[cyan]System[/]")
                .BorderColor(Color.Cyan)
                .Padding(1, 0);
        }

        var sys = latest.System;
        var lines = new List<string>
        {
            RenderBar("CPU", sys.CpuPercent),
            RenderBar("Mem", sys.MemoryPercent),
            RenderBar("Disk", sys.DiskPercent)
        };

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[cyan]System[/]")
            .BorderColor(Color.Cyan)
            .Padding(1, 0);
    }

    private static Panel BuildNetworkVpnPanel(EventLogEntry? latest)
    {
        var lines = new List<string>();

        // VPN section
        var ts = latest?.Tailscale;
        if (ts != null)
        {
            var vpnColor = ts.IsConnected ? "green" : "red";
            var vpnState = ts.IsConnected ? "Connected" : ts.BackendState;
            lines.Add($"[{vpnColor}]VPN[/]   {vpnState}  {ts.OnlinePeerCount}/{ts.PeerCount} peers");
            if (ts.SelfIp != null)
            {
                lines.Add($"[dim]       {ts.SelfIp}[/]");
            }
        }
        else
        {
            lines.Add("[dim]VPN   n/a[/]");
        }

        // Network section
        var net = latest?.Network;
        if (net != null)
        {
            var deviceText = $"Devices: {net.DeviceCount}";
            var trafficText = net.Traffic != null
                ? $"Traffic: {NetworkAnomalyDetector.FormatBytes(net.Traffic.TotalBytes)}"
                : "Traffic: n/a";

            lines.Add($"[blue]Net[/]   {deviceText}  |  {trafficText}");

            var sec = net.Security;
            if (sec != null && sec.TotalAlerts > 0)
            {
                var alertColor = sec.CriticalCount > 0 ? "red" : "yellow";
                lines.Add($"[{alertColor}]       Alerts: {sec.TotalAlerts} ({sec.CriticalCount}c/{sec.HighCount}h)[/]");
            }
            else
            {
                lines.Add("[dim]       Alerts: 0[/]");
            }
        }
        else
        {
            lines.Add("[dim]Net   n/a[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[blue]Network & VPN[/]")
            .BorderColor(Color.Blue)
            .Padding(1, 0);
    }

    private static Panel BuildContainersPanel(List<ContainerInfo> containers)
    {
        if (containers.Count == 0)
        {
            return new Panel(new Markup("[dim]Docker unavailable[/]"))
                .Header("[green]Containers[/]")
                .BorderColor(Color.Green)
                .Padding(1, 0);
        }

        var lines = new List<string>();
        foreach (var c in containers.Take(10))
        {
            var icon = c.IsRunning ? "[green]>[/]" : "[red]x[/]";
            var name = Markup.Escape(c.Name.TrimStart('/'));
            var uptime = c.IsRunning && c.Uptime != "N/A" ? $"[dim]{c.Uptime}[/]" : "";
            lines.Add($"{icon} {name,-25} {uptime}");
        }

        if (containers.Count > 10)
        {
            lines.Add($"[dim]  ... +{containers.Count - 10} more[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[green]Containers[/]")
            .BorderColor(Color.Green)
            .Padding(1, 0);
    }

    private static Panel BuildSummaryPanel(
        List<ServiceHealthResult> healthResults,
        EventLogEntry? latest)
    {
        var total = healthResults.Count;
        var healthy = healthResults.Count(r => r.IsHealthy);
        var running = healthResults.Count(r => r.IsRunning);

        var parts = new List<string>
        {
            $"[green]Healthy:[/] {healthy}/{total}",
            $"[blue]Running:[/] {running}/{total}"
        };

        if (latest != null)
        {
            var age = DateTime.UtcNow - latest.Timestamp;
            var ageText = age.TotalMinutes < 1 ? "just now" : $"{age.TotalMinutes:F0}m ago";
            var ageColor = age.TotalMinutes > 15 ? "yellow" : "dim";
            parts.Add($"[{ageColor}]Collected: {ageText}[/]");
        }

        return new Panel(new Markup(string.Join("  |  ", parts)))
            .Header("[yellow]Summary[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0);
    }

    private static Panel BuildAnomaliesPanel(List<NetworkAnomaly> anomalies)
    {
        if (anomalies.Count == 0)
        {
            return new Panel(new Markup("[green]No anomalies in last hour[/]"))
                .Header("[yellow]Anomalies[/]")
                .BorderColor(Color.Yellow)
                .Padding(1, 0);
        }

        var lines = anomalies
            .OrderByDescending(a => a.Timestamp)
            .Take(3)
            .Select(a =>
            {
                var color = a.Severity switch
                {
                    "critical" => "red",
                    "warning" => "yellow",
                    _ => "dim"
                };
                var time = a.Timestamp.ToLocalTime().ToString("HH:mm");
                return $"[{color}]! {Markup.Escape(a.Description)} ({time})[/]";
            })
            .ToList();

        if (anomalies.Count > 3)
        {
            lines.Add($"[dim]  +{anomalies.Count - 3} more[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[yellow]Anomalies[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0);
    }

    private static Panel BuildFooterPanel(EventLogEntry? latest, int refreshInterval)
    {
        var collectedText = "";
        if (latest != null)
        {
            var localTime = latest.Timestamp.ToLocalTime();
            collectedText = $"Last collected: {localTime:HH:mm:ss} [dim]|[/] ";
        }

        return new Panel(
            Align.Center(
                new Markup($"[dim]{collectedText}Refresh: {refreshInterval}s [dim]|[/] [yellow]Ctrl+C[/] to exit[/]"),
                VerticalAlignment.Middle))
            .BorderColor(Color.Grey)
            .Padding(0, 0);
    }

    // --- Helpers ---

    private static string RenderBar(string label, double percent, int width = 20)
    {
        var filled = (int)(percent / 100.0 * width);
        filled = Math.Clamp(filled, 0, width);
        var empty = width - filled;
        var color = percent switch
        {
            > 90 => "red",
            > 70 => "yellow",
            _ => "green"
        };
        var bar = $"[[{new string('=', filled)}{new string('-', empty)}]]";
        return $" {label,-4} [{color}]{bar}[/] {percent:F0}%";
    }
}
