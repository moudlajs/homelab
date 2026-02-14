using System.ComponentModel;
using HomeLab.Cli.Models;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
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
        IEventLogService eventLogService,
        INetworkAnomalyDetector anomalyDetector)
    {
        _dockerService = dockerService;
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

        await AnsiConsole.Live(new Markup("[dim]Loading dashboard...[/]"))
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                while (!_shouldExit)
                {
                    try
                    {
                        var dashboard = await BuildDashboard(settings.RefreshInterval);
                        ctx.UpdateTarget(dashboard);
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                    catch (Exception ex)
                    {
                        ctx.UpdateTarget(new Markup($"[red]Error: {Markup.Escape(ex.Message)}[/]"));
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                }
            });

        AnsiConsole.MarkupLine("\n[yellow]Dashboard stopped.[/]");
        return 0;
    }

    private async Task<Rows> BuildDashboard(int refreshInterval)
    {
        // Fetch all data in parallel
        var containersTask = SafeGetContainers();
        var latestEventTask = GetLatestEventAsync();
        var anomaliesTask = GetCachedAnomaliesAsync();

        await Task.WhenAll(containersTask, latestEventTask, anomaliesTask);

        var containers = await containersTask;
        var latest = await latestEventTask;
        var anomalies = await anomaliesTask;

        // Header
        var uptime = latest?.System?.Uptime;
        var uptimeText = !string.IsNullOrEmpty(uptime) ? $" | up {uptime}" : "";
        var header = new Rule($"[bold green]HomeLab Dashboard[/] [dim]| {DateTime.Now:HH:mm:ss}{uptimeText}[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("green")
        };

        // Build panels side by side: left column + right column
        var leftPanels = new Rows(
            BuildSystemGaugesPanel(latest),
            BuildNetworkVpnPanel(latest));

        var rightPanels = new Rows(
            BuildContainersPanel(containers),
            BuildAnomaliesPanel(anomalies));

        var columns = new Columns(leftPanels, rightPanels);

        // Footer
        var collectedText = "";
        if (latest != null)
        {
            var age = DateTime.UtcNow - latest.Timestamp;
            var ageText = age.TotalMinutes < 1 ? "just now" : $"{age.TotalMinutes:F0}m ago";
            var ageColor = age.TotalMinutes > 15 ? "yellow" : "dim";
            collectedText = $"[{ageColor}]Last collected: {ageText}[/] | ";
        }

        var footer = new Rule($"[dim]{collectedText}Refresh: {refreshInterval}s | [yellow]Ctrl+C[/] to exit[/]")
        {
            Justification = Justify.Center,
            Style = Style.Parse("grey")
        };

        return new Rows(header, new Text(""), columns, new Text(""), footer);
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

    private static Panel BuildSystemGaugesPanel(EventLogEntry? latest)
    {
        if (latest?.System == null)
        {
            return new Panel(new Markup("[dim]No event data — run monitor collect[/]"))
                .Header("[cyan]System[/]")
                .BorderColor(Color.Cyan)
                .Expand()
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
            .Expand()
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
            lines.Add($"[{vpnColor}]VPN[/]  {vpnState}  {ts.OnlinePeerCount}/{ts.PeerCount} peers");
            if (ts.SelfIp != null)
            {
                lines.Add($"[dim]      {ts.SelfIp}[/]");
            }
        }
        else
        {
            lines.Add("[dim]VPN  n/a[/]");
        }

        lines.Add("");

        // Network section
        var net = latest?.Network;
        if (net != null)
        {
            lines.Add($"[blue]Net[/]  Devices: {net.DeviceCount}");

            if (net.Traffic != null)
            {
                lines.Add($"      Traffic: {NetworkAnomalyDetector.FormatBytes(net.Traffic.TotalBytes)}");
            }
            else
            {
                lines.Add("[dim]      Traffic: n/a[/]");
            }

            var sec = net.Security;
            if (sec != null && sec.TotalAlerts > 0)
            {
                var alertColor = sec.CriticalCount > 0 ? "red" : "yellow";
                lines.Add($"[{alertColor}]      Alerts: {sec.TotalAlerts} ({sec.CriticalCount}c/{sec.HighCount}h)[/]");
            }
            else
            {
                lines.Add("[dim]      Alerts: 0[/]");
            }
        }
        else
        {
            lines.Add("[dim]Net  n/a[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[blue]Network & VPN[/]")
            .BorderColor(Color.Blue)
            .Expand()
            .Padding(1, 0);
    }

    private static Panel BuildContainersPanel(List<ContainerInfo> containers)
    {
        if (containers.Count == 0)
        {
            return new Panel(new Markup("[dim]Docker unavailable[/]"))
                .Header("[green]Containers[/]")
                .BorderColor(Color.Green)
                .Expand()
                .Padding(1, 0);
        }

        var running = containers.Count(c => c.IsRunning);
        var total = containers.Count;

        var lines = new List<string>
        {
            $"[green]{running}[/]/{total} running",
            ""
        };

        foreach (var c in containers.OrderByDescending(c => c.IsRunning).Take(12))
        {
            var icon = c.IsRunning ? "[green]>[/]" : "[red]x[/]";
            var name = Markup.Escape(c.Name.TrimStart('/'));
            var uptime = c.IsRunning && c.Uptime != "N/A" ? $"[dim]{c.Uptime}[/]" : "";
            lines.Add($" {icon} {name,-28} {uptime}");
        }

        if (containers.Count > 12)
        {
            lines.Add($"[dim]   ... +{containers.Count - 12} more[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[green]Containers[/]")
            .BorderColor(Color.Green)
            .Expand()
            .Padding(1, 0);
    }

    private static Panel BuildAnomaliesPanel(List<NetworkAnomaly> anomalies)
    {
        if (anomalies.Count == 0)
        {
            return new Panel(new Markup("[green]No anomalies in last hour[/]"))
                .Header("[yellow]Anomalies[/]")
                .BorderColor(Color.Yellow)
                .Expand()
                .Padding(1, 0);
        }

        var lines = anomalies
            .OrderByDescending(a => a.Timestamp)
            .Take(5)
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

        if (anomalies.Count > 5)
        {
            lines.Add($"[dim]  +{anomalies.Count - 5} more[/]");
        }

        return new Panel(new Markup(string.Join("\n", lines)))
            .Header("[yellow]Anomalies[/]")
            .BorderColor(Color.Yellow)
            .Expand()
            .Padding(1, 0);
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
