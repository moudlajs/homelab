using System.ComponentModel;
using HomeLab.Cli.Services.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Opens Grafana dashboards or lists available dashboards.
/// </summary>
public class MonitorDashboardCommand : AsyncCommand<MonitorDashboardCommand.Settings>
{
    private readonly IServiceClientFactory _clientFactory;

    public MonitorDashboardCommand(IServiceClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[uid]")]
        [Description("Dashboard UID to open (leave empty to list all)")]
        public string? Uid { get; set; }

        [CommandOption("--list")]
        [Description("List all dashboards")]
        [DefaultValue(false)]
        public bool List { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = _clientFactory.CreateGrafanaClient();

        // Get dashboards
        var dashboards = await client.GetDashboardsAsync();

        if (dashboards.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No dashboards found.[/]");
            return 0;
        }

        // If UID specified, open that dashboard
        if (!string.IsNullOrEmpty(settings.Uid) && !settings.List)
        {
            var dashboard = dashboards.FirstOrDefault(d => d.Uid == settings.Uid);

            if (dashboard == null)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Dashboard with UID '{settings.Uid}' not found.");
                AnsiConsole.MarkupLine("\nAvailable dashboards:");
                ListDashboards(dashboards);
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Opening dashboard: [cyan]{dashboard.Title}[/]");

            var url = client.GetDashboardUrl(settings.Uid);
            AnsiConsole.MarkupLine($"[dim]URL:[/] [link]{url}[/]");

            await client.OpenDashboardAsync(settings.Uid);
            return 0;
        }

        // List all dashboards
        AnsiConsole.Write(
            new FigletText("Dashboards")
                .Centered()
                .Color(Color.Purple));

        AnsiConsole.WriteLine();

        ListDashboards(dashboards);

        // Show how to open a dashboard
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]To open a dashboard:[/]");
        AnsiConsole.MarkupLine($"  [cyan]homelab monitor dashboard <uid>[/]");

        // Show Grafana URL
        AnsiConsole.WriteLine();
        var grafanaUrl = client.GetDashboardUrl();
        AnsiConsole.MarkupLine($"[dim]Grafana URL:[/] [link]{grafanaUrl}[/]");

        return 0;
    }

    private void ListDashboards(List<Models.DashboardInfo> dashboards)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]UID[/]");
        table.AddColumn("[yellow]Title[/]");
        table.AddColumn("[yellow]Tags[/]");

        foreach (var dashboard in dashboards.OrderBy(d => d.Title))
        {
            var star = dashboard.IsStarred ? "⭐ " : "";
            var tags = dashboard.Tags.Count > 0
                ? string.Join(", ", dashboard.Tags.Select(t => $"[dim]{t}[/]"))
                : "[dim]none[/]";

            table.AddRow(
                $"[cyan]{dashboard.Uid}[/]",
                $"{star}[white]{dashboard.Title}[/]",
                tags
            );
        }

        AnsiConsole.Write(table);
    }
}
