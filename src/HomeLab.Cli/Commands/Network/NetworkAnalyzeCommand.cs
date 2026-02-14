using System.ComponentModel;
using HomeLab.Cli.Models.AI;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Network;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Analyzes network event history for anomalies, trends, and security issues.
/// Optionally sends findings to AI for deeper analysis.
/// </summary>
public class NetworkAnalyzeCommand : AsyncCommand<NetworkAnalyzeCommand.Settings>
{
    private readonly IEventLogService _eventLogService;
    private readonly INetworkAnomalyDetector _anomalyDetector;
    private readonly ILlmService _llmService;

    public class Settings : CommandSettings
    {
        [CommandOption("--last <DURATION>")]
        [Description("Analysis window: 1h, 6h, 12h, 24h, 7d (default: 24h)")]
        public string Duration { get; set; } = "24h";

        [CommandOption("--ai")]
        [Description("Include AI-powered analysis of findings")]
        public bool UseAi { get; set; }
    }

    public NetworkAnalyzeCommand(
        IEventLogService eventLogService,
        INetworkAnomalyDetector anomalyDetector,
        ILlmService llmService)
    {
        _eventLogService = eventLogService;
        _anomalyDetector = anomalyDetector;
        _llmService = llmService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var since = ParseDuration(settings.Duration);
        var events = await _eventLogService.ReadEventsAsync(since: since);

        if (events.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No events found.[/] Run [cyan]monitor collect[/] to start logging.");
            return 0;
        }

        var anomalies = _anomalyDetector.DetectAnomalies(events);

        // Header
        AnsiConsole.MarkupLine($"[cyan]Network Analysis[/] — {events.Count} snapshots over {settings.Duration}\n");

        // Anomalies table
        RenderAnomalies(anomalies);

        // Device changes summary
        RenderDeviceChanges(events);

        // Traffic trend
        RenderTrafficTrend(events);

        // Security summary
        RenderSecuritySummary(events);

        // AI analysis
        if (settings.UseAi)
        {
            await RunAiAnalysis(events, anomalies);
        }

        return 0;
    }

    private static void RenderAnomalies(List<NetworkAnomaly> anomalies)
    {
        if (anomalies.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]No anomalies detected[/]\n");
            return;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Yellow);
        table.Title("[yellow]Anomalies[/]");
        table.AddColumn("[yellow]Time[/]");
        table.AddColumn("[yellow]Severity[/]");
        table.AddColumn("[yellow]Type[/]");
        table.AddColumn("[yellow]Description[/]");

