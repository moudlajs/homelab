using System.Text.RegularExpressions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Interactive shell mode. Launches when 'homelab' is run with no arguments.
/// Provides a REPL with tab completion and command history.
/// </summary>
public class ShellCommand : Command<ShellCommand.Settings>
{
    public class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Welcome banner
        AnsiConsole.Write(
            new FigletText("HomeLab")
                .Centered()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[dim]Interactive shell. Type commands without 'homelab' prefix.[/]");
        AnsiConsole.MarkupLine("[dim]Tab for completion, Up/Down for history. Type 'exit' to quit.[/]");
        AnsiConsole.WriteLine();

        // Setup ReadLine
        ReadLine.HistoryEnabled = true;
        ReadLine.AutoCompletionHandler = new ShellCompletionHandler();

        // Ctrl+C handling — don't kill the shell
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.WriteLine();
        };

        try
        {
            while (true)
            {
                string? input;
                try
                {
                    input = ReadLine.Read("homelab> ");
                }
                catch (OperationCanceledException)
                {
                    continue;
                }

                if (input == null)
                {
                    break; // EOF
                }

                var trimmed = input.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                // Special commands
                switch (trimmed.ToLowerInvariant())
                {
                    case "exit":
                    case "quit":
                    case "q":
                        AnsiConsole.MarkupLine("[dim]Bye![/]");
                        return 0;

                    case "clear":
                    case "cls":
                        AnsiConsole.Clear();
                        continue;

                    case "help":
                    case "?":
                        ShowHelp();
                        continue;

                    case "shell":
                        AnsiConsole.MarkupLine("[yellow]Already in interactive shell.[/]");
                        continue;
                }

                // Parse input to args and execute
                var args = ParseInput(trimmed);

                try
                {
                    var app = Program.CreateCommandApp();
                    app.Run(args);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                }

                AnsiConsole.WriteLine();
            }
        }
        finally
        {
            ReadLine.AutoCompletionHandler = null;
        }

        return 0;
    }

    private static string[] ParseInput(string input)
    {
        // Handle quoted strings: tv launch "Dog TV"
        var matches = Regex.Matches(input, @"[^\s""]+|""([^""]*)""");
        return matches
            .Cast<Match>()
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Value)
            .ToArray();
    }

    private static void ShowHelp()
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[yellow]Command[/]");
        table.AddColumn("[yellow]Description[/]");

        // System
        table.AddRow("[cyan]status[/]", "Homelab status dashboard");
        table.AddRow("[cyan]service[/] start/stop/restart", "Manage service lifecycle");
        table.AddRow("[cyan]config[/] view/edit/backup/restore", "Manage configuration");
        table.AddRow("[cyan]logs[/] <container>", "View container logs");
        table.AddRow("[cyan]image-update[/]", "Update container images");
        table.AddRow("[cyan]cleanup[/]", "Clean up Docker resources");
        table.AddRow("", "");

        // Monitoring
        table.AddRow("[cyan]monitor[/] report/ask/alerts/targets/dashboard/collect/history/schedule", "AI monitoring & metrics");
        table.AddRow("[cyan]vpn[/] status/up/down/devices", "VPN management (Tailscale)");
        table.AddRow("[cyan]network[/] scan/ports/devices/traffic/intrusion/status", "Network monitoring");
        table.AddRow("[cyan]dns[/] stats/blocked", "DNS & ad-blocking");
        table.AddRow("", "");

        // Devices & services
        table.AddRow("[cyan]tv[/] on/off/apps/launch/key/status/setup", "LG TV control");
        table.AddRow("[cyan]camera[/] list/status/setup/stream/snapshot/recordings", "Security cameras (Scrypted)");
        table.AddRow("[cyan]ha[/] status/control/get/list", "Home Assistant");
        table.AddRow("[cyan]traefik[/] status/routes/services/middlewares", "Traefik proxy");
        table.AddRow("[cyan]uptime[/] status/alerts/add/remove", "Uptime monitoring");
        table.AddRow("[cyan]speedtest[/] run/stats", "Speed testing");
        table.AddRow("[cyan]remote[/] connect/list/status/sync/remove", "Remote management");
        table.AddRow("", "");

        // Meta
        table.AddRow("[cyan]tui[/]", "Live dashboard");
        table.AddRow("[cyan]version[/]", "Version info");
        table.AddRow("[cyan]self-update[/]", "Update CLI");
        table.AddRow("[dim]clear[/]", "Clear screen");
        table.AddRow("[dim]exit[/]", "Exit shell");

        AnsiConsole.Write(table);
    }
}
