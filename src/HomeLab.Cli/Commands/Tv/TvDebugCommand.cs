using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvDebugCommand : AsyncCommand<TvDebugCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]TV not configured.[/]");
            return 1;
        }

        var client = new LgTvClient();
        client.SetVerboseLogging(msg => AnsiConsole.MarkupLine($"[dim]{msg.EscapeMarkup()}[/]"));

        try
        {
            await client.ConnectAsync(config.IpAddress, config.ClientKey);
            var foregroundApp = await client.GetForegroundAppAsync();
            AnsiConsole.MarkupLine($"[green]Foreground app:[/] {foregroundApp ?? "(none)"}");
            AnsiConsole.MarkupLine($"[dim]Expected app:[/] {config.DefaultApp ?? "(not set)"}");
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
