using System.ComponentModel;
using HomeLab.Cli.Services.Remote;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Remote;

/// <summary>
/// Syncs docker-compose files between local and remote.
/// </summary>
public class RemoteSyncCommand : AsyncCommand<RemoteSyncCommand.Settings>
{
    private readonly RemoteConnectionService _connectionService;
    private readonly ISshService _sshService;

    public RemoteSyncCommand()
    {
        _connectionService = new RemoteConnectionService();
        _sshService = new SshService();
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[name]")]
        [Description("Connection name (uses default if not specified)")]
        public string? Name { get; set; }

        [CommandOption("--push")]
        [Description("Push local docker-compose.yml to remote")]
        [DefaultValue(false)]
        public bool Push { get; set; }

        [CommandOption("--pull")]
        [Description("Pull remote docker-compose.yml to local")]
        [DefaultValue(false)]
        public bool Pull { get; set; }

        [CommandOption("--local-file")]
        [Description("Local docker-compose.yml file path")]
        public string? LocalFile { get; set; }

        [CommandOption("--remote-file")]
        [Description("Remote docker-compose.yml file path")]
        public string? RemoteFile { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Validate direction
        if (!settings.Push && !settings.Pull)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Please specify --push or --pull");
            return 1;
        }

        if (settings.Push && settings.Pull)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Cannot specify both --push and --pull");
            return 1;
        }

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
            return 1;
        }

        // Determine file paths
        var localFile = settings.LocalFile ?? "docker-compose.yml";
        var remoteFile = settings.RemoteFile ?? connection.ComposeFilePath ?? "~/docker-compose.yml";

        // Expand ~ in remote file path
        if (remoteFile.StartsWith("~/"))
        {
            var homeResult = await _sshService.ExecuteCommandAsync(connection, "echo $HOME");
            if (homeResult.Success)
            {
                var home = homeResult.Output.Trim();
                remoteFile = remoteFile.Replace("~", home);
            }
        }

        AnsiConsole.Write(
            new FigletText("Remote Sync")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.WriteLine();

        if (settings.Push)
        {
            return await PushFile(connection, localFile, remoteFile);
        }
        else
        {
            return await PullFile(connection, remoteFile, localFile);
        }
    }

    private async Task<int> PushFile(Models.RemoteConnection connection, string localFile, string remoteFile)
    {
        AnsiConsole.MarkupLine($"[blue]Pushing local file to remote...[/]");
        AnsiConsole.MarkupLine($"[dim]Local:[/]  {localFile}");
        AnsiConsole.MarkupLine($"[dim]Remote:[/] {remoteFile}\n");

        if (!File.Exists(localFile))
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Local file not found: {localFile}");
            return 1;
        }

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Uploading file...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _sshService.UploadFileAsync(connection, localFile, remoteFile);
                });

            AnsiConsole.MarkupLine("[green]✓[/] File uploaded successfully");

            // Show file info
            var fileInfo = new FileInfo(localFile);
            AnsiConsole.MarkupLine($"[dim]Size:[/] {FormatBytes(fileInfo.Length)}");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Upload failed: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> PullFile(Models.RemoteConnection connection, string remoteFile, string localFile)
    {
        AnsiConsole.MarkupLine($"[blue]Pulling remote file to local...[/]");
        AnsiConsole.MarkupLine($"[dim]Remote:[/] {remoteFile}");
        AnsiConsole.MarkupLine($"[dim]Local:[/]  {localFile}\n");

        // Check if local file exists and confirm overwrite
        if (File.Exists(localFile))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] Local file exists: {localFile}");
            if (!AnsiConsole.Confirm("Overwrite?", defaultValue: false))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }
        }

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Downloading file...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _sshService.DownloadFileAsync(connection, remoteFile, localFile);
                });

            AnsiConsole.MarkupLine("[green]✓[/] File downloaded successfully");

            // Show file info
            var fileInfo = new FileInfo(localFile);
            AnsiConsole.MarkupLine($"[dim]Size:[/] {FormatBytes(fileInfo.Length)}");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Download failed: {ex.Message}");
            return 1;
        }
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024.0:F2} KB";
        }

        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }
}
