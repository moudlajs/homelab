using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvInfoCommand : AsyncCommand<TvInfoCommand.Settings>
{
    public class Settings : CommandSettings
    {
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
            JsonElement? systemInfo = null;
            JsonElement? softwareInfo = null;
            JsonElement? powerState = null;

            if (settings.Verbose)
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                systemInfo = await client.GetSystemInfoAsync();
                softwareInfo = await client.GetSoftwareInfoAsync();
                powerState = await client.GetPowerStateAsync();
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Getting TV info...", async _ =>
                {
                    await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                    systemInfo = await client.GetSystemInfoAsync();
                    softwareInfo = await client.GetSoftwareInfoAsync();
                    try { powerState = await client.GetPowerStateAsync(); } catch { /* some models don't support this */ }
                });
            }

            var table = new Table().Border(TableBorder.Rounded).Title("[bold]TV Information[/]");
            table.AddColumn("Property");
            table.AddColumn("Value");

            // System info
            if (systemInfo != null)
            {
                AddProperty(table, "Model", systemInfo.Value, "modelName");
                AddProperty(table, "Serial", systemInfo.Value, "serialNumber");
                AddProperty(table, "Receiver Type", systemInfo.Value, "receiverType");
            }

            // Software info
            if (softwareInfo != null)
            {
                AddProperty(table, "Product Name", softwareInfo.Value, "product_name");
                AddProperty(table, "WebOS Version", softwareInfo.Value, "major_ver", "minor_ver");
                AddProperty(table, "Firmware", softwareInfo.Value, "fw_type");
            }

            // Power state
            if (powerState != null)
            {
                AddProperty(table, "Power State", powerState.Value, "state");
                AddProperty(table, "Processing", powerState.Value, "processing");
            }

            // Config info
            table.AddRow("[dim]IP Address[/]", config!.IpAddress);
            table.AddRow("[dim]MAC Address[/]", config.MacAddress);
            if (!string.IsNullOrEmpty(config.DefaultApp))
            {
                table.AddRow("[dim]Default App[/]", config.DefaultApp);
            }

            AnsiConsole.Write(table);
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

    private static void AddProperty(Table table, string label, JsonElement element, params string[] keys)
    {
        var values = new List<string>();
        foreach (var key in keys)
        {
            if (element.TryGetProperty(key, out var prop))
            {
                var val = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
                if (!string.IsNullOrEmpty(val))
                {
                    values.Add(val);
                }
            }
        }

        if (values.Count > 0)
        {
            table.AddRow(label, string.Join(".", values));
        }
    }
}
