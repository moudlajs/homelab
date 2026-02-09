namespace HomeLab.Cli.Commands;

/// <summary>
/// Tab completion handler for the interactive shell.
/// Provides command and subcommand completion.
/// </summary>
public class ShellCompletionHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; } = { ' ' };

    private static readonly Dictionary<string, string[]> CommandTree = new()
    {
        [""] = new[]
        {
            "status", "service", "config", "logs", "image-update", "cleanup",
            "version", "self-update", "tui",
            "vpn", "dns", "monitor", "remote", "uptime", "speedtest",
            "ha", "traefik", "network", "tv",
            "completion", "help", "clear", "exit"
        },
        ["vpn"] = new[] { "status", "up", "down", "devices" },
        ["dns"] = new[] { "stats", "blocked" },
        ["monitor"] = new[] { "report", "ask", "alerts", "targets", "dashboard" },
        ["remote"] = new[] { "connect", "list", "status", "sync", "remove" },
        ["uptime"] = new[] { "status", "alerts", "add", "remove" },
        ["speedtest"] = new[] { "run", "stats" },
        ["ha"] = new[] { "status", "control", "get", "list" },
        ["traefik"] = new[] { "status", "routes", "services", "middlewares" },
        ["network"] = new[] { "scan", "ports", "devices", "traffic", "intrusion", "status" },
        ["tv"] = new[] { "status", "on", "off", "setup", "apps", "launch", "key", "debug" }
    };

    public string[] GetSuggestions(string text, int index)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // No input yet — show top-level commands
        if (parts.Length == 0)
        {
            return CommandTree[""];
        }

        // Typing first word — complete top-level commands
        if (parts.Length == 1 && !text.EndsWith(' '))
        {
            return CommandTree[""]
                .Where(c => c.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        // First word complete, typing second — complete subcommands
        var firstWord = parts[0].ToLowerInvariant();
        if (CommandTree.TryGetValue(firstWord, out var subcommands))
        {
            if (parts.Length == 1 && text.EndsWith(' '))
            {
                return subcommands;
            }

            if (parts.Length == 2 && !text.EndsWith(' '))
            {
                return subcommands
                    .Where(c => c.StartsWith(parts[1], StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }
        }

        return Array.Empty<string>();
    }
}
