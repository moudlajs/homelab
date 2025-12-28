using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Lists all tracked network devices from ntopng.
/// </summary>
public class NetworkDevicesCommand : AsyncCommand<NetworkDevicesCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--active")]
        [Description("Show only active devices")]
        public bool ActiveOnly { get; set; }

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public NetworkDevicesCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Network Devices")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateNtopngClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking ntopng status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300, cancellationToken);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] ntopng is not available: {healthInfo.Message}");
            AnsiConsole.MarkupLine("[dim]Using mock data for demonstration[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] ntopng is healthy\n");
        }

        // Get devices
        List<HomeLab.Cli.Models.DeviceTraffic>? devices = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Fetching tracked devices...", async ctx =>
            {
                devices = await client.GetDevicesAsync();
            });

        if (devices == null || devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No devices found[/]");
            return 0;
        }

        // Filter active devices if requested
        if (settings.ActiveOnly)
        {
            devices = devices.Where(d => d.IsActive).ToList();
        }

        AnsiConsole.MarkupLine($"[green]Found {devices.Count} device(s)[/]");
        if (settings.ActiveOnly)
        {
            AnsiConsole.MarkupLine($"[dim]Showing only active devices[/]");
        }
        AnsiConsole.WriteLine();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, devices))
        {
            return 0;
        }

        // Create table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[cyan]Device Name[/]");
        table.AddColumn("[cyan]IP Address[/]");
        table.AddColumn("[cyan]MAC Address[/]");
        table.AddColumn("[cyan]First Seen[/]");
        table.AddColumn("[cyan]Last Seen[/]");
        table.AddColumn("[cyan]Sent[/]");
        table.AddColumn("[cyan]Received[/]");
        table.AddColumn("[cyan]Status[/]");

        foreach (var device in devices.OrderByDescending(d => d.BytesSent + d.BytesReceived))
        {
            var status = device.IsActive ? "[green]Active[/]" : "[dim]Inactive[/]";
            var firstSeen = device.FirstSeen.ToString("yyyy-MM-dd");
            var lastSeen = FormatRelativeTime(device.LastSeen);

            table.AddRow(
                device.DeviceName,
                device.IpAddress,
                device.MacAddress,
                firstSeen,
                lastSeen,
                FormatBytes(device.BytesSent),
                FormatBytes(device.BytesReceived),
                status
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Data collected at {DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";

        return dateTime.ToString("yyyy-MM-dd");
    }
}
