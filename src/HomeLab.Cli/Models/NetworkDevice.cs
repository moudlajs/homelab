namespace HomeLab.Cli.Models;

/// <summary>
/// Represents a network device discovered during scanning.
/// </summary>
public class NetworkDevice
{
    /// <summary>
    /// IP address of the device.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// MAC address of the device (if available).
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Hostname of the device (if available).
    /// </summary>
    public string? Hostname { get; set; }

    /// <summary>
    /// Vendor name based on MAC address lookup (if available).
    /// </summary>
    public string? Vendor { get; set; }

    /// <summary>
    /// List of open ports detected on the device.
    /// </summary>
    public List<int> OpenPorts { get; set; } = new();

    /// <summary>
    /// Operating system detection result (if available).
    /// </summary>
    public string? OsGuess { get; set; }

    /// <summary>
    /// Device status (up, down).
    /// </summary>
    public string Status { get; set; } = "up";

    /// <summary>
    /// Timestamp when the device was scanned.
    /// </summary>
    public DateTime ScannedAt { get; set; } = DateTime.Now;
}
