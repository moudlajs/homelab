using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvSettingsCommand : AsyncCommand<TvSettingsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--get <SETTING>")]
        [Description("Get a setting value (format: category.key, e.g., picture.brightness)")]
        public string? Get { get; set; }

        [CommandOption("--set <SETTING>")]
        [Description("Set a setting value (format: category.key=value, e.g., picture.brightness=50)")]
        public string? Set { get; set; }

        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(settings.Get) && string.IsNullOrEmpty(settings.Set))
        {
            ShowHelp();
            return 0;
        }

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

            if (!string.IsNullOrEmpty(settings.Get))
            {
                return await GetSettingAsync(client, settings.Get);
            }

            if (!string.IsNullOrEmpty(settings.Set))
            {
                return await SetSettingAsync(client, settings.Set);
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

    private static async Task<int> GetSettingAsync(Services.LgTv.LgTvClient client, string setting)
    {
        var parts = setting.Split('.', 2);
        if (parts.Length != 2)
        {
            AnsiConsole.MarkupLine("[red]Invalid format. Use: category.key (e.g., picture.brightness)[/]");
            return 1;
        }

        var category = parts[0];
        var key = parts[1];

        var response = await client.GetSystemSettingsAsync(category, new[] { key });

        if (response.TryGetProperty("settings", out var settingsObj))
        {
            if (settingsObj.TryGetProperty(key, out var value))
            {
                var val = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
                AnsiConsole.MarkupLine($"[cyan]{category}.{key}[/] = {val}");
            }
            else
            {
                // Show all returned settings
                AnsiConsole.MarkupLine($"[dim]{category} settings:[/]");
                foreach (var prop in settingsObj.EnumerateObject())
                {
                    var val = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                    AnsiConsole.MarkupLine($"  [cyan]{prop.Name}[/] = {val}");
                }
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Response: {response}[/]");
        }

        return 0;
    }

    private static async Task<int> SetSettingAsync(Services.LgTv.LgTvClient client, string setting)
    {
        var eqIndex = setting.IndexOf('=');
        if (eqIndex < 0)
        {
            AnsiConsole.MarkupLine("[red]Invalid format. Use: category.key=value (e.g., picture.brightness=50)[/]");
            return 1;
        }

        var path = setting[..eqIndex];
        var value = setting[(eqIndex + 1)..];

        var parts = path.Split('.', 2);
        if (parts.Length != 2)
        {
            AnsiConsole.MarkupLine("[red]Invalid format. Use: category.key=value (e.g., picture.brightness=50)[/]");
            return 1;
        }

        var category = parts[0];
        var key = parts[1];

        // Try to parse as number, otherwise use as string
        object settingValue = int.TryParse(value, out var intVal) ? intVal : value;

        await client.SetSystemSettingsAsync(category, new Dictionary<string, object> { { key, settingValue } });
        AnsiConsole.MarkupLine($"[green]Set {category}.{key} = {value}[/]");
        return 0;
    }

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]TV Settings[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [cyan]--get category.key[/]        Read a setting");
        AnsiConsole.MarkupLine("  [cyan]--set category.key=value[/]  Write a setting");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Known categories & keys:[/]");
        AnsiConsole.MarkupLine("  [dim]picture[/]   brightness, contrast, color, backlight, energySaving");
        AnsiConsole.MarkupLine("  [dim]option[/]    country, audioGuidance, screenOffMode");
        AnsiConsole.MarkupLine("  [dim]time[/]      autoOff15Min");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Note: Available settings vary by TV model and WebOS version.[/]");
    }
}
