namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a security alert detected by Suricata IDS.
/// </summary>
public class SecurityAlert
{
    /// <summary>
    /// Type of alert (e.g., "Suspicious Traffic", "Port Scan", "Malware Detected").
    /// </summary>
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: critical, high, medium, low.
    /// </summary>
    public string Severity { get; set; } = "medium";

    /// <summary>
    /// Source IP address.
    /// </summary>
    public string SourceIp { get; set; } = string.Empty;

    /// <summary>
    /// Destination IP address.
    /// </summary>
    public string DestinationIp { get; set; } = string.Empty;

    /// <summary>
    /// Source port number.
    /// </summary>
    public int SourcePort { get; set; }

    /// <summary>
    /// Destination port number.
    /// </summary>
    public int DestinationPort { get; set; }

    /// <summary>
    /// Protocol (TCP, UDP, ICMP, etc.).
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Suricata signature/rule that triggered the alert.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Signature ID.
    /// </summary>
    public long SignatureId { get; set; }

    /// <summary>
    /// Category of the alert (e.g., "Attempted Administrator Privilege Gain").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// When the alert was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Additional metadata about the alert.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
