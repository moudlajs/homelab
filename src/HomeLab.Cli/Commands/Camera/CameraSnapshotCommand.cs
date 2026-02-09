using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraSnapshotCommand : AsyncCommand<CameraSnapshotCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<DEVICE>")]
        [Description("Camera device ID or name")]
        public string Device { get; set; } = string.Empty;

        [CommandOption("--path <FILE>")]
        [Description("Output file path (default: ./snapshot.jpg)")]
        public string? OutputPath { get; set; }
    }

    public CameraSnapshotCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateScryptedClient();

        if (!await client.IsHealthyAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Scrypted is not reachable");
            return 1;
        }

        var device = await client.GetDeviceAsync(settings.Device);
        if (device == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Camera '{settings.Device.EscapeMarkup()}' not found");
            return 1;
        }

        if (!device.SupportsSnapshot)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Camera '{device.Name.EscapeMarkup()}' does not support snapshots");
            return 1;
        }

        byte[]? imageBytes = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Taking snapshot from {device.Name.EscapeMarkup()}...", async _ =>
            {
                imageBytes = await client.TakeSnapshotAsync(device.Id);
            });

        if (imageBytes == null || imageBytes.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Failed to capture snapshot (empty response)");
            return 1;
        }

        var outputPath = settings.OutputPath ?? $"snapshot_{device.Name.Replace(" ", "_").ToLowerInvariant()}.jpg";
        await File.WriteAllBytesAsync(outputPath, imageBytes);

        var sizeKb = imageBytes.Length / 1024.0;
        AnsiConsole.MarkupLine($"[green]✓[/] Snapshot saved to [cyan]{outputPath}[/] ({sizeKb:F1} KB)");

        return 0;
    }
}
