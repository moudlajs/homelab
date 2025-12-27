using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Displays the homelab status dashboard.
/// Shows running containers, resource usage, and health checks.
/// </summary>
public class StatusCommand : AsyncCommand
{
    private readonly IDockerService _dockerService;
    private readonly IServiceHealthCheckService _healthCheckService;

    public StatusCommand(
        IDockerService dockerService,
        IServiceHealthCheckService healthCheckService)
    {
        _dockerService = dockerService;
        _healthCheckService = healthCheckService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        // Show a fancy header
        AnsiConsole.Write(
            new FigletText("Homelab Status")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.WriteLine();

        // Perform health checks on all services
        await AnsiConsole.Status()
            .StartAsync("Checking service health...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(500); // Brief delay for UX
            });

        var healthResults = await _healthCheckService.CheckAllServicesAsync();

        if (healthResults.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No services discovered. Check your docker-compose.yml path in config.[/]");
            return 0;
        }

        // Create enhanced service status table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Service[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Docker[/]");
        table.AddColumn("[yellow]Health[/]");
        table.AddColumn("[yellow]Details[/]");

        foreach (var service in healthResults)
        {
            // Docker status
            var dockerColor = service.IsRunning ? "green" : "red";
            var dockerStatus = service.IsRunning ? "✓ Running" : "✗ Stopped";

            // Health status with icon
            var healthIcon = service.IsHealthy ? "🟢" : (service.IsRunning ? "🟡" : "🔴");
            var healthColor = service.IsHealthy ? "green" : (service.IsRunning ? "yellow" : "red");
            var healthText = service.IsHealthy ? "Healthy" : (service.IsRunning ? "Degraded" : "Unhealthy");

            // Service-specific details
            var details = GetServiceDetails(service);

            table.AddRow(
                service.ServiceName,
                $"[dim]{service.ServiceType}[/]",
                $"[{dockerColor}]{dockerStatus}[/]",
                $"{healthIcon} [{healthColor}]{healthText}[/]",
                details
            );
        }

        AnsiConsole.Write(table);

        // Summary stats
        AnsiConsole.WriteLine();
        var healthyCount = healthResults.Count(s => s.IsHealthy);
        var runningCount = healthResults.Count(s => s.IsRunning);
        var totalCount = healthResults.Count;

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[green]✓ Healthy:[/] {healthyCount}/{totalCount}",
            $"[blue]▶ Running:[/] {runningCount}/{totalCount}",
            $"[yellow]⚡ Total:[/] {totalCount}"
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Summary[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder()
        );

        return 0;
    }

    private string GetServiceDetails(ServiceHealthResult service)
    {
        if (service.ServiceHealth == null || service.ServiceHealth.Metrics.Count == 0)
        {
            return service.Message ?? "[dim]No metrics available[/]";
        }

        // Show the first 2 most interesting metrics
        var metrics = service.ServiceHealth.Metrics.Take(2);
        var details = string.Join(", ", metrics.Select(m => $"[dim]{m.Key}:[/] {m.Value}"));

        return details;
    }
}
