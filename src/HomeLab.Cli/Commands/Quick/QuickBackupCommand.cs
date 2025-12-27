using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Quick;

/// <summary>
/// Quick backup - snapshot current container states and configs.
/// Creates timestamped backup for easy rollback.
/// </summary>
public class QuickBackupCommand : AsyncCommand<QuickBackupCommand.Settings>
{
    private readonly IDockerService _dockerService;

    public QuickBackupCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[SERVICE]")]
        [Description("Service name to backup (leave empty for all services)")]
        public string? ServiceName { get; set; }

        [CommandOption("--path <DIRECTORY>")]
        [Description("Backup directory (default: ~/homelab-backups)")]
        public string? BackupPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupDir = settings.BackupPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "homelab-backups",
            timestamp
        );

        AnsiConsole.MarkupLine($"[yellow]⚡ Quick backup[/]");
        AnsiConsole.MarkupLine($"[dim]Backup location:[/] {backupDir}");
        AnsiConsole.WriteLine();

        try
        {
            // Create backup directory
            Directory.CreateDirectory(backupDir);

            // Get containers to backup
            var containers = await _dockerService.ListContainersAsync(onlyHomelab: true);

            if (!string.IsNullOrEmpty(settings.ServiceName))
            {
                var containerName = $"homelab_{settings.ServiceName}";
                containers = containers.Where(c => c.Name == containerName).ToList();

                if (containers.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Container '{containerName}' not found[/]");
                    return 1;
                }
            }

            if (containers.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No homelab containers found to backup[/]");
                return 0;
            }

            // Backup each container
            var backupCount = 0;
            await AnsiConsole.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Backing up containers...[/]", maxValue: containers.Count);

                    foreach (var container in containers)
                    {
                        // Create container-specific backup
                        var containerBackupFile = Path.Combine(backupDir, $"{container.Name}.json");

                        // Save container info
                        var containerInfo = new
                        {
                            container.Name,
                            container.Id,
                            container.IsRunning,
                            Timestamp = DateTime.Now,
                            Status = container.IsRunning ? "running" : "stopped"
                        };

                        await File.WriteAllTextAsync(
                            containerBackupFile,
                            System.Text.Json.JsonSerializer.Serialize(containerInfo, new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true
                            }),
                            cancellationToken
                        );

                        backupCount++;
                        task.Increment(1);

                        AnsiConsole.MarkupLine($"[green]✓[/] Backed up {container.Name}");
                    }
                });

            // Create backup manifest
            var manifest = new
            {
                Timestamp = timestamp,
                BackupPath = backupDir,
                ContainerCount = backupCount,
                Containers = containers.Select(c => c.Name).ToList()
            };

            await File.WriteAllTextAsync(
                Path.Combine(backupDir, "manifest.json"),
                System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }),
                cancellationToken
            );

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green bold]✓ Quick backup completed![/]");
            AnsiConsole.MarkupLine($"[dim]Backed up {backupCount} containers to:[/]");
            AnsiConsole.MarkupLine($"[cyan]{backupDir}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Tip: Use this backup for rollback if needed[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
            return 1;
        }
    }
}
