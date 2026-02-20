using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvNotifyCommand : AsyncCommand<TvNotifyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<MESSAGE>")]
        [Description("Message to display on TV screen")]
        public string Message { get; set; } = string.Empty;

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
            if (settings.Verbose)
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                await client.CreateToastAsync(settings.Message);
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Sending notification...", async _ =>
                {
                    await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                    await client.CreateToastAsync(settings.Message);
                });
            }

            AnsiConsole.MarkupLine("[green]Notification sent![/]");
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
