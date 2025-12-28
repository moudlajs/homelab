using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.HomeAssistant;

/// <summary>
/// Displays all Home Assistant entities and their states.
/// </summary>
public class HaStatusCommand : AsyncCommand<HaStatusCommand.Settings>
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

    public HaStatusCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Home Assistant")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateHomeAssistantClient();

        // Check health
        await AnsiConsole.Status()
            .StartAsync("Checking Home Assistant...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var healthInfo = await client.GetHealthInfoAsync();

        if (!healthInfo.IsHealthy)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Home Assistant is not healthy: {healthInfo.Message}");
            AnsiConsole.MarkupLine("[yellow]Note: Showing mock data for demonstration[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Home Assistant is healthy: {healthInfo.Message}\n");
        }

        // Get all entities
        var entities = await client.GetAllEntitiesAsync();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, entities))
            return 0;

        if (entities.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No entities found.[/]");
            return 0;
        }

        // Group by domain
        var grouped = entities.GroupBy(e => e.Domain).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            var count = group.Count();
            AnsiConsole.MarkupLine($"\n[yellow bold]{group.Key.ToUpper()}[/] [dim]({count} entities)[/]");

            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[yellow]Entity ID[/]");
            table.AddColumn("[yellow]Name[/]");
            table.AddColumn("[yellow]State[/]");
            table.AddColumn("[yellow]Last Updated[/]");

            foreach (var entity in group)
            {
                var stateColor = entity.Domain switch
                {
                    "light" when entity.State == "on" => "green",
                    "switch" when entity.State == "on" => "green",
                    "binary_sensor" when entity.State == "on" => "yellow",
                    _ => "cyan"
                };

                var timeAgo = FormatTimeAgo(entity.LastUpdated);

                table.AddRow(
                    $"[dim]{entity.EntityId}[/]",
                    entity.FriendlyName,
                    $"[{stateColor}]{entity.State}[/]",
                    $"[dim]{timeAgo}[/]"
                );
            }

            AnsiConsole.Write(table);
        }

        // Summary
        AnsiConsole.WriteLine();
        var totalEntities = entities.Count;
        var domains = grouped.Count();

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            $"[green]Total Entities:[/] {totalEntities}",
            $"[yellow]Domains:[/] {domains}"
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
}
