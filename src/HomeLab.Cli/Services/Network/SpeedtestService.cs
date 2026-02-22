using System.Diagnostics;
using System.Text.Json;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Network;

public class SpeedtestService : ISpeedtestService
{
    public bool IsInstalled()
    {
        // Try official Ookla speedtest first, then legacy speedtest-cli
        return IsToolInstalled("speedtest") || IsToolInstalled("speedtest-cli");
    }

    public async Task<SpeedtestSnapshot> RunAsync()
    {
        if (IsToolInstalled("speedtest"))
        {
            return await RunOoklaAsync();
        }

        if (IsToolInstalled("speedtest-cli"))
        {
            return await RunLegacyAsync();
        }

        throw new InvalidOperationException(
            "No speedtest tool installed. Install with: brew tap teamookla/speedtest && brew install speedtest");
    }

    private static async Task<SpeedtestSnapshot> RunOoklaAsync()
    {
        var output = await RunToolAsync("speedtest", "--accept-license --accept-gdpr --format=json");
        var json = JsonDocument.Parse(output).RootElement;

        // Ookla: bandwidth is bytes/sec, convert to Mbps (megabits)
        var downloadBytesPerSec = json.GetProperty("download").GetProperty("bandwidth").GetDouble();
        var uploadBytesPerSec = json.GetProperty("upload").GetProperty("bandwidth").GetDouble();
        var ping = json.GetProperty("ping").GetProperty("latency").GetDouble();

        var server = "";
        if (json.TryGetProperty("server", out var serverObj))
        {
            var name = serverObj.TryGetProperty("name", out var n) ? n.GetString() : "";
            var location = serverObj.TryGetProperty("location", out var l) ? l.GetString() : "";
            server = string.IsNullOrEmpty(location) ? name ?? "" : $"{name} ({location})";
        }

        var isp = json.TryGetProperty("isp", out var ispProp) ? ispProp.GetString() ?? "" : "";

        var ip = "";
        if (json.TryGetProperty("interface", out var iface) &&
            iface.TryGetProperty("externalIp", out var extIp))
        {
            ip = extIp.GetString() ?? "";
        }

        return new SpeedtestSnapshot
        {
            DownloadMbps = Math.Round(downloadBytesPerSec * 8 / 1_000_000, 2),
            UploadMbps = Math.Round(uploadBytesPerSec * 8 / 1_000_000, 2),
            PingMs = Math.Round(ping, 1),
            Server = server,
            Isp = isp,
            Ip = ip
        };
    }

    private static async Task<SpeedtestSnapshot> RunLegacyAsync()
    {
        var output = await RunToolAsync("speedtest-cli", "--json");
        var json = JsonDocument.Parse(output).RootElement;

        // Legacy: download/upload in bits/sec
        var downloadBps = json.GetProperty("download").GetDouble();
        var uploadBps = json.GetProperty("upload").GetDouble();
        var ping = json.GetProperty("ping").GetDouble();

        var server = "";
        if (json.TryGetProperty("server", out var serverObj))
        {
            var sponsor = serverObj.TryGetProperty("sponsor", out var s) ? s.GetString() : "";
            var name = serverObj.TryGetProperty("name", out var n) ? n.GetString() : "";
            server = string.IsNullOrEmpty(name) ? sponsor ?? "" : $"{sponsor} ({name})";
        }

        var isp = "";
        var ip = "";
        if (json.TryGetProperty("client", out var clientObj))
        {
            if (clientObj.TryGetProperty("isp", out var ispProp))
            {
                isp = ispProp.GetString() ?? "";
            }

            if (clientObj.TryGetProperty("ip", out var ipProp))
            {
                ip = ipProp.GetString() ?? "";
            }
        }

        return new SpeedtestSnapshot
        {
            DownloadMbps = Math.Round(downloadBps / 1_000_000, 2),
            UploadMbps = Math.Round(uploadBps / 1_000_000, 2),
            PingMs = Math.Round(ping, 1),
            Server = server,
            Isp = isp,
            Ip = ip
        };
    }

    private static bool IsToolInstalled(string tool)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = tool,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunToolAsync(string fileName, string arguments)
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
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} failed: {error}");
        }

        return output;
    }
}
