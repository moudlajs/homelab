using System.ComponentModel;
using HomeLab.Cli.Services.Docker;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Handles service lifecycle operations (start, stop, restart).
/// Usage: homelab service start adguard
/// </summary>
public class ServiceCommand : AsyncCommand<ServiceCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action to perform: start, stop, restart")]
        public string Action { get; set; } = string.Empty;

        [CommandArgument(1, "<service>")]
        [Description("Service name (e.g., adguard, wireguard)")]
        public string ServiceName { get; set; } = string.Empty;
    }

    private readonly IDockerService _dockerService;

    public ServiceCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        // Validate action
        var validActions = new[] { "start", "stop", "restart" };
        if (!validActions.Contains(settings.Action.ToLower()))
        {
            AnsiConsole.MarkupLine(
                "[red]Invalid action.[/] Valid: start, stop, restart");
            return 1; // Error exit code
        }

        // Perform action
        try
        {
            await AnsiConsole.Status()
                .StartAsync($"{settings.Action}ing {settings.ServiceName}...",
                async ctx =>
            {
                switch (settings.Action.ToLower())
                {
                    case "start":
                        await _dockerService.StartContainerAsync(
                            settings.ServiceName);
                        break;
                    case "stop":
                        await _dockerService.StopContainerAsync(
                            settings.ServiceName);
                        break;
                    case "restart":
                        await _dockerService.StopContainerAsync(
                            settings.ServiceName);
                        await Task.Delay(2000); // Wait 2s
                        await _dockerService.StartContainerAsync(
                            settings.ServiceName);
                        break;
                }
            });

            AnsiConsole.MarkupLine(
                $"[green]✓[/] Successfully {settings.Action}ed {settings.ServiceName}");

            return 0; // Success
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1; // Error
        }
    }
}
