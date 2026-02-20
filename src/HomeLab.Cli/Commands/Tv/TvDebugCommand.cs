using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvDebugCommand : AsyncCommand<TvDebugCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (!TvCommandHelper.ValidateConfig(config))
        {
            return 1;
        }

        var client = TvCommandHelper.CreateClient(verbose: true);
        try
        {
            await client.ConnectAsync(config!.IpAddress, config.ClientKey);
            var foregroundApp = await client.GetForegroundAppAsync();
            AnsiConsole.MarkupLine($"[green]Foreground app:[/] {foregroundApp ?? "(none)"}");
            AnsiConsole.MarkupLine($"[dim]Expected app:[/] {config.DefaultApp ?? "(not set)"}");
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
