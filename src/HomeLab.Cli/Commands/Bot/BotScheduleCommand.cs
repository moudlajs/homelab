using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands.Bot;

public class BotScheduleCommand : Command<BotScheduleCommand.Settings>
{
    private static readonly string PlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", "com.homelab.bot.plist");

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".homelab", "bot.log");

    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<action>")]
        [Description("Action: install, uninstall, or status")]
        public string Action { get; set; } = string.Empty;
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        return settings.Action.ToLowerInvariant() switch
        {
            "install" => Install(),
            "uninstall" => Uninstall(),
            "status" => Status(),
            _ => ShowUsage()
        };
    }

    private static int Install()
    {
        var homelabPath = FindHomelabBinary();
        if (homelabPath == null)
        {
            AnsiConsole.MarkupLine("[red]Could not find homelab binary.[/]");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        var plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>Label</key>
    <string>com.homelab.bot</string>
    <key>ProgramArguments</key>
    <array>
        <string>{homelabPath}</string>
        <string>bot</string>
        <string>start</string>
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
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

        AnsiConsole.MarkupLine("[green]Bot daemon installed[/]");
        AnsiConsole.MarkupLine($"  Binary: {homelabPath}");
        AnsiConsole.MarkupLine($"  Plist: {PlistPath}");
        AnsiConsole.MarkupLine($"  Log: {LogPath}");
        AnsiConsole.MarkupLine("[dim]Bot will auto-restart if it crashes.[/]");

        return 0;
    }

    private static int Uninstall()
    {
        if (!File.Exists(PlistPath))
        {
            AnsiConsole.MarkupLine("[yellow]Bot daemon not installed[/]");
            return 0;
        }

        RunLaunchctl("unload", PlistPath);
        File.Delete(PlistPath);

        AnsiConsole.MarkupLine("[green]Bot daemon uninstalled[/]");
        return 0;
    }

    private static int Status()
    {
        if (!File.Exists(PlistPath))
        {
            AnsiConsole.MarkupLine("[yellow]Not installed[/]");
            AnsiConsole.MarkupLine("[dim]Install with: homelab bot schedule install[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Installed[/]");
        AnsiConsole.MarkupLine($"  Plist: {PlistPath}");

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "launchctl",
                    Arguments = "list com.homelab.bot",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            AnsiConsole.MarkupLine(process.ExitCode == 0
                ? "  Status: [green]running[/]"
                : "  Status: [yellow]not loaded[/]");
        }
        catch
        {
            AnsiConsole.MarkupLine("  Status: [dim]unknown[/]");
        }

        if (File.Exists(LogPath))
        {
            var logInfo = new FileInfo(LogPath);
            AnsiConsole.MarkupLine($"  Log: {LogPath} ({logInfo.Length / 1024}KB)");
        }

        return 0;
    }

    private static int ShowUsage()
    {
        AnsiConsole.MarkupLine("[red]Unknown action. Use: install, uninstall, or status[/]");
        return 1;
    }

    private static string? FindHomelabBinary()
    {
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
        catch { }

        return null;
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