        foreach (var a in anomalies)
        {
            var sevColor = a.Severity switch
            {
                "critical" => "red",
                "warning" => "yellow",
                _ => "dim"
            };

            table.AddRow(
                a.Timestamp.ToLocalTime().ToString("MM-dd HH:mm"),
                $"[{sevColor}]{a.Severity}[/]",
                a.Type,
                Markup.Escape(a.Description));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void RenderDeviceChanges(List<EventLogEntry> events)
    {
        var allDevicesSeen = new Dictionary<string, DeviceBrief>();
        var firstSeen = new Dictionary<string, DateTime>();
        var lastSeen = new Dictionary<string, DateTime>();

        foreach (var evt in events)
        {
            if (evt.Network?.Devices == null)
            {
                continue;
            }

            foreach (var d in evt.Network.Devices)
            {
                allDevicesSeen[d.Ip] = d;
                if (!firstSeen.ContainsKey(d.Ip))
                {
                    firstSeen[d.Ip] = evt.Timestamp;
                }

                lastSeen[d.Ip] = evt.Timestamp;
            }
        }

        if (allDevicesSeen.Count == 0)
        {
            return;
        }

        // Devices that appeared after the first snapshot
        var firstSnapshot = events.First().Network?.Devices?.Select(d => d.Ip).ToHashSet() ?? new HashSet<string>();
        var newDevices = allDevicesSeen.Keys.Where(ip => !firstSnapshot.Contains(ip)).ToList();

        if (newDevices.Count > 0)
        {
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.BorderColor(Color.Green);
            table.Title($"[green]New Devices ({newDevices.Count})[/]");
            table.AddColumn("[yellow]IP[/]");
            table.AddColumn("[yellow]Hostname[/]");
            table.AddColumn("[yellow]Vendor[/]");
            table.AddColumn("[yellow]First Seen[/]");

            foreach (var ip in newDevices.Take(20))
            {
                var d = allDevicesSeen[ip];
                table.AddRow(
                    d.Ip,
                    Markup.Escape(d.Hostname ?? "-"),
                    Markup.Escape(d.Vendor ?? "-"),
                    firstSeen[ip].ToLocalTime().ToString("MM-dd HH:mm"));
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"[dim]Total unique devices seen: {allDevicesSeen.Count}[/]");
    }

    private static void RenderTrafficTrend(List<EventLogEntry> events)
    {
        var trafficValues = events
            .Where(e => e.Network?.Traffic != null)
            .Select(e => e.Network!.Traffic!.TotalBytes)
            .ToList();

        if (trafficValues.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No traffic data available (ntopng offline?)[/]\n");
            return;
        }

        var min = trafficValues.Min();
        var max = trafficValues.Max();
        var avg = (long)trafficValues.Average();

        AnsiConsole.MarkupLine($"[cyan]Traffic Trend:[/] min={NetworkAnomalyDetector.FormatBytes(min)} | avg={NetworkAnomalyDetector.FormatBytes(avg)} | max={NetworkAnomalyDetector.FormatBytes(max)}");

        // Show top talkers from latest snapshot
        var latest = events.Last().Network?.Traffic;
        if (latest?.TopTalkers.Count > 0)
        {
            var table = new Table();
            table.Border(TableBorder.Simple);
            table.AddColumn("[yellow]Top Talkers[/]");
            table.AddColumn("[yellow]Traffic[/]");

            foreach (var t in latest.TopTalkers)
            {
                var label = t.Name ?? t.Ip;
                table.AddRow(Markup.Escape(label), NetworkAnomalyDetector.FormatBytes(t.TotalBytes));
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
    }

    private static void RenderSecuritySummary(List<EventLogEntry> events)
    {
        var totalCritical = 0;
        var totalHigh = 0;
        var totalAlerts = 0;
        var signatures = new Dictionary<string, int>();

        foreach (var evt in events)
        {
            var sec = evt.Network?.Security;
            if (sec == null)
            {
                continue;
            }

            totalCritical += sec.CriticalCount;
            totalHigh += sec.HighCount;
            totalAlerts += sec.TotalAlerts;

            foreach (var alert in sec.RecentAlerts)
            {
                if (!signatures.TryAdd(alert.Signature, 1))
                {
                    signatures[alert.Signature]++;
                }
            }
        }

        if (totalAlerts == 0)
        {
            AnsiConsole.MarkupLine("[dim]No security alerts (Suricata offline or clean)[/]\n");
            return;
        }

        var critColor = totalCritical > 0 ? "red" : "green";
        var highColor = totalHigh > 0 ? "yellow" : "green";
        AnsiConsole.MarkupLine($"[cyan]Security:[/] [{critColor}]{totalCritical} critical[/] | [{highColor}]{totalHigh} high[/] | {totalAlerts} total alerts");

        if (signatures.Count > 0)
        {
            var table = new Table();
            table.Border(TableBorder.Simple);
            table.AddColumn("[yellow]Top Signatures[/]");
            table.AddColumn("[yellow]Count[/]");

            foreach (var (sig, count) in signatures.OrderByDescending(s => s.Value).Take(5))
            {
                table.AddRow(Markup.Escape(sig), count.ToString());
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
    }

    private async Task RunAiAnalysis(List<EventLogEntry> events, List<NetworkAnomaly> anomalies)
    {
        if (!await _llmService.IsAvailableAsync())
        {
            AnsiConsole.MarkupLine("[red]AI not configured[/] — add services.ai.token to config");
            return;
        }

        var prompt = BuildNetworkPrompt(events, anomalies);

        LlmResponse? response = null;

        await AnsiConsole.Status()
            .StartAsync($"AI analyzing network ({_llmService.ProviderName})...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                response = await _llmService.SendMessageAsync(
                    "You are a network security analyst for a homelab. Analyze the network data and anomalies. " +
                    "Focus on: suspicious activity, new unknown devices, unusual traffic patterns, security threats. " +
                    "Be concise and actionable. Use plain text, no markdown.",
                    prompt, 512);
            });

        if (response?.Success == true)
        {
            var panel = new Panel(Markup.Escape(response.Content))
                .Header("[cyan]AI Network Analysis[/]")
                .BorderColor(Color.Cyan)
                .RoundedBorder()
                .Padding(1, 1);

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();

            const decimal inputPricePerMillion = 1.00m;
            const decimal outputPricePerMillion = 5.00m;
            var cost = (response.InputTokens * inputPricePerMillion / 1_000_000m)
                     + (response.OutputTokens * outputPricePerMillion / 1_000_000m);
            AnsiConsole.MarkupLine($"[dim]Tokens: {response.InputTokens} in, {response.OutputTokens} out (~${cost:F4})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]AI request failed: {response?.Error ?? "Unknown error"}[/]");
        }
    }

    private static string BuildNetworkPrompt(List<EventLogEntry> events, List<NetworkAnomaly> anomalies)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Network analysis over {events.Count} snapshots ({events.First().Timestamp:g} to {events.Last().Timestamp:g} UTC):");
        sb.AppendLine();

        // Anomalies
        if (anomalies.Count > 0)
        {
            sb.AppendLine($"Detected anomalies ({anomalies.Count}):");
            foreach (var a in anomalies.Take(20))
            {
                sb.AppendLine($"  [{a.Severity}] {a.Timestamp:HH:mm} {a.Type}: {a.Description}");
            }

            sb.AppendLine();
        }

        // Latest device list
        var latestDevices = events.Last().Network?.Devices;
        if (latestDevices?.Count > 0)
        {
            sb.AppendLine($"Current devices ({latestDevices.Count}):");
            foreach (var d in latestDevices)
            {
                sb.AppendLine($"  {d.Ip} — {d.Hostname ?? d.Vendor ?? "unknown"} (MAC: {d.Mac ?? "?"})");
            }

            sb.AppendLine();
        }

        // Traffic
        var trafficValues = events
            .Where(e => e.Network?.Traffic != null)
            .Select(e => e.Network!.Traffic!.TotalBytes)
            .ToList();
        if (trafficValues.Count > 0)
        {
            sb.AppendLine($"Traffic: min={NetworkAnomalyDetector.FormatBytes(trafficValues.Min())}, avg={NetworkAnomalyDetector.FormatBytes((long)trafficValues.Average())}, max={NetworkAnomalyDetector.FormatBytes(trafficValues.Max())}");
        }

        // Security alerts
        var allAlerts = events
            .Where(e => e.Network?.Security != null)
            .SelectMany(e => e.Network!.Security!.RecentAlerts)
            .ToList();
        if (allAlerts.Count > 0)
        {
            sb.AppendLine($"Security alerts ({allAlerts.Count}):");
            foreach (var a in allAlerts.Take(15))
            {
                sb.AppendLine($"  [{a.Severity}] {a.Signature}: {a.SourceIp} -> {a.DestinationIp} ({a.Category ?? "?"})");
            }
        }

        return sb.ToString();
    }

    private static DateTime ParseDuration(string duration)
    {
        var now = DateTime.UtcNow;
        var d = duration.ToLowerInvariant().Trim();

        if (d.EndsWith('h') && int.TryParse(d[..^1], out var hours))
        {
            return now.AddHours(-hours);
        }

        if (d.EndsWith('d') && int.TryParse(d[..^1], out var days))
        {
            return now.AddDays(-days);
        }

        return now.AddHours(-24);
    }
}
