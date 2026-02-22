using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.Health;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HomeLab.Cli.Commands;

public class DoctorCommand : AsyncCommand<DoctorCommand.Settings>
{
    private readonly IDockerService _docker;
    private readonly IServiceHealthCheckService _healthCheck;
    private readonly IHomelabConfigService _configService;
    private readonly ISpeedtestService _speedtest;

    public class Settings : CommandSettings { }

    public DoctorCommand(
        IDockerService docker,
        IServiceHealthCheckService healthCheck,
        IHomelabConfigService configService,
        ISpeedtestService speedtest)
    {
        _docker = docker;
        _healthCheck = healthCheck;
        _configService = configService;
        _speedtest = speedtest;
    }

    private int _passed;
    private int _warnings;
    private int _failed;

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[bold cyan]HomeLab Doctor[/]").RuleStyle("dim"));
        AnsiConsole.WriteLine();

        await CheckSystemAsync();
        await CheckDockerAsync();
        await CheckServicesAsync();
        await CheckNetworkAsync();
        CheckConfig();
        CheckTools();
        await CheckVpnAsync();

        // Summary
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("dim"));
        var resultColor = _failed > 0 ? "red" : _warnings > 0 ? "yellow" : "green";
        AnsiConsole.MarkupLine(
            $"[{resultColor}]Result: {_passed} passed, {_warnings} warnings, {_failed} failed[/]");

        return _failed > 0 ? 1 : 0;
    }

    private async Task CheckSystemAsync()
    {
        PrintSection("System");

        // Disk usage
        try
        {
            var dfOutput = await RunCommandAsync("df", "-h /");
            var dfMatch = Regex.Match(dfOutput, @"(\d+)%");
            if (dfMatch.Success)
            {
                var diskPercent = int.Parse(dfMatch.Groups[1].Value);
                if (diskPercent >= 95)
                {
                    PrintFail($"Disk usage: {diskPercent}% (critical!)");
                }
                else if (diskPercent >= 90)
                {
                    PrintWarn($"Disk usage: {diskPercent}% (high)");
                }
                else
                {
                    PrintPass($"Disk usage: {diskPercent}%");
                }
            }
        }
        catch
        {
            PrintFail("Disk usage: could not check");
        }

        // Memory
        try
        {
            var memOutput = await RunCommandAsync("sysctl", "-n hw.memsize");
            if (long.TryParse(memOutput.Trim(), out var memBytes))
            {
                var totalGb = memBytes / (1024.0 * 1024.0 * 1024.0);
                var vmStat = await RunCommandAsync("vm_stat", "");
                var pageSize = 16384L;
                var pageSizeMatch = Regex.Match(vmStat, @"page size of (\d+) bytes");
                if (pageSizeMatch.Success)
                {
                    pageSize = long.Parse(pageSizeMatch.Groups[1].Value);
                }

                long active = 0, wired = 0, compressed = 0;
                var am = Regex.Match(vmStat, @"Pages active:\s+(\d+)");
                if (am.Success)
                {
                    active = long.Parse(am.Groups[1].Value);
                }

                var wm = Regex.Match(vmStat, @"Pages wired down:\s+(\d+)");
                if (wm.Success)
                {
                    wired = long.Parse(wm.Groups[1].Value);
                }

                var cm = Regex.Match(vmStat, @"Pages occupied by compressor:\s+(\d+)");
                if (cm.Success)
                {
                    compressed = long.Parse(cm.Groups[1].Value);
                }

                var usedBytes = (active + wired + compressed) * pageSize;
                var usedGb = usedBytes / (1024.0 * 1024.0 * 1024.0);
                var memPercent = (usedGb / totalGb) * 100;

                if (memPercent >= 95)
                {
                    PrintFail($"Memory: {memPercent:F0}% ({usedGb:F1}/{totalGb:F0} GB)");
                }
                else if (memPercent >= 85)
                {
                    PrintWarn($"Memory: {memPercent:F0}% ({usedGb:F1}/{totalGb:F0} GB)");
                }
                else
                {
                    PrintPass($"Memory: {memPercent:F0}% ({usedGb:F1}/{totalGb:F0} GB)");
                }
            }
        }
        catch
        {
            PrintFail("Memory: could not check");
        }

        // Uptime
        try
        {
            var uptimeOutput = await RunCommandAsync("uptime", "");
            var uptimeMatch = Regex.Match(uptimeOutput, @"up\s+(.+?),\s+\d+\s+user");
            if (uptimeMatch.Success)
            {
                PrintPass($"Uptime: {uptimeMatch.Groups[1].Value.Trim()}");
            }
        }
        catch { }
    }

    private async Task CheckDockerAsync()
    {
        PrintSection("Docker");

        try
        {
            var containers = await _docker.ListContainersAsync(onlyHomelab: true);
            PrintPass("Docker daemon reachable");

            foreach (var c in containers.OrderBy(c => c.Name))
            {
                if (c.IsRunning)
                {
                    PrintPass($"{c.Name}: running");
                }
                else
                {
                    PrintFail($"{c.Name}: stopped");
                }
            }

            if (containers.Count == 0)
            {
                PrintWarn("No homelab containers found");
            }
        }
        catch (Exception ex)
        {
            PrintFail($"Docker daemon: {ex.Message}");
        }
    }

    private async Task CheckServicesAsync()
    {
        PrintSection("Services");

        try
        {
            var results = await _healthCheck.CheckAllServicesAsync();
            foreach (var r in results.OrderBy(r => r.ServiceName))
            {
                if (r.IsHealthy)
                {
                    PrintPass($"{r.ServiceName}: healthy");
                }
                else if (r.IsRunning)
                {
                    PrintWarn($"{r.ServiceName}: running but unhealthy ({r.Message})");
                }
                else
                {
                    PrintFail($"{r.ServiceName}: {r.Status}");
                }
            }
        }
        catch (Exception ex)
        {
            PrintFail($"Service health checks: {ex.Message}");
        }
    }

    private async Task CheckNetworkAsync()
    {
        PrintSection("Network");

        // Internet connectivity
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("1.1.1.1", 5000);
            if (reply.Status == IPStatus.Success)
            {
                PrintPass($"Internet: reachable ({reply.RoundtripTime}ms)");
            }
            else
            {
                PrintFail($"Internet: unreachable ({reply.Status})");
            }
        }
        catch
        {
            PrintFail("Internet: unreachable");
        }

        // DNS resolution
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync("google.com");
            if (addresses.Length > 0)
            {
                PrintPass("DNS: resolving");
            }
            else
            {
                PrintFail("DNS: not resolving");
            }
        }
        catch
        {
            PrintFail("DNS: not resolving");
        }
    }

    private void CheckConfig()
    {
        PrintSection("Config");

        try
        {
            var config = _configService.GetServiceConfig("ai");
            if (config != null)
            {
                PrintPass("Config file found");

                if (!string.IsNullOrEmpty(config.Token))
                {
                    PrintPass("AI service configured");
                }
                else
                {
                    PrintWarn("AI service: no API token set");
                }
            }
            else
            {
                PrintWarn("AI service not configured");
            }

            var adguard = _configService.GetServiceConfig("adguard");
            if (adguard?.Username == "admin" && adguard?.Password == "admin")
            {
                PrintWarn("AdGuard credentials: still default");
            }
        }
        catch
        {
            PrintFail("Config file: could not read");
        }
    }

    private void CheckTools()
    {
        PrintSection("Tools");

        CheckTool("nmap");
        CheckTool("tailscale");

        if (_speedtest.IsInstalled())
        {
            PrintPass("speedtest installed");
        }
        else
        {
            PrintWarn("speedtest not installed (brew tap teamookla/speedtest && brew install speedtest)");
        }
    }

    private void CheckTool(string name)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = name,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
            if (process.ExitCode == 0)
            {
                PrintPass($"{name} installed");
            }
            else
            {
                PrintWarn($"{name} not installed");
            }
        }
        catch
        {
            PrintWarn($"{name}: could not check");
        }
    }

    private async Task CheckVpnAsync()
    {
        PrintSection("VPN");

        try
        {
            var output = await RunCommandAsync("tailscale", "status --json");
            if (output.Contains("\"BackendState\":\"Running\""))
            {
                var ipMatch = Regex.Match(output, @"""TailscaleIPs"":\[""([^""]+)""");
                var ip = ipMatch.Success ? ipMatch.Groups[1].Value : "connected";
                PrintPass($"Tailscale: connected ({ip})");
            }
            else
            {
                PrintWarn("Tailscale: not connected");
            }
        }
        catch
        {
            PrintWarn("Tailscale: could not check");
        }
    }

    private void PrintSection(string name)
    {
        AnsiConsole.MarkupLine($"[bold]{name}[/]");
    }

    private void PrintPass(string message)
    {
        _passed++;
        AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(message)}");
    }

    private void PrintWarn(string message)
    {
        _warnings++;
        AnsiConsole.MarkupLine($"  [yellow]⚠[/] {Markup.Escape(message)}");
    }

    private void PrintFail(string message)
    {
        _failed++;
        AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(message)}");
    }

    private static async Task<string> RunCommandAsync(string fileName, string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
