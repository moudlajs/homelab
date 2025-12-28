using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Displays security alerts from Suricata IDS.
/// </summary>
public class NetworkIntrusionCommand : AsyncCommand<NetworkIntrusionCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--severity <LEVEL>")]
        [Description("Filter by severity: critical, high, medium, low")]
        public string? Severity { get; set; }

        [CommandOption("--limit <N>")]
        [Description("Number of alerts to show (default: 50)")]
        public int Limit { get; set; } = 50;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public NetworkIntrusionCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Security Alerts")
                .Centered()
                .Color(Color.Red));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateSuricataClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking Suricata status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300, cancellationToken);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Suricata is not available: {healthInfo.Message}");
            AnsiConsole.MarkupLine("[dim]Using mock data for demonstration[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Suricata is monitoring network traffic\n");
        }

        // Get alerts
        List<HomeLab.Cli.Models.SecurityAlert>? alerts = null;

        var statusMessage = settings.Severity != null
            ? $"Fetching {settings.Severity} severity alerts..."
            : "Fetching security alerts...";

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(statusMessage, async ctx =>
            {
                alerts = await client.GetAlertsAsync(settings.Severity, settings.Limit);
            });

        if (alerts == null || alerts.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No security alerts found[/]");
            if (settings.Severity != null)
            {
                AnsiConsole.MarkupLine($"[dim]No alerts with severity: {settings.Severity}[/]");
            }
            return 0;
        }

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, alerts))
        {
            return 0;
        }

        // Display summary
        var criticalCount = alerts.Count(a => a.Severity == "critical");
        var highCount = alerts.Count(a => a.Severity == "high");
        var mediumCount = alerts.Count(a => a.Severity == "medium");
        var lowCount = alerts.Count(a => a.Severity == "low");

        var summaryPanel = new Panel(new Markup(
            $"[red]Critical:[/] {criticalCount}  " +
            $"[orange1]High:[/] {highCount}  " +
            $"[yellow]Medium:[/] {mediumCount}  " +
            $"[dim]Low:[/] {lowCount}"
        ))
        {
            Header = new PanelHeader("[red]Alert Summary[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(summaryPanel);
        AnsiConsole.WriteLine();

        // Display alerts table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.Title = new TableTitle($"[red]Security Alerts[/] [dim](Showing {alerts.Count} of {settings.Limit})[/]");
        table.AddColumn("[cyan]Time[/]");
        table.AddColumn("[cyan]Severity[/]");
        table.AddColumn("[cyan]Alert Type[/]");
        table.AddColumn("[cyan]Source[/]");
        table.AddColumn("[cyan]Destination[/]");
        table.AddColumn("[cyan]Protocol[/]");
        table.AddColumn("[cyan]Category[/]");

        foreach (var alert in alerts)
        {
            var severityColor = alert.Severity switch
            {
                "critical" => "red",
                "high" => "orange1",
                "medium" => "yellow",
                "low" => "dim",
                _ => "white"
            };

            var timeAgo = FormatTimeAgo(alert.Timestamp);

            table.AddRow(
                timeAgo,
                $"[{severityColor}]{alert.Severity.ToUpper()}[/]",
                alert.AlertType,
                $"{alert.SourceIp}:{alert.SourcePort}",
                $"{alert.DestinationIp}:{alert.DestinationPort}",
                alert.Protocol.ToUpper(),
                alert.Category ?? "Unknown"
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Collected at {DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        // Show recommendation if critical alerts found
        if (criticalCount > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[red]⚠ {criticalCount} critical alert(s) detected! Immediate action recommended.[/]");
        }

        return 0;
    }

    private static string FormatTimeAgo(DateTime timestamp)
    {
        var timeSpan = DateTime.Now - timestamp;

        if (timeSpan.TotalMinutes < 1)
        {
            return "Just now";
        }
        if (timeSpan.TotalMinutes < 60)
        {
            return $"{(int)timeSpan.TotalMinutes}m ago";
        }
        if (timeSpan.TotalHours < 24)
        {
            return $"{(int)timeSpan.TotalHours}h ago";
        }
        if (timeSpan.TotalDays < 7)
        {
            return $"{(int)timeSpan.TotalDays}d ago";
        }

        return timestamp.ToString("MM/dd HH:mm");
    }
}
