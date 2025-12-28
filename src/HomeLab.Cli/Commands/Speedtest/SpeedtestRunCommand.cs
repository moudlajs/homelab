using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Speedtest;

/// <summary>
/// Run a new internet speed test.
/// Triggers Speedtest Tracker to perform a new test.
/// </summary>
public class SpeedtestRunCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;

    public SpeedtestRunCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Running speed test...[/]");
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateSpeedtestClient();

        // Run speedtest
        var success = await AnsiConsole.Status()
            .StartAsync("Testing internet speed (this may take 30-60 seconds)...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                return await client.RunSpeedtestAsync();
            });

        if (success)
        {
            AnsiConsole.MarkupLine("[green]✓ Speed test completed successfully![/]");
            AnsiConsole.MarkupLine("[dim]Use 'homelab speedtest stats' to view results[/]");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine("[red]✗ Failed to run speed test[/]");
            AnsiConsole.MarkupLine("[yellow]Tip: Check if Speedtest Tracker is running[/]");
            return 1;
        }
    }
}
