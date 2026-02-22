using System.Diagnostics;
using System.Text.Json;
using HomeLab.Cli.Models.EventLog;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Network;

public class SpeedtestService : ISpeedtestService
{
    public bool IsInstalled()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "speedtest-cli",
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

    public async Task<SpeedtestSnapshot> RunAsync()
    {
        if (!IsInstalled())
        {
            throw new InvalidOperationException(
                "speedtest-cli is not installed. Install with: brew install speedtest-cli");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "speedtest-cli",
                Arguments = "--json",
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
            throw new InvalidOperationException(
                $"speedtest-cli failed: {error}");
        }

        var json = JsonDocument.Parse(output).RootElement;

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
}
