using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.UptimeKuma;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Uptime;

/// <summary>
/// Add a new service to uptime monitoring.
/// </summary>
public class UptimeAddCommand : AsyncCommand<UptimeAddCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public UptimeAddCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Name of the service to monitor")]
        public string Name { get; set; } = string.Empty;

        [CommandArgument(1, "<URL>")]
        [Description("URL to monitor (e.g., http://localhost:3000)")]
        public string Url { get; set; } = string.Empty;

        [CommandOption("--type <TYPE>")]
        [Description("Monitor type: http, port, ping (default: http)")]
        [DefaultValue("http")]
        public string Type { get; set; } = "http";
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine($"[yellow]Adding monitor:[/] {settings.Name}");
        AnsiConsole.MarkupLine($"[dim]URL:[/] {settings.Url}");
        AnsiConsole.MarkupLine($"[dim]Type:[/] {settings.Type}");
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateUptimeKumaClient();

        // Add monitor
        var success = await AnsiConsole.Status()
            .StartAsync("Adding monitor to Uptime Kuma...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                return await client.AddMonitorAsync(settings.Name, settings.Url, settings.Type);
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]✓ Successfully added monitor '{settings.Name}'[/]");
            AnsiConsole.MarkupLine("[dim]Use 'homelab uptime status' to view all monitors[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to add monitor '{settings.Name}'[/]");
            AnsiConsole.MarkupLine("[yellow]Tip: Check if Uptime Kuma is running and accessible[/]");
            return 1;
        }
    }
}
