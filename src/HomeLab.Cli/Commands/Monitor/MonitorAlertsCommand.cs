using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Displays active Prometheus alerts.
/// </summary>
public class MonitorAlertsCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;

    public MonitorAlertsCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Alerts")
                .Centered()
                .Color(Color.Red));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreatePrometheusClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking Prometheus status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Prometheus is not healthy: {healthInfo.Message}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] Prometheus is healthy\n");

        // Get active alerts
        var alerts = await client.GetActiveAlertsAsync();

        if (alerts.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ No active alerts! All systems normal.[/]");
            return 0;
        }

        // Create alerts table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Alert Name[/]");
        table.AddColumn("[yellow]Severity[/]");
        table.AddColumn("[yellow]Summary[/]");
        table.AddColumn("[yellow]Active For[/]");

        foreach (var alert in alerts)
        {
            var severityColor = alert.Severity.ToLowerInvariant() switch
            {
                "critical" => "red",
                "warning" => "yellow",
                _ => "blue"
            };

            var activeFor = DateTime.UtcNow - alert.ActiveAt;
            var activeForText = FormatDuration(activeFor);

            table.AddRow(
                $"[cyan]{alert.Name}[/]",
                $"[{severityColor}]{alert.Severity.ToUpper()}[/]",
                Markup.Escape(alert.Summary),
                $"[dim]{activeForText}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary
        AnsiConsole.WriteLine();
        var criticalCount = alerts.Count(a => a.Severity.ToLowerInvariant() == "critical");
        var warningCount = alerts.Count(a => a.Severity.ToLowerInvariant() == "warning");

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        if (criticalCount > 0)
            grid.AddRow($"[red]Critical Alerts:[/]", $"[red]{criticalCount}[/]");
        if (warningCount > 0)
            grid.AddRow($"[yellow]Warning Alerts:[/]", $"[yellow]{warningCount}[/]");

        grid.AddRow($"[blue]Total Active:[/]", $"[blue]{alerts.Count}[/]");

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Alert Summary[/]")
                .BorderColor(Color.Red)
                .RoundedBorder()
        );

        return 0;
    }

    private string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
            return $"{(int)duration.TotalSeconds}s";
        if (duration.TotalHours < 1)
            return $"{(int)duration.TotalMinutes}m";
        if (duration.TotalDays < 1)
            return $"{(int)duration.TotalHours}h";
        return $"{(int)duration.TotalDays}d {duration.Hours}h";
    }
}
