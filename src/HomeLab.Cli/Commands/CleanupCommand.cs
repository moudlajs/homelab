using System.ComponentModel;
using HomeLab.Cli.Services.Docker;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Cleans up unused Docker resources.
/// Usage: homelab cleanup [--volumes]
/// </summary>
public class CleanupCommand : AsyncCommand<CleanupCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-v|--volumes")]
        [Description("Also remove unused volumes")]
        [DefaultValue(false)]
        public bool IncludeVolumes { get; set; }

        [CommandOption("-f|--force")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool Force { get; set; }
    }

    private readonly IDockerService _dockerService;

    public CleanupCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            // Show warning
            var warningPanel = new Panel(
                "[yellow]This will remove:[/]\n" +
                "• Stopped containers\n" +
                "• Dangling images (not used by any container)\n" +
                (settings.IncludeVolumes ? "• Unused volumes\n" : "") +
                "\n[dim]Running containers and their images will not be affected[/]")
            {
                Header = new PanelHeader("⚠️  Cleanup Warning"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(warningPanel);
            AnsiConsole.WriteLine();

            // Confirm unless forced
            if (!settings.Force)
            {
                var confirm = AnsiConsole.Confirm("Do you want to proceed?");
                if (!confirm)
                {
                    AnsiConsole.MarkupLine("[yellow]Cleanup cancelled[/]");
                    return 0;
                }
            }

            CleanupResult result;

            await AnsiConsole.Status()
                .StartAsync("Cleaning up Docker resources...", async ctx =>
                {
                    result = await _dockerService.CleanupAsync(settings.IncludeVolumes);
                });

            result = await _dockerService.CleanupAsync(settings.IncludeVolumes);

            // Show results
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[yellow]Resource[/]");
            table.AddColumn("[yellow]Removed[/]");

            table.AddRow("Containers", result.RemovedContainers.ToString());
            table.AddRow("Images", result.RemovedImages.ToString());
            if (settings.IncludeVolumes)
            {
                table.AddRow("Volumes", result.RemovedVolumes.ToString());
            }

            var spaceMB = result.SpaceReclaimed / (1024.0 * 1024.0);
            table.AddRow("Space Reclaimed", $"{spaceMB:F2} MB");

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"\n[green]✓[/] Cleanup completed successfully");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
