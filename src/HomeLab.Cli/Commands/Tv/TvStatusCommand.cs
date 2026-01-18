using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvStatusCommand : AsyncCommand<TvStatusCommand.Settings>
{
    private readonly IWakeOnLanService _wolService;
    public class Settings : CommandSettings { }

    public TvStatusCommand(IWakeOnLanService wolService) => _wolService = wolService;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null) { AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]"); return 1; }

        AnsiConsole.Write(new Rule($"[blue]{config.Name}[/]").RuleStyle("grey"));

        bool isOnline = false;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Checking...", async _ =>
        {
            isOnline = await _wolService.IsReachableAsync(config.IpAddress, 3000);
        });

        var table = new Table().Border(TableBorder.Rounded).AddColumn("Property").AddColumn("Value");
        table.AddRow("Name", config.Name);
        table.AddRow("IP Address", config.IpAddress);
        table.AddRow("MAC Address", config.MacAddress);
        table.AddRow("Paired", config.ClientKey != null ? "[green]Yes[/]" : "[yellow]No[/]");
        table.AddRow("Status", isOnline ? "[green]Online[/]" : "[dim]Offline/Standby[/]");
        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(isOnline ? "[dim]Use[/] [cyan]homelab tv off[/]" : "[dim]Use[/] [cyan]homelab tv on[/]");
        return 0;
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
