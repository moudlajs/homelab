using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Network;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Network;

/// <summary>
/// Displays comprehensive network health and security status.
/// Combines data from nmap, ntopng, and Suricata.
/// </summary>
public class NetworkStatusCommand : AsyncCommand
{
    private readonly IServiceClientFactory _clientFactory;
    private readonly INmapService _nmapService;

    public NetworkStatusCommand(IServiceClientFactory clientFactory, INmapService nmapService)
    {
        _clientFactory = clientFactory;
        _nmapService = nmapService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Network Status")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        // Get all service clients
        var ntopngClient = _clientFactory.CreateNtopngClient();
        var suricataClient = _clientFactory.CreateSuricataClient();

        // Check health of all services
        await AnsiConsole.Status()
            .StartAsync("Checking network services...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                await Task.Delay(300, cancellationToken);
            });

        var ntopngHealth = await ntopngClient.GetHealthInfoAsync();
        var suricataHealth = await suricataClient.GetHealthInfoAsync();
        var nmapAvailable = _nmapService.IsNmapAvailable();

        // Service Health Panel
        var healthGrid = new Grid();
        healthGrid.AddColumn();
        healthGrid.AddColumn();
        healthGrid.AddColumn();

        healthGrid.AddRow(
            "[bold]Service[/]",
            "[bold]Status[/]",
            "[bold]Details[/]"
        );

        // nmap
        healthGrid.AddRow(
            "nmap",
            nmapAvailable ? "[green]✓ Available[/]" : "[red]✗ Not installed[/]",
            nmapAvailable ? "Network scanning ready" : "Install with: brew install nmap"
        );

        // ntopng
        var ntopngStatus = ntopngHealth.IsHealthy ? "[green]✓ Running[/]" : "[yellow]⚠ Offline[/]";
        healthGrid.AddRow(
            "ntopng",
            ntopngStatus,
            ntopngHealth.Message ?? "Traffic monitoring"
        );

        // Suricata
        var suricataStatus = suricataHealth.IsHealthy ? "[green]✓ Running[/]" : "[yellow]⚠ Offline[/]";
        healthGrid.AddRow(
            "Suricata",
            suricataStatus,
            suricataHealth.Message ?? "Intrusion detection"
        );

