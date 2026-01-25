using System.ComponentModel;
using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvKeyCommand : AsyncCommand<TvKeyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<KEY>")]
        [Description("Remote key to send (ENTER, OK, UP, DOWN, LEFT, RIGHT, BACK, PLAY, PAUSE, etc.)")]
        public string Key { get; set; } = string.Empty;

        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(config.ClientKey))
        {
            AnsiConsole.MarkupLine("[red]TV not paired. Run 'homelab tv setup' to pair.[/]");
            return 1;
        }

        var client = new LgTvClient();
        if (settings.Verbose)
        {
            client.SetVerboseLogging(msg => AnsiConsole.MarkupLine($"[dim]{msg.EscapeMarkup()}[/]"));
        }

        try
        {
            if (settings.Verbose)
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);
                await client.SendKeyAsync(settings.Key);
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Sending {settings.Key.ToUpper()}...", async _ =>
                {
                    await client.ConnectAsync(config.IpAddress, config.ClientKey);
                    await client.SendKeyAsync(settings.Key);
                });
            }

            AnsiConsole.MarkupLine($"[green]Sent key: {settings.Key.ToUpper()}[/]");
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

    private static async Task<TvConfig?> LoadTvConfigAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "tv.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TvConfig>(await File.ReadAllTextAsync(path));
        }
        catch
        {
            return null;
        }
    }
}
