using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraRecordingsCommand : AsyncCommand<CameraRecordingsCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--device <DEVICE>")]
        [Description("Filter by camera device ID or name")]
        public string? Device { get; set; }

        [CommandOption("--limit <N>")]
        [Description("Number of recordings to show (default: 20)")]
        [DefaultValue(20)]
        public int Limit { get; set; }

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public CameraRecordingsCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new FigletText("Recordings").Centered().Color(Color.Blue));
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateScryptedClient();

        if (!await client.IsHealthyAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Scrypted is not reachable");
            return 1;
        }

        // Resolve device ID if name given
        string? deviceId = null;
        if (!string.IsNullOrEmpty(settings.Device))
        {
            var device = await client.GetDeviceAsync(settings.Device);
            if (device == null)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Camera '{settings.Device.EscapeMarkup()}' not found");
                return 1;
            }
            deviceId = device.Id;
        }

        var recordings = await client.GetRecordingsAsync(deviceId, settings.Limit);

        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, recordings))
        {
            return 0;
        }

        if (recordings.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No recordings found.[/]");
            AnsiConsole.MarkupLine("[dim]Recordings require Scrypted NVR or a recording plugin.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Camera[/]");
        table.AddColumn("[yellow]Start[/]");
        table.AddColumn("[yellow]Duration[/]");
        table.AddColumn("[yellow]Trigger[/]");

        foreach (var rec in recordings)
        {
            var duration = rec.Duration.TotalMinutes < 1
                ? $"{rec.Duration.TotalSeconds:F0}s"
                : rec.Duration.TotalHours < 1
                    ? $"{rec.Duration.TotalMinutes:F0}m"
                    : $"{rec.Duration.TotalHours:F1}h";

            table.AddRow(
                rec.DeviceName.EscapeMarkup(),
                rec.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                duration,
                rec.TriggerType ?? "[dim]—[/]");
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Showing {recordings.Count} recording(s)[/]");

        return 0;
    }
}
