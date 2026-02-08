using System.ComponentModel;
using HomeLab.Cli.Services.Dependencies;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Displays the homelab status dashboard.
/// Shows running containers, resource usage, and health checks.
/// </summary>
public class StatusCommand : AsyncCommand<StatusCommand.Settings>
{
    private readonly IDockerService _dockerService;
    private readonly IServiceHealthCheckService _healthCheckService;
    private readonly ServiceDependencyGraph _dependencyGraph;
    private readonly IOutputFormatter _outputFormatter;

    public StatusCommand(
        IDockerService dockerService,
        IServiceHealthCheckService healthCheckService,
        IOutputFormatter outputFormatter)
    {
        _dockerService = dockerService;
        _healthCheckService = healthCheckService;
        _dependencyGraph = new ServiceDependencyGraph();
        _outputFormatter = outputFormatter;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("--watch")]
        [Description("Watch mode - refresh status every few seconds")]
        [DefaultValue(false)]
        public bool Watch { get; set; }

        [CommandOption("--interval")]
        [Description("Refresh interval in seconds for watch mode")]
        [DefaultValue(5)]
        public int Interval { get; set; } = 5;

        [CommandOption("--show-dependencies")]
        [Description("Show service dependency graph")]
        [DefaultValue(false)]
        public bool ShowDependencies { get; set; }

        [CommandOption("-o|--output <FORMAT>")]
        [Description("Output format: table (default), json, csv, yaml")]
        public string? Output { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file instead of stdout")]
        public string? ExportFile { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Check if export mode is requested
        if (!string.IsNullOrEmpty(settings.Output))
        {
            return await ExportStatus(settings);
        }

        if (settings.Watch)
        {
            return await RunWatchMode(settings, cancellationToken);
        }

        return await DisplayStatus(settings);
    }

    private async Task<int> RunWatchMode(Settings settings, CancellationToken cancellationToken)
    {
        Console.Clear();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Console.SetCursorPosition(0, 0);
                await DisplayStatus(settings);

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Press Ctrl+C to exit watch mode. Refreshing every {settings.Interval}s...[/]");

                await Task.Delay(TimeSpan.FromSeconds(settings.Interval), cancellationToken);
                Console.Clear();
            }
        }
        catch (TaskCanceledException)
        {
            // User pressed Ctrl+C
            AnsiConsole.MarkupLine("\n[yellow]Watch mode stopped.[/]");
        }

        return 0;
    }

    private async Task<int> DisplayStatus(Settings settings)
    {
        // Show a fancy header
        AnsiConsole.Write(
            new FigletText("Homelab Status")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.WriteLine();

        // Check if Docker is available
        if (!await _dockerService.IsDockerAvailableAsync())
        {
            AnsiConsole.MarkupLine("[yellow]Docker is not available on this machine.[/]");
            AnsiConsole.MarkupLine("[dim]Install Docker or check that the Docker daemon is running.[/]");
            AnsiConsole.MarkupLine("[dim]Commands that don't need Docker (tv, tailscale, network) still work.[/]");
            return 0;
        }

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

        // Show dependency graph if requested
        if (settings.ShowDependencies)
        {
            AnsiConsole.WriteLine();
            DisplayDependencyGraph();
        }

        return 0;
    }

    private void DisplayDependencyGraph()
    {
        var dependencies = _dependencyGraph.GetAllDependencies().ToList();

        if (dependencies.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No dependencies defined for current services.[/]");
            return;
        }

        var tree = new Tree("[yellow]Service Dependencies[/]");

        foreach (var dep in dependencies.OrderBy(d => d.ServiceName))
        {
            var typeIndicator = dep.Type == Models.DependencyType.Hard ? "[red]●[/]" : "[yellow]○[/]";
            var node = tree.AddNode($"{typeIndicator} [cyan]{dep.ServiceName}[/]");

            foreach (var dependency in dep.DependsOn)
            {
                var dependencyNode = node.AddNode($"[dim]└─[/] [green]{dependency}[/]");

                if (!string.IsNullOrEmpty(dep.Reason))
                {
                    dependencyNode.AddNode($"[dim italic]{dep.Reason}[/]");
                }
            }
        }

        var panel = new Panel(tree)
            .Header("[yellow]Dependency Graph[/]")
            .BorderColor(Color.Blue)
            .RoundedBorder();

        AnsiConsole.Write(panel);

        // Legend
        var legendGrid = new Grid();
        legendGrid.AddColumn();
        legendGrid.AddColumn();
        legendGrid.AddColumn();
        legendGrid.AddRow(
            "[red]● Hard dependency[/]",
            "[yellow]○ Soft dependency[/]",
            "[dim]Service requires these to start[/]"
        );

        AnsiConsole.Write(
            new Panel(legendGrid)
                .Header("[dim]Legend[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder()
        );
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

    private async Task<int> ExportStatus(Settings settings)
    {
        // Parse output format
        if (!Enum.TryParse<OutputFormat>(settings.Output, true, out var format))
        {
            AnsiConsole.MarkupLine($"[red]Invalid output format: {settings.Output}[/]");
            AnsiConsole.MarkupLine("[yellow]Valid formats: table, json, csv, yaml[/]");
            return 1;
        }

        // Get health check data
        var healthResults = await _healthCheckService.CheckAllServicesAsync();

        if (healthResults.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No services discovered.[/]");
            return 0;
        }

        // Create simplified export model (without Spectre.Console formatting)
        var exportData = healthResults.Select(s => new
        {
            Service = s.ServiceName,
            Type = s.ServiceType.ToString(),
            IsRunning = s.IsRunning,
            IsHealthy = s.IsHealthy,
            Status = s.IsHealthy ? "Healthy" : (s.IsRunning ? "Degraded" : "Unhealthy"),
            Message = s.Message,
            Metrics = s.ServiceHealth?.Metrics.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>()
        }).ToList();

        // Format the output
        var formatted = _outputFormatter.FormatCollection(exportData, format);

        // Export to file or stdout
        if (!string.IsNullOrEmpty(settings.ExportFile))
        {
            await File.WriteAllTextAsync(settings.ExportFile, formatted);
            AnsiConsole.MarkupLine($"[green]✓ Exported to {settings.ExportFile}[/]");
        }
        else
        {
            Console.WriteLine(formatted);
        }

        return 0;
    }
}
