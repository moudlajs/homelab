using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvAppsCommand : AsyncCommand<TvAppsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Show detailed debug output")]
        [CommandOption("-v|--verbose")]
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
            List<TvApp> apps = new();
            if (settings.Verbose)
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                apps = await client.GetAppsAsync();
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Connecting to {config!.Name}...", async _ =>
                {
                    await client.ConnectAsync(config.IpAddress, config.ClientKey);
                    apps = await client.GetAppsAsync();
                });
            }

            if (apps.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No apps found.[/]");
                return 0;
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("App Name")
                .AddColumn("App ID (use with 'tv launch')");

            foreach (var app in apps.OrderBy(a => a.Name))
            {
                table.AddRow(app.Name, $"[dim]{app.Id}[/]");
            }

            AnsiConsole.Write(new Rule($"[blue]Installed Apps on {config.Name}[/]").RuleStyle("grey"));
            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Launch an app:[/] [cyan]homelab tv launch <app-id>[/]");

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
