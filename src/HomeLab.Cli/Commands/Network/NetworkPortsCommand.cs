using System.ComponentModel;
using HomeLab.Cli.Services.Network;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Scans ports on a specific device.
/// </summary>
public class NetworkPortsCommand : AsyncCommand<NetworkPortsCommand.Settings>
{
    private readonly INmapService _nmapService;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--device <IP>")]
        [Description("IP address of device to scan")]
        public string? Device { get; set; }

        [CommandOption("--common")]
        [Description("Scan only common ports (faster)")]
        public bool CommonOnly { get; set; } = true;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public NetworkPortsCommand(INmapService nmapService, IOutputFormatter formatter)
    {
        _nmapService = nmapService;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Port Scan")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        // Check if nmap is available
        if (!_nmapService.IsNmapAvailable())
        {
            AnsiConsole.MarkupLine("[red]✗[/] nmap is not installed");
            AnsiConsole.MarkupLine("[yellow]Install it with:[/] [cyan]brew install nmap[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(settings.Device))
        {
            AnsiConsole.MarkupLine("[red]✗[/] Please specify a device IP address with --device");
            AnsiConsole.MarkupLine("[yellow]Example:[/] [cyan]homelab network ports --device 192.168.1.10[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]✓[/] nmap is available\n");

        // Scan ports
        List<HomeLab.Cli.Models.PortScanResult>? results = null;

        var scanType = settings.CommonOnly ? "common ports" : "ports 1-1000";
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Scanning {scanType} on {settings.Device}...", async ctx =>
            {
                results = await _nmapService.ScanPortsAsync(settings.Device, settings.CommonOnly);
            });

        if (results == null || results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No open ports found[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]Found {results.Count} open port(s)[/]\n");

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, results))
        {
            return 0;
        }

        // Create table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[cyan]Port[/]");
        table.AddColumn("[cyan]Protocol[/]");
        table.AddColumn("[cyan]State[/]");
        table.AddColumn("[cyan]Service[/]");
        table.AddColumn("[cyan]Version[/]");

        foreach (var result in results.OrderBy(r => r.Port))
        {
            var stateColor = result.State == "open" ? "green" : "yellow";
            var version = result.Version ?? "-";
            if (!string.IsNullOrEmpty(result.ExtraInfo))
            {
                version += $" ({result.ExtraInfo})";
            }

            table.AddRow(
                result.Port.ToString(),
                result.Protocol,
                $"[{stateColor}]{result.State}[/]",
                result.Service,
                version
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Scan completed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        return 0;
    }
}
