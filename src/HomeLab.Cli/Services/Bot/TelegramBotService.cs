using System.Diagnostics;
using System.Text;
using HomeLab.Cli.Commands.Tv;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.LgTv;
using HomeLab.Cli.Services.Network;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace HomeLab.Cli.Services.Bot;

public class TelegramBotService : ITelegramBotService
{
    private readonly IHomelabConfigService _configService;
    private readonly IDockerService _dockerService;
    private readonly ISpeedtestService _speedtestService;
    private readonly IWakeOnLanService _wolService;
    private readonly ILlmService _llmService;
    private readonly INmapService _nmapService;
    private readonly ISystemDataCollector _dataCollector;
    private readonly string _botToken;
    private readonly HashSet<long> _allowedUsers;

    public TelegramBotService(
        IHomelabConfigService configService,
        IDockerService dockerService,
        ISpeedtestService speedtestService,
        IWakeOnLanService wolService,
        ILlmService llmService,
        INmapService nmapService,
        ISystemDataCollector dataCollector)
    {
        _configService = configService;
        _dockerService = dockerService;
        _speedtestService = speedtestService;
        _wolService = wolService;
        _llmService = llmService;
        _nmapService = nmapService;
        _dataCollector = dataCollector;

        var config = configService.GetServiceConfig("telegram");
        _botToken = config.Token ?? string.Empty;
        _allowedUsers = ParseAllowedUsers(config.Username ?? string.Empty);
    }

    public bool IsConfigured() => !string.IsNullOrWhiteSpace(_botToken) && _allowedUsers.Count > 0;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var client = new TelegramBotClient(_botToken);
        var me = await client.GetMe(cancellationToken);
        Console.WriteLine($"Bot started: @{me.Username}");
        Console.WriteLine($"Authorized users: {string.Join(", ", _allowedUsers)}");
        Console.WriteLine("Listening for messages... Press Ctrl+C to stop.");

