using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvWakeCommand : AsyncCommand<TvWakeCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (!TvCommandHelper.ValidateConfig(config))
        {
            return 1;
        }

        var client = TvCommandHelper.CreateClient(settings.Verbose);
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Connecting...", async _ =>
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
            });

            // Check what's currently in the foreground
            var foregroundApp = await client.GetForegroundAppAsync();

            if (foregroundApp is "com.webos.app.screensaver" or "com.webos.app.screensaver-lite")
            {
                // Wake from screensaver by turning screen on + sending key
                await client.TurnScreenOnAsync();
                await Task.Delay(200);
                await client.SendKeyAsync("EXIT");
                AnsiConsole.MarkupLine("[green]Woke TV from screensaver.[/]");
            }
            else
            {
                // Try turning screen on in case it's in screen-off mode
                await client.TurnScreenOnAsync();
                AnsiConsole.MarkupLine($"[yellow]TV is already active[/] [dim](foreground: {foregroundApp})[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed: {ex.Message}[/]");
            return 1;
        }
        finally
        {
            await client.DisconnectAsync();
        }
    }
}
