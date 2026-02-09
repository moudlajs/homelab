using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Camera;

public class CameraStatusCommand : AsyncCommand<CameraStatusCommand.Settings>
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
    }

    public CameraStatusCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new FigletText("Camera Status").Centered().Color(Color.Blue));
        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateScryptedClient();
        var status = await client.GetSystemStatusAsync();

        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, status))
        {
            return 0;
        }

        var statusColor = status.IsOnline ? "green" : "red";
        var statusIcon = status.IsOnline ? "✓" : "✗";
        AnsiConsole.MarkupLine($"[{statusColor}]{statusIcon}[/] Scrypted is [bold]{(status.IsOnline ? "Running" : "Unavailable")}[/]\n");

        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow(new Markup("[yellow]URL:[/]"), new Markup($"[cyan]{status.BaseUrl}[/]"));
        grid.AddRow(new Markup("[yellow]Total Cameras:[/]"), new Markup($"[cyan]{status.TotalDevices}[/]"));
        grid.AddRow(new Markup("[yellow]Online:[/]"), new Markup($"[green]{status.OnlineDevices}[/]"));
        grid.AddRow(new Markup("[yellow]Recording:[/]"), new Markup($"[cyan]{status.RecordingDevices}[/]"));

        AnsiConsole.Write(
            new Panel(grid)
                .Header("[yellow]System Info[/]")
                .BorderColor(status.IsOnline ? Color.Green : Color.Red)
                .RoundedBorder());

        if (!status.IsOnline)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Start Scrypted with: [cyan]docker compose up -d scrypted[/]");
            AnsiConsole.MarkupLine("Configure with: [cyan]homelab camera setup[/]");
        }

        return 0;
    }
}