        client.OnMessage += async (message, type) =>
        {
            if (message.Text == null)
            {
                return;
            }

            if (!_allowedUsers.Contains(message.From!.Id))
            {
                await client.SendMessage(message.Chat.Id, "Unauthorized.", cancellationToken: default);
                return;
            }

            try
            {
                var response = await HandleMessageAsync(message.Text);
                if (response.Length > 4000)
                {
                    response = response[..4000] + "\n...truncated";
                }
                await client.SendMessage(message.Chat.Id, response, parseMode: ParseMode.Html, cancellationToken: default);
            }
            catch (Exception ex)
            {
                try
                {
                    await client.SendMessage(message.Chat.Id, $"Error: {Esc(ex.Message)}", cancellationToken: default);
                }
                catch
                {
                    // Can't even send error, ignore
                }
            }
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Bot stopped.");
        }
    }

    private async Task<string> HandleMessageAsync(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith('/'))
        {
            var parts = trimmed.Split(' ', 2);
            var command = parts[0].ToLowerInvariant();
            var atIndex = command.IndexOf('@');
            if (atIndex > 0)
            {
                command = command[..atIndex];
            }

            var args = parts.Length > 1 ? parts[1].Trim() : "";

            return command switch
            {
                "/start" or "/help" => GetHelpText(),
                "/status" => await HandleStatusAsync(),
                "/doctor" => await HandleDoctorAsync(),
                "/tv" => await HandleTvStatusAsync(),
                "/tv_on" => await HandleTvOnAsync(),
                "/tv_off" => await HandleTvOffAsync(),
                "/tv_app" => await HandleTvAppAsync(args),
                "/speedtest" => await HandleSpeedtestAsync(),
                "/vpn" => await HandleVpnAsync(),
                "/network" => await HandleNetworkAsync(),
                "/monitor" => await HandleMonitorAsync(),
                _ => $"Unknown command: {Esc(command)}\n\n{GetHelpText()}"
            };
        }

        return await HandleNaturalLanguageAsync(trimmed);
    }

    private static string GetHelpText()
    {
        return """
            <b>HomeLab Bot</b>

            /status — Homelab overview
            /doctor — Health check
            /tv — TV status
            /tv_on — Turn TV on
            /tv_off — Turn TV off
            /tv_app <i>name</i> — Launch TV app
            /speedtest — Run speed test
            /vpn — VPN status
            /network — Network devices
            /monitor — AI health report
            /help — This message

            Or just type naturally: <i>"is everything ok?"</i>
            """;
    }

    private async Task<string> HandleStatusAsync()
    {
        var sb = new StringBuilder("<b>Homelab Status</b>\n\n");

        try
        {
            var (cpu, mem, disk) = await GetSystemMetricsAsync();
            sb.AppendLine("<b>System</b>");
            sb.AppendLine($"CPU: {cpu}%");
            sb.AppendLine($"Memory: {mem}%");
            sb.AppendLine($"Disk: {disk}%");
            sb.AppendLine();
        }
        catch
        {
            sb.AppendLine("<i>System metrics unavailable</i>\n");
        }

        try
        {
            if (await _dockerService.IsDockerAvailableAsync())
            {
                var containers = await _dockerService.ListContainersAsync();
                sb.AppendLine("<b>Docker</b>");
                var running = containers.Count(c => c.IsRunning);
                sb.AppendLine($"{running}/{containers.Count} containers running");
                foreach (var c in containers)
                {
                    var icon = c.IsRunning ? "✅" : "❌";
                    sb.AppendLine($"  {icon} {Esc(c.Name)}");
                }
            }
            else
            {
                sb.AppendLine("<i>Docker not available</i>");
            }
        }
        catch
        {
            sb.AppendLine("<i>Docker info unavailable</i>");
        }

        return sb.ToString();
    }

    private async Task<string> HandleDoctorAsync()
    {
        var sb = new StringBuilder("<b>HomeLab Doctor</b>\n\n");
        var pass = 0;
        var warn = 0;
        var fail = 0;

        sb.AppendLine("<b>System</b>");
        try
        {
            var (cpu, mem, disk) = await GetSystemMetricsAsync();
            if (disk < 90) { sb.AppendLine($"✅ Disk: {disk}%"); pass++; }
            else if (disk < 95) { sb.AppendLine($"⚠️ Disk: {disk}%"); warn++; }
            else { sb.AppendLine($"❌ Disk: {disk}%"); fail++; }

            if (mem < 90) { sb.AppendLine($"✅ Memory: {mem}%"); pass++; }
            else { sb.AppendLine($"⚠️ Memory: {mem}%"); warn++; }
        }
        catch
        {
            sb.AppendLine("❌ System metrics unavailable"); fail++;
        }

        sb.AppendLine($"\n<b>Docker</b>");
        try
        {
            if (await _dockerService.IsDockerAvailableAsync())
            {
                sb.AppendLine("✅ Docker reachable"); pass++;
                var containers = await _dockerService.ListContainersAsync();
                foreach (var c in containers)
                {
                    if (c.IsRunning) { sb.AppendLine($"✅ {Esc(c.Name)}"); pass++; }
                    else { sb.AppendLine($"❌ {Esc(c.Name)}"); fail++; }
                }
            }
            else
            {
                sb.AppendLine("❌ Docker not reachable"); fail++;
            }
        }
        catch
        {
            sb.AppendLine("❌ Docker check failed"); fail++;
        }

        sb.AppendLine($"\n<b>Network</b>");
        try
        {
            var pingResult = await RunProcessAsync("ping", "-c 1 -W 3 1.1.1.1");
            if (pingResult.exitCode == 0) { sb.AppendLine("✅ Internet reachable"); pass++; }
            else { sb.AppendLine("❌ Internet unreachable"); fail++; }
        }
        catch
        {
            sb.AppendLine("❌ Ping failed"); fail++;
        }

        sb.AppendLine($"\n<b>Tools</b>");
        var tools = new[] { "nmap", "tailscale", "speedtest" };
        foreach (var tool in tools)
        {
            try
            {
                var result = await RunProcessAsync("which", tool);
                if (result.exitCode == 0) { sb.AppendLine($"✅ {tool}"); pass++; }
                else { sb.AppendLine($"⚠️ {tool} not installed"); warn++; }
            }
            catch
            {
                sb.AppendLine($"⚠️ {tool} not found"); warn++;
            }
        }

        sb.AppendLine($"\n<b>Result:</b> {pass} passed, {warn} warnings, {fail} failed");
        return sb.ToString();
    }

    private async Task<string> HandleTvStatusAsync()
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null)
        {
            return "TV not configured. Run <code>homelab tv setup</code> first.";
        }

        var isOnline = await _wolService.IsReachableAsync(config.IpAddress, 3000);
        var sb = new StringBuilder($"<b>{Esc(config.Name)}</b>\n\n");
        sb.AppendLine($"Status: {(isOnline ? "🟢 Online" : "🔴 Offline")}");
        sb.AppendLine($"IP: {config.IpAddress}");

        if (isOnline && !string.IsNullOrEmpty(config.ClientKey))
        {
            try
            {
                var client = TvCommandHelper.CreateClient();
                await client.ConnectAsync(config.IpAddress, config.ClientKey);

                var appId = await client.GetForegroundAppAsync();
                if (!string.IsNullOrEmpty(appId))
                {
                    var appName = appId;
                    try
                    {
                        var apps = await client.GetAppsAsync();
                        var match = apps.FirstOrDefault(a =>
                            a.Id.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            appName = match.Name;
                        }
                    }
                    catch { }
                    sb.AppendLine($"App: {Esc(appName)}");
                }

                try
                {
                    var volume = await client.GetVolumeAsync();
                    sb.AppendLine($"Volume: {volume}");
                }
                catch { }

                await client.DisconnectAsync();
            }
            catch { }
        }

        return sb.ToString();
    }

    private async Task<string> HandleTvOnAsync()
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null)
        {
            return "TV not configured.";
        }

        var sent = await _wolService.WakeAsync(config.MacAddress);
        return sent ? "✅ Wake-on-LAN packet sent. TV should turn on shortly." : "❌ Failed to send WOL packet.";
    }

    private async Task<string> HandleTvOffAsync()
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null)
        {
            return "TV not configured.";
        }

        if (string.IsNullOrEmpty(config.ClientKey))
        {
            return "TV not paired.";
        }

        try
        {
            var client = TvCommandHelper.CreateClient();
            await client.ConnectAsync(config.IpAddress, config.ClientKey);
            await client.PowerOffAsync();
            await client.DisconnectAsync();
            return "✅ TV turned off.";
        }
        catch (Exception ex)
        {
            return $"❌ Failed: {Esc(ex.Message)}";
        }
    }

    private async Task<string> HandleTvAppAsync(string appNameOrId)
    {
        if (string.IsNullOrWhiteSpace(appNameOrId))
        {
            return "Usage: /tv_app &lt;app name or ID&gt;\n\nExample: /tv_app Netflix";
        }

        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null)
        {
            return "TV not configured.";
        }

        if (string.IsNullOrEmpty(config.ClientKey))
        {
            return "TV not paired.";
        }

        try
        {
            var client = TvCommandHelper.CreateClient();
            await client.ConnectAsync(config.IpAddress, config.ClientKey);

            var apps = await client.GetAppsAsync();
            var app = apps.FirstOrDefault(a =>
                a.Id.Equals(appNameOrId, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(appNameOrId, StringComparison.OrdinalIgnoreCase));

            if (app == null)
            {
                await client.DisconnectAsync();
                var available = string.Join("\n", apps.OrderBy(a => a.Name).Select(a => $"  {Esc(a.Name)}"));
                return $"App not found: {Esc(appNameOrId)}\n\nAvailable apps:\n{available}";
            }

            await client.LaunchAppAsync(app.Id);
            await client.DisconnectAsync();
            return $"✅ Launched {Esc(app.Name)}";
        }
        catch (Exception ex)
        {
            return $"❌ Failed: {Esc(ex.Message)}";
        }
    }

    private async Task<string> HandleSpeedtestAsync()
    {
        if (!_speedtestService.IsInstalled())
        {
            return "❌ No speedtest tool installed.";
        }

        try
        {
            var result = await _speedtestService.RunAsync();
            var sb = new StringBuilder("<b>Speed Test Results</b>\n\n");
            sb.AppendLine($"⬇️ Download: <b>{result.DownloadMbps:F1} Mbps</b>");
            sb.AppendLine($"⬆️ Upload: <b>{result.UploadMbps:F1} Mbps</b>");
            sb.AppendLine($"🏓 Latency: <b>{result.PingMs:F0} ms</b>");
            sb.AppendLine($"Server: {Esc(result.Server)}");
            sb.AppendLine($"ISP: {Esc(result.Isp)}");
            sb.AppendLine($"IP: {result.Ip}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Speed test failed: {Esc(ex.Message)}";
        }
    }

    private async Task<string> HandleVpnAsync()
    {
        try
        {
            var result = await RunProcessAsync("tailscale", "status --json");
            if (result.exitCode != 0)
            {
                return "❌ Tailscale not running.";
            }

            var json = System.Text.Json.JsonDocument.Parse(result.output).RootElement;
            var state = json.TryGetProperty("BackendState", out var bs) ? bs.GetString() : "Unknown";
            var sb = new StringBuilder("<b>VPN Status</b>\n\n");
            sb.AppendLine($"State: {Esc(state)}");

            if (json.TryGetProperty("Self", out var self))
            {
                if (self.TryGetProperty("TailscaleIPs", out var ips) && ips.GetArrayLength() > 0)
                {
                    sb.AppendLine($"IP: {ips[0].GetString()}");
                }
            }

            if (json.TryGetProperty("Peer", out var peers))
            {
                var peerCount = 0;
                var onlineCount = 0;
                foreach (var peer in peers.EnumerateObject())
                {
                    peerCount++;
                    if (peer.Value.TryGetProperty("Online", out var online) && online.GetBoolean())
                    {
                        onlineCount++;
                    }
                }
                sb.AppendLine($"Peers: {onlineCount}/{peerCount} online");
            }

            return sb.ToString();
        }
        catch
        {
            return "❌ Tailscale status unavailable.";
        }
    }

    private async Task<string> HandleNetworkAsync()
    {
        try
        {
            if (!_nmapService.IsNmapAvailable())
            {
                return "❌ nmap is not installed.";
            }

            var devices = await _nmapService.ScanNetworkAsync("192.168.1.0/24", quickScan: true);
            var sb = new StringBuilder("<b>Network Devices</b>\n\n");
            sb.AppendLine($"Found {devices.Count} devices:\n");
            foreach (var d in devices.Take(20))
            {
                var name = !string.IsNullOrEmpty(d.Hostname) ? Esc(d.Hostname) : "<i>unknown</i>";
                sb.AppendLine($"  {d.IpAddress} — {name}");
            }
            if (devices.Count > 20)
            {
                sb.AppendLine($"\n  ...and {devices.Count - 20} more");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Network scan failed: {Esc(ex.Message)}";
        }
    }

    private async Task<string> HandleMonitorAsync()
    {
        try
        {
            if (!await _llmService.IsAvailableAsync())
            {
                return "❌ AI service not configured.";
            }

            var snapshot = await _dataCollector.CollectAsync();
            var prompt = _dataCollector.FormatAsPrompt(snapshot);
            var systemPrompt = "You are a concise homelab health analyst. Summarize the system state in 5-10 bullet points. Flag any issues. Keep it short — this goes to a Telegram message. Do NOT use markdown formatting, use plain text only.";
            var response = await _llmService.SendMessageAsync(systemPrompt, prompt, 500);
            if (!response.Success)
            {
                return $"❌ AI error: {Esc(response.Error ?? "unknown")}";
            }

            return $"<b>AI Health Report</b>\n\n{Esc(response.Content ?? "No response")}";
        }
        catch (Exception ex)
        {
            return $"❌ Monitor failed: {Esc(ex.Message)}";
        }
    }

    private async Task<string> HandleNaturalLanguageAsync(string text)
    {
        if (!await _llmService.IsAvailableAsync())
        {
            return "Natural language mode requires AI service. Use /help to see available commands.";
        }

        var systemPrompt = """
            You are a homelab assistant. Given the user's message, determine which command to run.
            Respond with ONLY one of these commands (nothing else):
            STATUS - for homelab overview, container status, system health
            DOCTOR - for comprehensive health check, diagnostics
            TV_STATUS - for TV status, what's playing
            TV_ON - to turn TV on
            TV_OFF - to turn TV off
            TV_APP <name> - to launch a TV app (include the app name)
            SPEEDTEST - to run internet speed test
            VPN - for VPN/Tailscale status
            NETWORK - for network device scan
            MONITOR - for AI health report
            HELP - if unclear or unrelated to homelab
            """;

        var response = await _llmService.SendMessageAsync(systemPrompt, text, 50);
        if (!response.Success)
        {
            return "AI unavailable. Use /help for commands.";
        }

        var cmd = response.Content?.Trim() ?? "HELP";

        if (cmd.StartsWith("TV_APP "))
        {
            var appName = cmd["TV_APP ".Length..].Trim();
            return await HandleTvAppAsync(appName);
        }

        return cmd.ToUpperInvariant() switch
        {
            "STATUS" => await HandleStatusAsync(),
            "DOCTOR" => await HandleDoctorAsync(),
            "TV_STATUS" => await HandleTvStatusAsync(),
            "TV_ON" => await HandleTvOnAsync(),
            "TV_OFF" => await HandleTvOffAsync(),
            "SPEEDTEST" => await HandleSpeedtestAsync(),
            "VPN" => await HandleVpnAsync(),
            "NETWORK" => await HandleNetworkAsync(),
            "MONITOR" => await HandleMonitorAsync(),
            _ => GetHelpText()
        };
    }

    /// <summary>
    /// Escapes HTML special characters for Telegram HTML parse mode.
    /// </summary>
    private static string Esc(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    private static async Task<(int exitCode, string output)> RunProcessAsync(string fileName, string arguments)
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
        return (process.ExitCode, output);
    }

    private static async Task<(int cpu, int mem, int disk)> GetSystemMetricsAsync()
    {
        // CPU via top
        var cpuResult = await RunProcessAsync("top", "-l 1 -n 0 -stats cpu");
        var cpuPercent = 0;
        foreach (var line in cpuResult.output.Split('\n'))
        {
            if (line.Contains("CPU usage"))
            {
                var parts = line.Split(',');
                foreach (var part in parts)
                {
                    if (part.Contains("idle"))
                    {
                        var val = part.Trim().Split('%')[0].Trim().Split(' ').Last();
                        if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var idle))
                        {
                            cpuPercent = (int)Math.Round(100 - idle);
                        }
                    }
                }
            }
        }

        // Memory via memory_pressure
        var memResult = await RunProcessAsync("memory_pressure", "");
        var memPercent = 0;
        foreach (var line in memResult.output.Split('\n'))
        {
            if (line.Contains("System-wide memory free percentage:"))
            {
                var val = line.Split(':').Last().Trim().TrimEnd('%');
                if (int.TryParse(val, out var free))
                {
                    memPercent = 100 - free;
                }
            }
        }

        // Disk via df
        var diskResult = await RunProcessAsync("df", "-h /");
        var diskPercent = 0;
        var lines = diskResult.output.Split('\n');
        if (lines.Length > 1)
        {
            var fields = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length >= 5)
            {
                var pct = fields[4].TrimEnd('%');
                int.TryParse(pct, out diskPercent);
            }
        }

        return (cpuPercent, memPercent, diskPercent);
    }

    private static HashSet<long> ParseAllowedUsers(string csv)
    {
        var result = new HashSet<long>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return result;
        }

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (long.TryParse(part.Trim(), out var id))
            {
                result.Add(id);
            }
        }
        return result;
    }
}
