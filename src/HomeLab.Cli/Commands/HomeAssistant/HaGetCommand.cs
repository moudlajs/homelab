using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.HomeAssistant;

/// <summary>
/// Get details of a specific Home Assistant entity.
/// </summary>
public class HaGetCommand : AsyncCommand<HaGetCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ENTITY_ID>")]
        [Description("Entity ID (e.g., light.living_room)")]
        public string EntityId { get; set; } = string.Empty;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public HaGetCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateHomeAssistantClient();

        AnsiConsole.MarkupLine($"[yellow]Fetching entity:[/] [cyan]{settings.EntityId}[/]\n");

        var entity = await client.GetEntityAsync(settings.EntityId);

        if (entity == null)
        {
            AnsiConsole.MarkupLine($"[red]✗ Entity not found:[/] {settings.EntityId}");
            return 1;
        }

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, entity))
        {
            return 0;
        }

        // Display entity details
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[yellow]Entity ID:[/]", entity.EntityId);
        grid.AddRow("[yellow]Friendly Name:[/]", entity.FriendlyName);
        grid.AddRow("[yellow]Domain:[/]", entity.Domain);
        grid.AddRow("[yellow]State:[/]", $"[cyan]{entity.State}[/]");
        grid.AddRow("[yellow]Last Changed:[/]", $"[dim]{entity.LastChanged:yyyy-MM-dd HH:mm:ss} UTC[/]");
        grid.AddRow("[yellow]Last Updated:[/]", $"[dim]{entity.LastUpdated:yyyy-MM-dd HH:mm:ss} UTC[/]");

        AnsiConsole.Write(
            new Panel(grid)
                .Header($"[cyan]{entity.FriendlyName}[/]")
                .BorderColor(Color.Blue)
                .RoundedBorder()
        );

        // Display attributes if any
        if (entity.Attributes.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow bold]Attributes:[/]");

            var attrTable = new Table();
            attrTable.Border(TableBorder.Rounded);
            attrTable.AddColumn("[yellow]Attribute[/]");
            attrTable.AddColumn("[yellow]Value[/]");

            foreach (var attr in entity.Attributes)
            {
                var value = attr.Value?.ToString() ?? "[dim]null[/]";
                attrTable.AddRow(attr.Key, value);
            }

            AnsiConsole.Write(attrTable);
        }

        return 0;
    }
}
