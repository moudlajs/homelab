using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Commands.Vpn;

/// <summary>
/// Removes a VPN peer.
/// </summary>
public class VpnRemovePeerCommand : AsyncCommand<VpnRemovePeerCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public VpnRemovePeerCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name of the VPN peer to remove")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("-f|--force")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool Force { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateWireGuardClient();

        // Check if peer exists
        var peers = await client.GetPeersAsync();
        var peer = peers.FirstOrDefault(p => p.Name.Equals(settings.Name, StringComparison.OrdinalIgnoreCase));

        if (peer == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Peer '{settings.Name}' not found.");
            AnsiConsole.MarkupLine($"\nAvailable peers:");

            if (peers.Count == 0)
            {
                AnsiConsole.MarkupLine($"  [dim]No peers configured[/]");
            }
            else
            {
                foreach (var p in peers)
                {
                    AnsiConsole.MarkupLine($"  - {p.Name}");
                }
            }

            return 1;
        }

        // Confirm removal unless forced
        if (!settings.Force)
        {
            var confirm = AnsiConsole.Confirm(
                $"Are you sure you want to remove peer [yellow]{peer.Name}[/] ({peer.AllowedIPs})?",
                defaultValue: false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]Removal cancelled.[/]");
                return 0;
            }
        }

        // Remove the peer
        try
        {
            await AnsiConsole.Status()
                .StartAsync($"Removing peer '{peer.Name}'...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await client.RemovePeerAsync(peer.Name);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Peer '{peer.Name}' removed successfully!");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to remove peer: {ex.Message}");
            return 1;
        }

        return 0;
    }
}
