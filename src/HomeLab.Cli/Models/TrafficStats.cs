namespace HomeLab.Cli.Models;

/// <summary>
/// Represents overall network traffic statistics.
/// </summary>
public class TrafficStats
{
    /// <summary>
    /// Top talking devices (highest traffic volume).
    /// </summary>
    public List<TopTalker> TopTalkers { get; set; } = new();

    /// <summary>
    /// Traffic breakdown by protocol (TCP, UDP, HTTP, etc.).
    /// </summary>
    public Dictionary<string, long> ProtocolStats { get; set; } = new();

    /// <summary>
    /// Total bytes transferred across the network.
    /// </summary>
    public long TotalBytesTransferred { get; set; }

    /// <summary>
    /// Number of active flows/connections.
    /// </summary>
    public int ActiveFlows { get; set; }

    /// <summary>
    /// Timestamp when these statistics were collected.
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Represents a device with high traffic volume.
/// </summary>
public class TopTalker
{
    /// <summary>
    /// Device IP address.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Device name or hostname.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Total bytes transferred (sent + received).
    /// </summary>
    public long TotalBytes { get; set; }

    /// <summary>
    /// Bytes sent.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Bytes received.
    /// </summary>
    public long BytesReceived { get; set; }
}
