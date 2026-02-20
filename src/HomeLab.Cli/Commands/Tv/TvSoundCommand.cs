using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvSoundCommand : AsyncCommand<TvSoundCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[OUTPUT]")]
        [Description("Sound output to switch to (e.g., tv_speaker, external_arc). Omit to show current.")]
        public string? Output { get; set; }

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
                if (!string.IsNullOrEmpty(settings.Output))
                {
                    await client.ChangeSoundOutputAsync(settings.Output);
                }
                else
                {
                    response = await client.GetSoundOutputAsync();
                }
            }
            else
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync(
                    string.IsNullOrEmpty(settings.Output) ? "Getting sound output..." : $"Switching to {settings.Output}...",
                    async _ =>
                    {
                        await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                        if (!string.IsNullOrEmpty(settings.Output))
                        {
                            await client.ChangeSoundOutputAsync(settings.Output);
                        }
                        else
                        {
                            response = await client.GetSoundOutputAsync();
                        }
                    });
            }

            if (!string.IsNullOrEmpty(settings.Output))
            {
                AnsiConsole.MarkupLine($"[green]Sound output changed to {settings.Output}![/]");
                return 0;
            }

            if (response != null)
            {
                var soundOutput = response.Value.TryGetProperty("soundOutput", out var so) ? so.GetString() : null;
                if (soundOutput != null)
                {
                    AnsiConsole.MarkupLine($"Current sound output: [cyan]{soundOutput}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[dim]Response: {response.Value}[/]");
                }
            }

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
