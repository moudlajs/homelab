using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.UptimeKuma;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Commands.Uptime;

/// <summary>
/// Displays uptime monitoring status for all services.
/// Shows which services are up/down and their uptime percentages.
/// </summary>
public class UptimeStatusCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;

    public UptimeStatusCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Uptime Status")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.WriteLine();

        // Get Uptime Kuma client from factory
        var client = _clientFactory.CreateUptimeKumaClient();

        // Check if Uptime Kuma is healthy
        await AnsiConsole.Status()
            .StartAsync("Checking Uptime Kuma...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Uptime Kuma is not healthy: {healthInfo.Message}");
            AnsiConsole.MarkupLine("[yellow]Note: Showing mock data for demonstration[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Uptime Kuma is healthy\n");
        }

        // Get all monitors
        var monitors = await client.GetMonitorsAsync();

        if (monitors.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No monitors configured yet.[/]");
            AnsiConsole.MarkupLine("[dim]Use 'homelab uptime add <name> <url>' to add a monitor.[/]");
            return 0;
        }

        // Create monitors table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Monitor[/]");
        table.AddColumn("[yellow]URL[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Uptime[/]");
        table.AddColumn("[yellow]Response[/]");

        foreach (var monitor in monitors)
        {
            var statusIcon = monitor.Status == MonitorStatus.Up ? "🟢" : "🔴";
            var statusColor = monitor.Status == MonitorStatus.Up ? "green" : "red";
            var statusText = monitor.Status.ToString().ToUpper();

            var uptimeColor = monitor.UptimePercentage >= 99 ? "green" :
                             monitor.UptimePercentage >= 95 ? "yellow" : "red";

            table.AddRow(
                monitor.Name,
                $"[dim]{monitor.Url}[/]",
                $"[dim]{monitor.Type}[/]",
                $"{statusIcon} [{statusColor}]{statusText}[/]",
                $"[{uptimeColor}]{monitor.UptimePercentage:F2}%[/]",
                monitor.Status == MonitorStatus.Up
                    ? $"[dim]{monitor.AverageResponse}ms[/]"
                    : "[dim]-[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary stats
        AnsiConsole.WriteLine();
        var upCount = monitors.Count(m => m.Status == MonitorStatus.Up);
        var downCount = monitors.Count(m => m.Status == MonitorStatus.Down);
        var avgUptime = monitors.Count > 0 ? monitors.Average(m => m.UptimePercentage) : 0;

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[green]✓ Up:[/] {upCount}/{monitors.Count}",
            $"[red]✗ Down:[/] {downCount}/{monitors.Count}",
            $"[yellow]⚡ Avg Uptime:[/] {avgUptime:F2}%"
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
