using System.ComponentModel;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Remote;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Remote;

/// <summary>
/// Adds or updates a remote connection profile.
/// </summary>
public class RemoteConnectCommand : AsyncCommand<RemoteConnectCommand.Settings>
{
    private readonly RemoteConnectionService _connectionService;
    private readonly ISshService _sshService;

    public RemoteConnectCommand()
    {
        _connectionService = new RemoteConnectionService();
        _sshService = new SshService();
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name for this connection (e.g., 'mac-mini', 'production')")]
        public string Name { get; set; } = string.Empty;

        [CommandArgument(1, "<host>")]
        [Description("Remote host (IP address or hostname)")]
        public string Host { get; set; } = string.Empty;

        [CommandOption("-u|--username")]
        [Description("SSH username")]
        public string? Username { get; set; }

        [CommandOption("-p|--port")]
        [Description("SSH port")]
        [DefaultValue(22)]
        public int Port { get; set; } = 22;

        [CommandOption("-k|--key-file")]
        [Description("Path to SSH private key file")]
        public string? KeyFile { get; set; }

        [CommandOption("--docker-socket")]
        [Description("Docker socket path on remote host")]
        [DefaultValue("unix:///var/run/docker.sock")]
        public string DockerSocket { get; set; } = "unix:///var/run/docker.sock";

        [CommandOption("--compose-file")]
        [Description("Path to docker-compose.yml on remote host")]
        public string? ComposeFile { get; set; }

        [CommandOption("--default")]
        [Description("Set this as the default connection")]
        [DefaultValue(false)]
        public bool SetDefault { get; set; }

        [CommandOption("--test")]
        [Description("Test the connection before saving")]
        [DefaultValue(true)]
        public bool TestConnection { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Remote Connect")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        // Get username if not provided
        var username = settings.Username;
        if (string.IsNullOrEmpty(username))
        {
            username = AnsiConsole.Ask<string>("SSH username:");
        }

        // Create connection profile
        var connection = new RemoteConnection
        {
            Name = settings.Name,
            Host = settings.Host,
            Port = settings.Port,
            Username = username,
            KeyFile = settings.KeyFile,
            DockerSocket = settings.DockerSocket,
            ComposeFilePath = settings.ComposeFile,
            IsDefault = settings.SetDefault
        };

        // Test connection if requested
        if (settings.TestConnection)
        {
            AnsiConsole.MarkupLine($"[blue]Testing connection to {settings.Host}...[/]");

            var testResult = await AnsiConsole.Status()
                .StartAsync("Connecting...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await _sshService.TestConnectionAsync(connection);
                });

            if (!testResult)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Connection test failed!");
                AnsiConsole.MarkupLine("[yellow]Save anyway?[/]");

                if (!AnsiConsole.Confirm("Continue?", defaultValue: false))
                {
                    return 1;
                }
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✓[/] Connection successful!");

                // Check if Docker is running
                var dockerRunning = await _sshService.IsDockerRunningAsync(connection);
                if (dockerRunning)
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Docker is running on remote host");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]⚠[/] Docker may not be running on remote host");
                }

                connection.LastConnected = DateTime.UtcNow;
            }
        }

        // Save connection
        _connectionService.AddConnection(connection);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Connection profile '[cyan]{settings.Name}[/]' saved");

        if (settings.SetDefault)
        {
            _connectionService.SetDefaultConnection(settings.Name);
            AnsiConsole.MarkupLine($"[green]✓[/] Set as default connection");
        }

        // Show connection details
        AnsiConsole.WriteLine();
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Property[/]");
        table.AddColumn("[yellow]Value[/]");

        table.AddRow("Name", $"[cyan]{connection.Name}[/]");
        table.AddRow("Host", connection.Host);
        table.AddRow("Port", connection.Port.ToString());
        table.AddRow("Username", connection.Username);
        table.AddRow("Key File", connection.KeyFile ?? "[dim]none (will use SSH agent)[/]");
        table.AddRow("Docker Socket", $"[dim]{connection.DockerSocket}[/]");
        table.AddRow("Compose File", connection.ComposeFilePath ?? "[dim]not set[/]");
        table.AddRow("Default", connection.IsDefault ? "[green]Yes[/]" : "[dim]No[/]");

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Use 'homelab remote status {0}' to check remote homelab status[/]", settings.Name);

        return 0;
    }
}
