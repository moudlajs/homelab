using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Displays Prometheus scrape targets status.
/// </summary>
public class MonitorTargetsCommand : AsyncCommand<MonitorTargetsCommand.Settings>
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

    public MonitorTargetsCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Fetching Prometheus targets...[/]\n");

        var client = _clientFactory.CreatePrometheusClient();

        // Get targets
        var targets = await client.GetTargetsAsync();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, targets))
        {
            return 0;
        }

        if (targets.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No scrape targets found.[/]");
            return 0;
        }

        // Create targets table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Job[/]");
        table.AddColumn("[yellow]Instance[/]");
        table.AddColumn("[yellow]Health[/]");
        table.AddColumn("[yellow]Last Scrape[/]");
        table.AddColumn("[yellow]Duration[/]");

        foreach (var target in targets)
        {
            var healthIcon = target.Health == "up" ? "🟢" : "🔴";
            var healthColor = target.Health == "up" ? "green" : "red";
            var healthText = target.Health.ToUpper();

            var lastScrape = target.LastScrape.HasValue
                ? FormatTimeAgo(target.LastScrape.Value)
                : "Never";

            var duration = target.ScrapeDuration > 0
                ? $"{target.ScrapeDuration:F3}s"
                : "N/A";

            table.AddRow(
                $"[cyan]{target.Job}[/]",
                target.Instance,
                $"{healthIcon} [{healthColor}]{healthText}[/]",
                $"[dim]{lastScrape}[/]",
                $"[dim]{duration}[/]"
            );
        }

        AnsiConsole.Write(table);

        // Summary
        AnsiConsole.WriteLine();
        var upCount = targets.Count(t => t.Health == "up");
        var downCount = targets.Count(t => t.Health != "up");

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow($"[green]Up:[/]", $"[green]{upCount}[/]");
        grid.AddRow($"[red]Down:[/]", $"[red]{downCount}[/]");
        grid.AddRow($"[blue]Total:[/]", $"[blue]{targets.Count}[/]");

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]Target Summary[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder()
        );

        return 0;
    }

    private string FormatTimeAgo(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalSeconds < 60)
        {
            return $"{(int)timeAgo.TotalSeconds}s ago";
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
