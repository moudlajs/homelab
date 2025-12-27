using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Dns;

/// <summary>
/// Displays DNS statistics from AdGuard Home.
/// </summary>
public class DnsStatsCommand : AsyncCommand<DnsStatsCommand.Settings>
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

    public DnsStatsCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("DNS Stats")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateAdGuardClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking AdGuard Home status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] AdGuard Home is not healthy: {healthInfo.Message}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]✓[/] AdGuard Home is healthy\n");

        // Get DNS stats
        var stats = await client.GetStatsAsync();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, stats))
            return 0;

        // Create stats panel
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            new Markup("[yellow]Total Queries:[/]"),
            new Markup($"[cyan]{stats.TotalQueries:N0}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Blocked Queries:[/]"),
            new Markup($"[red]{stats.BlockedQueries:N0}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Block Percentage:[/]"),
            new Markup($"[red]{stats.BlockedPercentage:F2}%[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Safe Browsing Blocks:[/]"),
            new Markup($"[orange3]{stats.SafeBrowsingBlocks:N0}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Parental Blocks:[/]"),
            new Markup($"[orange3]{stats.ParentalBlocks:N0}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Last Updated:[/]"),
            new Markup($"[dim]{stats.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC[/]")
        );

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]DNS Statistics[/]")
                .BorderColor(Color.Green)
                .RoundedBorder()
        );

        // Create visual chart
        AnsiConsole.WriteLine();
        var totalQueries = (int)Math.Min(stats.TotalQueries, 100);
        var blockedQueries = (int)Math.Round((double)stats.BlockedQueries / stats.TotalQueries * totalQueries);
        var allowedQueries = totalQueries - blockedQueries;

        var chart = new BarChart()
            .Width(60)
            .Label("[green bold]Query Distribution[/]")
            .CenterLabel();

        if (allowedQueries > 0)
            chart.AddItem("Allowed", allowedQueries, Color.Green);
        if (blockedQueries > 0)
            chart.AddItem("Blocked", blockedQueries, Color.Red);

        AnsiConsole.Write(chart);

        return 0;
    }
}
