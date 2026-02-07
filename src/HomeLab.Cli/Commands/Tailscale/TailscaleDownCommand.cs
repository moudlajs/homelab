using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tailscale;

public class TailscaleDownCommand : AsyncCommand<TailscaleDownCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings { }

    public TailscaleDownCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateTailscaleClient();

        if (!await client.IsTailscaleInstalledAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Tailscale CLI is not installed");
            return 1;
        }

        var currentStatus = await client.GetStatusAsync();
        if (!currentStatus.IsConnected)
        {
            AnsiConsole.MarkupLine("[yellow]![/] Tailscale is already disconnected");
            return 0;
        }

        await AnsiConsole.Status()
            .StartAsync("Disconnecting from Tailscale...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await client.DisconnectAsync();
            });

        AnsiConsole.MarkupLine("[green]✓[/] Disconnected from Tailscale");

        return 0;
    }
}
