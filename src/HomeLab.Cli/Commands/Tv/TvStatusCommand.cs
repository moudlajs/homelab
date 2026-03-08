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

        // If TV is online and paired, show now playing info
        if (isOnline && !string.IsNullOrEmpty(config.ClientKey))
        {
            try
            {
                var client = TvCommandHelper.CreateClient();
                await client.ConnectAsync(config.IpAddress, config.ClientKey);

                var appId = await client.GetForegroundAppAsync();
                if (!string.IsNullOrEmpty(appId))
                {
                    // Get friendly app name
                    var appName = appId;
                    try
                    {
                        var apps = await client.GetAppsAsync();
                        var match = apps.FirstOrDefault(a =>
                            a.Id.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            appName = match.Name;
                        }
                    }
                    catch
                    {
                        // Fall back to raw app ID
                    }

                    table.AddRow("App", $"[cyan]{appName.EscapeMarkup()}[/]");

                    // If on live TV app, try to get channel info
                    if (appId.Contains("livetv", StringComparison.OrdinalIgnoreCase) ||
                        appId.Contains("tv.viewer", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var channel = await client.GetCurrentChannelAsync();
                            var channelName = channel.TryGetProperty("channelName", out var cn)
                                ? cn.GetString() : null;
                            var channelNumber = channel.TryGetProperty("channelNumber", out var cnum)
                                ? cnum.GetString() : null;
                            if (!string.IsNullOrEmpty(channelName))
                            {
                                var channelDisplay = string.IsNullOrEmpty(channelNumber)
                                    ? channelName
                                    : $"{channelName} ({channelNumber})";
                                table.AddRow("Channel", channelDisplay.EscapeMarkup());
                            }
                        }
                        catch
                        {
                            // Channel info not available
                        }
                    }
                }

                try
                {
                    var volume = await client.GetVolumeAsync();
                    table.AddRow("Volume", volume.ToString());
                }
                catch
                {
                    // Volume not available
                }

                await client.DisconnectAsync();
            }
            catch
            {
                // WebSocket connection failed — just show basic status
            }
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(isOnline ? "[dim]Use[/] [cyan]homelab tv off[/]" : "[dim]Use[/] [cyan]homelab tv on[/]");
        return 0;
    }
}
