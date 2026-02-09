using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// TUI (Terminal UI) command for live homelab dashboard.
/// Like htop but for your homelab!
/// </summary>
public class TuiCommand : AsyncCommand<TuiCommand.Settings>
{
    private readonly IDockerService _dockerService;
    private readonly IServiceHealthCheckService _healthCheckService;
    private readonly IServiceClientFactory _clientFactory;
    private bool _shouldExit = false;

    public class Settings : CommandSettings
    {
        [CommandOption("--refresh <SECONDS>")]
        [Description("Refresh interval in seconds (default: 2)")]
        [DefaultValue(2)]
        public int RefreshInterval { get; set; } = 2;
    }

    public TuiCommand(
        IDockerService dockerService,
        IServiceHealthCheckService healthCheckService,
        IServiceClientFactory clientFactory)
    {
        _dockerService = dockerService;
        _healthCheckService = healthCheckService;
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Check if Docker is available
        if (!await _dockerService.IsDockerAvailableAsync())
        {
            AnsiConsole.MarkupLine("[yellow]Docker is not available on this machine.[/]");
            AnsiConsole.MarkupLine("[dim]The TUI dashboard requires Docker to display service status.[/]");
            AnsiConsole.MarkupLine("[dim]Install Docker or check that the Docker daemon is running.[/]");
            return 0;
        }

        // Set up Ctrl+C handler
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _shouldExit = true;
        };

        await AnsiConsole.Live(CreateLayout())
            .AutoClear(true)
            .StartAsync(async ctx =>
            {
                while (!_shouldExit)
                {
                    try
                    {
                        var layout = await CreateLiveDashboard();
                        ctx.UpdateTarget(layout);
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Error updating dashboard: {ex.Message}[/]");
                        await Task.Delay(TimeSpan.FromSeconds(settings.RefreshInterval));
                    }
                }
            });

        AnsiConsole.MarkupLine("\n[yellow]Dashboard stopped.[/]");
        return 0;
    }

    private Layout CreateLayout()
    {
        return new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3),
                new Layout("Body"),
                new Layout("Footer").Size(3));
    }

    private async Task<Layout> CreateLiveDashboard()
    {
        var now = DateTime.Now;

        // Header
        var header = new Panel(
            Align.Center(
                new Markup($"[bold green]HomeLab Dashboard[/] [dim]|[/] {now:yyyy-MM-dd HH:mm:ss} [dim]|[/] Press [yellow]Ctrl+C[/] to exit"),
                VerticalAlignment.Middle))
            .BorderColor(Color.Green)
            .Padding(0, 0);

        // Get service health data
        var healthResults = await _healthCheckService.CheckAllServicesAsync();

        // Create service table
        var serviceTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[yellow]Service[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Type[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Docker[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Health[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Uptime[/]").Centered())
            .AddColumn(new TableColumn("[yellow]Details[/]"));

        foreach (var result in healthResults)
        {
            var dockerStatus = result.IsRunning ? "[green]✓ Running[/]" : "[red]✗ Stopped[/]";

            var healthStatus = result.IsHealthy
                ? "🟢 [green]Healthy[/]"
                : "🔴 [red]Unhealthy[/]";

            var uptime = "-"; // TODO: Add uptime tracking
            var details = result.Metrics.Count > 0
                ? Markup.Escape(string.Join(", ", result.Metrics.Select(kv => $"{kv.Key}: {kv.Value}")))
                : "[dim]No metrics[/]";

            serviceTable.AddRow(
                result.ServiceName,
                $"[dim]{result.ServiceType}[/]",
                dockerStatus,
                healthStatus,
                uptime,
                details
            );
        }

        // Stats summary
        var totalServices = healthResults.Count;
        var healthyCount = healthResults.Count(r => r.IsHealthy);
        var runningCount = healthResults.Count(r => r.IsRunning);

        var statsPanel = new Panel(
            new Markup($"[green]✓ Healthy:[/] {healthyCount}/{totalServices}  " +
                      $"[blue]▶ Running:[/] {runningCount}/{totalServices}  " +
                      $"[yellow]⚡ Total:[/] {totalServices}"))
            .Header("[yellow]Summary[/]")
            .BorderColor(Color.Yellow)
            .Padding(1, 0);

        // System info (if available)
        var systemInfo = await GetSystemInfo();
        var systemPanel = new Panel(systemInfo)
            .Header("[cyan]System[/]")
            .BorderColor(Color.Cyan)
            .Padding(1, 0);

        // Docker containers panel
        var containerInfo = await GetContainerInfo();
        var containerPanel = new Panel(containerInfo)
            .Header("[green]Containers[/]")
            .BorderColor(Color.Green)
            .Padding(1, 0);

        // Footer
        var footer = new Panel(
            Align.Center(
                new Markup("[dim]Shortcuts: [yellow]Ctrl+C[/] Exit | Live updates every {settings.RefreshInterval}s[/]"),
                VerticalAlignment.Middle))
            .BorderColor(Color.Grey)
            .Padding(0, 0);

        // Assemble layout with new panels
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(3).Update(header),
                new Layout("Body").SplitColumns(
                    new Layout("Left").SplitRows(
                        new Layout("Services").Update(serviceTable),
                        new Layout("Stats").Size(5).Update(statsPanel)
                    ),
                    new Layout("Right").Size(45).SplitRows(
                        new Layout("System").Update(systemPanel),
                        new Layout("Containers").Size(10).Update(containerPanel)
                    )
                ),
                new Layout("Footer").Size(3).Update(footer)
            );

        return layout;
    }

    private async Task<Markup> GetSystemInfo()
    {
        try
        {
            // Get Docker info
            var dockerInfo = await _dockerService.GetSystemInfoAsync();

            var info = new List<string>
            {
                $"[cyan]Docker:[/] {dockerInfo.ServerVersion ?? "Unknown"}",
                $"[cyan]OS:[/] {dockerInfo.OperatingSystem ?? "Unknown"}",
                $"[cyan]Architecture:[/] {dockerInfo.Architecture ?? "Unknown"}",
                $"[cyan]CPUs:[/] {dockerInfo.NCPU}",
                $"[cyan]Memory:[/] {FormatBytes(dockerInfo.MemTotal)}",
                "",
                $"[cyan]Containers:[/]",
                $"  Running: [green]{dockerInfo.ContainersRunning}[/]",
                $"  Stopped: [yellow]{dockerInfo.ContainersStopped}[/]",
                $"  Total: {dockerInfo.Containers}",
                "",
                $"[cyan]Images:[/] {dockerInfo.Images}"
            };

            return new Markup(string.Join("\n", info));
        }
        catch
        {
            return new Markup("[red]Failed to get system info[/]");
        }
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private async Task<Markup> GetContainerInfo()
    {
        try
        {
            var containers = await _dockerService.ListContainersAsync(onlyHomelab: false);

            if (containers.Count == 0)
            {
                return new Markup("[dim]No containers found[/]");
            }

            var info = new List<string>();
            foreach (var container in containers.Take(8))
            {
                var icon = container.IsRunning ? "[green]>[/]" : "[red]x[/]";
                info.Add($"{icon} {Markup.Escape(container.Name)}");
            }

            if (containers.Count > 8)
            {
                info.Add($"[dim]... +{containers.Count - 8} more[/]");
            }

            return new Markup(string.Join("\n", info));
        }
        catch
        {
            return new Markup("[dim]Container data unavailable[/]");
        }
    }
}
