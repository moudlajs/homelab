using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using HomeLab.Cli.Services.Update;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

/// <summary>
/// Update the HomeLab CLI to the latest version from GitHub releases.
/// </summary>
public class SelfUpdateCommand : AsyncCommand<SelfUpdateCommand.Settings>
{
    private readonly IGitHubReleaseService _releaseService;

    public class Settings : CommandSettings
    {
        [CommandOption("--check")]
        [Description("Check for updates without installing")]
        public bool CheckOnly { get; set; }

        [CommandOption("--version <VERSION>")]
        [Description("Install a specific version (e.g., v1.6.0 or 1.6.0)")]
        public string? TargetVersion { get; set; }

        [CommandOption("--force")]
        [Description("Skip confirmation prompt")]
        public bool Force { get; set; }
    }

    public SelfUpdateCommand(IGitHubReleaseService releaseService)
    {
        _releaseService = releaseService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Self Update")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        // Get current version
        var currentVersion = GetCurrentVersion();
        AnsiConsole.MarkupLine($"[yellow]Current version:[/] [cyan]{currentVersion}[/]\n");

        // Fetch release information
        GitHubRelease? release = null;

        await AnsiConsole.Status()
            .StartAsync("Checking for updates...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);

                if (!string.IsNullOrEmpty(settings.TargetVersion))
                {
                    release = await _releaseService.GetReleaseByTagAsync(settings.TargetVersion);
                }
                else
                {
                    release = await _releaseService.GetLatestReleaseAsync();
                }
            });

        if (release == null)
        {
            AnsiConsole.MarkupLine("[red]✗ Failed to fetch release information from GitHub[/]");
            AnsiConsole.MarkupLine("[yellow]Please check your internet connection and try again[/]");
            return 1;
        }

        // Display release information
        var releaseVersion = release.TagName.TrimStart('v');
        var versionComparison = _releaseService.CompareVersions(releaseVersion, currentVersion);

        AnsiConsole.MarkupLine($"[yellow]Latest version:[/] [green]{release.TagName}[/]");
        AnsiConsole.MarkupLine($"[yellow]Released:[/] [dim]{release.PublishedAt:yyyy-MM-dd}[/]\n");

        if (versionComparison == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ You are already running the latest version![/]");
            return 0;
        }
        else if (versionComparison < 0 && string.IsNullOrEmpty(settings.TargetVersion))
        {
            AnsiConsole.MarkupLine("[yellow]⚠ You are running a newer version than the latest release[/]");
            AnsiConsole.MarkupLine("[dim]This might be a pre-release or development version[/]");
            return 0;
        }

        // Show release notes
        if (!string.IsNullOrEmpty(release.Body))
        {
            var releaseNotes = new Panel(release.Body.Length > 500
                ? release.Body.Substring(0, 500) + "..."
                : release.Body)
                .Header($"[cyan]Release Notes - {release.TagName}[/]")
                .BorderColor(Color.Cyan)
                .RoundedBorder();

            AnsiConsole.Write(releaseNotes);
            AnsiConsole.WriteLine();
        }

        // If check-only mode, exit here
        if (settings.CheckOnly)
        {
            if (versionComparison > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓ Update available: {currentVersion} → {release.TagName}[/]");
                AnsiConsole.MarkupLine("[dim]Run 'homelab self-update' to install[/]");
            }
            return 0;
        }

        // Find the correct binary asset for this platform
        var platformSuffix = GetPlatformSuffix();
        var asset = release.Assets.FirstOrDefault(a =>
            a.Name.Contains(platformSuffix, StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals("HomeLab.Cli", StringComparison.OrdinalIgnoreCase) ||
            a.Name.Equals("homelab", StringComparison.OrdinalIgnoreCase));

        if (asset == null)
        {
            AnsiConsole.MarkupLine($"[red]✗ No compatible binary found for this platform[/]");
            AnsiConsole.MarkupLine($"[yellow]Release URL:[/] {release.HtmlUrl}");
            return 1;
        }

        AnsiConsole.MarkupLine($"[yellow]Binary:[/] {asset.Name} ({FormatBytes(asset.Size)})");
        AnsiConsole.WriteLine();

        // Confirm installation
        if (!settings.Force)
        {
            var confirm = AnsiConsole.Confirm(
                $"[cyan]Install version {release.TagName}?[/]",
                false);

            if (!confirm)
            {
                AnsiConsole.MarkupLine("[yellow]Update cancelled[/]");
                return 0;
            }
        }

        // Download and install
        return await DownloadAndInstall(asset, release.TagName);
    }

    private async Task<int> DownloadAndInstall(GitHubAsset asset, string version)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"homelab-{version}");
        var installPath = "/usr/local/bin/homelab";

        try
        {
            // Download
            var downloadSuccess = await AnsiConsole.Status()
                .StartAsync("Downloading update...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await _releaseService.DownloadAssetAsync(asset, tempPath);
                });

            if (!downloadSuccess)
            {
                AnsiConsole.MarkupLine("[red]✗ Failed to download binary[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[green]✓ Download complete[/]");

            // Make executable
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var chmodProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x {tempPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (chmodProcess != null)
                {
                    await chmodProcess.WaitForExitAsync();
                }
            }

            // Replace binary (requires sudo)
            AnsiConsole.MarkupLine("\n[yellow]Installing update (requires sudo)...[/]");

            var cpProcess = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"cp {tempPath} {installPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (cpProcess == null)
            {
                AnsiConsole.MarkupLine("[red]✗ Failed to start installation process[/]");
                return 1;
            }

            await cpProcess.WaitForExitAsync();

            if (cpProcess.ExitCode != 0)
            {
                AnsiConsole.MarkupLine("[red]✗ Installation failed[/]");
                AnsiConsole.MarkupLine("[yellow]You may need to manually copy the binary:[/]");
                AnsiConsole.MarkupLine($"[dim]sudo cp {tempPath} {installPath}[/]");
                return 1;
            }

            // Clean up temp file
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            AnsiConsole.MarkupLine($"[green]✓ Successfully updated to version {version}![/]");
            AnsiConsole.MarkupLine("\n[dim]Run 'homelab version' to verify the installation[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Update failed: {ex.Message}[/]");
            return 1;
        }
    }

    private string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "Unknown";

        return version.TrimStart('v');
    }

    private string GetPlatformSuffix()
    {
        // Determine platform-specific binary suffix
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "macos-arm64",
                Architecture.X64 => "macos-x64",
                _ => "macos-arm64"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "linux-arm64",
                Architecture.X64 => "linux-x64",
                _ => "linux-x64"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "win-arm64",
                Architecture.X64 => "win-x64",
                _ => "win-x64"
            };
        }

        return "macos-arm64"; // Default fallback
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
