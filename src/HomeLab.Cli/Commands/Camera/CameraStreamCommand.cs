using System.ComponentModel;
using System.Diagnostics;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraStreamCommand : AsyncCommand<CameraStreamCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<DEVICE>")]
        [Description("Camera device ID or name")]
        public string Device { get; set; } = string.Empty;

        [CommandOption("--open")]
        [Description("Open stream URL in default browser")]
        public bool Open { get; set; }

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public CameraStreamCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new FigletText("Camera Stream").Centered().Color(Color.Blue));
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateScryptedClient();

        if (!await client.IsHealthyAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Scrypted is not reachable");
            return 1;
        }

        // Resolve device
        var device = await client.GetDeviceAsync(settings.Device);
        if (device == null)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Camera '{settings.Device.EscapeMarkup()}' not found");
            AnsiConsole.MarkupLine("Use [cyan]homelab camera list[/] to see available cameras.");
            return 1;
        }

        var streamInfo = await client.GetStreamInfoAsync(device.Id);
        streamInfo.DeviceName = device.Name;

        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, streamInfo))
        {
            return 0;
        }

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(new Markup("[yellow]Camera:[/]"), new Markup($"[cyan]{device.Name.EscapeMarkup()}[/]"));
        grid.AddRow(new Markup("[yellow]Status:[/]"), new Markup(device.Online ? "[green]Online[/]" : "[red]Offline[/]"));

        if (!string.IsNullOrEmpty(streamInfo.RtspUrl))
        {
            grid.AddRow(new Markup("[yellow]RTSP:[/]"), new Markup($"[dim]{streamInfo.RtspUrl}[/]"));
        }

        if (!string.IsNullOrEmpty(streamInfo.WebRtcUrl))
        {
            grid.AddRow(new Markup("[yellow]WebRTC:[/]"), new Markup($"[dim]{streamInfo.WebRtcUrl}[/]"));
        }

        if (!string.IsNullOrEmpty(streamInfo.ManagementUrl))
        {
            grid.AddRow(new Markup("[yellow]Management:[/]"), new Markup($"[dim]{streamInfo.ManagementUrl}[/]"));
        }

        AnsiConsole.Write(
            new Panel(grid)
                .Header($"[yellow]Stream: {device.Name.EscapeMarkup()}[/]")
                .BorderColor(Color.Green)
                .RoundedBorder());

        if (settings.Open && !string.IsNullOrEmpty(streamInfo.WebRtcUrl))
        {
            AnsiConsole.MarkupLine($"\n[dim]Opening stream in browser...[/]");
            Process.Start(new ProcessStartInfo(streamInfo.WebRtcUrl) { UseShellExecute = true });
        }
        else if (!settings.Open)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Use --open to open in browser, or copy RTSP URL for VLC:[/]");
            if (!string.IsNullOrEmpty(streamInfo.RtspUrl))
            {
                AnsiConsole.MarkupLine($"[dim]  vlc {streamInfo.RtspUrl}[/]");
            }
        }

        return 0;
    }
}
