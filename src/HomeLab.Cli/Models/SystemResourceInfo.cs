namespace HomeLab.Cli.Models;

/// <summary>
/// Represents system resource usage information.
/// </summary>
public class SystemResourceInfo
{
    /// <summary>
    /// CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Total memory in GB.
    /// </summary>
    public double TotalMemoryGB { get; set; }

    /// <summary>
    /// Used memory in GB.
    /// </summary>
    public double UsedMemoryGB { get; set; }

    /// <summary>
    /// Memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsagePercent { get; set; }

    /// <summary>
    /// Total disk space in GB.
    /// </summary>
    public double TotalDiskGB { get; set; }

    /// <summary>
    /// Used disk space in GB.
    /// </summary>
    public double UsedDiskGB { get; set; }

    /// <summary>
    /// Disk usage percentage (0-100).
    /// </summary>
    public double DiskUsagePercent { get; set; }

    /// <summary>
    /// Number of running containers.
    /// </summary>
    public int RunningContainers { get; set; }

    /// <summary>
    /// Total number of containers.
    /// </summary>
    public int TotalContainers { get; set; }
}
