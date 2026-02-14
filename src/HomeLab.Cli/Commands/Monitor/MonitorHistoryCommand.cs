using System.ComponentModel;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Displays event timeline with gap detection and change highlights.
/// </summary>
public class MonitorHistoryCommand : AsyncCommand<MonitorHistoryCommand.Settings>
{
    private readonly IEventLogService _logService;

    public class Settings : CommandSettings
    {
        [CommandOption("--last <DURATION>")]
        [Description("Time window: 1h, 6h, 12h, 24h, 7d (default: 24h)")]
        public string Duration { get; set; } = "24h";

        [CommandOption("--changes-only")]
        [Description("Show only rows where state changed")]
        public bool ChangesOnly { get; set; }
    }

    public MonitorHistoryCommand(IEventLogService logService)
    {
        _logService = logService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var since = ParseDuration(settings.Duration);
        var events = await _logService.ReadEventsAsync(since: since);

        if (events.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No events found.[/] Run [cyan]monitor collect[/] to start logging.");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Cyan);
        table.AddColumn("[yellow]Time[/]");
        table.AddColumn("[yellow]CPU%[/]");
        table.AddColumn("[yellow]Mem%[/]");
        table.AddColumn("[yellow]Docker[/]");
        table.AddColumn("[yellow]Tailscale[/]");
        table.AddColumn("[yellow]Power[/]");
        table.AddColumn("[yellow]Devices[/]");
        table.AddColumn("[yellow]Traffic[/]");
        table.AddColumn("[yellow]Alerts[/]");
        table.AddColumn("[yellow]Issues[/]");

        var gapCount = 0;
        var sleepWakeCount = 0;
        var containerChanges = 0;
        var tailscaleDrops = 0;

        EventLogEntry? prev = null;

        foreach (var evt in events)
        {
            // Gap detection
            if (prev != null)
            {
                var gap = evt.Timestamp - prev.Timestamp;
                if (gap.TotalMinutes > 10)
                {
                    gapCount++;
                    var gapText = FormatTimeSpan(gap);
                    table.AddRow(
                        $"[red]--- GAP: {gapText} ---[/]",
                        "", "", "", "", "", "", "", "", "");
                }
            }

            // Change detection
            if (settings.ChangesOnly && prev != null && !HasChanged(prev, evt))
            {
                prev = evt;
                continue;
            }

            // Track stats
            if (prev != null)
            {
                if (prev.Docker?.RunningCount != evt.Docker?.RunningCount)
                {
                    containerChanges++;
                }

                if (prev.Tailscale?.IsConnected == true && evt.Tailscale?.IsConnected == false)
                {
                    tailscaleDrops++;
                }
            }

            // Power events
            var powerText = "";
            if (evt.Power?.RecentEvents.Count > 0)
            {
                sleepWakeCount += evt.Power.RecentEvents.Count;
                powerText = string.Join(", ", evt.Power.RecentEvents.Select(e => e.Type));
            }

            // Issues column
            var issues = new List<string>();
            if (evt.System?.CpuPercent > 80)
            {
                issues.Add("[red]CPU high[/]");
            }

            if (evt.System?.MemoryPercent > 90)
            {
                issues.Add("[red]Mem high[/]");
            }

            if (evt.Tailscale?.IsConnected == false)
            {
                issues.Add("[red]VPN down[/]");
            }

            if (evt.Docker is { Available: true } && evt.Docker.RunningCount < evt.Docker.TotalCount)
            {
                issues.Add($"[yellow]{evt.Docker.TotalCount - evt.Docker.RunningCount} stopped[/]");
            }

            if (evt.Errors.Count > 0)
            {
                issues.Add($"[dim]{evt.Errors.Count} warn[/]");
            }

            var tsColor = evt.Tailscale?.IsConnected == true ? "green" : "red";
            var tsState = evt.Tailscale?.BackendState ?? "?";
            var dockerText = evt.Docker?.Available == true
                ? $"{evt.Docker.RunningCount}/{evt.Docker.TotalCount}"
                : "[dim]n/a[/]";

            var trafficText = evt.Network?.Traffic != null
                ? FormatBytes(evt.Network.Traffic.TotalBytes)
                : "[dim]n/a[/]";

            var alertText = FormatAlertColumn(evt.Network?.Security);

            table.AddRow(
                evt.Timestamp.ToLocalTime().ToString("MM-dd HH:mm"),
                $"{evt.System?.CpuPercent ?? 0}",
                $"{evt.System?.MemoryPercent ?? 0}",
                dockerText,
                $"[{tsColor}]{Markup.Escape(tsState)}[/]",
                Markup.Escape(powerText),
                $"{evt.Network?.DeviceCount ?? 0}",
                trafficText,
                alertText,
                issues.Count > 0 ? string.Join(", ", issues) : "[green]OK[/]");

            prev = evt;
        }

        AnsiConsole.Write(table);

        // Summary footer
        AnsiConsole.WriteLine();
        var totalAlerts = events.Sum(e => e.Network?.Security?.TotalAlerts ?? 0);
        var alertSummary = totalAlerts > 0 ? $" | Security alerts: {totalAlerts}" : "";
        AnsiConsole.MarkupLine($"[dim]Entries: {events.Count} | Gaps: {gapCount} | Sleep/Wake: {sleepWakeCount} | Container changes: {containerChanges} | Tailscale drops: {tailscaleDrops}{alertSummary}[/]");

        return 0;
    }

    private static DateTime ParseDuration(string duration)
    {
        var now = DateTime.UtcNow;
        var d = duration.ToLowerInvariant().Trim();

        if (d.EndsWith('h') && int.TryParse(d[..^1], out var hours))
        {
            return now.AddHours(-hours);
        }

        if (d.EndsWith('d') && int.TryParse(d[..^1], out var days))
        {
            return now.AddDays(-days);
        }

        // Default: 24 hours
        return now.AddHours(-24);
    }

    private static bool HasChanged(EventLogEntry prev, EventLogEntry curr)
    {
        if (prev.Docker?.RunningCount != curr.Docker?.RunningCount)
        {
            return true;
        }

        if (prev.Tailscale?.IsConnected != curr.Tailscale?.IsConnected)
        {
            return true;
        }

        if (prev.Tailscale?.OnlinePeerCount != curr.Tailscale?.OnlinePeerCount)
        {
            return true;
        }

        if (curr.Power?.RecentEvents.Count > 0)
        {
            return true;
        }

        if (Math.Abs((prev.System?.CpuPercent ?? 0) - (curr.System?.CpuPercent ?? 0)) > 20)
        {
            return true;
        }

        if (prev.Network?.DeviceCount != curr.Network?.DeviceCount)
        {
            return true;
        }

        // Traffic change > 50%
        var prevBytes = prev.Network?.Traffic?.TotalBytes ?? 0;
        var currBytes = curr.Network?.Traffic?.TotalBytes ?? 0;
        if (prevBytes > 0 && currBytes > 0 && Math.Abs(currBytes - prevBytes) > prevBytes * 0.5)
        {
            return true;
        }

        // New security alerts
        if ((curr.Network?.Security?.TotalAlerts ?? 0) > 0 && (prev.Network?.Security?.TotalAlerts ?? 0) == 0)
        {
            return true;
        }

        if (curr.Errors.Count > 0 && prev.Errors.Count == 0)
        {
            return true;
        }

        return false;
    }

    private static string FormatAlertColumn(Models.EventLog.SecuritySummary? security)
    {
        if (security == null || security.TotalAlerts == 0)
        {
            return "[dim]0[/]";
        }

        var parts = new List<string>();
        if (security.CriticalCount > 0)
        {
            parts.Add($"[red]{security.CriticalCount}c[/]");
        }

        if (security.HighCount > 0)
        {
            parts.Add($"[yellow]{security.HighCount}h[/]");
        }

        var other = security.TotalAlerts - security.CriticalCount - security.HighCount;
        if (other > 0)
        {
            parts.Add($"{other}");
        }

        return string.Join(" ", parts);
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

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            return $"{(int)ts.TotalDays}d {ts.Hours}h";
        }

        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }

        return $"{(int)ts.TotalMinutes}m";
    }
}