        var healthPanel = new Panel(healthGrid)
        {
            Header = new PanelHeader("[yellow]Service Health[/]"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(healthPanel);
        AnsiConsole.WriteLine();

        // Get network statistics
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Gathering network statistics...", async ctx =>
            {
                await Task.Delay(500, cancellationToken);
            });

        // Traffic Statistics (from ntopng)
        if (ntopngHealth.IsHealthy)
        {
            try
            {
                var devices = await ntopngClient.GetDevicesAsync();
                var trafficStats = await ntopngClient.GetTrafficStatsAsync();
                var activeDevices = devices.Count(d => d.IsActive);

                var trafficGrid = new Grid();
                trafficGrid.AddColumn();
                trafficGrid.AddColumn();

                trafficGrid.AddRow("[cyan]Active Devices:[/]", $"{activeDevices}");
                trafficGrid.AddRow("[cyan]Total Devices:[/]", $"{devices.Count}");
                trafficGrid.AddRow("[cyan]Total Traffic:[/]", FormatBytes(trafficStats.TotalBytesTransferred));
                trafficGrid.AddRow("[cyan]Active Flows:[/]", $"{trafficStats.ActiveFlows}");

                var trafficPanel = new Panel(trafficGrid)
                {
                    Header = new PanelHeader("[green]Network Traffic[/]"),
                    Border = BoxBorder.Rounded
                };

                AnsiConsole.Write(trafficPanel);
                AnsiConsole.WriteLine();

                // Top Talkers
                if (trafficStats.TopTalkers.Count > 0)
                {
                    var topTalkersTable = new Table();
                    topTalkersTable.Border(TableBorder.Rounded);
                    topTalkersTable.Title = new TableTitle("[yellow]Top Talkers[/]");
                    topTalkersTable.AddColumn("[cyan]Rank[/]");
                    topTalkersTable.AddColumn("[cyan]Device[/]");
                    topTalkersTable.AddColumn("[cyan]Total Traffic[/]");

                    var topTalkers = trafficStats.TopTalkers.Take(5).ToList();
                    for (int i = 0; i < topTalkers.Count; i++)
                    {
                        var talker = topTalkers[i];
                        var rankColor = i switch
                        {
                            0 => "gold1",
                            1 => "silver",
                            2 => "orange3",
                            _ => "white"
                        };

                        topTalkersTable.AddRow(
                            $"[{rankColor}]{i + 1}[/]",
                            talker.DeviceName,
                            $"[green]{FormatBytes(talker.TotalBytes)}[/]"
                        );
                    }

                    AnsiConsole.Write(topTalkersTable);
                    AnsiConsole.WriteLine();
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Failed to get traffic stats: {ex.Message}");
            }
        }

        // Security Alerts (from Suricata)
        if (suricataHealth.IsHealthy)
        {
            try
            {
                var alerts = await suricataClient.GetAlertsAsync(limit: 100);
                var recentAlerts = alerts.Where(a => a.Timestamp > DateTime.Now.AddHours(-24)).ToList();

                var criticalCount = recentAlerts.Count(a => a.Severity == "critical");
                var highCount = recentAlerts.Count(a => a.Severity == "high");
                var mediumCount = recentAlerts.Count(a => a.Severity == "medium");
                var lowCount = recentAlerts.Count(a => a.Severity == "low");

                var alertsGrid = new Grid();
                alertsGrid.AddColumn();
                alertsGrid.AddColumn();

                alertsGrid.AddRow("[red]Critical:[/]", $"{criticalCount}");
                alertsGrid.AddRow("[orange1]High:[/]", $"{highCount}");
                alertsGrid.AddRow("[yellow]Medium:[/]", $"{mediumCount}");
                alertsGrid.AddRow("[dim]Low:[/]", $"{lowCount}");
                alertsGrid.AddRow("[cyan]Total (24h):[/]", $"{recentAlerts.Count}");

                var alertsPanel = new Panel(alertsGrid)
                {
                    Header = new PanelHeader("[red]Security Alerts (24h)[/]"),
                    Border = BoxBorder.Rounded
                };

                AnsiConsole.Write(alertsPanel);
                AnsiConsole.WriteLine();

                // Show latest critical alert if exists
                var latestCritical = recentAlerts
                    .Where(a => a.Severity == "critical")
                    .OrderByDescending(a => a.Timestamp)
                    .FirstOrDefault();

                if (latestCritical != null)
                {
                    var timeAgo = FormatTimeAgo(latestCritical.Timestamp);

                    var criticalAlertPanel = new Panel(new Markup(
                        $"[red]{latestCritical.AlertType}[/]\n" +
                        $"[dim]From:[/] {latestCritical.SourceIp}:{latestCritical.SourcePort}\n" +
                        $"[dim]To:[/] {latestCritical.DestinationIp}:{latestCritical.DestinationPort}\n" +
                        $"[dim]Time:[/] {timeAgo}"
                    ))
                    {
                        Header = new PanelHeader("[red]⚠ Latest Critical Alert[/]"),
                        Border = BoxBorder.Rounded,
                        BorderStyle = new Style(Color.Red)
                    };

                    AnsiConsole.Write(criticalAlertPanel);
                    AnsiConsole.WriteLine();
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠[/] Failed to get security alerts: {ex.Message}");
            }
        }

        // Overall Status
        var overallStatus = DetermineOverallStatus(ntopngHealth.IsHealthy, suricataHealth.IsHealthy, nmapAvailable);
        var statusColor = overallStatus switch
        {
            "healthy" => "green",
            "warning" => "yellow",
            "critical" => "red",
            _ => "white"
        };

        AnsiConsole.MarkupLine($"[{statusColor}]Overall Network Status: {overallStatus.ToUpper()}[/]");
        AnsiConsole.MarkupLine($"[dim]Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}[/]");

        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private static string FormatTimeAgo(DateTime timestamp)
    {
        var timeSpan = DateTime.Now - timestamp;

        if (timeSpan.TotalMinutes < 1)
        {
            return "Just now";
        }
        if (timeSpan.TotalMinutes < 60)
        {
            return $"{(int)timeSpan.TotalMinutes}m ago";
        }
        if (timeSpan.TotalHours < 24)
        {
            return $"{(int)timeSpan.TotalHours}h ago";
        }

        return timestamp.ToString("MM/dd HH:mm");
    }

    private static string DetermineOverallStatus(bool ntopngHealthy, bool suricataHealthy, bool nmapAvailable)
    {
        var servicesHealthy = 0;
        var totalServices = 3;

        if (nmapAvailable)
        {
            servicesHealthy++;
        }
        if (ntopngHealthy)
        {
            servicesHealthy++;
        }
        if (suricataHealthy)
        {
            servicesHealthy++;
        }

        if (servicesHealthy == totalServices)
        {
            return "healthy";
        }
        if (servicesHealthy >= 2)
        {
            return "warning";
        }
        return "critical";
    }
}
