using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvInputCommand : AsyncCommand<TvInputCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[INPUT_ID]")]
        [Description("Input source ID to switch to (e.g., HDMI_1). Omit to list available inputs.")]
        public string? InputId { get; set; }

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
            JsonElement? response = null;

            if (settings.Verbose)
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                if (!string.IsNullOrEmpty(settings.InputId))
                {
                    await client.SwitchInputAsync(settings.InputId);
                }
                else
                {
                    response = await client.GetExternalInputListAsync();
                }
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync(
                    string.IsNullOrEmpty(settings.InputId) ? "Getting input sources..." : $"Switching to {settings.InputId}...",
                    async _ =>
                    {
                        await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                        if (!string.IsNullOrEmpty(settings.InputId))
                        {
                            await client.SwitchInputAsync(settings.InputId);
                        }
                        else
                        {
                            response = await client.GetExternalInputListAsync();
                        }
                    });
            }

            if (!string.IsNullOrEmpty(settings.InputId))
            {
                AnsiConsole.MarkupLine($"[green]Switched to {settings.InputId}![/]");
                return 0;
            }

            // Display input list
            if (response == null)
            {
                AnsiConsole.MarkupLine("[red]No response from TV.[/]");
                return 1;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("ID");
            table.AddColumn("Label");
            table.AddColumn("Connected");
            table.AddColumn("Icon");

            if (response.Value.TryGetProperty("devices", out var devices))
            {
                foreach (var device in devices.EnumerateArray())
                {
                    var id = device.GetProperty("id").GetString() ?? "";
                    var label = device.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "";
                    var connected = device.TryGetProperty("connected", out var c) && c.GetBoolean();
                    var icon = device.TryGetProperty("icon", out var i) ? i.GetString() ?? "" : "";

                    table.AddRow(
                        id,
                        label,
                        connected ? "[green]Yes[/]" : "[dim]No[/]",
                        icon
                    );
                }
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("[dim]Switch with: homelab tv input <ID>[/]");
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
}
