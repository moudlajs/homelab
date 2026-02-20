using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvOnCommand : AsyncCommand<TvOnCommand.Settings>
{
    private readonly IWakeOnLanService _wolService;

    public class Settings : CommandSettings
    {
        [CommandOption("-a|--app <APP>")]
        [Description("Launch app after TV wakes up (use app ID or name, or 'default' for saved default)")]
        public string? App { get; set; }

        [CommandOption("-k|--key <KEY>")]
        [Description("Send remote key after app launches (e.g., ENTER, OK, PLAY). Can be used multiple times.")]
        public string[]? Keys { get; set; }

        [CommandOption("--delay <MS>")]
        [Description("Delay in ms between key presses (default: 500)")]
        public int KeyDelay { get; set; } = 500;
    }

    public TvOnCommand(IWakeOnLanService wolService) => _wolService = wolService;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null) { AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]"); return 1; }

        // Determine which app to launch
        string? appToLaunch = null;
        if (!string.IsNullOrEmpty(settings.App))
        {
            appToLaunch = settings.App.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? config.DefaultApp
                : settings.App;
        }

        AnsiConsole.MarkupLine($"[dim]Sending Wake-on-LAN to {config.Name}...[/]");
        var success = await _wolService.WakeAsync(config.MacAddress);
        if (!success)
        {
            AnsiConsole.MarkupLine("[red]Failed to send Wake-on-LAN packet.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Magic packet sent to {config.Name}![/]");

        // Wait for TV to boot
        AnsiConsole.MarkupLine("[dim]Waiting for TV to boot...[/]");
        var bootTimeout = DateTime.Now.AddSeconds(15);
        var isOnline = false;

        while (DateTime.Now < bootTimeout)
        {
            await Task.Delay(2000);
            if (await _wolService.IsReachableAsync(config.IpAddress))
            {
                isOnline = true;
                break;
            }
        }

        if (isOnline)
        {
            AnsiConsole.MarkupLine($"[green]{config.Name} is now online![/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]TV may still be booting...[/]");
        }

        // Launch app if requested
        if (!string.IsNullOrEmpty(appToLaunch) && !string.IsNullOrEmpty(config.ClientKey))
        {
            // Give TV a moment to fully initialize WebOS
            await Task.Delay(3000);

            var client = TvCommandHelper.CreateClient();
            try
            {
                string? appName = null;
                string? launchedAppId = null;
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Launching {appToLaunch}...", async _ =>
                {
                    await client.ConnectAsync(config.IpAddress, config.ClientKey);

                    // Find the app
                    var apps = await client.GetAppsAsync();
                    var match = apps.FirstOrDefault(a =>
                        a.Id.Equals(appToLaunch, StringComparison.OrdinalIgnoreCase) ||
                        a.Name.Contains(appToLaunch, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        await client.LaunchAppAsync(match.Id);
                        appName = match.Name;
                        launchedAppId = match.Id;
                    }
                });

                if (appName != null)
                {
                    AnsiConsole.MarkupLine($"[green]Launched {appName}![/]");

                    // Send keys if requested
                    if (settings.Keys != null && settings.Keys.Length > 0 && launchedAppId != null)
                    {
                        // Wait for app to be in foreground
                        AnsiConsole.MarkupLine("[dim]Waiting for app to be ready...[/]");
                        var appReady = await client.WaitForAppAsync(launchedAppId, 30);

                        if (appReady)
                        {
                            // Extra delay for UI to fully render
                            await Task.Delay(3000);

                            foreach (var key in settings.Keys)
                            {
                                try
                                {
                                    await client.SendKeyAsync(key);
                                    AnsiConsole.MarkupLine($"[dim]Sent key: {key.ToUpper()}[/]");
                                    await Task.Delay(settings.KeyDelay);
                                }
                                catch (Exception ex)
                                {
                                    AnsiConsole.MarkupLine($"[yellow]Could not send key '{key}': {ex.Message}[/]");
                                }
                            }
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]App didn't start in time, skipping keys.[/]");
                        }
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]App '{appToLaunch}' not found.[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Could not launch app: {ex.Message}[/]");
            }
            finally
            {
                await client.DisconnectAsync();
            }
        }
        else if (!string.IsNullOrEmpty(appToLaunch) && string.IsNullOrEmpty(config.ClientKey))
        {
            AnsiConsole.MarkupLine("[yellow]TV not paired - cannot launch app. Run 'homelab tv setup' to pair.[/]");
        }

        // Send keys even without app launch (if TV is already on)
        if (settings.Keys != null && settings.Keys.Length > 0 && string.IsNullOrEmpty(appToLaunch) && !string.IsNullOrEmpty(config.ClientKey))
        {
            var client = TvCommandHelper.CreateClient();
            try
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);
                foreach (var key in settings.Keys)
                {
                    try
                    {
                        await client.SendKeyAsync(key);
                        AnsiConsole.MarkupLine($"[dim]Sent key: {key.ToUpper()}[/]");
                        await Task.Delay(settings.KeyDelay);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Could not send key '{key}': {ex.Message}[/]");
                    }
                }
            }
            finally
            {
                await client.DisconnectAsync();
            }
        }

        return 0;
    }

}
