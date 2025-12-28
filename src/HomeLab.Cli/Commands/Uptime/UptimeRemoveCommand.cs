using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.UptimeKuma;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Uptime;

/// <summary>
/// Remove a monitor from uptime tracking.
/// </summary>
public class UptimeRemoveCommand : AsyncCommand<UptimeRemoveCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public UptimeRemoveCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ID>")]
        [Description("Monitor ID to remove")]
        public int MonitorId { get; set; }

        [CommandOption("--force")]
        [Description("Skip confirmation prompt")]
        public bool Force { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateUptimeKumaClient();

        // Get monitor details first
        UptimeMonitor? monitor = null;

        await AnsiConsole.Status()
            .StartAsync("Fetching monitor details...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                monitor = await client.GetMonitorAsync(settings.MonitorId);
            });

        if (monitor == null)
        {
            // If we can't get the monitor, show all monitors to help user
            var allMonitors = await client.GetMonitorsAsync();

            if (allMonitors.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No monitors found.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[red]✗ Monitor ID {settings.MonitorId} not found.[/]\n");
            AnsiConsole.MarkupLine("[yellow]Available monitors:[/]");

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[yellow]ID[/]");
            table.AddColumn("[yellow]Name[/]");
            table.AddColumn("[yellow]URL[/]");

            foreach (var m in allMonitors)
            {
                table.AddRow(m.Id.ToString(), m.Name, $"[dim]{m.Url}[/]");
            }

            AnsiConsole.Write(table);
            return 1;
        }

        // Show what will be removed
        AnsiConsole.WriteLine();
        var infoPanel = new Panel(
            new Markup($"[yellow]ID:[/] {monitor.Id}\n" +
                      $"[yellow]Name:[/] {monitor.Name}\n" +
                      $"[yellow]URL:[/] {monitor.Url}\n" +
                      $"[yellow]Type:[/] {monitor.Type}"))
            .Header("[red]Monitor to Remove[/]")
            .BorderColor(Color.Red)
            .RoundedBorder();

        AnsiConsole.Write(infoPanel);
        AnsiConsole.WriteLine();

        // Confirm removal unless --force is used
        if (!settings.Force)
        {
            var confirm = AnsiConsole.Confirm(
                $"[red]Are you sure you want to remove monitor '{monitor.Name}'?[/]",
                false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                return 0;
            }
        }

        // Remove monitor
        var success = await AnsiConsole.Status()
            .StartAsync("Removing monitor from Uptime Kuma...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                return await client.RemoveMonitorAsync(settings.MonitorId);
            });

        if (success)
        {
            AnsiConsole.MarkupLine($"[green]✓ Successfully removed monitor '{monitor.Name}'[/]");
            AnsiConsole.MarkupLine("[dim]Use 'homelab uptime status' to view remaining monitors[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗ Failed to remove monitor '{monitor.Name}'[/]");
            AnsiConsole.MarkupLine("[yellow]Tip: Check if Uptime Kuma is running and accessible[/]");
            return 1;
        }
    }
}
