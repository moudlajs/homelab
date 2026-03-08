using System.Diagnostics;
using System.Text;
using HomeLab.Cli.Commands.Tv;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;
using HomeLab.Cli.Services.Docker;
using HomeLab.Cli.Services.LgTv;
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
    private readonly string _botToken;
    private readonly HashSet<long> _allowedUsers;

    public TelegramBotService(
        IHomelabConfigService configService,
        IDockerService dockerService,
        ISpeedtestService speedtestService,
        IWakeOnLanService wolService,
        ILlmService llmService)
    {
        _configService = configService;
        _dockerService = dockerService;
        _speedtestService = speedtestService;
        _wolService = wolService;
        _llmService = llmService;

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
                // Telegram message limit is 4096 chars
                if (response.Length > 4000)
                {
                    response = response[..4000] + "\n...truncated";
                }
                await client.SendMessage(message.Chat.Id, response, parseMode: ParseMode.Markdown, cancellationToken: default);
            }
            catch (Exception ex)
            {
                try
                {
                    await client.SendMessage(message.Chat.Id, $"Error: {ex.Message}", cancellationToken: default);
                }
                catch
                {
                    // Can't even send error, ignore
                }
            }
        };

        // Keep alive until cancelled
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

        // Slash commands
        if (trimmed.StartsWith('/'))
        {
            var parts = trimmed.Split(' ', 2);
            var command = parts[0].ToLowerInvariant().TrimEnd('@'); // Remove @botname suffix
            // Also strip the bot username if present (e.g. /help@homelab_bot -> /help)
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
                _ => $"Unknown command: {command}\n\n{GetHelpText()}"
            };
        }

        // Natural language — route through AI
        return await HandleNaturalLanguageAsync(trimmed);
    }

    private static string GetHelpText()
    {
        return """
            *HomeLab Bot*

            /status — Homelab overview
            /doctor — Health check
            /tv — TV status
            /tv\_on — Turn TV on
            /tv\_off — Turn TV off
            /tv\_app _name_ — Launch TV app
            /speedtest — Run speed test
            /vpn — VPN status
            /help — This message

            Or just type naturally: _"is everything ok?"_
            """;
    }

    private async Task<string> HandleStatusAsync()
    {
        var sb = new StringBuilder("*Homelab Status*\n\n");

        // System info
        try
        {
            var (cpu, mem, disk) = await GetSystemMetricsAsync();
            sb.AppendLine("*System*");
            sb.AppendLine($"CPU: {cpu}%");
            sb.AppendLine($"Memory: {mem}%");
            sb.AppendLine($"Disk: {disk}%");
            sb.AppendLine();
        }
        catch
        {
            sb.AppendLine("_System metrics unavailable_\n");
        }

        // Docker
        try
        {
            if (await _dockerService.IsDockerAvailableAsync())
            {
                var containers = await _dockerService.ListContainersAsync();
                sb.AppendLine("*Docker*");
                var running = containers.Count(c => c.IsRunning);
                sb.AppendLine($"{running}/{containers.Count} containers running");
                foreach (var c in containers)
                {
                    var icon = c.IsRunning ? "✅" : "❌";
                    sb.AppendLine($"  {icon} {c.Name}");
                }
            }
            else
            {
                sb.AppendLine("_Docker not available_");
            }
        }
        catch
        {
            sb.AppendLine("_Docker info unavailable_");
        }

        return sb.ToString();
    }

    private async Task<string> HandleDoctorAsync()
    {
        var sb = new StringBuilder("*HomeLab Doctor*\n\n");
        var pass = 0;
        var warn = 0;
        var fail = 0;

        // System
        sb.AppendLine("*System*");
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

        // Docker
        sb.AppendLine("\n*Docker*");
        try
        {
            if (await _dockerService.IsDockerAvailableAsync())
            {
                sb.AppendLine("✅ Docker reachable"); pass++;
                var containers = await _dockerService.ListContainersAsync();
                foreach (var c in containers)
                {
                    if (c.IsRunning) { sb.AppendLine($"✅ {c.Name}"); pass++; }
                    else { sb.AppendLine($"❌ {c.Name}"); fail++; }
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

        // Network
        sb.AppendLine("\n*Network*");
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

        // Tools
        sb.AppendLine("\n*Tools*");
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

        sb.AppendLine($"\n*Result:* {pass} passed, {warn} warnings, {fail} failed");
        return sb.ToString();
    }

    private async Task<string> HandleTvStatusAsync()
    {
        var config = await TvCommandHelper.LoadTvConfigAsync();
        if (config == null)
        {
            return "TV not configured. Run `homelab tv setup` first.";
        }

        var isOnline = await _wolService.IsReachableAsync(config.IpAddress, 3000);
        var sb = new StringBuilder($"*{config.Name}*\n\n");
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
                    sb.AppendLine($"App: {appName}");
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
            return $"❌ Failed: {ex.Message}";
        }
    }

    private async Task<string> HandleTvAppAsync(string appNameOrId)
    {
        if (string.IsNullOrWhiteSpace(appNameOrId))
        {
            return "Usage: /tv\\_app <app name or ID>\n\nExample: /tv\\_app Netflix";
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

            // Find app by name or ID
            var apps = await client.GetAppsAsync();
            var app = apps.FirstOrDefault(a =>
                a.Id.Equals(appNameOrId, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(appNameOrId, StringComparison.OrdinalIgnoreCase));

            if (app == null)
            {
                await client.DisconnectAsync();
                var available = string.Join("\n", apps.OrderBy(a => a.Name).Select(a => $"  {a.Name}"));
                return $"App not found: {appNameOrId}\n\nAvailable apps:\n{available}";
            }

            await client.LaunchAppAsync(app.Id);
            await client.DisconnectAsync();
            return $"✅ Launched {app.Name}";
        }
        catch (Exception ex)
        {
            return $"❌ Failed: {ex.Message}";
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
            var sb = new StringBuilder("*Speed Test Results*\n\n");
            sb.AppendLine($"⬇️ Download: *{result.DownloadMbps:F1} Mbps*");
            sb.AppendLine($"⬆️ Upload: *{result.UploadMbps:F1} Mbps*");
            sb.AppendLine($"🏓 Latency: *{result.PingMs:F0} ms*");
            sb.AppendLine($"Server: {result.Server}");
            sb.AppendLine($"ISP: {result.Isp}");
            sb.AppendLine($"IP: {result.Ip}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Speed test failed: {ex.Message}";
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
            var sb = new StringBuilder("*VPN Status*\n\n");
            sb.AppendLine($"State: {state}");

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
            _ => GetHelpText()
        };
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
