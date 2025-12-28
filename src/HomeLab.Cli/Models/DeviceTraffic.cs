namespace HomeLab.Cli.Models;

/// <summary>
/// Represents traffic statistics for a network device.
/// </summary>
public class DeviceTraffic
{
    /// <summary>
    /// Device name or hostname.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the device.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// MAC address of the device.
    /// </summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// First time the device was seen on the network.
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// Last time the device was seen on the network.
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Total bytes sent by the device.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// Total bytes received by the device.
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Current throughput in bytes per second (if available).
    /// </summary>
    public long? ThroughputBps { get; set; }

    /// <summary>
    /// Operating system guess (if available).
    /// </summary>
    public string? Os { get; set; }

    /// <summary>
    /// Whether the device is currently active.
    /// </summary>
    public bool IsActive { get; set; }
}
