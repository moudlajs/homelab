using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraListCommand : AsyncCommand<CameraListCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }

        [CommandOption("--online")]
        [Description("Show only online cameras")]
        public bool OnlineOnly { get; set; }
    }

    public CameraListCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new FigletText("Cameras").Centered().Color(Color.Blue));
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateScryptedClient();

        if (!await client.IsHealthyAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Scrypted is not reachable");
            AnsiConsole.MarkupLine($"\nMake sure Scrypted is running. Start with: [cyan]docker compose up -d scrypted[/]");
            return 1;
        }

        var devices = await client.GetDevicesAsync();

        if (settings.OnlineOnly)
        {
            devices = devices.Where(d => d.Online).ToList();
        }

        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, devices))
        {
            return 0;
        }

        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No cameras found in Scrypted.[/]");
            AnsiConsole.MarkupLine("Add cameras via the Scrypted web UI.");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Name[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Snapshot[/]");
        table.AddColumn("[yellow]Stream[/]");
        table.AddColumn("[yellow]Motion[/]");
        table.AddColumn("[yellow]Recording[/]");

        foreach (var device in devices)
        {
            var statusColor = device.Online ? "green" : "red";
            var statusText = device.Online ? "Online" : "Offline";

            table.AddRow(
                $"[cyan]{device.Name.EscapeMarkup()}[/]",
                $"[{statusColor}]{statusText}[/]",
                device.Type,
                device.SupportsSnapshot ? "[green]✓[/]" : "[dim]—[/]",
                device.SupportsStreaming ? "[green]✓[/]" : "[dim]—[/]",
                device.HasMotionSensor ? "[green]✓[/]" : "[dim]—[/]",
                device.SupportsRecording ? "[green]✓[/]" : "[dim]—[/]");
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        var online = devices.Count(d => d.Online);
        AnsiConsole.Write(
            new Panel($"[green]Online:[/] {online}  [yellow]Total:[/] {devices.Count}")
                .Header("[yellow]Cameras[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder());

        return 0;
    }
}
