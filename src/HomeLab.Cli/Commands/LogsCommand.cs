using System.ComponentModel;
using HomeLab.Cli.Services.Docker;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Displays container logs.
/// Usage: homelab logs <container> [--lines 100]
/// </summary>
public class LogsCommand : AsyncCommand<LogsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<container>")]
        [Description("Container name to view logs from")]
        public string ContainerName { get; set; } = string.Empty;

        [CommandOption("-n|--lines <COUNT>")]
        [Description("Number of lines to show (default: 100)")]
        [DefaultValue(100)]
        public int Lines { get; set; } = 100;

        [CommandOption("-f|--follow")]
        [Description("Follow log output (not yet implemented)")]
        [DefaultValue(false)]
        public bool Follow { get; set; }
    }

    private readonly IDockerService _dockerService;

    public LogsCommand(IDockerService dockerService)
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
            string logs;

            await AnsiConsole.Status()
                .StartAsync($"Fetching logs for {settings.ContainerName}...", async ctx =>
                {
                    logs = await _dockerService.GetContainerLogsAsync(
                        settings.ContainerName,
                        settings.Lines);
                });

            logs = await _dockerService.GetContainerLogsAsync(
                settings.ContainerName,
                settings.Lines);

            if (string.IsNullOrWhiteSpace(logs))
            {
                AnsiConsole.MarkupLine("[yellow]No logs found[/]");
                return 0;
            }

            var panel = new Panel(logs)
            {
                Header = new PanelHeader($"📋 Logs: {settings.ContainerName} (last {settings.Lines} lines)"),
                Border = BoxBorder.Rounded
            };

            AnsiConsole.Write(panel);

            if (settings.Follow)
            {
                AnsiConsole.MarkupLine("[dim]Note: Follow mode not yet implemented[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }
}
