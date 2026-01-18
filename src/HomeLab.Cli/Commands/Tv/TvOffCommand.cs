using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvOffCommand : AsyncCommand<TvOffCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null) { AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]"); return 1; }
        if (string.IsNullOrEmpty(config.ClientKey)) { AnsiConsole.MarkupLine("[red]TV not paired. Run 'homelab tv setup' to pair.[/]"); return 1; }

        var client = new LgTvClient();
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Connecting to {config.Name}...", async _ =>
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);
            });
            await client.PowerOffAsync();
            AnsiConsole.MarkupLine($"[green]{config.Name} turned off![/]");
            return 0;
        }
        catch (Exception ex) { AnsiConsole.MarkupLine($"[red]Failed: {ex.Message}[/]"); return 1; }
        finally { await client.DisconnectAsync(); }
    }

    private static async Task<TvConfig?> LoadTvConfigAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "tv.json");
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<TvConfig>(await File.ReadAllTextAsync(path)); }
        catch { return null; }
    }
}
