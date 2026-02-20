using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;

namespace HomeLab.Cli.Commands.Tv;

internal static class TvCommandHelper
{
    public static async Task<TvConfig?> LoadTvConfigAsync()
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

    public static LgTvClient CreateClient(bool verbose = false)
    {
        var client = new LgTvClient();
        if (verbose)
        {
            client.SetVerboseLogging(msg => AnsiConsole.MarkupLine($"[dim]{msg.EscapeMarkup()}[/]"));
        }
        return client;
    }

    public static bool ValidateConfig(TvConfig? config, bool requirePairing = true)
    {
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]");
            return false;
        }

        if (requirePairing && string.IsNullOrEmpty(config.ClientKey))
        {
            AnsiConsole.MarkupLine("[red]TV not paired. Run 'homelab tv setup' to pair.[/]");
            return false;
        }

        return true;
    }
}
