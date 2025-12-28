using HomeLab.Cli.Services.Abstractions;

namespace HomeLab.Cli.Models;

/// <summary>
/// Represents overall network health and status.
/// Combines data from all network monitoring services.
/// </summary>
public class NetworkStatus
{
    /// <summary>
    /// Number of active devices on the network.
    /// </summary>
    public int ActiveDevices { get; set; }

    /// <summary>
    /// Number of recent security alerts (last 24 hours).
    /// </summary>
    public int RecentAlerts { get; set; }

    /// <summary>
    /// Top bandwidth-consuming devices.
    /// </summary>
    public List<DeviceTraffic> TopTalkers { get; set; } = new();

    /// <summary>
    /// Health status of network monitoring services.
    /// </summary>
    public Dictionary<string, ServiceHealthInfo> ServiceHealth { get; set; } = new();

    /// <summary>
    /// Total network bandwidth in the last hour (bytes).
    /// </summary>
    public long TotalBandwidth { get; set; }

    /// <summary>
    /// Number of open ports across all devices.
    /// </summary>
    public int TotalOpenPorts { get; set; }

    /// <summary>
    /// Most recent critical alert, if any.
    /// </summary>
    public SecurityAlert? LatestCriticalAlert { get; set; }

    /// <summary>
    /// Overall network health status: healthy, warning, critical.
    /// </summary>
    public string OverallStatus { get; set; } = "healthy";

    /// <summary>
    /// When this status was collected.
    /// </summary>
    public DateTime CollectedAt { get; set; } = DateTime.Now;
}
