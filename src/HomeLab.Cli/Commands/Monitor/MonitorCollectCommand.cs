using System.ComponentModel;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.EventLog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Collects a single event snapshot and appends it to the event log.
/// Designed to be run periodically via LaunchAgent or manually.
/// </summary>
public class MonitorCollectCommand : AsyncCommand<MonitorCollectCommand.Settings>
{
    private readonly IEventCollector _collector;
    private readonly IEventLogService _logService;

    public class Settings : CommandSettings
    {
        [CommandOption("--quiet")]
        [Description("Suppress output (for scheduled runs)")]
        public bool Quiet { get; set; }
    }

    public MonitorCollectCommand(IEventCollector collector, IEventLogService logService)
    {
        _collector = collector;
        _logService = logService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Warn if external drive is not available
        if (!settings.Quiet && _logService is EventLogService els && !els.IsUsingExternalDrive)
        {
            AnsiConsole.MarkupLine("[yellow]External drive not mounted — writing to ~/.homelab/events.jsonl[/]");
        }

        EventLogEntry? entry = null;

        if (!settings.Quiet)
        {
            await AnsiConsole.Status()
                .StartAsync("Collecting event data...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    entry = await _collector.CollectEventAsync();
                });
        }
        else
        {
            entry = await _collector.CollectEventAsync();
        }

        if (entry == null)
        {
            if (!settings.Quiet)
            {
                AnsiConsole.MarkupLine("[red]Failed to collect event data[/]");
            }

            return 1;
        }

        await _logService.WriteEventAsync(entry);

        // Auto-cleanup old entries
        await _logService.CleanupAsync();

        if (!settings.Quiet)
        {
            AnsiConsole.MarkupLine($"[green]Event logged[/] at {entry.Timestamp:HH:mm:ss} UTC");

            if (entry.System != null)
            {
                AnsiConsole.MarkupLine($"  CPU: {entry.System.CpuPercent}% | Mem: {entry.System.MemoryPercent}% | Disk: {entry.System.DiskPercent}%");
            }

            if (entry.Tailscale != null)
            {
                AnsiConsole.MarkupLine($"  Tailscale: {entry.Tailscale.BackendState} ({entry.Tailscale.OnlinePeerCount}/{entry.Tailscale.PeerCount} peers online)");
            }

            if (entry.Docker != null && entry.Docker.Available)
            {
                AnsiConsole.MarkupLine($"  Docker: {entry.Docker.RunningCount}/{entry.Docker.TotalCount} containers running");
            }

            if (entry.Network != null)
            {
                var netParts = new List<string> { $"Devices: {entry.Network.DeviceCount}" };
                if (entry.Network.Traffic != null)
                {
                    netParts.Add($"Traffic: {FormatBytes(entry.Network.Traffic.TotalBytes)}");
                }

                if (entry.Network.Security != null && entry.Network.Security.TotalAlerts > 0)
                {
                    netParts.Add($"Alerts: {entry.Network.Security.TotalAlerts} ({entry.Network.Security.CriticalCount}c/{entry.Network.Security.HighCount}h)");
                }

                AnsiConsole.MarkupLine($"  Network: {string.Join(" | ", netParts)}");
            }

            if (entry.Errors.Count > 0)
            {
                AnsiConsole.MarkupLine($"  [yellow]Warnings: {entry.Errors.Count}[/]");
                foreach (var err in entry.Errors)
                {
                    AnsiConsole.MarkupLine($"    [dim]- {Markup.Escape(err)}[/]");
                }
            }
        }

        return 0;
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
}
