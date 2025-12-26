using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Displays the homelab status dashboard.
/// Shows running containers, resource usage, and health checks.
/// </summary>
public class StatusCommand : AsyncCommand
{
    // Dependency injection - we'll explain this
    private readonly IDockerService _dockerService;

    public StatusCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        // Show a fancy header
        AnsiConsole.Write(
            new FigletText("Homelab Status")
                .Centered()
                .Color(Color.Green));

        // Get container info from Docker
        var containers = await _dockerService.ListContainersAsync();

        // Create a table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Container[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Uptime[/]");

        // Fill table with data
        foreach (var container in containers)
        {
            var statusColor = container.IsRunning ? "green" : "red";
            var statusText = container.IsRunning ? "Running" : "Stopped";

            table.AddRow(
                container.Name,
                $"[{statusColor}]{statusText}[/]",
                container.Uptime
            );
        }

        // Display the table
        AnsiConsole.Write(table);

        // Return 0 = success (Unix convention)
        return 0;
    }
}
