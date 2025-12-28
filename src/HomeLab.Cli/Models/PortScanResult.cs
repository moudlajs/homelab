namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a port scan result for a specific device.
/// </summary>
public class PortScanResult
{
    /// <summary>
    /// IP address of the scanned device.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (tcp, udp).
    /// </summary>
    public string Protocol { get; set; } = "tcp";

    /// <summary>
    /// Service name running on the port (if detected).
    /// </summary>
    public string Service { get; set; } = "unknown";

    /// <summary>
    /// Port state (open, closed, filtered).
    /// </summary>
    public string State { get; set; } = "unknown";

    /// <summary>
    /// Service version (if detected).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Additional information about the service.
    /// </summary>
    public string? ExtraInfo { get; set; }
}
