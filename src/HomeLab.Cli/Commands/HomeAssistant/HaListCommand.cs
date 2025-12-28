using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.HomeAssistant;

/// <summary>
/// List Home Assistant entities by domain (lights, switches, sensors, etc.).
/// </summary>
public class HaListCommand : AsyncCommand<HaListCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<DOMAIN>")]
        [Description("Domain to list (light, switch, sensor, etc.)")]
        public string Domain { get; set; } = string.Empty;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public HaListCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateHomeAssistantClient();

        AnsiConsole.MarkupLine($"[yellow]Listing {settings.Domain} entities...[/]\n");

        var entities = await client.GetEntitiesByDomainAsync(settings.Domain);

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, entities))
        {
            return 0;
        }

        if (entities.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No {settings.Domain} entities found.[/]");
            return 0;
        }

        // Create table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Entity ID[/]");
        table.AddColumn("[yellow]Name[/]");
        table.AddColumn("[yellow]State[/]");
        table.AddColumn("[yellow]Last Updated[/]");

        foreach (var entity in entities.OrderBy(e => e.FriendlyName))
        {
            var stateColor = settings.Domain.ToLower() switch
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

        // Summary
        AnsiConsole.WriteLine();
        var onCount = entities.Count(e => e.State == "on");
        var offCount = entities.Count(e => e.State == "off");

        AnsiConsole.MarkupLine($"[green]Total:[/] {entities.Count} | [green]On:[/] {onCount} | [dim]Off:[/] {offCount}");

        return 0;
    }

    private string FormatTimeAgo(DateTime dateTime)
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
