using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Configuration;
using System.ComponentModel;
using System.Diagnostics;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Handles configuration management operations.
/// Usage: homelab config [action]
/// </summary>
public class ConfigCommand : AsyncCommand<ConfigCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[action]")]
        [Description("Action: view, edit, backup, restore, list-backups")]
        public string? Action { get; set; }

        [CommandOption("--backup <name>")]
        [Description("Backup name to restore (used with 'restore' action)")]
        public string? BackupName { get; set; }
    }

    private readonly IConfigService _configService;

    public ConfigCommand(IConfigService configService)
    {
        _configService = configService;
    }

    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        var action = settings.Action?.ToLower() ?? "view";

        try
        {
            switch (action)
            {
                case "view":
                    await ViewConfigAsync();
                    break;

                case "edit":
                    await EditConfigAsync();
                    break;

                case "backup":
                    await BackupConfigAsync();
                    break;

                case "restore":
                    await RestoreConfigAsync(settings.BackupName);
                    break;

                case "list-backups":
                    await ListBackupsAsync();
                    break;

                default:
                    AnsiConsole.MarkupLine(
                        "[red]Invalid action.[/] Valid: view, edit, backup, restore, list-backups");
                    return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }
    }

    private async Task ViewConfigAsync()
    {
        var content = await _configService.GetComposeFileAsync();

        var panel = new Panel(content)
        {
            Header = new PanelHeader("📄 Docker Compose Configuration"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
    }

    private async Task EditConfigAsync()
    {
        // Get current config
        var currentContent = await _configService.GetComposeFileAsync();

        // Determine editor
        var editor = Environment.GetEnvironmentVariable("EDITOR") ?? "nano";

        // Create temp file
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, currentContent);

        try
        {
            // Open editor
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = editor,
                Arguments = tempFile,
                UseShellExecute = false
            });

            if (process == null)
            {
                AnsiConsole.MarkupLine("[red]Failed to start editor[/]");
                return;
            }

            await process.WaitForExitAsync();

            // Ask for confirmation
            var confirm = AnsiConsole.Confirm(
                "Save changes to docker-compose.yml?");

            if (confirm)
            {
                var newContent = await File.ReadAllTextAsync(tempFile);
                await _configService.UpdateComposeFileAsync(newContent);
                AnsiConsole.MarkupLine("[green]✓[/] Configuration updated successfully");
                AnsiConsole.MarkupLine("[dim]A backup was created automatically[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]Changes discarded[/]");
            }
        }
        finally
        {
            // Clean up temp file
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private async Task BackupConfigAsync()
    {
        await AnsiConsole.Status()
            .StartAsync("Creating backup...", async ctx =>
            {
                var backupName = await _configService.BackupConfigAsync();
                AnsiConsole.MarkupLine(
                    $"[green]✓[/] Backup created: [blue]{backupName}[/]");
            });
    }

    private async Task RestoreConfigAsync(string? backupName)
    {
        if (string.IsNullOrEmpty(backupName))
        {
            // Show available backups and let user choose
            var backups = await _configService.ListBackupsAsync();

            if (backups.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No backups found[/]");
                return;
            }

            backupName = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Which backup do you want to restore?")
                    .AddChoices(backups));
        }

        var confirm = AnsiConsole.Confirm(
            $"Are you sure you want to restore from '{backupName}'?");

        if (confirm)
        {
            await _configService.RestoreBackupAsync(backupName);
            AnsiConsole.MarkupLine("[green]✓[/] Configuration restored successfully");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Restore cancelled[/]");
        }
    }

    private async Task ListBackupsAsync()
    {
        var backups = await _configService.ListBackupsAsync();

        if (backups.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No backups found[/]");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Backup Name[/]");
        table.AddColumn("[yellow]Date[/]");

        foreach (var backup in backups)
        {
            // Parse timestamp from filename (docker-compose.20231226_143022.yml.bak)
            var parts = backup.Replace("docker-compose.", "")
                .Replace(".yml.bak", "")
                .Split('_');

            var dateStr = parts.Length >= 2
                ? $"{parts[0]} {parts[1]}"
                : "Unknown";

            table.AddRow(backup, dateStr);
        }

        AnsiConsole.Write(table);
    }
}
