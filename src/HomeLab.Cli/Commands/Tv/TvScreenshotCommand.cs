using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tv;

public class TvScreenshotCommand : AsyncCommand<TvScreenshotCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-o|--output <PATH>")]
        [Description("Output file path (default: auto-generated in screenshots dir)")]
        public string? Output { get; set; }

        [CommandOption("-v|--verbose")]
        [Description("Show detailed debug output")]
        public bool Verbose { get; set; }
    }

    private const string ExternalDrivePath = "/Volumes/T9";

    private static readonly string ExternalScreenshotDir = Path.Combine(
        ExternalDrivePath, ".homelab", "screenshots");

    private static readonly string FallbackScreenshotDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".homelab", "screenshots");

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
            string? imageUrl = null;

            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Capturing screenshot...", async _ =>
            {
                await client.ConnectAsync(config!.IpAddress, config.ClientKey);
                imageUrl = await client.CaptureScreenshotAsync();
            });

            if (string.IsNullOrEmpty(imageUrl))
            {
                AnsiConsole.MarkupLine("[red]Failed to capture screenshot — no image URL returned.[/]");
                AnsiConsole.MarkupLine("[dim]Try with -v flag for debug output.[/]");
                return 1;
            }

            // Determine output path
            var outputPath = settings.Output ?? GenerateOutputPath();
            var dir = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(dir);

            // Download the image from the TV
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Downloading...", async _ =>
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var imageBytes = await http.GetByteArrayAsync(imageUrl);
                await File.WriteAllBytesAsync(outputPath, imageBytes, cancellationToken);
            });

            var fileInfo = new FileInfo(outputPath);
            AnsiConsole.MarkupLine($"[green]Screenshot saved:[/] {outputPath} [dim]({fileInfo.Length / 1024}KB)[/]");
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

    private static string GenerateOutputPath()
    {
        var dir = ResolveScreenshotDir();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return Path.Combine(dir, $"tv_{timestamp}.jpg");
    }

    private static string ResolveScreenshotDir()
    {
        if (Directory.Exists(ExternalDrivePath))
        {
            try
            {
                Directory.CreateDirectory(ExternalScreenshotDir);
                var testFile = Path.Combine(ExternalScreenshotDir, ".write_test");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
                return ExternalScreenshotDir;
            }
            catch { }
        }

        return FallbackScreenshotDir;
    }
}
