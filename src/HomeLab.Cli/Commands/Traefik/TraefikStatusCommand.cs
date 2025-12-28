using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Traefik;

/// <summary>
/// Displays Traefik reverse proxy status overview.
/// </summary>
public class TraefikStatusCommand : AsyncCommand<TraefikStatusCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings
    {
    }

    public TraefikStatusCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Traefik Status")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTraefikClient();

        // Check health
        var isHealthy = await client.IsHealthyAsync();
        if (!isHealthy)
        {
            AnsiConsole.MarkupLine("[red]✗ Traefik API is not accessible[/]");
            AnsiConsole.MarkupLine("[yellow]Note: Showing mock data for demonstration[/]");
            AnsiConsole.WriteLine();
        }

        // Get overview
        var overview = await client.GetOverviewAsync();

        // Display overview
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(
            new Markup("[yellow]Total Routers:[/]"),
            new Markup($"[cyan]{overview.TotalRouters}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Healthy Routers:[/]"),
            new Markup($"[green]{overview.HealthyRouters}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Total Services:[/]"),
            new Markup($"[cyan]{overview.TotalServices}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Healthy Services:[/]"),
            new Markup($"[green]{overview.HealthyServices}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Total Middlewares:[/]"),
            new Markup($"[cyan]{overview.TotalMiddlewares}[/]")
        );
        grid.AddRow(
            new Markup("[yellow]Entry Points:[/]"),
            new Markup($"[dim]{string.Join(", ", overview.EntryPoints)}[/]")
        );

        var panel = new Panel(grid)
            .Header("[cyan]Overview[/]")
            .BorderColor(Color.Blue)
            .RoundedBorder();

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Display recent routes
        var routes = await client.GetRoutesAsync();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[yellow]Route[/]");
        table.AddColumn("[yellow]Rule[/]");
        table.AddColumn("[yellow]Entry Point[/]");
        table.AddColumn("[yellow]TLS[/]");
        table.AddColumn("[yellow]Status[/]");

        foreach (var route in routes.Take(10))
        {
            var tlsIcon = route.TLS ? "🔒" : "🔓";
            var statusColor = route.Status == "enabled" ? "green" : "red";

            table.AddRow(
                route.Name,
                $"[dim]{route.Rule}[/]",
                route.EntryPoint,
                tlsIcon,
                $"[{statusColor}]{route.Status}[/]"
            );
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
