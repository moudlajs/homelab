using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using HomeLab.Cli.Services.UptimeKuma;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Uptime;

/// <summary>
/// Displays recent uptime alerts and incidents.
/// Shows when services went down and how long they were offline.
/// </summary>
public class UptimeAlertsCommand : AsyncCommand<UptimeAlertsCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public UptimeAlertsCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--limit <COUNT>")]
        [Description("Number of recent alerts to show (default: 10)")]
        [DefaultValue(10)]
        public int Limit { get; set; } = 10;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Recent Alerts")
                .Centered()
                .Color(Color.Red));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateUptimeKumaClient();

        // Get recent incidents
        await AnsiConsole.Status()
            .StartAsync("Fetching recent alerts...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var incidents = await client.GetIncidentsAsync(settings.Limit);

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, incidents))
        {
            return 0;
        }

        if (incidents.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ No recent incidents! All services are healthy.[/]");
            return 0;
        }

        // Create incidents table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Time[/]");
        table.AddColumn("[yellow]Monitor[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Duration[/]");
        table.AddColumn("[yellow]Message[/]");

        foreach (var incident in incidents.OrderByDescending(i => i.StartedAt).Take(settings.Limit))
        {
            var statusIcon = incident.Status == "down" ? "🔴" : "🟢";
            var statusColor = incident.Status == "down" ? "red" : "green";
            var statusText = incident.Status.ToUpper();

            var durationText = incident.Duration.TotalDays >= 1
                ? $"{(int)incident.Duration.TotalDays}d {incident.Duration.Hours}h"
                : incident.Duration.TotalHours >= 1
                    ? $"{(int)incident.Duration.TotalHours}h {incident.Duration.Minutes}m"
                    : $"{(int)incident.Duration.TotalMinutes}m";

            var durationColor = incident.Duration.TotalHours >= 1 ? "red" :
                               incident.Duration.TotalMinutes >= 30 ? "yellow" : "dim";

            table.AddRow(
                $"[dim]{incident.StartedAt:MM-dd HH:mm}[/]",
                incident.MonitorName,
                $"{statusIcon} [{statusColor}]{statusText}[/]",
                $"[{durationColor}]{durationText}[/]",
                $"[dim]{incident.Message}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary stats
        AnsiConsole.WriteLine();
        var activeIncidents = incidents.Count(i => i.Status == "down");
        var resolvedIncidents = incidents.Count(i => i.Status == "recovered");
        var totalDowntime = TimeSpan.FromSeconds(incidents.Sum(i => i.Duration.TotalSeconds));

        var totalDowntimeText = totalDowntime.TotalDays >= 1
            ? $"{(int)totalDowntime.TotalDays}d {totalDowntime.Hours}h"
            : totalDowntime.TotalHours >= 1
                ? $"{(int)totalDowntime.TotalHours}h {totalDowntime.Minutes}m"
                : $"{(int)totalDowntime.TotalMinutes}m";

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[red]⚠ Active:[/] {activeIncidents}",
            $"[green]✓ Resolved:[/] {resolvedIncidents}",
            $"[yellow]⏱ Total Downtime:[/] {totalDowntimeText}"
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Summary[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder()
        );

        return 0;
    }
}
