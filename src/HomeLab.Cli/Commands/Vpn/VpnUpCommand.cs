using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Vpn;

public class VpnUpCommand : AsyncCommand<VpnUpCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings { }

    public VpnUpCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateTailscaleClient();

        if (!await client.IsTailscaleInstalledAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Tailscale CLI is not installed");
            AnsiConsole.MarkupLine("\nInstall with: [cyan]brew install tailscale[/]");
            return 1;
        }

        var currentStatus = await client.GetStatusAsync();
        if (currentStatus.IsConnected)
        {
            AnsiConsole.MarkupLine("[yellow]![/] Already connected to Tailscale");
            AnsiConsole.MarkupLine($"  Tailnet: [cyan]{currentStatus.TailnetName}[/]");
            if (currentStatus.Self != null)
            {
                AnsiConsole.MarkupLine($"  IP: [cyan]{currentStatus.Self.PrimaryIP}[/]");
            }

            return 0;
        }

        await AnsiConsole.Status()
            .StartAsync("Connecting to Tailscale...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await client.ConnectAsync();
            });

        AnsiConsole.MarkupLine("[green]✓[/] Connected to Tailscale");

        var status = await client.GetStatusAsync();
        if (status.Self != null)
        {
            AnsiConsole.MarkupLine($"  Tailnet: [cyan]{status.TailnetName}[/]");
            AnsiConsole.MarkupLine($"  IP: [cyan]{status.Self.PrimaryIP}[/]");
            AnsiConsole.MarkupLine($"  Hostname: [cyan]{status.Self.HostName}[/]");
        }

        return 0;
    }
}
