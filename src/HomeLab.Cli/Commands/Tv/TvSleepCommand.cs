using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvSleepCommand : AsyncCommand<TvSleepCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[MINUTES]")]
        [Description("Minutes until auto-off (15, 30, 60, 90, 120, 180, 240) or 'off' to disable")]
        public string? Minutes { get; set; }

        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

    private static readonly int[] ValidMinutes = { 15, 30, 60, 90, 120, 180, 240 };

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

            if (string.IsNullOrEmpty(settings.Minutes))
            {
                return await ShowSleepTimerAsync(client);
            }

            return await SetSleepTimerAsync(client, settings.Minutes);
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

    private static async Task<int> ShowSleepTimerAsync(Services.LgTv.LgTvClient client)
    {
        var response = await client.GetSystemSettingsAsync("time", new[] { "sleepTimer" });

        if (response.TryGetProperty("settings", out var settingsObj) &&
            settingsObj.TryGetProperty("sleepTimer", out var timer))
        {
            var val = timer.ValueKind == JsonValueKind.String ? timer.GetString() : timer.ToString();
            if (val is null or "off" or "0" or "Off")
            {
                AnsiConsole.MarkupLine("[dim]Sleep timer:[/] off");
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Sleep timer:[/] [cyan]{val}[/]");
            }
        }
        else
        {
            // Dump what we got for debugging
            AnsiConsole.MarkupLine("[yellow]Could not read sleep timer.[/]");
            AnsiConsole.MarkupLine($"[dim]Response: {response}[/]");
        }

        return 0;
    }

    private static async Task<int> SetSleepTimerAsync(Services.LgTv.LgTvClient client, string value)
    {
        string timerValue;

        if (value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            timerValue = "off";
        }
        else if (int.TryParse(value, out var minutes))
        {
            if (!ValidMinutes.Contains(minutes))
            {
                AnsiConsole.MarkupLine($"[red]Invalid value. Use: {string.Join(", ", ValidMinutes)} or 'off'[/]");
                return 1;
            }

            timerValue = $"{minutes}m";
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Invalid value. Use minutes ({string.Join(", ", ValidMinutes)}) or 'off'[/]");
            return 1;
        }

        await client.SetSystemSettingsAsync("time", new Dictionary<string, object> { { "sleepTimer", timerValue } });
        if (timerValue == "off")
        {
            AnsiConsole.MarkupLine("[green]Sleep timer disabled.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Sleep timer set to {value} minutes.[/]");
        }

        return 0;
    }
}
