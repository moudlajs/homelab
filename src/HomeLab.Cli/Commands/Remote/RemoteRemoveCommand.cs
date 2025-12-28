using System.ComponentModel;
using HomeLab.Cli.Services.Remote;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Remote;

/// <summary>
/// Removes a remote connection profile.
/// </summary>
public class RemoteRemoveCommand : Command<RemoteRemoveCommand.Settings>
{
    private readonly RemoteConnectionService _connectionService;

    public RemoteRemoveCommand()
    {
        _connectionService = new RemoteConnectionService();
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Connection name to remove")]
        public string Name { get; set; } = string.Empty;

        [CommandOption("-y|--yes")]
        [Description("Skip confirmation prompt")]
        [DefaultValue(false)]
        public bool SkipConfirmation { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var connection = _connectionService.GetConnection(settings.Name);

        if (connection == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Connection '[cyan]{settings.Name}[/]' not found");
            return 1;
        }

        // Show connection details
        AnsiConsole.MarkupLine($"\n[yellow]Removing connection:[/] [cyan]{connection.Name}[/]");
        AnsiConsole.MarkupLine($"[dim]Host:[/] {connection.Host}");
        AnsiConsole.MarkupLine($"[dim]User:[/] {connection.Username}");

        if (connection.IsDefault)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ This is the default connection[/]");
        }

        // Confirm removal
        if (!settings.SkipConfirmation)
        {
            if (!AnsiConsole.Confirm("\nAre you sure you want to remove this connection?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }
        }

        // Remove connection
        var removed = _connectionService.RemoveConnection(settings.Name);

        if (removed)
        {
            AnsiConsole.MarkupLine($"\n[green]✓[/] Connection '[cyan]{settings.Name}[/]' removed");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"\n[red]✗[/] Failed to remove connection");
            return 1;
        }
    }
}
