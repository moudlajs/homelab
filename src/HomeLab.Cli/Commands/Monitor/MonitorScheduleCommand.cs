using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Monitor;

/// <summary>
/// Manages a macOS LaunchAgent that runs 'homelab monitor collect --quiet' periodically.
/// </summary>
public class MonitorScheduleCommand : Command<MonitorScheduleCommand.Settings>
{
    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "com.homelab.monitor.plist");

    private const string ExternalDrivePath = "/Volumes/T9";

    private static readonly string LogPath = Directory.Exists(ExternalDrivePath)
        ? Path.Combine(ExternalDrivePath, ".homelab", "monitor-collect.log")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".homelab", "monitor-collect.log");

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action: install or uninstall")]
        public string Action { get; set; } = string.Empty;

        [CommandOption("--interval <SECONDS>")]
        [Description("Collection interval in seconds (default: 600)")]
        [DefaultValue(600)]
        public int Interval { get; set; } = 600;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        switch (settings.Action.ToLowerInvariant())
        {
            case "install":
                return Install(settings.Interval);
            case "uninstall":
                return Uninstall();
            case "status":
                return Status();
            default:
                AnsiConsole.MarkupLine("[red]Unknown action. Use: install, uninstall, or status[/]");
                return 1;
        }
    }

    private static int Install(int interval)
    {
        // Find the homelab binary
        var homelabPath = FindHomelabBinary();
        if (homelabPath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find homelab binary. Make sure it's in your PATH.[/]");
            return 1;
        }

        // Ensure log directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.homelab.monitor</string>
    <key>ProgramArguments</key>
    <array>
        <string>{homelabPath}</string>
        <string>monitor</string>
        <string>collect</string>
        <string>--quiet</string>
    </array>
    <key>StartInterval</key>
    <integer>{interval}</integer>
    <key>RunAtLoad</key>
    <true/>
    <key>StandardOutPath</key>
    <string>{LogPath}</string>
    <key>StandardErrorPath</key>
    <string>{LogPath}</string>
    <key>EnvironmentVariables</key>
    <dict>
        <key>PATH</key>
        <string>/usr/local/bin:/opt/homebrew/bin:/usr/bin:/bin:{Path.GetDirectoryName(homelabPath)}</string>
    </dict>
</dict>
</plist>";

        // Unload existing if present
        if (File.Exists(PlistPath))
        {
            RunLaunchctl("unload", PlistPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
        File.WriteAllText(PlistPath, plistContent);

        var result = RunLaunchctl("load", PlistPath);
        if (result != 0)
        {
            AnsiConsole.MarkupLine("[red]Failed to load LaunchAgent[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Monitor schedule installed[/]");
        AnsiConsole.MarkupLine($"  Interval: every {interval} seconds ({interval / 60} minutes)");
        AnsiConsole.MarkupLine($"  Binary: {homelabPath}");
        AnsiConsole.MarkupLine($"  Plist: {PlistPath}");
        AnsiConsole.MarkupLine($"  Log: {LogPath}");

        return 0;
    }

    private static int Uninstall()
    {
        if (!File.Exists(PlistPath))
        {
            AnsiConsole.MarkupLine("[yellow]No schedule installed[/]");
            return 0;
        }

        RunLaunchctl("unload", PlistPath);
        File.Delete(PlistPath);

        AnsiConsole.MarkupLine("[green]Monitor schedule uninstalled[/]");

        var eventsPath = ResolveEventsPath();
        AnsiConsole.MarkupLine($"[dim]Event log preserved at {eventsPath}[/]");

        return 0;
    }

    private static int Status()
    {
        if (!File.Exists(PlistPath))
        {
            AnsiConsole.MarkupLine("[yellow]Not installed[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Installed[/]");
        AnsiConsole.MarkupLine($"  Plist: {PlistPath}");

        // Check if loaded
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "launchctl",
                    Arguments = "list com.homelab.monitor",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                AnsiConsole.MarkupLine("  Status: [green]running[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("  Status: [yellow]not loaded[/]");
            }
        }
        catch
        {
            AnsiConsole.MarkupLine("  Status: [dim]unknown[/]");
        }

        // Show log tail
        if (File.Exists(LogPath))
        {
            var logInfo = new FileInfo(LogPath);
            AnsiConsole.MarkupLine($"  Log: {LogPath} ({logInfo.Length / 1024}KB)");
        }

        // Show event count
        var eventsPath = ResolveEventsPath();
        if (File.Exists(eventsPath))
        {
            var lineCount = File.ReadLines(eventsPath).Count();
            AnsiConsole.MarkupLine($"  Events: {lineCount} entries");
        }

        return 0;
    }

    private static string? FindHomelabBinary()
    {
        // Check common locations
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "homelab"),
            "/usr/local/bin/homelab",
            "/opt/homebrew/bin/homelab"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Try which
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "homelab",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                return output;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static string ResolveEventsPath()
    {
        return Directory.Exists(ExternalDrivePath)
            ? Path.Combine(ExternalDrivePath, ".homelab", "events.jsonl")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".homelab", "events.jsonl");
    }

    private static int RunLaunchctl(string action, string plistPath)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "launchctl",
                    Arguments = $"{action} {plistPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return 1;
        }
    }
}
