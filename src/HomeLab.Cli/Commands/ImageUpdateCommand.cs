using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;
using System.ComponentModel;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Updates container images.
/// Usage: homelab image-update <image>
/// </summary>
public class ImageUpdateCommand : AsyncCommand<ImageUpdateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<image>")]
        [Description("Image name to update (e.g., nginx, postgres:14)")]
        public string ImageName { get; set; } = string.Empty;
    }

    private readonly IDockerService _dockerService;

    public ImageUpdateCommand(IDockerService dockerService)
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
            await AnsiConsole.Status()
                .StartAsync($"Pulling latest image: {settings.ImageName}...", async ctx =>
                {
                    await _dockerService.PullImageAsync(settings.ImageName);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Successfully pulled latest [blue]{settings.ImageName}[/]");
            AnsiConsole.MarkupLine("[dim]Remember to restart containers to use the new image[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
