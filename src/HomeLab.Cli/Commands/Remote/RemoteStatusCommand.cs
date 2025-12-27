using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using HomeLab.Cli.Services.Remote;

namespace HomeLab.Cli.Commands.Remote;

/// <summary>
/// Displays status of a remote homelab.
/// </summary>
public class RemoteStatusCommand : AsyncCommand<RemoteStatusCommand.Settings>
{
    private readonly RemoteConnectionService _connectionService;
    private readonly ISshService _sshService;

    public RemoteStatusCommand()
    {
        _connectionService = new RemoteConnectionService();
        _sshService = new SshService();
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Connection name (uses default if not specified)")]
        public string? Name { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Get connection
        var connection = string.IsNullOrEmpty(settings.Name)
            ? _connectionService.GetDefaultConnection()
            : _connectionService.GetConnection(settings.Name);

        if (connection == null)
        {
            if (string.IsNullOrEmpty(settings.Name))
            {
                AnsiConsole.MarkupLine("[red]✗[/] No default connection configured");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Connection '[cyan]{settings.Name}[/]' not found");
            }

            AnsiConsole.MarkupLine("\n[dim]List connections with:[/]");
            AnsiConsole.MarkupLine("  [cyan]homelab remote list[/]");
            return 1;
        }

        AnsiConsole.Write(
            new FigletText($"Remote: {connection.Name}")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        // Test connection
        AnsiConsole.MarkupLine($"[blue]Connecting to {connection.Host}...[/]");

        var isConnected = await AnsiConsole.Status()
            .StartAsync("Testing connection...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                return await _sshService.TestConnectionAsync(connection);
            });

        if (!isConnected)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to connect to remote host");
            AnsiConsole.MarkupLine($"[dim]Host:[/] {connection.Host}:{connection.Port}");
            AnsiConsole.MarkupLine($"[dim]User:[/] {connection.Username}");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]✓[/] Connected successfully\n");

        // Update last connected timestamp
        connection.LastConnected = DateTime.UtcNow;
        _connectionService.AddConnection(connection);

        // Check Docker status
        var dockerRunning = await AnsiConsole.Status()
            .StartAsync("Checking Docker status...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                return await _sshService.IsDockerRunningAsync(connection);
            });

        if (!dockerRunning)
        {
            AnsiConsole.MarkupLine("[yellow]⚠[/] Docker is not running on remote host");
            return 0;
        }

        AnsiConsole.MarkupLine("[green]✓[/] Docker is running\n");

        // Get Docker info
        var dockerInfo = await _sshService.ExecuteCommandAsync(connection, "docker info --format '{{.ServerVersion}}|||{{.NCPU}}|||{{.MemTotal}}'");

        if (dockerInfo.Success)
        {
            var parts = dockerInfo.Output.Trim().Split("|||");
            if (parts.Length >= 3)
            {
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn("[yellow]Property[/]");
                table.AddColumn("[yellow]Value[/]");

                table.AddRow("Host", $"[cyan]{connection.Host}[/]");
                table.AddRow("Docker Version", parts[0].Trim());
                table.AddRow("CPUs", parts[1].Trim());
                table.AddRow("Memory", FormatBytes(parts[2].Trim()));

                AnsiConsole.Write(table);
            }
        }

        // Get running containers
        AnsiConsole.WriteLine();
        var containersResult = await _sshService.ExecuteCommandAsync(connection, "docker ps --format '{{.Names}}|||{{.Status}}|||{{.Image}}'");

        if (containersResult.Success && !string.IsNullOrWhiteSpace(containersResult.Output))
        {
            var containers = containersResult.Output.Trim().Split('\n');

            var containerTable = new Table();
            containerTable.Border(TableBorder.Rounded);
            containerTable.AddColumn("[yellow]Container[/]");
            containerTable.AddColumn("[yellow]Status[/]");
            containerTable.AddColumn("[yellow]Image[/]");

            foreach (var container in containers)
            {
                var parts = container.Split("|||");
                if (parts.Length >= 3)
                {
                    var status = parts[1].Contains("Up") ? $"[green]{parts[1]}[/]" : $"[red]{parts[1]}[/]";
                    containerTable.AddRow(
                        $"[cyan]{parts[0]}[/]",
                        status,
                        $"[dim]{parts[2]}[/]"
                    );
                }
            }

            AnsiConsole.Write(containerTable);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green]Running containers:[/] {containers.Length}");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No containers running[/]");
        }

        return 0;
    }

    private string FormatBytes(string bytesStr)
    {
        if (long.TryParse(bytesStr, out var bytes))
        {
            var gb = bytes / (1024.0 * 1024.0 * 1024.0);
            return $"{gb:F2} GB";
        }
        return bytesStr;
    }
}
