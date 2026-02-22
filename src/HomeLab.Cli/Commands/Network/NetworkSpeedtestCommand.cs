using System.ComponentModel;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.EventLog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

public class NetworkSpeedtestCommand : AsyncCommand<NetworkSpeedtestCommand.Settings>
{
    private readonly ISpeedtestService _speedtest;
    private readonly IEventLogService _eventLog;

    public class Settings : CommandSettings
    {
        [CommandOption("--history")]
        [Description("Show past speed test results")]
        public bool History { get; set; }

        [CommandOption("--last <PERIOD>")]
        [Description("History period (e.g., 7d, 30d). Default: 7d")]
        public string? Last { get; set; }
    }

    public NetworkSpeedtestCommand(ISpeedtestService speedtest, IEventLogService eventLog)
    {
        _speedtest = speedtest;
        _eventLog = eventLog;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.History)
        {
            return await ShowHistoryAsync(settings.Last ?? "7d");
        }

        return await RunSpeedtestAsync();
    }

    private async Task<int> RunSpeedtestAsync()
    {
        if (!_speedtest.IsInstalled())
        {
            AnsiConsole.MarkupLine("[red]No speedtest tool installed.[/]");
            AnsiConsole.MarkupLine("[dim]Install with: brew tap teamookla/speedtest && brew install speedtest[/]");
            return 1;
        }

        SpeedtestSnapshot result;

        try
        {
            result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("Running speed test (this takes ~30s)...", async _ =>
                {
                    return await _speedtest.RunAsync();
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Speed test failed: {ex.Message}[/]");
            return 1;
        }

        // Display results
        AnsiConsole.WriteLine();

        var maxSpeed = Math.Max(result.DownloadMbps, result.UploadMbps);
        var chart = new BarChart()
            .Width(50)
            .Label($"[bold]Speed Test Results[/]")
            .AddItem("Download", result.DownloadMbps, Color.Green)
            .AddItem("Upload", result.UploadMbps, Color.Blue);

        AnsiConsole.Write(chart);
        AnsiConsole.WriteLine();

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddRow("[dim]Download:[/]", $"[green bold]{result.DownloadMbps:F1} Mbps[/]");
        grid.AddRow("[dim]Upload:[/]", $"[blue bold]{result.UploadMbps:F1} Mbps[/]");
        grid.AddRow("[dim]Latency:[/]", $"[cyan]{result.PingMs:F0} ms[/]");
        grid.AddRow("[dim]Server:[/]", result.Server);
        grid.AddRow("[dim]ISP:[/]", result.Isp);
        grid.AddRow("[dim]IP:[/]", result.Ip);
        AnsiConsole.Write(grid);

        // Save to event log
        try
        {
            var entry = new EventLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Speedtest = result
            };
            await _eventLog.WriteEventAsync(entry);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Result saved to event log.[/]");
        }
        catch
        {
            // Don't fail the command if logging fails
        }

        return 0;
    }

    private async Task<int> ShowHistoryAsync(string period)
    {
        var since = ParsePeriod(period);
        var events = await _eventLog.ReadEventsAsync(since);
        var speedtestEvents = events
            .Where(e => e.Speedtest != null)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        if (speedtestEvents.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No speed test results found.[/]");
            AnsiConsole.MarkupLine("[dim]Run 'homelab network speedtest' to capture one.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.Title("[bold]Speed Test History[/]");
        table.AddColumn("[yellow]Date[/]");
        table.AddColumn(new TableColumn("[green]Download[/]").RightAligned());
        table.AddColumn(new TableColumn("[blue]Upload[/]").RightAligned());
        table.AddColumn(new TableColumn("[cyan]Latency[/]").RightAligned());
        table.AddColumn("[dim]Server[/]");

        foreach (var e in speedtestEvents)
        {
            var s = e.Speedtest!;
            table.AddRow(
                e.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                $"[green]{s.DownloadMbps:F1} Mbps[/]",
                $"[blue]{s.UploadMbps:F1} Mbps[/]",
                $"[cyan]{s.PingMs:F0} ms[/]",
                s.Server
            );
        }

        AnsiConsole.Write(table);

        // Show averages
        var avgDown = speedtestEvents.Average(e => e.Speedtest!.DownloadMbps);
        var avgUp = speedtestEvents.Average(e => e.Speedtest!.UploadMbps);
        var avgPing = speedtestEvents.Average(e => e.Speedtest!.PingMs);
        AnsiConsole.MarkupLine(
            $"\n[dim]Average:[/] [green]{avgDown:F1} Mbps[/] down, [blue]{avgUp:F1} Mbps[/] up, [cyan]{avgPing:F0} ms[/] latency ({speedtestEvents.Count} tests)");

        return 0;
    }

    private static DateTime ParsePeriod(string period)
    {
        var now = DateTime.UtcNow;
        if (period.EndsWith('d') && int.TryParse(period[..^1], out var days))
        {
            return now.AddDays(-days);
        }

        if (period.EndsWith('h') && int.TryParse(period[..^1], out var hours))
        {
            return now.AddHours(-hours);
        }

        return now.AddDays(-7);
    }
}
