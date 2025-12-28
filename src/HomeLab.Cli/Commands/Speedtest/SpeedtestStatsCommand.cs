using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Speedtest;

/// <summary>
/// Display internet speed test statistics and history.
/// Shows download/upload speeds and ping over time.
/// </summary>
public class SpeedtestStatsCommand : AsyncCommand<SpeedtestStatsCommand.Settings>
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

    public SpeedtestStatsCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Speed Stats")
                .Centered()
                .Color(Color.Cyan1));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateSpeedtestClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking Speedtest Tracker...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Speedtest Tracker is not healthy: {healthInfo.Message}");
            AnsiConsole.MarkupLine("[yellow]Note: Showing mock data for demonstration[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Speedtest Tracker is healthy\n");
        }

        // Get stats and recent results
        var stats = await client.GetStatsAsync(30);
        var recentResults = await client.GetRecentResultsAsync(5);

        // Try export if requested (export recent results)
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, recentResults))
        {
            return 0;
        }

        // Create stats panel
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            new Markup("[yellow]Average Download:[/]"),
            new Markup($"[cyan]{stats.AvgDownload:F1} Mbps[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Average Upload:[/]"),
            new Markup($"[cyan]{stats.AvgUpload:F1} Mbps[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Average Ping:[/]"),
            new Markup($"[cyan]{stats.AvgPing:F1} ms[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Speed Range:[/]"),
            new Markup($"[dim]{stats.MinDownload:F1} - {stats.MaxDownload:F1} Mbps[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Total Tests (30d):[/]"),
            new Markup($"[dim]{stats.TotalTests}[/]")
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]30-Day Statistics[/]")
                .BorderColor(Color.Green)
                .RoundedBorder()
        );

        // Recent results table
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow bold]Recent Test Results:[/]\n");

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Time[/]");
        table.AddColumn("[yellow]Download[/]");
        table.AddColumn("[yellow]Upload[/]");
        table.AddColumn("[yellow]Ping[/]");
        table.AddColumn("[yellow]Server[/]");

        foreach (var result in recentResults.OrderByDescending(r => r.Timestamp))
        {
            var downloadColor = result.DownloadSpeed >= 400 ? "green" :
                               result.DownloadSpeed >= 300 ? "yellow" : "red";

            table.AddRow(
                $"[dim]{result.Timestamp:MM-dd HH:mm}[/]",
                $"[{downloadColor}]{result.DownloadSpeed:F1} Mbps[/]",
                $"[cyan]{result.UploadSpeed:F1} Mbps[/]",
                $"[dim]{result.Ping:F0} ms[/]",
                $"[dim]{result.Server}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Create visual chart
        AnsiConsole.WriteLine();
        var chart = new BarChart()
            .Width(60)
            .Label("[cyan bold]Speed Comparison[/]")
            .CenterLabel();

        chart.AddItem("Avg Download", (int)stats.AvgDownload, Color.Cyan1);
        chart.AddItem("Avg Upload", (int)stats.AvgUpload, Color.Blue);
        chart.AddItem("Min Download", (int)stats.MinDownload, Color.Red);
        chart.AddItem("Max Download", (int)stats.MaxDownload, Color.Green);

        AnsiConsole.Write(chart);

        return 0;
    }
}
