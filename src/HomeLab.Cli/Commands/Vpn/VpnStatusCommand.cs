using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Commands.Vpn;

/// <summary>
/// Displays VPN peer status and statistics.
/// </summary>
public class VpnStatusCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;

    public VpnStatusCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("VPN Status")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        // Get WireGuard client
        var client = _clientFactory.CreateWireGuardClient();

        // Check health first
        await AnsiConsole.Status()
            .StartAsync("Checking WireGuard status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] WireGuard service is not healthy: {healthInfo.Message}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] WireGuard service is healthy\n");

        // Get all peers
        var peers = await client.GetPeersAsync();

        if (peers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No VPN peers configured.[/]");
            AnsiConsole.MarkupLine("\nUse [cyan]homelab vpn add-peer <name>[/] to add a peer.");
            return 0;
        }

        // Create peers table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Peer Name[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]IP Address[/]");
        table.AddColumn("[yellow]Last Handshake[/]");
        table.AddColumn("[yellow]Transfer[/]");

        foreach (var peer in peers)
        {
            var statusIcon = peer.IsActive ? "🟢" : "🔴";
            var statusColor = peer.IsActive ? "green" : "red";
            var statusText = peer.IsActive ? "Active" : "Inactive";

            var lastHandshake = peer.LastHandshake.HasValue
                ? FormatTimeAgo(peer.LastHandshake.Value)
                : "Never";

            var transfer = $"↓ {FormatBytes(peer.BytesReceived)} / ↑ {FormatBytes(peer.BytesSent)}";

            table.AddRow(
                peer.Name,
                $"{statusIcon} [{statusColor}]{statusText}[/]",
                peer.AllowedIPs,
                $"[dim]{lastHandshake}[/]",
                $"[dim]{transfer}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary
        AnsiConsole.WriteLine();
        var activePeers = peers.Count(p => p.IsActive);
        var totalPeers = peers.Count;

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[green]Active Peers:[/] {activePeers}",
            $"[yellow]Total Peers:[/] {totalPeers}"
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Summary[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder()
        );

        return 0;
    }

    private string FormatTimeAgo(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalMinutes < 1)
            return "Just now";
        if (timeAgo.TotalMinutes < 60)
            return $"{(int)timeAgo.TotalMinutes}m ago";
        if (timeAgo.TotalHours < 24)
            return $"{(int)timeAgo.TotalHours}h ago";
        return $"{(int)timeAgo.TotalDays}d ago";
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
