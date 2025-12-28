using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Traefik;

/// <summary>
/// Displays all Traefik middlewares.
/// </summary>
public class TraefikMiddlewaresCommand : AsyncCommand<TraefikMiddlewaresCommand.Settings>
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

    public TraefikMiddlewaresCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Traefik Middlewares")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTraefikClient();
        var middlewares = await client.GetMiddlewaresAsync();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, middlewares))
            return 0;

        // Display as table (default)
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[yellow]Middleware[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Configuration[/]");

        foreach (var middleware in middlewares)
        {
            var statusColor = middleware.Status == "enabled" ? "green" : "red";
            var configList = middleware.Config.Any()
                ? string.Join("\n", middleware.Config.Select(kv => $"{kv.Key}: {kv.Value}"))
                : "[dim]none[/]";

            table.AddRow(
                middleware.Name,
                middleware.Type,
                $"[{statusColor}]{middleware.Status}[/]",
                $"[dim]{configList}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total middlewares: {middlewares.Count}[/]");

        return 0;
    }
}
