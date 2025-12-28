using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Traefik;

/// <summary>
/// Displays all Traefik backend services.
/// </summary>
public class TraefikServicesCommand : AsyncCommand<TraefikServicesCommand.Settings>
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

    public TraefikServicesCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Traefik Services")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTraefikClient();
        var services = await client.GetServicesAsync();

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, services))
            return 0;

        // Display as table (default)
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[yellow]Service[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Servers[/]");
        table.AddColumn("[yellow]Load Balancer[/]");
        table.AddColumn("[yellow]Status[/]");

        foreach (var service in services)
        {
            var statusColor = service.Status == "healthy" ? "green" : "red";
            var serversList = service.Servers.Any()
                ? string.Join("\n", service.Servers)
                : "[dim]none[/]";

            table.AddRow(
                service.Name,
                service.Type,
                $"[dim]{serversList}[/]",
                service.LoadBalancer,
                $"[{statusColor}]{service.Status}[/]"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total services: {services.Count}[/]");

        return 0;
    }
}
