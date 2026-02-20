using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvScreenCommand : AsyncCommand<TvScreenCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<ACTION>")]
        [Description("Screen action: on or off")]
        public string Action { get; set; } = string.Empty;

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

        var action = settings.Action.ToLowerInvariant();
        if (action != "on" && action != "off")
        {
            AnsiConsole.MarkupLine("[red]Invalid action. Use 'on' or 'off'.[/]");
            return 1;
        }

        var client = TvCommandHelper.CreateClient(settings.Verbose);
        try
        {
            if (settings.Verbose)
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                if (action == "off")
                {
                    await client.TurnScreenOffAsync();
                }
                else
                {
                    await client.TurnScreenOnAsync();
                }
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Turning screen {action}...", async _ =>
                {
                    await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                    if (action == "off")
                    {
                        await client.TurnScreenOffAsync();
                    }
                    else
                    {
                        await client.TurnScreenOnAsync();
                    }
                });
            }

            AnsiConsole.MarkupLine($"[green]Screen turned {action}![/]");
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
