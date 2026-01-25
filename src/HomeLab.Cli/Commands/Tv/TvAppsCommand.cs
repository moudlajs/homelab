using System.Text.Json;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.LgTv;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvAppsCommand : AsyncCommand<TvAppsCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [System.ComponentModel.Description("Show detailed debug output")]
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await LoadTvConfigAsync();
        if (config == null)
        {
            AnsiConsole.MarkupLine("[red]TV not configured. Run 'homelab tv setup' first.[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(config.ClientKey))
        {
            AnsiConsole.MarkupLine("[red]TV not paired. Run 'homelab tv setup' to pair.[/]");
            return 1;
        }

        var client = new LgTvClient();
        if (settings.Verbose)
        {
            client.SetVerboseLogging(msg => AnsiConsole.MarkupLine($"[dim]{msg.EscapeMarkup()}[/]"));
        }

        try
        {
            List<TvApp> apps = new();
            if (settings.Verbose)
            {
                await client.ConnectAsync(config.IpAddress, config.ClientKey);
                apps = await client.GetAppsAsync();
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync($"Connecting to {config.Name}...", async _ =>
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

    private static async Task<TvConfig?> LoadTvConfigAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "tv.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TvConfig>(await File.ReadAllTextAsync(path));
        }
        catch
        {
            return null;
        }
    }
}
