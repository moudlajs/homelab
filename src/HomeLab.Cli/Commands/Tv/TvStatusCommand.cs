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
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (!TvCommandHelper.ValidateConfig(config, requirePairing: false))
        {
            return 1;
        }

        AnsiConsole.Write(new Rule($"[blue]{config!.Name}[/]").RuleStyle("grey"));

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
}
