using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvChannelCommand : AsyncCommand<TvChannelCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[CHANNEL]")]
        [Description("Channel number, 'up', or 'down'. Omit to show current channel.")]
        public string? Channel { get; set; }

        [CommandOption("-l|--list")]
        [Description("List all available channels")]
        public bool List { get; set; }

        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

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

            // List channels
            if (settings.List)
            {
                return await ListChannelsAsync(client);
            }

            // Channel up/down
            if (settings.Channel != null)
            {
                var ch = settings.Channel.ToLowerInvariant();
                if (ch == "up")
                {
                    await client.ChannelUpAsync();
                    AnsiConsole.MarkupLine("[green]Channel up![/]");
                    return 0;
                }
                if (ch == "down")
                {
                    await client.ChannelDownAsync();
                    AnsiConsole.MarkupLine("[green]Channel down![/]");
                    return 0;
                }

                // Tune to specific channel number
                return await TuneToChannelAsync(client, settings.Channel);
            }

            // Show current channel
            return await ShowCurrentChannelAsync(client);
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

    private static async Task<int> ShowCurrentChannelAsync(Services.LgTv.LgTvClient client)
    {
        try
        {
            var response = await client.GetCurrentChannelAsync();
            var channelName = response.TryGetProperty("channelName", out var cn) ? cn.GetString() : null;
            var channelNumber = response.TryGetProperty("channelNumber", out var cnum) ? cnum.GetString() : null;

            if (channelName != null || channelNumber != null)
            {
                AnsiConsole.MarkupLine($"Current channel: [cyan]{channelNumber ?? "?"}[/] - [cyan]{channelName ?? "Unknown"}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No channel info available (TV may be on an app input).[/]");
            }
            return 0;
        }
        catch
        {
            AnsiConsole.MarkupLine("[yellow]No channel info available (TV may be on an app input).[/]");
            return 0;
        }
    }

    private static async Task<int> ListChannelsAsync(Services.LgTv.LgTvClient client)
    {
        var response = await client.GetChannelListAsync();

        if (!response.TryGetProperty("channelList", out var channelList))
        {
            AnsiConsole.MarkupLine("[yellow]No channels available.[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("#");
        table.AddColumn("Name");
        table.AddColumn("ID");

        foreach (var ch in channelList.EnumerateArray())
        {
            var num = ch.TryGetProperty("channelNumber", out var n) ? n.GetString() ?? "" : "";
            var name = ch.TryGetProperty("channelName", out var cn) ? cn.GetString() ?? "" : "";
            var id = ch.TryGetProperty("channelId", out var ci) ? ci.GetString() ?? "" : "";
            table.AddRow(num, name, $"[dim]{id}[/]");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task<int> TuneToChannelAsync(Services.LgTv.LgTvClient client, string channelInput)
    {
        // Get channel list and find by number
        var response = await client.GetChannelListAsync();
        if (!response.TryGetProperty("channelList", out var channelList))
        {
            AnsiConsole.MarkupLine("[yellow]No channels available.[/]");
            return 1;
        }

        foreach (var ch in channelList.EnumerateArray())
        {
            var num = ch.TryGetProperty("channelNumber", out var n) ? n.GetString() : null;
            var id = ch.TryGetProperty("channelId", out var ci) ? ci.GetString() : null;

            if (num == channelInput || id == channelInput)
            {
                if (id != null)
                {
                    await client.OpenChannelAsync(id);
                    var name = ch.TryGetProperty("channelName", out var cn) ? cn.GetString() : channelInput;
                    AnsiConsole.MarkupLine($"[green]Tuned to {num ?? ""} - {name}![/]");
                    return 0;
                }
            }
        }

        AnsiConsole.MarkupLine($"[red]Channel '{channelInput}' not found.[/]");
        AnsiConsole.MarkupLine("[dim]Use --list to see available channels.[/]");
        return 1;
    }
}
