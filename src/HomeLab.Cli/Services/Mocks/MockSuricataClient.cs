using HomeLab.Cli.Models;
using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Services.Mocks;

/// <summary>
/// Mock implementation of Suricata client for testing without actual Suricata installation.
/// </summary>
public class MockSuricataClient : ISuricataClient
{
    public string ServiceName => "Suricata (Mock)";

    public Task<bool> IsHealthyAsync()
    {
        return Task.FromResult(true);
    }

    public Task<ServiceHealthInfo> GetHealthInfoAsync()
    {
        return Task.FromResult(new ServiceHealthInfo
        {
            ServiceName = ServiceName,
            IsHealthy = true,
            Status = "Running",
            Message = "Mock Suricata IDS service",
            Metrics = new Dictionary<string, string>
            {
                { "Mode", "Mock" },
                { "Alerts (24h)", "12" },
                { "Rules Loaded", "25000" }
            }
        });
    }

    public Task<List<SecurityAlert>> GetAlertsAsync(string? severity = null, int limit = 50)
    {
        var alerts = GetMockAlerts();

        // Filter by severity if specified
        if (!string.IsNullOrEmpty(severity))
        {
            alerts = alerts
                .Where(a => a.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply limit
        alerts = alerts.Take(limit).ToList();

        return Task.FromResult(alerts);
    }

    public Task<Dictionary<string, object>> GetStatsAsync()
    {
        var alerts = GetMockAlerts();

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

        return Task.FromResult(stats);
    }

    private List<SecurityAlert> GetMockAlerts()
    {
        return new List<SecurityAlert>
        {
            new()
            {
                AlertType = "Potential SSH Brute Force",
                Severity = "critical",
                SourceIp = "185.220.101.45",
                DestinationIp = "192.168.1.10",
                SourcePort = 45123,
                DestinationPort = 22,
                Protocol = "tcp",
                Signature = "ET SCAN Potential SSH Scan",
                SignatureId = 2001219,
                Category = "Attempted Administrator Privilege Gain",
                Timestamp = DateTime.Now.AddMinutes(-15),
                Metadata = new Dictionary<string, string>
                {
                    { "attack_target", "Server" },
                    { "severity", "1" }
                }
            },
            new()
            {
                AlertType = "Port Scan Detected",
                Severity = "high",
                SourceIp = "192.168.1.250",
                DestinationIp = "192.168.1.1",
                SourcePort = 54321,
                DestinationPort = 80,
                Protocol = "tcp",
                Signature = "ET SCAN Rapid Port Scan",
                SignatureId = 2009582,
                Category = "Network Scan",
                Timestamp = DateTime.Now.AddHours(-2),
                Metadata = new Dictionary<string, string>
                {
                    { "scan_type", "rapid" }
                }
            },
            new()
            {
                AlertType = "Suspicious DNS Query",
                Severity = "medium",
                SourceIp = "192.168.1.20",
                DestinationIp = "8.8.8.8",
                SourcePort = 53421,
                DestinationPort = 53,
                Protocol = "udp",
                Signature = "ET DNS Query to Suspicious TLD",
                SignatureId = 2019401,
                Category = "Potentially Bad Traffic",
                Timestamp = DateTime.Now.AddHours(-5),
                Metadata = new Dictionary<string, string>
                {
                    { "dns_query", "malware.example.xyz" }
                }
            },
            new()
            {
                AlertType = "Possible DDoS Traffic",
                Severity = "high",
                SourceIp = "203.0.113.42",
                DestinationIp = "192.168.1.10",
                SourcePort = 12345,
                DestinationPort = 443,
                Protocol = "tcp",
                Signature = "ET DOS Possible DDoS Attack",
                SignatureId = 2100498,
                Category = "Denial of Service",
                Timestamp = DateTime.Now.AddHours(-8),
                Metadata = new Dictionary<string, string>
                {
                    { "packet_rate", "high" }
                }
            },
            new()
            {
                AlertType = "TLS Certificate Expired",
                Severity = "low",
                SourceIp = "192.168.1.30",
                DestinationIp = "93.184.216.34",
                SourcePort = 43256,
                DestinationPort = 443,
                Protocol = "tcp",
                Signature = "ET TLS Expired Certificate",
                SignatureId = 2803013,
                Category = "Protocol Command Decode",
                Timestamp = DateTime.Now.AddHours(-12),
                Metadata = new Dictionary<string, string>
                {
                    { "cert_issue", "expired" }
                }
            },
            new()
            {
                AlertType = "Outbound Malware Communication",
                Severity = "critical",
                SourceIp = "192.168.1.50",
                DestinationIp = "198.51.100.89",
                SourcePort = 49876,
                DestinationPort = 8080,
                Protocol = "tcp",
                Signature = "ET MALWARE Known C2 Communication",
                SignatureId = 2024364,
                Category = "A Network Trojan was detected",
                Timestamp = DateTime.Now.AddHours(-18),
                Metadata = new Dictionary<string, string>
                {
                    { "malware_family", "GenericC2" },
                    { "confidence", "high" }
                }
            },
            new()
            {
                AlertType = "ICMP Ping Sweep",
                Severity = "medium",
                SourceIp = "192.168.1.100",
                DestinationIp = "192.168.1.0",
                SourcePort = 0,
                DestinationPort = 0,
                Protocol = "icmp",
                Signature = "ET SCAN ICMP Ping Sweep",
                SignatureId = 2000356,
                Category = "Network Scan",
                Timestamp = DateTime.Now.AddDays(-1),
                Metadata = new Dictionary<string, string>
                {
                    { "scan_type", "ping_sweep" }
                }
            },
            new()
            {
                AlertType = "HTTP User-Agent Suspicious",
                Severity = "low",
                SourceIp = "192.168.1.25",
                DestinationIp = "172.217.14.206",
                SourcePort = 52341,
                DestinationPort = 80,
                Protocol = "tcp",
                Signature = "ET USER_AGENTS Suspicious User Agent",
                SignatureId = 2008705,
                Category = "Potentially Bad Traffic",
                Timestamp = DateTime.Now.AddDays(-2),
                Metadata = new Dictionary<string, string>
                {
                    { "user_agent", "python-requests" }
                }
            },
            new()
            {
                AlertType = "SMB Traffic on Non-Standard Port",
                Severity = "medium",
                SourceIp = "192.168.1.150",
                DestinationIp = "192.168.1.10",
                SourcePort = 54123,
                DestinationPort = 8445,
                Protocol = "tcp",
                Signature = "ET POLICY SMB over Non-Standard Port",
                SignatureId = 2002911,
                Category = "Policy Violation",
                Timestamp = DateTime.Now.AddDays(-3),
                Metadata = new Dictionary<string, string>
                {
                    { "expected_port", "445" }
                }
            },
            new()
            {
                AlertType = "FTP Brute Force Attempt",
                Severity = "high",
                SourceIp = "198.18.0.45",
                DestinationIp = "192.168.1.30",
                SourcePort = 43211,
                DestinationPort = 21,
                Protocol = "tcp",
                Signature = "ET FTP Brute Force Login Attempt",
                SignatureId = 2010935,
                Category = "Attempted User Privilege Gain",
                Timestamp = DateTime.Now.AddDays(-5),
                Metadata = new Dictionary<string, string>
                {
                    { "attempt_count", "50" }
                }
            },
            new()
            {
                AlertType = "Telnet Traffic Detected",
                Severity = "low",
                SourceIp = "192.168.1.200",
                DestinationIp = "192.168.1.1",
                SourcePort = 51234,
                DestinationPort = 23,
                Protocol = "tcp",
                Signature = "ET POLICY Telnet Connection Detected",
                SignatureId = 2002023,
                Category = "Policy Violation",
                Timestamp = DateTime.Now.AddDays(-7),
                Metadata = new Dictionary<string, string>
                {
                    { "protocol", "telnet" },
                    { "recommendation", "Use SSH instead" }
                }
            },
            new()
            {
                AlertType = "Cryptocurrency Mining Activity",
                Severity = "high",
                SourceIp = "192.168.1.75",
                DestinationIp = "104.18.234.15",
                SourcePort = 61234,
                DestinationPort = 3333,
                Protocol = "tcp",
                Signature = "ET COINMINER Known Mining Pool Connection",
                SignatureId = 2028371,
                Category = "Potentially Unwanted Program",
                Timestamp = DateTime.Now.AddDays(-10),
                Metadata = new Dictionary<string, string>
                {
                    { "mining_pool", "pool.example.com" },
                    { "cryptocurrency", "Unknown" }
                }
            }
        };
    }
}
