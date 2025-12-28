using System.Text.Json;
using System.Text.Json.Serialization;
using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;
using HomeLab.Cli.Services.Configuration;

namespace HomeLab.Cli.Services.Suricata;

/// <summary>
/// Real Suricata client that parses EVE JSON log files.
/// Suricata outputs alerts in EVE (Extensible Event Format) JSON format.
/// </summary>
public class SuricataClient : ISuricataClient
{
    private readonly string _logPath;

    public SuricataClient(IHomelabConfigService configService)
    {
        var serviceConfig = configService.GetServiceConfig("suricata");
        _logPath = serviceConfig.LogPath ?? "~/homelab/data/suricata/logs/eve.json";

        // Expand tilde to home directory
        if (_logPath.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _logPath = Path.Combine(home, _logPath.Substring(2));
        }
    }

    public string ServiceName => "Suricata";

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            // Check if log file exists and is accessible
            if (!File.Exists(_logPath))
            {
                return false;
            }

            // Try to read the file
            using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return new ServiceHealthInfo
                {
                    ServiceName = ServiceName,
                    IsHealthy = false,
                    Status = "Not Found",
                    Message = $"EVE log file not found: {_logPath}"
                };
            }

            var fileInfo = new FileInfo(_logPath);
            var lastModified = fileInfo.LastWriteTime;
            var isStale = (DateTime.Now - lastModified).TotalMinutes > 10;

            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = !isStale,
                Status = isStale ? "Stale" : "Running",
                Message = isStale
                    ? "Log file hasn't been updated in 10+ minutes"
                    : "Suricata is actively logging events",
                Metrics = new Dictionary<string, string>
                {
                    { "Log File", _logPath },
                    { "File Size", $"{fileInfo.Length / 1024} KB" },
                    { "Last Updated", lastModified.ToString("yyyy-MM-dd HH:mm:ss") }
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealthInfo
            {
                ServiceName = ServiceName,
                IsHealthy = false,
                Status = "Error",
                Message = $"Failed to check Suricata status: {ex.Message}"
            };
        }
    }

    public async Task<List<SecurityAlert>> GetAlertsAsync(string? severity = null, int limit = 50)
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                throw new FileNotFoundException($"EVE log file not found: {_logPath}");
            }

            var alerts = new List<SecurityAlert>();

            // Read file in reverse (most recent first) using ReadLines with Reverse
            var lines = File.ReadLines(_logPath).Reverse().Take(10000); // Read last 10k lines for performance

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var eveEvent = JsonSerializer.Deserialize<EveEvent>(line);

                    // Only process alert events
                    if (eveEvent?.EventType != "alert" || eveEvent.Alert == null)
                    {
                        continue;
                    }

                    var alert = ConvertToSecurityAlert(eveEvent);

                    // Filter by severity if specified
                    if (!string.IsNullOrEmpty(severity) &&
                        !alert.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    alerts.Add(alert);

                    // Stop if we've reached the limit
                    if (alerts.Count >= limit)
                    {
                        break;
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed JSON lines
                    continue;
                }
            }

            return alerts;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read Suricata alerts: {ex.Message}", ex);
        }
    }

    public async Task<Dictionary<string, object>> GetStatsAsync()
    {
        try
        {
            var alerts = await GetAlertsAsync(limit: 1000);

            var stats = new Dictionary<string, object>
            {
                { "total_alerts", alerts.Count },
                { "critical_alerts", alerts.Count(a => a.Severity == "critical") },
                { "high_alerts", alerts.Count(a => a.Severity == "high") },
                { "medium_alerts", alerts.Count(a => a.Severity == "medium") },
                { "low_alerts", alerts.Count(a => a.Severity == "low") },
                { "unique_source_ips", alerts.Select(a => a.SourceIp).Distinct().Count() },
                { "unique_dest_ips", alerts.Select(a => a.DestinationIp).Distinct().Count() },
                {
                    "top_categories", alerts
                        .GroupBy(a => a.Category ?? "Unknown")
                        .OrderByDescending(g => g.Count())
                        .Take(5)
                        .ToDictionary(g => g.Key, g => g.Count())
                }
            };

            return stats;
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private SecurityAlert ConvertToSecurityAlert(EveEvent eveEvent)
    {
        var severity = DetermineSeverity(eveEvent.Alert!.Severity);

        return new SecurityAlert
        {
            AlertType = eveEvent.Alert.Signature ?? "Unknown Alert",
            Severity = severity,
            SourceIp = eveEvent.SrcIp ?? "0.0.0.0",
            DestinationIp = eveEvent.DestIp ?? "0.0.0.0",
            SourcePort = eveEvent.SrcPort ?? 0,
            DestinationPort = eveEvent.DestPort ?? 0,
            Protocol = eveEvent.Proto ?? "unknown",
            Signature = eveEvent.Alert.Signature ?? "Unknown",
            SignatureId = eveEvent.Alert.SignatureId,
            Category = eveEvent.Alert.Category,
            Timestamp = eveEvent.Timestamp ?? DateTime.Now,
            Metadata = new Dictionary<string, string>
            {
                { "gid", eveEvent.Alert.Gid.ToString() },
                { "rev", eveEvent.Alert.Rev.ToString() }
            }
        };
    }

    private string DetermineSeverity(int suricataSeverity)
    {
        // Suricata severity: 1 = high, 2 = medium, 3 = low
        return suricataSeverity switch
        {
            1 => "critical",
            2 => "high",
            3 => "medium",
            _ => "low"
        };
    }

    // Internal DTOs for parsing Suricata EVE JSON format
    private class EveEvent
    {
        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        [JsonPropertyName("src_ip")]
        public string? SrcIp { get; set; }

        [JsonPropertyName("dest_ip")]
        public string? DestIp { get; set; }

        [JsonPropertyName("src_port")]
        public int? SrcPort { get; set; }

        [JsonPropertyName("dest_port")]
        public int? DestPort { get; set; }

        [JsonPropertyName("proto")]
        public string? Proto { get; set; }

        [JsonPropertyName("alert")]
        public EveAlert? Alert { get; set; }
    }

    private class EveAlert
    {
        [JsonPropertyName("signature")]
        public string? Signature { get; set; }

        [JsonPropertyName("signature_id")]
        public long SignatureId { get; set; }

        [JsonPropertyName("gid")]
        public int Gid { get; set; }

        [JsonPropertyName("rev")]
        public int Rev { get; set; }

        [JsonPropertyName("severity")]
        public int Severity { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }
    }
}
