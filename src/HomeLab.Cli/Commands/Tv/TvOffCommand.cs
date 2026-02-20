using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvOffCommand : AsyncCommand<TvOffCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (!TvCommandHelper.ValidateConfig(config))
        {
            return 1;
        }

        var client = TvCommandHelper.CreateClient();
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Connecting to {config!.Name}...", async _ =>
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);
            });
            await client.PowerOffAsync();
            AnsiConsole.MarkupLine($"[green]{config.Name} turned off![/]");
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
