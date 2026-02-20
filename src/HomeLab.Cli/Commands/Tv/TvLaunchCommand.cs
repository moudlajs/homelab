using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvLaunchCommand : AsyncCommand<TvLaunchCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<APP>")]
        [Description("App ID or partial name to launch (use 'tv apps' to see available apps)")]
        public string App { get; set; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (!TvCommandHelper.ValidateConfig(config))
        {
            return 1;
        }

        // Handle "default" keyword - read from config
        var appToFind = settings.App;
        if (appToFind.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(config!.DefaultApp))
            {
                AnsiConsole.MarkupLine("[red]No default app configured.[/]");
                AnsiConsole.MarkupLine("[dim]Set DefaultApp in ~/.homelab/tv.json[/]");
                return 1;
            }
            appToFind = config.DefaultApp;
        }

        var client = TvCommandHelper.CreateClient();
        try
        {
            string? appIdToLaunch = null;
            string? appName = null;

            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Connecting to {config!.Name}...", async _ =>
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);

                // Get list of apps to find the right one
                var apps = await client.GetAppsAsync();

                // Try exact ID match first
                var exactMatch = apps.FirstOrDefault(a =>
                    a.Id.Equals(appToFind, StringComparison.OrdinalIgnoreCase));

                if (exactMatch != null)
                {
                    appIdToLaunch = exactMatch.Id;
                    appName = exactMatch.Name;
                }
                else
                {
                    // Try partial name match
                    var partialMatch = apps.FirstOrDefault(a =>
                        a.Name.Contains(appToFind, StringComparison.OrdinalIgnoreCase) ||
                        a.Id.Contains(appToFind, StringComparison.OrdinalIgnoreCase));

                    if (partialMatch != null)
                    {
                        appIdToLaunch = partialMatch.Id;
                        appName = partialMatch.Name;
                    }
                }
            });

            if (appIdToLaunch == null)
            {
                AnsiConsole.MarkupLine($"[red]App '{appToFind}' not found.[/]");
                AnsiConsole.MarkupLine("[dim]Use[/] [cyan]homelab tv apps[/] [dim]to see available apps.[/]");
                return 1;
            }

            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Launching {appName}...", async _ =>
            {
                await client.LaunchAppAsync(appIdToLaunch);
            });

            AnsiConsole.MarkupLine($"[green]Launched {appName}![/]");
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
