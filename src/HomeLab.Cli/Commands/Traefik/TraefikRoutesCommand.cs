using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Commands.Traefik;

/// <summary>
/// Displays all Traefik HTTP routers.
/// </summary>
public class TraefikRoutesCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;

    public TraefikRoutesCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Traefik Routes")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTraefikClient();
        var routes = await client.GetRoutesAsync();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue);

        table.AddColumn("[yellow]Route[/]");
        table.AddColumn("[yellow]Rule[/]");
        table.AddColumn("[yellow]Service[/]");
        table.AddColumn("[yellow]Entry Point[/]");
        table.AddColumn("[yellow]Middlewares[/]");
        table.AddColumn("[yellow]TLS[/]");

        foreach (var route in routes)
        {
            var tlsIcon = route.TLS ? "[green]🔒 Yes[/]" : "[dim]🔓 No[/]";
            var middlewares = route.Middlewares.Any()
                ? string.Join(", ", route.Middlewares)
                : "[dim]none[/]";

            table.AddRow(
                route.Name,
                $"[dim]{route.Rule}[/]",
                route.Service,
                route.EntryPoint,
                $"[dim]{middlewares}[/]",
                tlsIcon
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Total routes: {routes.Count}[/]");

        return 0;
    }
}
