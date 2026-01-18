using System.ComponentModel;
using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvSetupCommand : AsyncCommand<TvSetupCommand.Settings>
{
    private readonly IWakeOnLanService _wolService;

    public class Settings : CommandSettings
    {
        [CommandOption("--ip <IP>")][Description("TV IP address")] public string? IpAddress { get; set; }
        [CommandOption("--mac <MAC>")][Description("TV MAC address for Wake-on-LAN")] public string? MacAddress { get; set; }
        [CommandOption("--name <NAME>")][Description("Friendly name for the TV")] public string? Name { get; set; }
    }

    public TvSetupCommand(IWakeOnLanService wolService) => _wolService = wolService;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[blue]TV Setup Wizard[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var ipAddress = settings.IpAddress ?? AnsiConsole.Prompt(new TextPrompt<string>("TV IP address:"));
        var macAddress = settings.MacAddress ?? AnsiConsole.Prompt(new TextPrompt<string>("TV MAC address (XX:XX:XX:XX:XX:XX):"));
        var name = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Friendly name:").DefaultValue("Living Room TV"));

        AnsiConsole.MarkupLine("[bold]Step 1:[/] Testing connectivity...");
        var isReachable = await _wolService.IsReachableAsync(ipAddress);
        if (!isReachable)
        {
            AnsiConsole.MarkupLine("[yellow]TV is not reachable. It may be off.[/]");
            if (!AnsiConsole.Confirm("Continue setup anyway?", false))
            {
                return 1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[green]TV is reachable![/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Step 2:[/] Pairing with TV...");
        AnsiConsole.MarkupLine("[yellow]A pairing prompt will appear on your TV. Please accept it.[/]");

        string? clientKey = null;
        var client = new LgTvClient();
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Connecting (trying ports 3000 and 3001)...", async _ =>
            {
                clientKey = await client.ConnectAsync(ipAddress);
            });
            AnsiConsole.MarkupLine("[green]Successfully paired! Client key saved.[/]");
        }
        catch (TimeoutException)
        {
            AnsiConsole.MarkupLine("[yellow]Pairing timed out. Make sure you accept the prompt on TV![/]");
            if (!AnsiConsole.Confirm("Save config anyway (Wake-on-LAN only, no off/control)?", true)) { await client.DisconnectAsync(); return 1; }
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]{ex.Message}[/]");
            if (!AnsiConsole.Confirm("Save config anyway (Wake-on-LAN only, no off/control)?", true)) { await client.DisconnectAsync(); return 1; }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Connection failed: {ex.Message}[/]");
            AnsiConsole.MarkupLine("[dim]Make sure TV is ON and 'LG Connect Apps' is enabled in TV settings.[/]");
            if (!AnsiConsole.Confirm("Save config anyway (Wake-on-LAN only)?", true))
            {
                return 1;
            }
        }
        finally { await client.DisconnectAsync(); }

        var tvConfig = new TvConfig { Name = name, IpAddress = ipAddress, MacAddress = macAddress, ClientKey = clientKey, Type = TvType.LgWebOs };
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab");
        Directory.CreateDirectory(configDir);
        await File.WriteAllTextAsync(Path.Combine(configDir, "tv.json"), JsonSerializer.Serialize(tvConfig, new JsonSerializerOptions { WriteIndented = true }));

        AnsiConsole.MarkupLine("[green]Configuration saved![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Commands: homelab tv on | homelab tv off | homelab tv status[/]");
        return 0;
    }
}
