using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Vpn;

public class VpnStatusCommand : AsyncCommand<VpnStatusCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public VpnStatusCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("VPN Status")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTailscaleClient();

        if (!await client.IsTailscaleInstalledAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Tailscale CLI is not installed");
            AnsiConsole.MarkupLine("\nInstall with: [cyan]brew install tailscale[/]");
            return 1;
        }

        await AnsiConsole.Status()
            .StartAsync("Checking Tailscale status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var status = await client.GetStatusAsync();

        var statusColor = status.IsConnected ? "green" : "red";
        var statusIcon = status.IsConnected ? "✓" : "✗";
        AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon}[/] Tailscale is [bold]{status.BackendState}[/]\n");

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, status))
        {
            return 0;
        }

        // Connection info panel
        var connectionGrid = new Grid();
        connectionGrid.AddColumn();
        connectionGrid.AddColumn();

        connectionGrid.AddRow(
            new Markup("[yellow]Tailnet:[/]"),
            new Markup($"[cyan]{status.TailnetName ?? "N/A"}[/]"));
        connectionGrid.AddRow(
            new Markup("[yellow]Backend State:[/]"),
            new Markup($"[cyan]{status.BackendState}[/]"));

        if (status.Self != null)
        {
            connectionGrid.AddRow(
                new Markup("[yellow]Hostname:[/]"),
                new Markup($"[cyan]{status.Self.HostName}[/]"));
            connectionGrid.AddRow(
                new Markup("[yellow]Tailscale IP:[/]"),
                new Markup($"[cyan]{status.Self.PrimaryIP ?? "N/A"}[/]"));
            connectionGrid.AddRow(
                new Markup("[yellow]DNS Name:[/]"),
                new Markup($"[dim]{status.Self.DNSName}[/]"));
        }

        var version = await client.GetVersionAsync();
        connectionGrid.AddRow(
            new Markup("[yellow]Version:[/]"),
            new Markup($"[dim]{version ?? "Unknown"}[/]"));

        AnsiConsole.Write(
            new Panel(connectionGrid)
                .Header("[yellow]Connection Info[/]")
                .BorderColor(Color.Green)
                .RoundedBorder());

        // Peers table
        if (status.IsConnected && status.Peers.Count > 0)
        {
            AnsiConsole.WriteLine();

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[yellow]Hostname[/]");
            table.AddColumn("[yellow]Status[/]");
            table.AddColumn("[yellow]Tailscale IP[/]");
            table.AddColumn("[yellow]OS[/]");
            table.AddColumn("[yellow]Last Seen[/]");

            foreach (var peer in status.Peers.OrderBy(p => p.HostName))
            {
                var onlineColor = peer.Online ? "green" : "red";
                var onlineText = peer.Online ? "Online" : "Offline";
                var lastSeen = peer.LastSeen.HasValue
                    ? FormatTimeAgo(peer.LastSeen.Value)
                    : "Never";

                table.AddRow(
                    peer.HostName,
                    $"[{onlineColor}]{onlineText}[/]",
                    peer.PrimaryIP ?? "N/A",
                    peer.OS,
                    $"[dim]{lastSeen}[/]");
            }

            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            var onlinePeers = status.Peers.Count(p => p.Online);
            AnsiConsole.Write(
                new Panel($"[green]Online:[/] {onlinePeers}  [yellow]Total:[/] {status.Peers.Count}")
                    .Header("[yellow]Peers[/]")
                    .BorderColor(Color.Grey)
                    .RoundedBorder());
        }
        else if (!status.IsConnected)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Tailscale is not connected.[/]");
            AnsiConsole.MarkupLine("Use [cyan]homelab vpn up[/] to connect.");
        }

        return 0;
    }

    private static string FormatTimeAgo(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;
        if (timeAgo.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (timeAgo.TotalMinutes < 60)
        {
            return $"{(int)timeAgo.TotalMinutes}m ago";
        }

        if (timeAgo.TotalHours < 24)
        {
            return $"{(int)timeAgo.TotalHours}h ago";
        }

        return $"{(int)timeAgo.TotalDays}d ago";
    }
}
