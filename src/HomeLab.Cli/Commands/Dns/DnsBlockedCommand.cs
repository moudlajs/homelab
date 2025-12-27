using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;

namespace HomeLab.Cli.Commands.Dns;

/// <summary>
/// Displays top blocked domains.
/// </summary>
public class DnsBlockedCommand : AsyncCommand<DnsBlockedCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public DnsBlockedCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public class Settings : CommandSettings
    {
        [CommandOption("-n|--limit")]
        [Description("Number of domains to display")]
        [DefaultValue(10)]
        public int Limit { get; set; } = 10;

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]Fetching top blocked domains...[/]\n");

        var client = _clientFactory.CreateAdGuardClient();

        // Get blocked domains
        var blockedDomains = await client.GetTopBlockedDomainsAsync(settings.Limit);

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, blockedDomains))
            return 0;

        if (blockedDomains.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No blocked domains found.[/]");
            return 0;
        }

        // Create table
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Rank[/]");
        table.AddColumn("[yellow]Domain[/]");
        table.AddColumn("[yellow]Blocked Count[/]");

        int rank = 1;
        foreach (var domain in blockedDomains)
        {
            var rankColor = rank switch
            {
                1 => "red",
                2 => "orange3",
                3 => "yellow",
                _ => "white"
            };

            table.AddRow(
                $"[{rankColor}]{rank}[/]",
                $"[cyan]{domain.Domain}[/]",
                $"[red]{domain.Count:N0}[/]"
            );

            rank++;
        }

        AnsiConsole.Write(table);

        // Summary
        AnsiConsole.WriteLine();
        var totalBlocked = blockedDomains.Sum(d => d.Count);
        AnsiConsole.MarkupLine($"[green]Total blocks from top {blockedDomains.Count} domains:[/] [red]{totalBlocked:N0}[/]");

        return 0;
    }
}
