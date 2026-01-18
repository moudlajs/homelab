using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Quick;

/// <summary>
/// Quick command to turn on the TV for your dog!
/// </summary>
public class QuickDogTvCommand : AsyncCommand<QuickDogTvCommand.Settings>
{
    private readonly IWakeOnLanService _wolService;
    public class Settings : CommandSettings { }

    public QuickDogTvCommand(IWakeOnLanService wolService) => _wolService = wolService;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null) { AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]"); return 1; }

        AnsiConsole.MarkupLine("[yellow]Turning on TV for your dog...[/]");
        var success = await _wolService.WakeAsync(config.MacAddress);
        if (success) { AnsiConsole.MarkupLine("[green]Magic packet sent! TV should turn on shortly.[/]"); return 0; }
        AnsiConsole.MarkupLine("[red]Failed to send Wake-on-LAN packet.[/]");
        return 1;
    }

    private static async Task<TvConfig?> LoadTvConfigAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "tv.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try { return JsonSerializer.Deserialize<TvConfig>(await File.ReadAllTextAsync(path)); }
        catch { return null; }
    }
}
