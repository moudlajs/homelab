using System.ComponentModel;
using System.Diagnostics;
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
    private readonly HttpClient _httpClient;

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

    public SelfUpdateCommand(IGitHubReleaseService releaseService, HttpClient httpClient)
    {
        _releaseService = releaseService;
        _httpClient = httpClient;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(
            new FigletText("Self Update")
                .Centered()
                .Color(Color.Cyan));

        AnsiConsole.WriteLine();

        // Get current version (strip git hash)
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
        var releaseVersion = GitHubReleaseService.NormalizeVersion(release.TagName);
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
                ? release.Body[..500] + "..."
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
            AnsiConsole.MarkupLine("[red]✗ No compatible binary found for this platform[/]");
            AnsiConsole.MarkupLine($"[yellow]Looking for:[/] {platformSuffix}");
            AnsiConsole.MarkupLine($"[yellow]Available:[/] {string.Join(", ", release.Assets.Select(a => a.Name))}");
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
        var installPath = GetInstallPath();
        var backupPath = installPath + ".bak";

        AnsiConsole.MarkupLine($"[yellow]Install path:[/] {installPath}");

        try
        {
            // Download with progress bar
            var downloadSuccess = false;

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new TransferSpeedColumn(),
                    new RemainingTimeColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask($"Downloading {asset.Name}", maxValue: asset.Size);

                    try
                    {
                        using var response = await _httpClient.GetAsync(
                            asset.BrowserDownloadUrl,
                            HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();

                        await using var contentStream = await response.Content.ReadAsStreamAsync();
                        await using var fileStream = File.Create(tempPath);

                        var buffer = new byte[81920];
                        int bytesRead;
                        while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                            task.Increment(bytesRead);
                        }

                        downloadSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Download error: {ex.Message}[/]");
                    }
                });

            if (!downloadSuccess)
            {
                CleanupFile(tempPath);
                AnsiConsole.MarkupLine("[red]✗ Failed to download binary[/]");
                return 1;
            }

            // Verify download is not empty/corrupt
            var downloadedSize = new FileInfo(tempPath).Length;
            if (downloadedSize < 1024)
            {
                CleanupFile(tempPath);
                AnsiConsole.MarkupLine("[red]✗ Downloaded file is too small — likely corrupt[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓ Download complete[/] [dim]({FormatBytes(downloadedSize)})[/]");

            // Make executable
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await RunProcessAsync("chmod", $"+x \"{tempPath}\"");
            }

            // Backup current binary before overwriting
            var hasBackup = false;
            if (File.Exists(installPath))
            {
                try
                {
                    File.Copy(installPath, backupPath, overwrite: true);
                    hasBackup = true;
                    AnsiConsole.MarkupLine("[dim]Backed up current binary[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠ Could not create backup: {ex.Message}[/]");
                    AnsiConsole.MarkupLine("[dim]Continuing without rollback safety net[/]");
                }
            }

            // Install: copy to target location
            var needsSudo = installPath.StartsWith("/usr/local/") || installPath.StartsWith("/usr/bin/");

            if (needsSudo)
            {
                AnsiConsole.MarkupLine("[yellow]Installing (requires sudo)...[/]");
                var result = await RunProcessAsync("sudo", $"cp \"{tempPath}\" \"{installPath}\"");
                if (result != 0)
                {
                    AnsiConsole.MarkupLine("[red]✗ Installation failed[/]");
                    AnsiConsole.MarkupLine($"[yellow]Manual install:[/] [dim]sudo cp \"{tempPath}\" \"{installPath}\"[/]");
                    return 1;
                }
            }
            else
            {
                var dir = Path.GetDirectoryName(installPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(tempPath, installPath, overwrite: true);
            }

            // macOS: clear quarantine and ad-hoc sign
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var xattrResult = await RunProcessAsync("xattr", $"-cr \"{installPath}\"");
                var codesignResult = await RunProcessAsync("codesign", $"-f -s - \"{installPath}\"");

                if (codesignResult != 0)
                {
                    AnsiConsole.MarkupLine("[red]✗ Code signing failed — binary may be killed by macOS[/]");
                    if (hasBackup)
                    {
                        await Rollback(installPath, backupPath, needsSudo);
                        return 1;
                    }
                }
            }

            // Verify the new binary actually runs
            AnsiConsole.MarkupLine("[dim]Verifying new binary...[/]");
            var verifyResult = await RunProcessAsync(installPath, "version");
            if (verifyResult != 0)
            {
                AnsiConsole.MarkupLine("[red]✗ New binary failed verification[/]");
                if (hasBackup)
                {
                    await Rollback(installPath, backupPath, needsSudo);
                    return 1;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ No backup available — manual reinstall required[/]");
                    AnsiConsole.MarkupLine($"[yellow]Temp file kept at:[/] [dim]{tempPath}[/]");
                    return 1;
                }
            }

            AnsiConsole.MarkupLine("[green]✓ Verification passed[/]");

            // Clean up temp and backup files
            CleanupFile(tempPath);
            CleanupFile(backupPath);

            AnsiConsole.MarkupLine($"\n[green]✓ Successfully updated to version {version}![/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Update failed: {ex.Message}[/]");

            // Attempt rollback on unexpected failure
            if (File.Exists(backupPath))
            {
                await Rollback(installPath, backupPath, false);
            }

            CleanupFile(tempPath);
            return 1;
        }
    }

    private static async Task Rollback(string installPath, string backupPath, bool needsSudo)
    {
        AnsiConsole.MarkupLine("[yellow]Rolling back to previous version...[/]");
        try
        {
            if (needsSudo)
            {
                await RunProcessAsync("sudo", $"cp \"{backupPath}\" \"{installPath}\"");
            }
            else
            {
                File.Copy(backupPath, installPath, overwrite: true);
            }

            // Re-sign the restored binary on macOS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await RunProcessAsync("xattr", $"-cr \"{installPath}\"");
                await RunProcessAsync("codesign", $"-f -s - \"{installPath}\"");
            }

            AnsiConsole.MarkupLine("[green]✓ Rolled back to previous version[/]");
            CleanupFile(backupPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Rollback failed: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[yellow]Backup kept at:[/] [dim]{backupPath}[/]");
        }
    }

    private static void CleanupFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Detects where the current binary is installed.
    /// Falls back to ~/.local/bin/homelab.
    /// </summary>
    private static string GetInstallPath()
    {
        // Try to detect current binary location
        var currentPath = Process.GetCurrentProcess().MainModule?.FileName;

        if (!string.IsNullOrEmpty(currentPath) &&
            !currentPath.Contains("dotnet") &&
            File.Exists(currentPath))
        {
            return currentPath;
        }

        // Default to ~/.local/bin/homelab
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".local", "bin", "homelab");
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "Unknown";

        return GitHubReleaseService.NormalizeVersion(version);
    }

    private static string GetPlatformSuffix()
    {
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

        return "macos-arm64";
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                return -1;
            }

            await process.WaitForExitAsync();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
