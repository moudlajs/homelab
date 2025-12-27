using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Remote;

namespace HomeLab.Cli.Commands.Remote;

/// <summary>
/// Lists all configured remote connections.
/// </summary>
public class RemoteListCommand : Command
{
    private readonly RemoteConnectionService _connectionService;

    public RemoteListCommand()
    {
        _connectionService = new RemoteConnectionService();
    }

    public override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        var connections = _connectionService.ListConnections();

        if (connections.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No remote connections configured.[/]");
            AnsiConsole.MarkupLine("\n[dim]Add a connection with:[/]");
            AnsiConsole.MarkupLine("  [cyan]homelab remote connect <name> <host> -u <username>[/]");
            return 0;
        }

        AnsiConsole.Write(
            new FigletText("Remote Connections")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Name[/]");
        table.AddColumn("[yellow]Host[/]");
        table.AddColumn("[yellow]Username[/]");
        table.AddColumn("[yellow]Port[/]");
        table.AddColumn("[yellow]Default[/]");
        table.AddColumn("[yellow]Last Connected[/]");

        foreach (var conn in connections.OrderBy(c => c.Name))
        {
            var defaultIndicator = conn.IsDefault ? "⭐" : "";
            var lastConnected = conn.LastConnected.HasValue
                ? FormatTimeAgo(conn.LastConnected.Value)
                : "[dim]Never[/]";

            table.AddRow(
                $"{defaultIndicator} [cyan]{conn.Name}[/]",
                conn.Host,
                conn.Username,
                conn.Port.ToString(),
                conn.IsDefault ? "[green]Yes[/]" : "[dim]No[/]",
                lastConnected
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]Total connections:[/] {connections.Count}");

        return 0;
    }

    private string FormatTimeAgo(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;

        if (timeAgo.TotalMinutes < 1)
            return "[green]Just now[/]";
        if (timeAgo.TotalMinutes < 60)
            return $"[dim]{(int)timeAgo.TotalMinutes}m ago[/]";
        if (timeAgo.TotalHours < 24)
            return $"[dim]{(int)timeAgo.TotalHours}h ago[/]";
        if (timeAgo.TotalDays < 7)
            return $"[dim]{(int)timeAgo.TotalDays}d ago[/]";
        return $"[dim]{dateTime:yyyy-MM-dd}[/]";
    }
}
