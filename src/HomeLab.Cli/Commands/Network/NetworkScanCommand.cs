using System.ComponentModel;
using HomeLab.Cli.Services.Network;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Scans the network for active devices.
/// </summary>
public class NetworkScanCommand : AsyncCommand<NetworkScanCommand.Settings>
{
    private readonly INmapService _nmapService;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--range <CIDR>")]
        [Description("Network range to scan (e.g., 192.168.1.0/24)")]
        public string? Range { get; set; }

        [CommandOption("--quick")]
        [Description("Quick scan (no port detection, faster)")]
        public bool Quick { get; set; }

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public NetworkScanCommand(INmapService nmapService, IOutputFormatter formatter)
    {
        _nmapService = nmapService;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Network Scan")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.WriteLine();

        // Check if nmap is available
        if (!_nmapService.IsNmapAvailable())
        {
            AnsiConsole.MarkupLine("[red]✗[/] nmap is not installed");
            AnsiConsole.MarkupLine("[yellow]Install it with:[/] [cyan]brew install nmap[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]✓[/] nmap is available\n");

        // Default network range
        var networkRange = settings.Range ?? "192.168.1.0/24";

        // Scan network
        List<HomeLab.Cli.Models.NetworkDevice>? devices = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Scanning network {networkRange}...", async ctx =>
            {
                devices = await _nmapService.ScanNetworkAsync(networkRange, settings.Quick);
            });

        if (devices == null || devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No devices found[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Found {devices.Count} device(s)[/]\n");

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, devices))
        {
            return 0;
        }

        // Create table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[cyan]IP Address[/]");
        table.AddColumn("[cyan]MAC Address[/]");
        table.AddColumn("[cyan]Hostname[/]");
        table.AddColumn("[cyan]Vendor[/]");
        table.AddColumn("[cyan]Open Ports[/]");
        table.AddColumn("[cyan]OS Guess[/]");

        foreach (var device in devices.OrderBy(d => d.IpAddress))
        {
            var ports = device.OpenPorts.Count > 0
                ? string.Join(", ", device.OpenPorts.Take(10))  // Show first 10 ports
                : "-";

            if (device.OpenPorts.Count > 10)
            {
                ports += $" (+{device.OpenPorts.Count - 10} more)";
            }

            table.AddRow(
                device.IpAddress,
                device.MacAddress ?? "-",
                device.Hostname ?? "-",
                device.Vendor ?? "-",
                ports,
                device.OsGuess ?? "-"
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Scan completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        return 0;
    }
}
