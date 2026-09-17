using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
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

    /// <summary>
    /// Ports the LG WebOS SSAP service listens on while the TV is powered on.
    /// </summary>
    public static readonly int[] WebOsPorts = { 3000, 3001 };

    /// <summary>
    /// Determines whether the configured TV is actually reachable.
    /// Probes the WebOS service ports instead of using ICMP: when a TV's DHCP
    /// lease moves and an unrelated device picks up the old address, that device
    /// answers ping, which reported the TV as Online when it was not.
    /// </summary>
    public static async Task<bool> IsTvOnlineAsync(IWakeOnLanService wolService, string ipAddress, int timeoutMs = 3000)
    {
        foreach (var port in WebOsPorts)
        {
            if (await wolService.IsPortOpenAsync(ipAddress, port, timeoutMs))
            {
                return true;
            }
        }

        return false;
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
