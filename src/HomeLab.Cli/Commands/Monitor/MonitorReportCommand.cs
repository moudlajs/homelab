using System.ComponentModel;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// AI-powered homelab health report.
/// Collects system, Docker, and Prometheus data, then sends to LLM for analysis.
/// </summary>
public class MonitorReportCommand : AsyncCommand<MonitorReportCommand.Settings>
{
    private readonly ILlmService _llmService;
    private readonly ISystemDataCollector _dataCollector;

    public class Settings : CommandSettings
    {
        [CommandOption("--raw")]
        [Description("Show raw collected data instead of AI summary")]
        public bool Raw { get; set; }
    }

    public MonitorReportCommand(ILlmService llmService, ISystemDataCollector dataCollector)
    {
        _llmService = llmService;
        _dataCollector = dataCollector;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("AI Report")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        // Collect data
        HomelabDataSnapshot? snapshot = null;

        await AnsiConsole.Status()
            .StartAsync("Collecting homelab data...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                snapshot = await _dataCollector.CollectAsync();
            });

        if (snapshot == null)
        {
            AnsiConsole.MarkupLine("[red]Failed to collect data[/]");
            return 1;
        }

        var prompt = _dataCollector.FormatAsPrompt(snapshot);

        // Raw mode: just show the collected data
        if (settings.Raw || !await _llmService.IsAvailableAsync())
        {
            if (!settings.Raw)
            {
                AnsiConsole.MarkupLine("[yellow]AI not configured — showing raw data[/]");
                AnsiConsole.MarkupLine("[dim]Add services.ai.token to ~/.config/homelab/homelab-cli.yaml[/]\n");
            }

            RenderRawData(snapshot);
            return 0;
        }

        // Send to AI
        LlmResponse? response = null;

        await AnsiConsole.Status()
            .StartAsync($"Analyzing with {_llmService.ProviderName}...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);

                var systemPrompt = "You are a homelab monitoring assistant. Analyze the following system data and provide a concise health report. " +
                    "Flag any issues or potential problems, note anything unusual, and give specific recommendations. " +
                    "Be specific with numbers. Keep it under 200 words. Use plain text, no markdown.";

                response = await _llmService.SendMessageAsync(systemPrompt, prompt, 1024);
            });

        if (response == null || !response.Success)
        {
            AnsiConsole.MarkupLine($"[red]AI analysis failed: {response?.Error ?? "Unknown error"}[/]\n");
            AnsiConsole.MarkupLine("[yellow]Falling back to raw data:[/]\n");
            RenderRawData(snapshot);
            return 1;
        }

        // Render AI response
        var panel = new Panel(Markup.Escape(response.Content))
            .Header("[cyan]AI Health Report[/]")
            .BorderColor(Color.Cyan)
            .RoundedBorder()
            .Padding(1, 1);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(FormatCost(response.InputTokens, response.OutputTokens));

        return 0;
    }

    private static void RenderRawData(HomelabDataSnapshot snapshot)
    {
        // System
        if (snapshot.System != null)
        {
            var sysTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Green);
            sysTable.AddColumn("[yellow]Metric[/]");
            sysTable.AddColumn("[yellow]Value[/]");

            sysTable.AddRow("CPU", $"{snapshot.System.CpuCount} cores, {snapshot.System.CpuUsagePercent:F1}% usage");
            sysTable.AddRow("Memory", $"{snapshot.System.UsedMemoryGB:F1}/{snapshot.System.TotalMemoryGB:F1} GB ({snapshot.System.MemoryUsagePercent:F0}%)");
            sysTable.AddRow("Disk", $"{snapshot.System.DiskUsed}/{snapshot.System.DiskTotal} ({snapshot.System.DiskUsagePercent}%)");
            sysTable.AddRow("Disk Free", snapshot.System.DiskAvailable);
            sysTable.AddRow("Uptime", snapshot.System.Uptime);

            AnsiConsole.Write(new Panel(sysTable).Header("[green]System[/]").BorderColor(Color.Green).RoundedBorder());
            AnsiConsole.WriteLine();
        }

        // Docker
        if (snapshot.Docker != null)
        {
            if (snapshot.Docker.Available)
            {
                var dockerTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Blue);
                dockerTable.AddColumn("[yellow]Container[/]");
                dockerTable.AddColumn("[yellow]Status[/]");

                foreach (var c in snapshot.Docker.Containers)
                {
                    var status = c.IsRunning ? "[green]Running[/]" : "[red]Stopped[/]";
                    dockerTable.AddRow(Markup.Escape(c.Name), status);
                }

                AnsiConsole.Write(new Panel(dockerTable)
                    .Header($"[blue]Docker ({snapshot.Docker.RunningContainers}/{snapshot.Docker.TotalContainers} running)[/]")
                    .BorderColor(Color.Blue).RoundedBorder());
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Docker: not available[/]");
            }

            AnsiConsole.WriteLine();
        }

        // Prometheus
        if (snapshot.Prometheus != null)
        {
            if (snapshot.Prometheus.Available)
            {
                var promInfo = $"Alerts: {snapshot.Prometheus.ActiveAlerts} active | Targets: {snapshot.Prometheus.TargetsUp} up, {snapshot.Prometheus.TargetsDown} down";
                AnsiConsole.Write(new Panel(promInfo).Header("[yellow]Prometheus[/]").BorderColor(Color.Yellow).RoundedBorder());
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Prometheus: not available[/]");
            }

            AnsiConsole.WriteLine();
        }

        // Network
        if (snapshot.Network != null)
        {
            if (snapshot.Network.NmapAvailable && snapshot.Network.Devices.Count > 0)
            {
                var netTable = new Table().Border(TableBorder.Rounded).BorderColor(Color.Purple);
                netTable.AddColumn("[yellow]IP[/]");
                netTable.AddColumn("[yellow]Hostname[/]");
                netTable.AddColumn("[yellow]Vendor[/]");

                foreach (var d in snapshot.Network.Devices)
                {
                    netTable.AddRow(
                        Markup.Escape(d.Ip),
                        Markup.Escape(d.Hostname),
                        Markup.Escape(d.Vendor));
                }

                AnsiConsole.Write(new Panel(netTable)
                    .Header($"[purple]Network ({snapshot.Network.DevicesFound} devices)[/]")
                    .BorderColor(Color.Purple).RoundedBorder());
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Network scan: not available[/]");
            }

            if (snapshot.Network.SuricataAvailable && snapshot.Network.SecurityAlerts > 0)
            {
                AnsiConsole.MarkupLine($"[red]Security alerts: {snapshot.Network.SecurityAlerts} ({snapshot.Network.CriticalAlerts} critical)[/]");
            }

            AnsiConsole.WriteLine();
        }

        // Errors
        if (snapshot.Errors.Count > 0)
        {
            AnsiConsole.MarkupLine("[dim]Collection notes:[/]");
            foreach (var error in snapshot.Errors)
            {
                AnsiConsole.MarkupLine($"[dim]  - {Markup.Escape(error)}[/]");
            }
        }
    }

    private static string FormatCost(int inputTokens, int outputTokens)
    {
        const decimal inputPricePerMillion = 1.00m;
        const decimal outputPricePerMillion = 5.00m;

        var cost = (inputTokens * inputPricePerMillion / 1_000_000m)
                 + (outputTokens * outputPricePerMillion / 1_000_000m);

        return $"[dim]Tokens: {inputTokens} in, {outputTokens} out (~${cost:F4})[/]";
    }
}
