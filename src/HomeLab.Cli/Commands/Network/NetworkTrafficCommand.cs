using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Displays network traffic statistics.
/// </summary>
public class NetworkTrafficCommand : AsyncCommand<NetworkTrafficCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--device <IP>")]
        [Description("Show stats for specific device only")]
        public string? Device { get; set; }

        [CommandOption("--top <N>")]
        [Description("Number of top talkers to show (default: 10)")]
        public int TopCount { get; set; } = 10;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public NetworkTrafficCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Network Traffic")
                .Centered()
                .Color(Color.Purple));

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

        // Get traffic stats
        HomeLab.Cli.Models.TrafficStats? stats = null;

        var statusMessage = settings.Device != null
            ? $"Fetching traffic stats for {settings.Device}..."
            : "Fetching network traffic stats...";

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(statusMessage, async ctx =>
            {
                stats = await client.GetTrafficStatsAsync(settings.Device);
            });

        if (stats == null)
        {
            AnsiConsole.MarkupLine("[yellow]No traffic stats available[/]");
            return 0;
        }

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, stats))
        {
            return 0;
        }

        // Display overview
        var overviewPanel = new Panel(new Markup(
            $"[cyan]Total Traffic:[/] {FormatBytes(stats.TotalBytesTransferred)}\n" +
            $"[cyan]Active Flows:[/] {stats.ActiveFlows}\n" +
            $"[cyan]Collected:[/] {stats.CollectedAt:yyyy-MM-dd HH:mm:ss}"
        ))
        {
            Header = new PanelHeader("[yellow]Traffic Overview[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(overviewPanel);
        AnsiConsole.WriteLine();

        // Top Talkers Table
        if (stats.TopTalkers.Count > 0)
        {
            var topTalkersTable = new Table();
            topTalkersTable.Border(TableBorder.Rounded);
            topTalkersTable.Title = new TableTitle("[yellow]Top Talkers[/]");
            topTalkersTable.AddColumn("[cyan]Rank[/]");
            topTalkersTable.AddColumn("[cyan]Device[/]");
            topTalkersTable.AddColumn("[cyan]IP Address[/]");
            topTalkersTable.AddColumn("[cyan]Total Traffic[/]");
            topTalkersTable.AddColumn("[cyan]Sent[/]");
            topTalkersTable.AddColumn("[cyan]Received[/]");
            topTalkersTable.AddColumn("[cyan]% of Total[/]");

            var topTalkers = stats.TopTalkers.Take(settings.TopCount).ToList();
            for (int i = 0; i < topTalkers.Count; i++)
            {
                var talker = topTalkers[i];
                var percentage = stats.TotalBytesTransferred > 0
                    ? (talker.TotalBytes * 100.0 / stats.TotalBytesTransferred)
                    : 0;

                var rankColor = i switch
                {
                    0 => "gold1",
                    1 => "silver",
                    2 => "orange3",
                    _ => "white"
                };

                topTalkersTable.AddRow(
                    $"[{rankColor}]{i + 1}[/]",
                    talker.DeviceName,
                    talker.IpAddress,
                    $"[green]{FormatBytes(talker.TotalBytes)}[/]",
                    FormatBytes(talker.BytesSent),
                    FormatBytes(talker.BytesReceived),
                    $"{percentage:F1}%"
                );
            }

            AnsiConsole.Write(topTalkersTable);
            AnsiConsole.WriteLine();
        }

        // Protocol Statistics
        if (stats.ProtocolStats.Count > 0)
        {
            var protocolChart = new BarChart()
                .Width(60)
                .Label("[yellow]Traffic by Protocol[/]");

            foreach (var protocol in stats.ProtocolStats.OrderByDescending(p => p.Value).Take(10))
            {
                var mb = protocol.Value / (1024.0 * 1024.0);
                protocolChart.AddItem(protocol.Key, mb, Color.Blue);
            }

            AnsiConsole.Write(protocolChart);
            AnsiConsole.WriteLine();
        }

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
}
