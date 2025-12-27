using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Quick;

/// <summary>
/// Quick restart - stop and start a service with no confirmation.
/// Fast operation for daily use.
/// </summary>
public class QuickRestartCommand : AsyncCommand<QuickRestartCommand.Settings>
{
    private readonly IDockerService _dockerService;

    public QuickRestartCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SERVICE>")]
        [Description("Service name to restart (e.g., adguard, wireguard)")]
        public string ServiceName { get; set; } = string.Empty;

        [CommandOption("--wait <SECONDS>")]
        [Description("Wait time between stop and start (default: 2 seconds)")]
        [DefaultValue(2)]
        public int WaitSeconds { get; set; } = 2;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var containerName = $"homelab_{settings.ServiceName}";

        AnsiConsole.MarkupLine($"[yellow]⚡ Quick restart:[/] {settings.ServiceName}");
        AnsiConsole.WriteLine();

        try
        {
            // Stop the container
            await AnsiConsole.Status()
                .StartAsync($"Stopping {settings.ServiceName}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _dockerService.StopContainerAsync(containerName);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Stopped {settings.ServiceName}");

            // Wait a bit
            if (settings.WaitSeconds > 0)
            {
                AnsiConsole.MarkupLine($"[dim]Waiting {settings.WaitSeconds} seconds...[/]");
                await Task.Delay(TimeSpan.FromSeconds(settings.WaitSeconds), cancellationToken);
            }

            // Start the container
            await AnsiConsole.Status()
                .StartAsync($"Starting {settings.ServiceName}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _dockerService.StartContainerAsync(containerName);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Started {settings.ServiceName}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green bold]✓ Quick restart completed![/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
            return 1;
        }
    }
}
