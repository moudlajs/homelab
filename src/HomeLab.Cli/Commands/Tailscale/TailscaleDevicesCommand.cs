using System.ComponentModel;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Output;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Tailscale;

public class TailscaleDevicesCommand : AsyncCommand<TailscaleDevicesCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly IOutputFormatter _formatter;

    public class Settings : CommandSettings
    {
        [CommandOption("--online")]
        [Description("Show only online devices")]
        public bool OnlineOnly { get; set; }

        [CommandOption("--output <FORMAT>")]
        [Description("Output format: table, json, csv, yaml")]
        public string? OutputFormat { get; set; }

        [CommandOption("--export <FILE>")]
        [Description("Export to file")]
        public string? ExportFile { get; set; }
    }

    public TailscaleDevicesCommand(IServiceClientFactory clientFactory, IOutputFormatter formatter)
    {
        _clientFactory = clientFactory;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Tailscale Devices")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        var client = _clientFactory.CreateTailscaleClient();

        if (!await client.IsTailscaleInstalledAsync())
        {
            AnsiConsole.MarkupLine("[red]✗[/] Tailscale CLI is not installed");
            return 1;
        }

        await AnsiConsole.Status()
            .StartAsync("Fetching devices...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300);
            });

        var status = await client.GetStatusAsync();

        if (!status.IsConnected)
        {
            AnsiConsole.MarkupLine("[red]✗[/] Not connected to Tailscale");
            AnsiConsole.MarkupLine("Use [cyan]homelab tailscale up[/] to connect.");
            return 1;
        }

        // Build device list (self + peers)
        var allDevices = new List<TailscaleDevice>();
        if (status.Self != null)
        {
            allDevices.Add(status.Self);
        }

        allDevices.AddRange(status.Peers);

        var devices = settings.OnlineOnly
            ? allDevices.Where(d => d.Online).ToList()
            : allDevices;

        // Try export if requested
        if (await OutputHelper.TryExportAsync(_formatter, settings.OutputFormat, settings.ExportFile, devices))
        {
            return 0;
        }

        if (devices.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No devices found.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Hostname[/]");
        table.AddColumn("[yellow]Status[/]");
        table.AddColumn("[yellow]Tailscale IP[/]");
        table.AddColumn("[yellow]DNS Name[/]");
        table.AddColumn("[yellow]OS[/]");
        table.AddColumn("[yellow]Last Seen[/]");

        foreach (var device in devices.OrderByDescending(d => d.Online).ThenBy(d => d.HostName))
        {
            var isSelf = status.Self != null && device.Id == status.Self.Id;
            var hostNameDisplay = isSelf ? $"{device.HostName} [dim](self)[/]" : device.HostName;

            var statusColor = device.Online ? "green" : "red";
            var statusText = device.Online ? "Online" : "Offline";

            var lastSeen = device.LastSeen.HasValue
                ? FormatTimeAgo(device.LastSeen.Value)
                : (device.Online ? "Now" : "Never");

            table.AddRow(
                hostNameDisplay,
                $"[{statusColor}]{statusText}[/]",
                device.PrimaryIP ?? "N/A",
                $"[dim]{device.DNSName}[/]",
                device.OS,
                $"[dim]{lastSeen}[/]");
        }

        AnsiConsole.Write(table);

        AnsiConsole.WriteLine();
        var onlineCount = devices.Count(d => d.Online);
        AnsiConsole.Write(
            new Panel($"[green]Online:[/] {onlineCount}  [yellow]Total:[/] {devices.Count}")
                .Header("[yellow]Summary[/]")
                .BorderColor(Color.Grey)
                .RoundedBorder());

        return 0;
    }

    private static string FormatTimeAgo(DateTime dateTime)
    {
        var timeAgo = DateTime.UtcNow - dateTime;
        if (timeAgo.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (timeAgo.TotalMinutes < 60)
        {
            return $"{(int)timeAgo.TotalMinutes}m ago";
        }

        if (timeAgo.TotalHours < 24)
        {
            return $"{(int)timeAgo.TotalHours}h ago";
        }

        return $"{(int)timeAgo.TotalDays}d ago";
    }
}
