using Spectre.Console;
using Spectre.Console.Cli;
using HomeLab.Cli.Services.Docker;
using System.ComponentModel;

namespace HomeLab.Cli.Commands.Quick;

/// <summary>
/// Quick fix - stop container, clear cache/temp data, and restart.
/// Useful for fixing stuck or misbehaving services.
/// </summary>
public class QuickFixCommand : AsyncCommand<QuickFixCommand.Settings>
{
    private readonly IDockerService _dockerService;

    public QuickFixCommand(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<SERVICE>")]
        [Description("Service name to fix (e.g., adguard, prometheus)")]
        public string ServiceName { get; set; } = string.Empty;

        [CommandOption("--clear-logs")]
        [Description("Clear container logs during fix")]
        [DefaultValue(false)]
        public bool ClearLogs { get; set; }

        [CommandOption("--force")]
        [Description("Force fix without confirmation")]
        [DefaultValue(true)]
        public bool Force { get; set; } = true;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var containerName = $"homelab_{settings.ServiceName}";

        AnsiConsole.MarkupLine($"[yellow]⚡ Quick fix:[/] {settings.ServiceName}");
        AnsiConsole.MarkupLine($"[dim]This will stop the container, clear cache, and restart[/]");
        AnsiConsole.WriteLine();

        // Confirm if not forced
        if (!settings.Force)
        {
            if (!AnsiConsole.Confirm($"Fix {settings.ServiceName}?"))
            {
                AnsiConsole.MarkupLine("[yellow]Cancelled[/]");
                return 0;
            }
        }

        try
        {
            // Step 1: Stop the container
            await AnsiConsole.Status()
                .StartAsync($"Stopping {settings.ServiceName}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _dockerService.StopContainerAsync(containerName);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Stopped {settings.ServiceName}");

            // Step 2: Clear cache (simulated - would need volume/exec access)
            await AnsiConsole.Status()
                .StartAsync("Clearing cache and temp files...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    // TODO: Implement actual cache clearing via docker exec
                    // docker exec <container> rm -rf /tmp/* /var/cache/*
                    // For now, just simulate
                    await Task.Delay(1500, cancellationToken);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Cleared cache (simulated)");

            // Step 3: Clear logs if requested
            if (settings.ClearLogs)
            {
                await AnsiConsole.Status()
                    .StartAsync("Clearing container logs...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        // TODO: Implement log clearing
                        // truncate -s 0 $(docker inspect --format='{{.LogPath}}' <container>)
                        await Task.Delay(500, cancellationToken);
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Cleared logs (simulated)");
            }

            // Step 4: Wait a moment
            AnsiConsole.MarkupLine($"[dim]Waiting 2 seconds...[/]");
            await Task.Delay(2000, cancellationToken);

            // Step 5: Start the container fresh
            await AnsiConsole.Status()
                .StartAsync($"Starting {settings.ServiceName}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    await _dockerService.StartContainerAsync(containerName);
                });

            AnsiConsole.MarkupLine($"[green]✓[/] Started {settings.ServiceName}");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[green bold]✓ Quick fix completed![/]");
            AnsiConsole.MarkupLine($"[dim]Service should be running fresh now[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[yellow]Tip:[/] Check status with 'homelab status'");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"[yellow]Tip:[/] Try 'homelab service restart {settings.ServiceName}' instead");
            return 1;
        }
    }
}
